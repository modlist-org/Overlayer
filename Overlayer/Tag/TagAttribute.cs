namespace Overlayer.Tag;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class TagAttribute(string name) : Attribute {
    public TagAttribute() : this(null!) { }

    public string Name = name;
    public TagType TagType;
}

public class Test {
    [Tag(TagType = TagType.None)]
    public void Method1() {
    }
}