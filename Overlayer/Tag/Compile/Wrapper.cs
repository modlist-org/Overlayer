using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;

namespace Overlayer.Tag.Compile;

public static class Wrapper {
    public static CompiledPlaceholder Wrap(Placeholder placeholder) {
        if(!TagManager.TryGet(placeholder.Name, out var tag)) {
            return CompileResultFactory.Error(
                DiagnosticId.TagNotFound,
                placeholder.Name
            );
        }

        return Compiler.Compile(tag, placeholder);
    }
}