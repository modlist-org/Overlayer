using Overlayer.Tag.Runtime;

namespace Overlayer.Tag.Diagnostics;

public static class CompileResultFactory {
    public static CompiledPlaceholder Create(Func<string> del, params CompileDiagnostic[] diagnostics)
        => new(del, diagnostics);

    public static CompiledPlaceholder Error(DiagnosticId code, params object[] args) {
        return new CompiledPlaceholder(
            static () => string.Empty, [
                new CompileDiagnostic(code, CompileSeverity.Error, args)
            ]
        );
    }

    public static CompiledPlaceholder Warning(DiagnosticId code, params object[] args) {
        return new CompiledPlaceholder(
            static () => string.Empty, [
                new CompileDiagnostic(code, CompileSeverity.Warning, args)
            ]
        );
    }

    public static CompiledPlaceholder Info(DiagnosticId code, params object[] args) {
        return new CompiledPlaceholder(
            static () => string.Empty, [
                new CompileDiagnostic(code, CompileSeverity.Info, args)
            ]
        );
    }
}