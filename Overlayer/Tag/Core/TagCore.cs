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

public class TagCore {
    public string Name { get; }
    public string Description { get; } = null;
    public TagType TagType { get; }
    public MemberInfo Member { get; }
    public ScriptObject JSFunction { get; }
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

    private Delegate _compiledDelegate;

    public readonly bool IsMethod;
    public readonly bool IsProperty;
    public readonly bool IsField;
    public readonly bool IsJS;

    public TagCore(string name, MemberInfo member, TagType tagType, string description = null) {
        Name = name;
        Description = description;
        TagType = tagType;
        Member = member;
        IsJS = false;

        IsMethod = member is MethodInfo;
        IsProperty = member is PropertyInfo;
        IsField = member is FieldInfo;

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
        public JSParameterInfo(string name, Type type) {
            NameImpl = name;
            ClassImpl = type;
            AttrsImpl = ParameterAttributes.None;
        }
    }

    public TagCore(string name, ScriptObject jsInvoker, int argCount, TagType tagType, string description = null) {
        Name = name;
        Description = description;
        TagType = tagType & ~TagType.Advanced;
        Member = null;
        IsJS = true;
        JSFunction = jsInvoker;

        IsMethod = false;
        IsProperty = false;
        IsField = false;

        ReturnType = typeof(object);
        Parameters = new ParameterInfo[argCount];
        for(int i = 0; i < argCount; i++) {
            Parameters[i] = new JSParameterInfo($"arg{i}", typeof(object));
        }

        RequiredParameterCount = 0;
        foreach(var p in Parameters) {
            if(p != null && !p.HasDefaultValue) {
                RequiredParameterCount++;
            }
        }
    }

    public object Invoke(params object[] args) {
        if(IsJS) {
            return JSFunction.Invoke(false, (object)args);
        }

        if(_compiledDelegate == null) {
            var method = (MethodInfo)Member;
            var paramExpressions = new Expression[Parameters.Length];
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            for(int i = 0; i < Parameters.Length; i++) {
                var index = Expression.Constant(i);
                var accessor = Expression.ArrayIndex(argsParam, index);
                paramExpressions[i] = Expression.Convert(accessor, Parameters[i].ParameterType);
            }

            var call = Expression.Call(null, method, paramExpressions);
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