using Overlayer.Tag.Runtime;

namespace Overlayer.Tag.Diagnostics;

public static class CompileResultFactory {
    public static CompiledPlaceholder Create(Func<string> del, params CompileDiagnostic[] diagnostics)
        => new(del, diagnostics);

    public static CompiledPlaceholder Warning(DiagnosticId code, DiagnosticContext context, params object[] data) {
        return new CompiledPlaceholder(
            static () => string.Empty, [
                new CompileDiagnostic(code, CompileSeverity.Warning, context, data)
            ]
        );
    }

    public static CompiledPlaceholder Info(DiagnosticId code, DiagnosticContext context, params object[] data) {
        return new CompiledPlaceholder(
            static () => string.Empty, [
                new CompileDiagnostic(code, CompileSeverity.Info, context, data)
            ]
        );
    }
}