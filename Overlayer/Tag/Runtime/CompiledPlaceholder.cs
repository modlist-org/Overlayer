using Overlayer.Tag.Diagnostics;

namespace Overlayer.Tag.Runtime;

public sealed class CompiledPlaceholder {
    public Func<string> Delegate { get; }
    public CompileDiagnostic[] Diagnostics { get; }

    public bool IsValid => !field;
    public bool HasWarning { get; }

    // Set for plain JS tag calls; lets the text engine batch them per frame.
    public JsBinding Js { get; }

    public CompiledPlaceholder(Func<string> del, CompileDiagnostic[] diagnostics, JsBinding js = null) {
        Js = js;
        Delegate = del ?? EmptyDelegates.ReturnEmpty;
        Diagnostics = diagnostics ?? [];

        bool hasError = false;
        bool hasWarning = false;

        foreach (var t in Diagnostics) {
            var severity = t.Severity;

            switch (severity) {
                case CompileSeverity.Error:
                    hasError = true;
                    break;
                case CompileSeverity.Warning:
                    hasWarning = true;
                    break;
            }
        }

        IsValid = hasError;
        HasWarning = hasWarning;
    }

    public string Get() => Delegate();
}
