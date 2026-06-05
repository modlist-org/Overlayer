namespace Overlayer.Tag.Diagnostics;

public readonly record struct CompileDiagnostic {
    public DiagnosticId Id { get; }
    public CompileSeverity Severity { get; }
    public DiagnosticContext Context { get; }
    public object[] Data { get; }

    public CompileDiagnostic(
        DiagnosticId id,
        CompileSeverity severity,
        DiagnosticContext context,
        object[] data
    ) {
        Id = id;
        Severity = severity;
        Context = context;
        Data = data;
    }

    public override string ToString()
        => $"[{Severity}] {Id} at {Context}: {string.Join(", ", Data)}";
}