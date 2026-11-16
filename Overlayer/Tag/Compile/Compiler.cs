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
        string exprStr = parsed.Expression;
        return Compile(tag, parsed, exprStr.EndsWith("()") ? exprStr[..^2].Trim() : exprStr, parsed.Args);
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
            // Block flags are checked at runtime so no recompile is needed
            // when play/pause state flips. While blocked (e.g. the frames of
            // a scene transition), keep the last good value instead of
            // flashing raw text; raw is only the never-evaluated fallback.
            if (tag.IsJS && tag.JSFunction != null) {
                string format = sig.HasFormat ? sig.Format : null;
                js = new JsBinding(tag, ExpressionBuilder.ArgumentValues(tag, sig), format == null
                    ? JsResultFormatter.ToDisplayString
                    : value => JsResultFormatter.ToFormattedString(value, format));
                // Same call and formatting as the expression tree, plus result memo.
                inner = js.Invoke;
            }
            if (!HasBlockFlag(tag)) {
                compiledFunc = inner;
            } else if (js != null) {
                var binding = js;
                var preview = new Preview(inner, parsed.Raw);
                compiledFunc = () => binding.IsBlocked ? binding.Last ?? preview.Get() : binding.Last = inner();
            } else {
                string last = null;
                var preview = new Preview(inner, parsed.Raw);
                compiledFunc = () => {
                    if (IsBlocked(tag)) {
                        return last ?? preview.Get();
                    }
                    return last = inner();
                };
            }
        }

        return new CompiledPlaceholder(
            compiledFunc,
            [.. diagnostics],
            js
        );
    }

    // A blocked tag that never ran shows its raw text. In the HUD editor show what it says
    // right now instead (0 combo, 0.00% ...), so a layout can be judged before playing.
    // A tag that can't run outside play falls back to raw for good (no throw every frame).
    /// <summary>Set while the HUD editor is open: blocked tags that never ran show their current value.</summary>
    internal static bool ShowBlockedLive { get; set; }

    private sealed class Preview(Func<string> inner, string raw) {
        private bool failed;

        public string Get() {
            if (failed || !ShowBlockedLive) {
                return raw;
            }
            try {
                return inner() ?? raw;
            } catch {
                failed = true;
                return raw;
            }
        }
    }

    private static bool HasBlockFlag(TagCore tag)
        => (tag.TagType & (TagType.BlockOnNotPlaying | TagType.BlockOnPaused)) != 0;

    private static bool IsBlocked(TagCore tag)
        => ((tag.TagType & TagType.BlockOnNotPlaying) != 0 && !PlaybackState.IsPlaying)
        || ((tag.TagType & TagType.BlockOnPaused) != 0 && PlaybackState.IsPaused);
}