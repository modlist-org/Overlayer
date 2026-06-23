using System.Reflection;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Compile;

public static class AdvancedCompiler {
    public static CompiledPlaceholder Compile(TagCore tag, ParsedTag parsed) {
        var diagnostics = new List<CompileDiagnostic>();
        var context = new DiagnosticContext(parsed.Name, parsed.Index, parsed.Length);

        if(tag.Member is not MethodInfo mi) {
            throw new NotSupportedException("Advanced tag must be a Static Method.");
        }

        Func<string> runtimeDelegate;
        try {
            runtimeDelegate = (Func<string>)mi.Invoke(null, [parsed, context, diagnostics]);
        } catch(Exception ex) {
            diagnostics.Add(new CompileDiagnostic(
                DiagnosticId.AdvancedTagException,
                CompileSeverity.Error,
                context,
                [ex.InnerException?.Message ?? ex.Message]
            ));
            runtimeDelegate = () => parsed.Raw;
        }

        return CompileResultFactory.Create(runtimeDelegate, [.. diagnostics]);
    }
}