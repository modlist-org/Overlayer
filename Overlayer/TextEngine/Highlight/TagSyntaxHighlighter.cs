using Overlayer.Tag.Core;
using Overlayer.TextEngine.Parse;

namespace Overlayer.TextEngine.Highlight;

public enum TagSyntaxKind {
    Delimiter,
    Tag,
    UnknownTag,
    Argument,
    Format,
    Separator,
    JsKeyword,
    JsControl,
    JsString,
    JsNumber,
    JsComment,
    JsFunction,
    JsProperty,
    JsType,
    JsConstant,
    JsRegexp,
    JsEscape
}

public readonly struct TagSyntaxSpan(int index, int length, TagSyntaxKind kind) {
    public readonly int Index = index;
    public readonly int Length = length;
    public readonly TagSyntaxKind Kind = kind;
}

public static class TagSyntaxHighlighter {
    public static TagSyntaxSpan[] GetSpans(string source) {
        if (string.IsNullOrEmpty(source)) {
            return [];
        }

        var spans = new List<TagSyntaxSpan>();
        foreach (var parsed in Parser.Parse(source)) {
            int end = parsed.Index + parsed.Length;
            if (parsed.Index < 0 || end > source.Length || parsed.Length < 2) {
                continue;
            }

            spans.Add(new(parsed.Index, 1, TagSyntaxKind.Delimiter));
            spans.Add(new(end - 1, 1, TagSyntaxKind.Delimiter));

            int exprLen = parsed.Expression.Length;
            int exprStart = parsed.Index + 1;
            while (exprStart < end - 1 && char.IsWhiteSpace(source[exprStart])) {
                exprStart++;
            }

            string lookupName = parsed.Expression.EndsWith("()") ? parsed.Expression[..^2].Trim() : parsed.Expression;
            bool known = ExprCore.TagLookupProvider != null && ExprCore.TagLookupProvider(lookupName, out _);

            bool isLegacyJs = parsed.Expression.Equals("JSExpr", StringComparison.OrdinalIgnoreCase)
                || parsed.Expression.StartsWith("JSExpr(", StringComparison.OrdinalIgnoreCase);

            if (isLegacyJs) {
                spans.Add(new(exprStart, exprLen, TagSyntaxKind.UnknownTag));
            } else if (known && IsSimpleIdentifier(parsed.Expression)) {
                spans.Add(new(exprStart, exprLen, TagSyntaxKind.Tag));
            } else if (IsSimpleIdentifier(parsed.Expression)) {
                spans.Add(new(exprStart, exprLen, TagSyntaxKind.UnknownTag));
            } else {
                foreach (var jsSpan in JsSyntaxHighlighter.GetSpans(parsed.Expression)) {
                    spans.Add(new(exprStart + jsSpan.Index, jsSpan.Length, jsSpan.Kind));
                }
            }

            if (!string.IsNullOrEmpty(parsed.Format)) {
                int colonIdx = exprStart + exprLen;
                while (colonIdx < end - 1 && source[colonIdx] != ':') {
                    colonIdx++;
                }
                if (colonIdx < end - 1 && source[colonIdx] == ':') {
                    spans.Add(new(colonIdx, 1, TagSyntaxKind.Separator));
                    int fmtStart = colonIdx + 1;
                    while (fmtStart < end - 1 && char.IsWhiteSpace(source[fmtStart])) {
                        fmtStart++;
                    }
                    int fmtEnd = end - 1;
                    while (fmtEnd > fmtStart && char.IsWhiteSpace(source[fmtEnd - 1])) {
                        fmtEnd--;
                    }
                    if (fmtEnd > fmtStart) {
                        spans.Add(new(fmtStart, fmtEnd - fmtStart, TagSyntaxKind.Format));
                    }
                }
            }
        }
        AddIncompleteTag(spans, source);
        return [.. spans];
    }

    private static void AddIncompleteTag(List<TagSyntaxSpan> spans, string source) {
        int opening = source.LastIndexOf('{');
        int closing = source.LastIndexOf('}');
        if (opening <= closing) {
            return;
        }

        int nameStart = opening + 1;
        int nameEnd = nameStart;
        while (nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] == '_')) {
            nameEnd++;
        }

        spans.Add(new(opening, 1, TagSyntaxKind.Delimiter));
        if (nameEnd > nameStart) {
            string name = source[nameStart..nameEnd];
            bool isLegacy = name.Equals("JSExpr", StringComparison.OrdinalIgnoreCase);
            bool known = !isLegacy && ExprCore.TagLookupProvider != null && ExprCore.TagLookupProvider(name, out _);
            spans.Add(new(nameStart, nameEnd - nameStart, known ? TagSyntaxKind.Tag : TagSyntaxKind.UnknownTag));
        }
    }

    private static bool IsSimpleIdentifier(string s) {
        if (string.IsNullOrEmpty(s)) return false;
        string t = s.EndsWith("()") ? s[..^2].Trim() : s;
        if (t.Length == 0) return false;
        if (!char.IsLetter(t[0]) && t[0] != '_') return false;
        for (int i = 1; i < t.Length; i++) {
            if (!char.IsLetterOrDigit(t[i]) && t[i] != '_') return false;
        }
        return true;
    }

    public static TagSyntaxKind?[] BuildSyntaxKinds(string source, TagSyntaxSpan[] spans) {
        if (source == null) return [];
        var kinds = new TagSyntaxKind?[source.Length];
        if (spans != null) {
            foreach (var span in spans) {
                int start = Math.Max(0, Math.Min(span.Index, kinds.Length));
                int end = Math.Max(start, Math.Min(start + span.Length, kinds.Length));
                for (int i = start; i < end; i++) {
                    kinds[i] = span.Kind;
                }
            }
        }
        return kinds;
    }

    public static uint GetColorRgba(TagSyntaxKind kind) => kind switch {
        TagSyntaxKind.Delimiter => 0xA6ACCDFF,
        TagSyntaxKind.Tag => 0x96FF91FF,
        TagSyntaxKind.UnknownTag => 0xE2676DFF,
        TagSyntaxKind.Argument => 0xFFD580FF,
        TagSyntaxKind.Format => 0x82D2CEFF,
        TagSyntaxKind.Separator => 0x8990B3FF,
        TagSyntaxKind.JsKeyword => 0x569CD6FF,
        TagSyntaxKind.JsControl => 0xC586C0FF,
        TagSyntaxKind.JsString => 0xCE9178FF,
        TagSyntaxKind.JsNumber => 0xB5CEA8FF,
        TagSyntaxKind.JsComment => 0x6A9955FF,
        TagSyntaxKind.JsFunction => 0xDCDCAAFF,
        TagSyntaxKind.JsProperty => 0x9CDCFEFF,
        TagSyntaxKind.JsType => 0x4EC9B0FF,
        TagSyntaxKind.JsConstant => 0x4FC1FFFF,
        TagSyntaxKind.JsRegexp => 0xD16969FF,
        TagSyntaxKind.JsEscape => 0xD7BA7DFF,
        _ => 0xFFFFFFFF
    };
}
