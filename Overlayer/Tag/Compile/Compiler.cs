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

        if(sig == null || !sig.IsExecutable) {
            compiledFunc = () => parsed.Raw;
        } else {
            var expr = ExpressionBuilder.Build(tag, sig, diagnostics);
            var lambda = Expression.Lambda<Func<string>>(expr);
            Func<string> inner = lambda.Compile();
            // Block flags are checked at runtime so no recompile is needed
            // when play/pause state flips. Blocked tags render raw text.
            compiledFunc = HasBlockFlag(tag)
                ? () => IsBlocked(tag) ? parsed.Raw : inner()
                : inner;
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