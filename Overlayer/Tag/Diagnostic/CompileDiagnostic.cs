namespace Overlayer.Tag.Diagnostics;

public readonly struct CompileDiagnostic : IEquatable<CompileDiagnostic> {
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

    public bool Equals(CompileDiagnostic other)
        => Id == other.Id
        && Severity == other.Severity
        && Equals(Context, other.Context)
        && Data == other.Data;

    public override bool Equals(object obj)
        => obj is CompileDiagnostic other && Equals(other);

    public override int GetHashCode() {
        unchecked {
            int hash = 17;
            hash = hash * 31 + Id.GetHashCode();
            hash = hash * 31 + Severity.GetHashCode();
            hash = hash * 31 + Context.GetHashCode();
            hash = hash * 31 + (Data?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(CompileDiagnostic left, CompileDiagnostic right)
        => left.Equals(right);

    public static bool operator !=(CompileDiagnostic left, CompileDiagnostic right)
        => !left.Equals(right);

    public override string ToString()
        => $"[{Severity}] {Id} at {Context}: {string.Join(", ", Data)}";
}