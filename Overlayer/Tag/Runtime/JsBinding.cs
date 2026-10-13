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

    internal string Complete(object result) {
        string text = Formatted(result);
        if (blockable) Last = text;
        return text;
    }

    internal string Invoke() => Complete(Function.Invoke(false, Args));

    // Unchanged primitive results reuse their text. Immutable so a binding
    // shared by engines on different threads never sees a torn pair.
    private sealed class Memo(object value, string text) {
        public readonly object Value = value;
        public readonly string Text = text;
    }

    private Memo memo;

    private string Formatted(object result) {
        var last = memo;
        if (last != null && SameValue(last.Value, result)) return last.Text;
        string text = Finish(result);
        if (result is int or long or double or float or bool) memo = new Memo(result, text);
        return text;
    }

    private static bool SameValue(object a, object b) => (a, b) switch {
        (double x, double y) => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y),
        (float x, float y) => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y),
        (int x, int y) => x == y,
        (long x, long y) => x == y,
        (bool x, bool y) => x == y,
        _ => false
    };
}
