namespace Overlayer.Tag.Core;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class TagAttribute : Attribute {
    public string Name { get; set; }
    public TagType TagType { get; set; }
}