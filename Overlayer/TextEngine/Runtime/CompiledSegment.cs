namespace Overlayer.TextEngine.Runtime;

public readonly struct CompiledSegment(int index, int length, Tag.Runtime.Replacer replacer) {
    public readonly int Index = index;
    public readonly int Length = length;
    public readonly Tag.Runtime.Replacer Replacer = replacer;
}
