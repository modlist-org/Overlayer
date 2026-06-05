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
    public TagType TagType { get; }
    public MemberInfo Member { get; }
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

    public readonly bool IsMethod;
    public readonly bool IsProperty;
    public readonly bool IsField;

    public TagCore(string name, MemberInfo member, TagType tagType) {
        Name = name;
        TagType = tagType;
        Member = member;

        IsMethod = member is MethodInfo;
        IsProperty = member is PropertyInfo;
        IsField = member is FieldInfo;

        if(member is MethodInfo method) {
            Parameters = method.GetParameters();
            ReturnType = method.ReturnType;
        } else if(member is PropertyInfo prop) {
            Parameters = prop.GetGetMethod()?.GetParameters() ?? [];
            ReturnType = prop.PropertyType;
        } else if(member is FieldInfo field) {
            Parameters = [];
            ReturnType = field.FieldType;
        } else {
            Parameters = [];
            ReturnType = typeof(void);
        }

        RequiredParameterCount = 0;
        foreach(var p in Parameters) {
            if(!p.HasDefaultValue) {
                RequiredParameterCount++;
            }
        }
    }
}