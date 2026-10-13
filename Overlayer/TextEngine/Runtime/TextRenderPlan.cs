using Overlayer.Native;
using Overlayer.TextEngine.Parse;

namespace Overlayer.TextEngine.Runtime;

// Compile ranges once. Every render evaluates tags in C#/V8 first; Zig only
// sees UTF-16 data, never callbacks, ScriptObjects or pointers kept after a call.
internal sealed class TextRenderPlan {
    private readonly string template;
    private readonly OvTextSlot[] slots;
    private readonly string[] packedValues;
    private readonly int[] dirtyIndices;
    private readonly int staticLength;
    private char[] packed = [];
    private int packedLength;
    private bool packedReady;

    internal TextRenderPlan(string template, IReadOnlyList<ParsedTag> tags) {
        this.template = template;
        slots = new OvTextSlot[tags.Count];
        packedValues = new string[tags.Count];
        dirtyIndices = new int[tags.Count];
        staticLength = template.Length;
        int last = 0;
        for (int i = 0; i < tags.Count; i++) {
            var tag = tags[i];
            if (tag.Index < last || tag.Length < 0 || tag.Index > template.Length - tag.Length)
                throw new ArgumentException("Tag ranges must be ordered, non-overlapping and inside the template.");
            slots[i] = new OvTextSlot { Index = (uint)tag.Index, Length = (uint)tag.Length };
            last = tag.Index + tag.Length;
            staticLength -= tag.Length;
        }
    }

    internal int OutputLength(ReadOnlySpan<string> values) {
        int length = template.Length;
        checked {
            for (int i = 0; i < slots.Length; i++) {
                if (values[i] != null) length += values[i].Length - (int)slots[i].Length;
            }
        }
        return length;
    }

    internal int Compose(ReadOnlySpan<string> values, Span<char> destination, bool? native = null) {
        // End-to-end benchmarks include packing. Modern .NET wins for normal
        // overlays; native bulk copying only pays off on very large templates.
        bool useNative = native ?? (staticLength >= 1_048_576 && OvTextRenderer.IsSupported);
        if (useNative && OvTextRenderer.IsSupported) {
            Pack(values);
            if (OvTextRenderer.TryRender(template, slots, packed.AsSpan(0, packedLength), destination, out int written))
                return written;
        }
        return ComposeManaged(values, destination);
    }

    private void Pack(ReadOnlySpan<string> values) {
        bool repack = !packedReady;
        int dirtyCount = 0;
        int needed = packedReady ? packedLength : 0;
        for (int i = 0; i < slots.Length; i++) {
            string value = values[i];
            if (packedReady && string.Equals(value, packedValues[i], StringComparison.Ordinal)) continue;
            dirtyIndices[dirtyCount++] = i;
            int delta = (value?.Length ?? 0) - (packedReady ? packedValues[i]?.Length ?? 0 : 0);
            needed = checked(needed + delta);
            if (delta != 0) repack = true;
        }
        if (packedReady && dirtyCount == 0) return;
        if (packed.Length < needed) packed = new char[Math.Max(needed, packed.Length * 2)];
        if (!repack) {
            for (int n = 0; n < dirtyCount; n++) {
                int i = dirtyIndices[n];
                string value = values[i];
                slots[i].TextLength = value == null ? uint.MaxValue : (uint)value.Length;
                if (value != null) value.AsSpan().CopyTo(packed.AsSpan((int)slots[i].Offset));
                packedValues[i] = value;
            }
            return;
        }
        int cursor = 0;
        for (int i = 0; i < slots.Length; i++) {
            string value = values[i];
            slots[i].Offset = (uint)cursor;
            slots[i].TextLength = value == null ? uint.MaxValue : (uint)value.Length;
            if (value != null) {
                value.AsSpan().CopyTo(packed.AsSpan(cursor));
                cursor += value.Length;
            }
            packedValues[i] = value;
        }
        packedLength = cursor;
        packedReady = true;
    }

    private int ComposeManaged(ReadOnlySpan<string> values, Span<char> destination) {
        int written = 0;
        int last = 0;
        for (int i = 0; i < slots.Length && written < destination.Length; i++) {
            int from = (int)slots[i].Index;
            Copy(template.AsSpan(last, from - last), destination, ref written);
            string value = values[i];
            Copy(value == null ? template.AsSpan(from, (int)slots[i].Length) : value.AsSpan(), destination, ref written);
            last = from + (int)slots[i].Length;
        }
        if (written < destination.Length) Copy(template.AsSpan(last), destination, ref written);
        return written;
    }

    private static void Copy(ReadOnlySpan<char> source, Span<char> destination, ref int written) {
        int length = Math.Min(source.Length, destination.Length - written);
        source[..length].CopyTo(destination[written..]);
        written += length;
    }
}
