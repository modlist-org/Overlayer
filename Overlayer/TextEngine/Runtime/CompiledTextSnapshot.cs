namespace Overlayer.TextEngine.Runtime;

// A renderer leases one complete generation. Recompilation cannot mix new text
// with old ranges or dispose JS/tag bindings while they are being evaluated.
internal sealed class CompiledTextSnapshot {
    internal readonly object Identity = new();
    internal readonly string Text;
    internal readonly CompiledSegment[] Segments;
    internal readonly TextRenderPlan Plan;
    // JS tag calls in segment order; the text engine batches them per frame.
    internal readonly Tag.Runtime.JsBinding[] JsCalls;
    private int references = 1;

    internal CompiledTextSnapshot(string text, CompiledSegment[] segments, TextRenderPlan plan) {
        Text = text;
        Segments = segments;
        Plan = plan;
        JsCalls = [.. segments.Select(s => s.Replacer.Compiled.Js).Where(js => js != null)];
    }

    internal void Retain() => Interlocked.Increment(ref references);
    internal void Release() {
        if (Interlocked.Decrement(ref references) != 0) return;
        foreach (var segment in Segments) segment.Replacer.Dispose();
    }
}
