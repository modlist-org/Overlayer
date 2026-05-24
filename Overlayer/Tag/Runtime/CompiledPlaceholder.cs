using Overlayer.Tag.Diagnostics;

namespace Overlayer.Tag.Runtime;

public sealed class CompiledPlaceholder {
    public Func<string> Delegate { get; }
    public CompileDiagnostic[] Diagnostics { get; }

    public bool IsValid => !field;
    public bool HasWarning { get; }

    public CompiledPlaceholder(Func<string> del, CompileDiagnostic[] diagnostics) {
        Delegate = del ?? EmptyDelegates.ReturnEmpty;
        Diagnostics = diagnostics ?? [];

        bool hasError = false;
        bool hasWarning = false;

        for(int i = 0; i < Diagnostics.Length; i++) {
            var severity = Diagnostics[i].Severity;

            if(severity == CompileSeverity.Error) {
                hasError = true;
            } else if(severity == CompileSeverity.Warning) {
                hasWarning = true;
            }
        }

        IsValid = hasError;
        HasWarning = hasWarning;
    }

    public string Get() => Delegate();
}