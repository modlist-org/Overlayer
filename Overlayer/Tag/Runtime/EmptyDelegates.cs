namespace Overlayer.Tag.Runtime;

public static class EmptyDelegates {
    public static string ReturnEmpty() => string.Empty;
    public static CompiledPlaceholder EmptyCompiled { get; } = new(ReturnEmpty, []);
}
