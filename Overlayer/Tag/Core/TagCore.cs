using Microsoft.ClearScript;
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

        public JSParameterInfo(string name, Type type, bool hasDefault = false) {
            _name = name;
            _type = type;
            _hasDefault = hasDefault;
            NameImpl = name;
            ClassImpl = type;
            PositionImpl = 0;
            AttrsImpl = hasDefault ? ParameterAttributes.HasDefault : ParameterAttributes.None;
        }

        public override string Name => _name ?? NameImpl;
        public override Type ParameterType => _type ?? ClassImpl;

        public override bool HasDefaultValue => _hasDefault;
        public override object DefaultValue => Undefined.Value;
        public override object RawDefaultValue => Undefined.Value;
    }

    public TagCore(string name, ScriptObject jsInvoker, string[] paramNames, TagType tagType, string description = null) {
        Name = name;
        Description = description;
        TagType = tagType & ~TagType.Advanced;
        Member = null;
        MemberType = TagMemberType.JS;
        JSFunction = jsInvoker;
        ReturnType = typeof(object);

        Parameters = new ParameterInfo[paramNames.Length];
        for(int i = 0; i < paramNames.Length; i++) {
            Parameters[i] = new JSParameterInfo(paramNames[i], typeof(object));
        }

        RequiredParameterCount = Parameters.Length;
    }

    public object Invoke(params object[] args) {
        if(MemberType == TagMemberType.Unknown || Member == null) {
            return null;
        }

        if(IsJS) {
            return JSFunction.Invoke(false, (object)args);
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

        return _compiledDelegate.DynamicInvoke((object)args);
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