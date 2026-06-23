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
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

    private Delegate _compiledDelegate;

    public readonly bool IsMethod;
    public readonly bool IsProperty;
    public readonly bool IsField;

    public TagCore(string name, MemberInfo member, TagType tagType, string description = null) {
        Name = name;
        Description = description;
        TagType = tagType;
        Member = member;

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

    public object Invoke(params object[] args) {
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
}