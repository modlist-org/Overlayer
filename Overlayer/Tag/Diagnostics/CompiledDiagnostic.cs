namespace Overlayer.Tag.Diagnostics;

public readonly struct CompileDiagnostic(DiagnosticId id, CompileSeverity severity, params object[] context) {
    public readonly DiagnosticId Id = id;
    public readonly CompileSeverity Severity = severity;
    public readonly object[] Context = context;
}