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
}

public class TagCore {
    public string Name { get; }
    public string Description { get; } = null;
    public TagType TagType { get; }
    public MemberInfo Member { get; }
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

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

        switch (member) {
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
}