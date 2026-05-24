namespace Overlayer.Tag.Compile;

public sealed class ResolvedSignature {
    public string[] Args;
    public string Format;

    public bool HasFormat;

    public CompileState State;

    public static ResolvedSignature Invalid { get; } = new() {
        Args = [],
        Format = null,
        HasFormat = false,
        State = CompileState.Error
    };

    public bool IsExecutable => State != CompileState.Error;
}