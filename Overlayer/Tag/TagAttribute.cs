namespace Overlayer.Tag;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class TagAttribute() : Attribute {
    public string Name { get; }
    public TagType TagType { get; set; }
}