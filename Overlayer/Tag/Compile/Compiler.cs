using Overlayer.ModuleAPI;
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
        var diagnostics = new List<CompileDiagnostic>();

        var placeholder = new Placeholder(parsed.Name, parsed.Args);

        var context = new DiagnosticContext(
            parsed.Name,
            parsed.Index,
            parsed.Length
        );

        var sig = SignatureResolver.Resolve(tag, placeholder, diagnostics, context);
        Func<string> compiledFunc;

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
            // Block flags are checked at runtime so no recompile is needed
            // when play/pause state flips. While blocked (e.g. the frames of
            // a scene transition), keep the last good value instead of
            // flashing raw text; raw is only the never-evaluated fallback.
            if (!HasBlockFlag(tag)) {
                compiledFunc = inner;
            } else {
                string last = null;
                compiledFunc = () => {
                    if (IsBlocked(tag)) {
                        return last ?? parsed.Raw;
                    }
                    return last = inner();
                };
            }
        }

        return new CompiledPlaceholder(
            compiledFunc,
            [.. diagnostics]
        );
    }

    private static bool HasBlockFlag(TagCore tag)
        => (tag.TagType & (TagType.BlockOnNotPlaying | TagType.BlockOnPaused)) != 0;

    private static bool IsBlocked(TagCore tag)
        => ((tag.TagType & TagType.BlockOnNotPlaying) != 0 && !PlaybackState.IsPlaying)
        || ((tag.TagType & TagType.BlockOnPaused) != 0 && PlaybackState.IsPaused);
}