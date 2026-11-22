using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using System.Linq.Expressions;

namespace Overlayer.Tag.Compile;

public static class Compiler {
    public static CompiledPlaceholder Compile(
        TagCore tag,
        ParsedTag parsed
    ) {
        string exprStr = parsed.Expression;
        return Compile(tag, parsed, exprStr.EndsWith("()") ? exprStr[..^2].Trim() : exprStr, SignatureResolver.ColonArgs(tag, parsed.Args));
    }

    // Call syntax {Name(a, b)} supplies its own name and arguments.
    public static CompiledPlaceholder Compile(
        TagCore tag,
        ParsedTag parsed,
        string lookupName,
        string[] args
    ) {
        var diagnostics = new List<CompileDiagnostic>();
        var placeholder = new Placeholder(lookupName, args);

        var context = new DiagnosticContext(
            lookupName,
            parsed.Index,
            parsed.Length
        );

        var sig = SignatureResolver.Resolve(tag, placeholder, diagnostics, context);
        Func<string> compiledFunc;
        JsBinding js = null;

        if (sig == null || !sig.IsExecutable) {
            compiledFunc = () => parsed.Raw;
        } else {
            Func<string> inner;
            try {
                var expr = ExpressionBuilder.Build(tag, sig, diagnostics);
                var lambda = Expression.Lambda<Func<string>>(expr);
                inner = lambda.Compile();
            } catch (Exception e) {
                diagnostics.Add(new CompileDiagnostic(
                    DiagnosticId.InternalError,
                    CompileSeverity.Error,
                    context,
                    [e]
                ));
                return new CompiledPlaceholder(
                    () => parsed.Raw,
                    [.. diagnostics]
                );
            }
            if (tag.IsJS && tag.JSFunction != null) {
                string format = sig.HasFormat ? sig.Format : null;
                js = new JsBinding(tag, ExpressionBuilder.ArgumentValues(tag, sig), format == null
                    ? JsResultFormatter.ToDisplayString
                    : value => JsResultFormatter.ToFormattedString(value, format));
                // Same call and formatting as the expression tree, plus result memo.
                inner = js.Invoke;
            }
            compiledFunc = inner;
        }

        return new CompiledPlaceholder(
            compiledFunc,
            [.. diagnostics],
            js
        );
    }
}