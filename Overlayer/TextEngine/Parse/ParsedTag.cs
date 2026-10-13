namespace Overlayer.TextEngine.Parse;

public readonly struct ParsedTag(string raw, string expr, string format, int index, int length) {
    public readonly string Raw = raw;
    public readonly string Expression = expr;
    public readonly string Format = format;
    public readonly int Index = index;
    public readonly int Length = length;

    public ParsedTag(string raw, string name, string[] args, int index, int length)
        : this(raw, name, args != null && args.Length > 0 ? args[0] : null, index, length) { }

    public string Name => Expression;
    public string[] Args => string.IsNullOrEmpty(Format) ? [] : [Format];
}