using Microsoft.ClearScript;
using Overlayer.Tag.Compile;
using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Tag.Core;

[Flags]
public enum TagType {
    None = 0,
    BlockOnNotPlaying = 1 << 0,
    BlockOnPaused = 1 << 1,
    BlockOnAll = BlockOnNotPlaying | BlockOnPaused,

    ProcessFormat = 1 << 8,

    JsOnly = 1 << 9,

    Hide = 1 << 16,

    Advanced = 1 << 24,
}

public enum TagMemberType {
    Unknown = 0,
    Method,
    Property,
    Field,
    JS,
}

public class TagCore {
    public string Name { get; }
    public string Description { get; } = null;
    public TagType TagType { get; }
    public MemberInfo Member { get; }
    public TagMemberType MemberType { get; }
    public ScriptObject JSFunction { get; }
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

    private Delegate _compiledDelegate;

    public bool IsMethod => MemberType == TagMemberType.Method;
    public bool IsProperty => MemberType == TagMemberType.Property;
    public bool IsField => MemberType == TagMemberType.Field;
    public bool IsJS => MemberType == TagMemberType.JS;

    public TagCore(string name, MemberInfo member, TagType tagType, string description = null) {
        Name = name;
        Description = description;
        TagType = tagType;
        Member = member;

        if(member is MethodInfo) {
            MemberType = TagMemberType.Method;
        } else if(member is PropertyInfo) {
            MemberType = TagMemberType.Property;
        } else if(member is FieldInfo) {
            MemberType = TagMemberType.Field;
        }

        switch(member) {
            case MethodInfo method:
                Parameters = method.GetParameters();
                ReturnType = method.ReturnType;
                break;
            case PropertyInfo prop:
                Parameters = prop.GetGetMethod()?.GetParameters() ?? [];
                ReturnType = prop.PropertyType;
                break;
            case FieldInfo field:
                Parameters = [];
                ReturnType = field.FieldType;
                break;
            default:
                Parameters = [];
                ReturnType = typeof(void);
                break;
        }

        RequiredParameterCount = 0;
        foreach(var p in Parameters) {
            if(!p.HasDefaultValue) {
                RequiredParameterCount++;
            }
        }
    }

    public class JSParameterInfo : ParameterInfo {
        private readonly string _name;
        private readonly Type _type;
        private readonly bool _hasDefault;

        public JSParameterInfo(string name, Type type, int position, bool hasDefault = false) {
            _name = name;
            _type = type;
            _hasDefault = hasDefault;
            NameImpl = name;
            ClassImpl = type;
            PositionImpl = position;
            AttrsImpl = hasDefault ? ParameterAttributes.HasDefault : ParameterAttributes.None;
        }

        public override string Name => _name ?? NameImpl;
        public override Type ParameterType => _type ?? ClassImpl;

        public override bool HasDefaultValue => _hasDefault;
        public override object DefaultValue => Undefined.Value;
        public override object RawDefaultValue => Undefined.Value;
    }

    public TagCore(string name, ScriptObject jsInvoker, string[] paramNames, TagType tagType, string description = null, Type returnType = null) {
        Name = name;
        Description = description;
        TagType = tagType & ~TagType.Advanced;
        Member = null;
        MemberType = TagMemberType.JS;
        JSFunction = jsInvoker;
        ReturnType = returnType ?? typeof(object);

        Parameters = new ParameterInfo[paramNames.Length];
        for(int i = 0; i < paramNames.Length; i++) {
            string paramName = paramNames[i];
            bool isOptional = paramName.EndsWith('?');
            if(isOptional) {
                paramName = paramName[..^1];
            }

            Parameters[i] = new JSParameterInfo(paramName, typeof(object), i, isOptional);
        }

        RequiredParameterCount = Parameters.Count(p => !p.HasDefaultValue);
    }

    // Coerces a runtime argument (typically from V8/ClearScript, where every
    // JS number may arrive as double) to a tag parameter type. Mirrors the
    // compile-time rules of ArgConverter so Fx/JS calls behave exactly like
    // TextEngine {Tag:args} calls instead of throwing InvalidCastException.
    internal static object CoerceArg(object value, Type target) {
        if(target == null) {
            throw new ArgumentNullException(nameof(target));
        }
        if(value == null) {
            return target.IsValueType && Nullable.GetUnderlyingType(target) == null
                ? Activator.CreateInstance(target)
                : null;
        }
        if(target.IsInstanceOfType(value)) {
            return value;
        }
        Type nonNullable = Nullable.GetUnderlyingType(target) ?? target;
        if(target != nonNullable) {
            // Boxed underlying values unbox into Nullable<T> directly.
            return CoerceArg(value, nonNullable);
        }
        if(target == typeof(string)) {
            return value.ToString();
        }
        if(target.IsEnum) {
            if(value is string name) {
                return Enum.Parse(target, name, true);
            }
            return Enum.ToObject(target, value);
        }
        if(value is string text) {
            return ArgConverter.Convert(text, target);
        }
        if(value is IConvertible && (target.IsPrimitive || target == typeof(decimal))) {
            return Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
        }
        throw new InvalidCastException(
            $"Cannot convert '{value.GetType().FullName}' to '{target.FullName}'.");
    }

    private static object DefaultOf(Type type) {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object[] NormalizeArgs(ParameterInfo[] parameters, object[] args) {
        args ??= [];
        bool variadic = parameters.Length > 0
            && parameters[parameters.Length - 1].GetCustomAttribute<ParamArrayAttribute>() != null;
        int fixedCount = variadic ? parameters.Length - 1 : parameters.Length;
        var normalized = new object[parameters.Length];
        for(int i = 0; i < parameters.Length; i++) {
            if(variadic && i == parameters.Length - 1) {
                Type elementType = parameters[i].ParameterType.GetElementType() ?? typeof(object);
                int extra = Math.Max(0, args.Length - fixedCount);
                var packed = Array.CreateInstance(elementType, extra);
                for(int j = 0; j < extra; j++) {
                    packed.SetValue(CoerceArg(args[fixedCount + j], elementType), j);
                }
                normalized[i] = packed;
            } else if(i < args.Length) {
                normalized[i] = CoerceArg(args[i], parameters[i].ParameterType);
            } else if(parameters[i].HasDefaultValue) {
                try {
                    object fallback = parameters[i].DefaultValue;
                    if(fallback == null || fallback == DBNull.Value || fallback == Type.Missing) {
                        normalized[i] = DefaultOf(parameters[i].ParameterType);
                    } else {
                        normalized[i] = CoerceArg(fallback, parameters[i].ParameterType);
                    }
                } catch {
                    normalized[i] = DefaultOf(parameters[i].ParameterType);
                }
            } else {
                normalized[i] = DefaultOf(parameters[i].ParameterType);
            }
        }
        return normalized;
    }

    public object Invoke(params object[] args) {
        if(IsJS) {
            // The V8 engine is disposed and recreated on script reload.
            // Calls racing the reload (or via stale compiled placeholders)
            // throw ObjectDisposedException; treat as "no value" so the
            // caller falls back instead of spamming warnings every frame.
            try {
                return JSFunction.Invoke(false, args);
            } catch(ObjectDisposedException) {
                return null;
            }
        }

        if(MemberType == TagMemberType.Unknown || Member == null) {
            return null;
        }

        if(_compiledDelegate == null) {
            var argsParam = Expression.Parameter(typeof(object[]), "args");
            Expression call;

            switch(MemberType) {
                case TagMemberType.Method:
                    var method = (MethodInfo)Member;
                    var paramExpressions = new Expression[Parameters.Length];
                    for(int i = 0; i < Parameters.Length; i++) {
                        var accessor = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                        paramExpressions[i] = Expression.Convert(accessor, Parameters[i].ParameterType);
                    }
                    call = Expression.Call(null, method, paramExpressions);
                    break;

                case TagMemberType.Property:
                    call = Expression.Property(null, (PropertyInfo)Member);
                    break;

                case TagMemberType.Field:
                    call = Expression.Field(null, (FieldInfo)Member);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported MemberType: {MemberType}");
            }

            var castResult = Expression.Convert(call, typeof(object));
            _compiledDelegate = Expression.Lambda<Func<object[], object>>(castResult, argsParam).Compile();
        }

        // The compiled delegate unboxes positionally, so normalize first:
        // coerce every element to its parameter type (JS numbers often arrive
        // as double), pack params arrays, and pad missing arguments.
        // NOTE: the (object) cast is load-bearing. DynamicInvoke takes
        // params object[], and without it the array would spread instead of
        // binding to the delegate's single object[] parameter.
        return _compiledDelegate.DynamicInvoke((object)NormalizeArgs(Parameters, args));
    }

    public override string ToString() {
        var paramList = Parameters.Length > 0
            ? string.Join(", ", Parameters.Select(p => p != null ? $"{p.ParameterType.Name} {p.Name}" : "object arg"))
            : "None";

        return $"Name: {Name} | " +
               $"Type: {TagType} | " +
               $"Return: {ReturnType.Name} | " +
               $"Params: ({paramList}) | " +
               $"IsJS: {IsJS} | " +
               $"Compiled: {(_compiledDelegate != null || IsJS ? "Yes" : "No")}";
    }
}
