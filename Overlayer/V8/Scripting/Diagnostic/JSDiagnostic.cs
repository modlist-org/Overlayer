using Overlayer.V8.Scripting.Tag;

namespace Overlayer.V8.Scripting.Diagnostic;

public readonly struct JSDiagnostic : IEquatable<JSDiagnostic> {
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

    public bool Equals(JSDiagnostic other)
        => Id == other.Id
        && Severity == other.Severity
        && FilePath == other.FilePath
        && Data == other.Data;

    public override bool Equals(object obj)
        => obj is JSDiagnostic other && Equals(other);

    public override int GetHashCode() {
        unchecked {
            int hash = 17;
            hash = hash * 31 + Id.GetHashCode();
            hash = hash * 31 + Severity.GetHashCode();
            hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
            hash = hash * 31 + (Data?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(JSDiagnostic left, JSDiagnostic right)
        => left.Equals(right);

    public static bool operator !=(JSDiagnostic left, JSDiagnostic right)
        => !left.Equals(right);

    public override string ToString()
        => $"[{Id}] File: {Path.GetFileName(FilePath)} | Data: {string.Join(", ", Data)}";
}