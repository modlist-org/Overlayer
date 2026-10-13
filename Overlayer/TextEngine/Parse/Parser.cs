using Overlayer.Native;

namespace Overlayer.TextEngine.Parse;

public static class Parser {
    public static List<ParsedTag> Parse(string input) {
        if (string.IsNullOrEmpty(input)) {
            return [];
        }

        Span<OvTagSpan> stackBuf = stackalloc OvTagSpan[32];
        int count = OvTagScanner.Scan(input, stackBuf);
        if (count == 0) {
            return [];
        }

        if (count <= stackBuf.Length) {
            return BuildTags(input, stackBuf[..count]);
        }

        var heapBuf = new OvTagSpan[count];
        OvTagScanner.Scan(input, heapBuf);
        return BuildTags(input, heapBuf);
    }

    private static List<ParsedTag> BuildTags(string input, ReadOnlySpan<OvTagSpan> spans) {
        var tags = new List<ParsedTag>(spans.Length);
        for (int i = 0; i < spans.Length; i++) {
            ref readonly var s = ref spans[i];
            string raw = input.Substring((int)s.RawStart, (int)s.RawLen);
            string expr = input.Substring((int)s.ExprStart, (int)s.ExprLen).Trim();
            string format = s.FmtLen > 0 ? input.Substring((int)s.FmtStart, (int)s.FmtLen).Trim() : null;

            tags.Add(new ParsedTag(raw, expr, format, (int)s.RawStart, (int)s.RawLen));
        }

        return tags;
    }
}
