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

    public override string ToString() {
        string argsStr = Args != null ? string.Join(", ", Args.Select(a => $"\"{a}\"")) : "null";
        string formatStr = Format != null ? $"\"{Format}\"" : "null";

        return $"[ResolvedSignature] State: {State} | Args: [{argsStr}] | HasFormat: {HasFormat} (Format: {formatStr}) | IsExecutable: {IsExecutable}";
    }
}