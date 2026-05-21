namespace Overlayer.Tag;

[Flags]
public enum TagType {
    None = 0,
    BlockOnNotPlaying = 1 << 0,
    BlockOnPaused = 1 << 1,
    BlockOnAll = BlockOnNotPlaying | BlockOnPaused,

    Hide = 1 << 8,
}

public sealed class Tag(string name, Type type, Func<string[], string> invoker, TagType tagType = TagType.None) {
    public string Name { get; } = name;
    public Type Type { get; } = type;
    public TagType TagType { get; } = tagType;

    public Func<string[], string> Invoker { get; set; } = invoker;

    public string Invoke(string[] args) {
        return Invoker(args);
    }
}