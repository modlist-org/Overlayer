namespace Overlayer.Tag.Diagnostics;

public readonly struct DiagnosticContext(string tagName, int index, int length) {
    public readonly string TagName = tagName;
    public readonly int Index = index;
    public readonly int Length = length;

    public override string ToString()
        => $"Tag: {TagName}, Index: {Index}, Length: {Length}";
}