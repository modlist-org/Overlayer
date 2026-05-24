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

public sealed class TagCore {
    public string Name { get; }
    public TagType TagType { get; }
    public MethodInfo Method { get; }
    public ParameterInfo[] Parameters { get; }
    public int RequiredParameterCount { get; }
    public Type ReturnType { get; }

    public TagCore(string name, MethodInfo method, TagType tagType) {
        Name = name;
        TagType = tagType;
        Method = method;

        Parameters = method.GetParameters();

        int required = 0;
        for(int i = 0; i < Parameters.Length; i++) {
            if(!Parameters[i].HasDefaultValue) {
                required++;
            }
        }

        RequiredParameterCount = required;
        ReturnType = method.ReturnType;
    }
}