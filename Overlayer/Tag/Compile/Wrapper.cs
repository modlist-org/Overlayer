using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Compile;

public static class Wrapper {
    public static CompiledPlaceholder Wrap(ParsedTag parsed) {
        if(!TagManager.TryGet(parsed.Name, out var tag)) {
            return new CompiledPlaceholder(
                () => parsed.Raw, [
                    new CompileDiagnostic(
                        DiagnosticId.TagNotFound,
                        CompileSeverity.Error,
                        new(parsed.Name, parsed.Index, parsed.Length),
                        [parsed.Name]
                    )
                ]
            );
        }

        return Compiler.Compile(tag, parsed);
    }
}