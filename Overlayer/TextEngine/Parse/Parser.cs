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
            string name = input.Substring((int)s.NameStart, (int)s.NameLen);
            string[] args;

            switch (s.Type) {
                case OvTagType.Simple:
                    args = [];
                    break;
                case OvTagType.JsExpr:
                    var jsSpan = input.AsSpan((int)s.ArgsStart, (int)s.ArgsLen);
                    args = jsSpan.IsWhiteSpace() ? [] : [jsSpan.ToString()];
                    break;
                default:
                    args = SplitArgs(input.AsSpan((int)s.ArgsStart, (int)s.ArgsLen));
                    break;
            }

            tags.Add(new ParsedTag(raw, name, args, (int)s.RawStart, (int)s.RawLen));
        }

        return tags;
    }

    private static string[] SplitArgs(ReadOnlySpan<char> span) {
        if (span.IsWhiteSpace()) {
            return [];
        }

        int count = 1;
        for (int i = 0; i < span.Length; i++) {
            if (span[i] == ',') count++;
        }

        var result = new string[count];
        int start = 0;
        int idx = 0;
        for (int i = 0; i < span.Length; i++) {
            if (span[i] == ',') {
                result[idx++] = span[start..i].Trim().ToString();
                start = i + 1;
            }
        }
        result[idx] = span[start..].Trim().ToString();
        return result;
    }
}
