using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using System.Linq.Expressions;

namespace Overlayer.Tag.Compile;

public static class Compiler {
    public static CompiledPlaceholder Compile(TagCore tag, Placeholder placeholder) {
        var diagnostics = new List<CompileDiagnostic>();

        var sig = SignatureResolver.Resolve(tag, placeholder, diagnostics);
        if(sig.State == CompileState.Error) {
            return CompileResultFactory.Create(
                EmptyDelegates.ReturnEmpty,
                [.. diagnostics]
            );
        }

        var expr = ExpressionBuilder.Build(tag, sig, diagnostics);

        var lambda = Expression.Lambda<Func<string>>(expr);

        return new CompiledPlaceholder(
            lambda.Compile(),
            [.. diagnostics]
        );
    }
}