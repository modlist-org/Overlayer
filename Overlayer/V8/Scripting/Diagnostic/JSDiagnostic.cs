using Overlayer.V8.Scripting.Tag;

namespace Overlayer.V8.Scripting.Diagnostic;

public readonly record struct JSDiagnostic {
    public JSTagDiagnosticId Id { get; }
    public JSSeverity Severity { get; }
    public string FilePath { get; }
    public object[] Data { get; }

    public JSDiagnostic(
        JSTagDiagnosticId type,
        JSSeverity severity,
        string filePath,
        params object[] data
    ) {
        Id = type;
        Severity = severity;
        FilePath = filePath;
        Data = data;
    }

    public override string ToString()
        => $"[{Id}] File: {Path.GetFileName(FilePath)} | Data: {string.Join(", ", Data)}";
}