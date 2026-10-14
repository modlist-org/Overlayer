using Microsoft.ClearScript;
using Overlayer.ModuleAPI;
using Overlayer.Tag.Core;

namespace Overlayer.Tag.Runtime;

// What a compiled JS tag calls, split from how its result is formatted, so
// JsBatch can run many tags in one V8 transition and format each like before.
public sealed class JsBinding {
    internal readonly TagCore Tag;
    internal readonly object[] Args;
    internal readonly Func<object, string> Finish;
    private readonly bool blockable;
    // Last good text for block-flagged tags (shown while blocked).
    internal string Last;

    internal JsBinding(TagCore tag, object[] args, Func<object, string> finish) {
        Tag = tag;
        Args = args;
        Finish = finish;
        blockable = (tag.TagType & (TagType.BlockOnNotPlaying | TagType.BlockOnPaused)) != 0;
    }

    internal ScriptObject Function => Tag.JSFunction;

    internal bool IsBlocked => blockable && (
        ((Tag.TagType & TagType.BlockOnNotPlaying) != 0 && !PlaybackState.IsPlaying) ||
        ((Tag.TagType & TagType.BlockOnPaused) != 0 && PlaybackState.IsPaused));

    internal string Complete(object result) => Keep(Formatted(result));

    // Batched results: a number arrives unboxed and a string as a range of the
    // batch's result string. Unchanged values reuse the last text without
    // boxing, copying or formatting.
    internal string CompleteNumber(double value) {
        var last = memo;
        return Keep(last != null && last.Kind == MemoKind.Number && last.Bits == BitConverter.DoubleToInt64Bits(value)
            ? last.Text
            : Remember(JsBatch.Narrow(value)));
    }

    internal string CompleteString(string source, int start, int length) {
        var last = memo;
        return Keep(last != null && last.Kind == MemoKind.String && last.Str.Length == length
            && string.CompareOrdinal(last.Str, 0, source, start, length) == 0
            ? last.Text
            : Remember(source.Substring(start, length)));
    }

    internal string Invoke() => Complete(Function.Invoke(false, Args));

    private string Keep(string text) {
        if (blockable) Last = text;
        return text;
    }

    private enum MemoKind : byte { Number, Bool, String }

    // Immutable so a binding shared by engines on different threads never sees
    // a torn pair. Numbers key on the double's bits: ClearScript's .NET type for
    // a number is a function of its value, so the bits decide the formatting.
    private sealed class Memo(MemoKind kind, long bits, string str, string text) {
        public readonly MemoKind Kind = kind;
        public readonly long Bits = bits;
        public readonly string Str = str;
        public readonly string Text = text;
    }

    private Memo memo;

    private string Formatted(object result) {
        var last = memo;
        if (last != null && result != null && Matches(last, result)) return last.Text;
        return Remember(result);
    }

    private string Remember(object result) {
        string text = Finish(result);
        memo = result switch {
            int or long or double or float => new Memo(MemoKind.Number, BitConverter.DoubleToInt64Bits(Convert.ToDouble(result)), null, text),
            bool b => new Memo(MemoKind.Bool, b ? 1 : 0, null, text),
            string str => new Memo(MemoKind.String, 0, str, text),
            _ => null
        };
        return text;
    }

    private static bool Matches(Memo memo, object result) => (memo.Kind, result) switch {
        (MemoKind.Number, int or long or double or float) => memo.Bits == BitConverter.DoubleToInt64Bits(Convert.ToDouble(result)),
        (MemoKind.Bool, bool b) => memo.Bits == (b ? 1 : 0),
        (MemoKind.String, string str) => string.Equals(memo.Str, str, StringComparison.Ordinal),
        _ => false
    };
}
