namespace Overlayer.Tag.Replace;

public sealed class CompiledPlaceholder(
    Func<string> @delegate,
    Exception error = null
) {
    public Func<string> Delegate { get; } = @delegate;
    public Exception Error { get; } = error;
    public bool IsValid => Error == null;
    public string Get() => Delegate();
}