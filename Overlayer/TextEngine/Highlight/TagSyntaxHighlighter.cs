using Overlayer.Tag.Core;
using Overlayer.TextEngine.Parse;

namespace Overlayer.TextEngine.Highlight;

public enum TagSyntaxKind {
    Delimiter,
    Tag,
    UnknownTag,
    Argument,
    Format,
    Separator
}

public readonly struct TagSyntaxSpan(int index, int length, TagSyntaxKind kind) {
    public readonly int Index = index;
    public readonly int Length = length;
    public readonly TagSyntaxKind Kind = kind;
}

public static class TagSyntaxHighlighter {
    public static TagSyntaxSpan[] GetSpans(string source) {
        if(string.IsNullOrEmpty(source)) {
            return [];
        }

        var spans = new List<TagSyntaxSpan>();
        foreach(var parsed in Parser.Parse(source)) {
            int end = parsed.Index + parsed.Length;
            if(parsed.Index < 0 || end > source.Length || parsed.Length < 2) {
                continue;
            }

            bool known = TagManager.TryGet(parsed.Name, out var tag);
            spans.Add(new(parsed.Index, 1, TagSyntaxKind.Delimiter));
            spans.Add(new(parsed.Index + 1, parsed.Name.Length, known ? TagSyntaxKind.Tag : TagSyntaxKind.UnknownTag));
            spans.Add(new(end - 1, 1, TagSyntaxKind.Delimiter));

            int cursor = parsed.Index + parsed.Name.Length + 1;
            if(cursor >= end - 1) {
                continue;
            }

            char separator = source[cursor];
            if(separator == ':') {
                spans.Add(new(cursor, 1, TagSyntaxKind.Separator));
                AddArguments(spans, source, cursor + 1, end - 1, tag, parsed.Args.Length);
            } else if(separator == '(') {
                spans.Add(new(cursor, 1, TagSyntaxKind.Delimiter));
                int argsEnd = source[end - 2] == ')' ? end - 2 : end - 1;
                AddArguments(spans, source, cursor + 1, argsEnd, tag, parsed.Args.Length);
                if(argsEnd == end - 2) {
                    spans.Add(new(argsEnd, 1, TagSyntaxKind.Delimiter));
                }
            }
        }
        AddIncompleteTag(spans, source);
        return [.. spans];
    }

    private static void AddIncompleteTag(List<TagSyntaxSpan> spans, string source) {
        int opening = source.LastIndexOf('{');
        int closing = source.LastIndexOf('}');
        if(opening <= closing) {
            return;
        }

        int nameStart = opening + 1;
        int nameEnd = nameStart;
        while(nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] == '_')) {
            nameEnd++;
        }

        spans.Add(new(opening, 1, TagSyntaxKind.Delimiter));
        if(nameEnd > nameStart) {
            bool known = TagManager.TryGet(source[nameStart..nameEnd], out _);
            spans.Add(new(nameStart, nameEnd - nameStart, known ? TagSyntaxKind.Tag : TagSyntaxKind.UnknownTag));
        }
    }

    private static void AddArguments(
        List<TagSyntaxSpan> spans,
        string source,
        int start,
        int end,
        TagCore tag,
        int argumentCount
    ) {
        int argumentIndex = 0;
        int segmentStart = start;
        for(int i = start; i <= end; i++) {
            if(i < end && source[i] != ',') {
                continue;
            }

            int left = segmentStart;
            int right = i;
            while(left < right && char.IsWhiteSpace(source[left])) left++;
            while(right > left && char.IsWhiteSpace(source[right - 1])) right--;
            if(right > left) {
                spans.Add(new(left, right - left, GetArgumentKind(tag, argumentIndex, argumentCount)));
            }

            if(i < end) {
                spans.Add(new(i, 1, TagSyntaxKind.Separator));
            }
            segmentStart = i + 1;
            argumentIndex++;
        }
    }

    private static TagSyntaxKind GetArgumentKind(TagCore tag, int index, int count) {
        if(tag == null) {
            return TagSyntaxKind.Argument;
        }

        bool format = tag.Parameters.Length == 0 ||
            (tag.TagType & TagType.ProcessFormat) != 0 && index == count - 1;
        return format ? TagSyntaxKind.Format : TagSyntaxKind.Argument;
    }
}
