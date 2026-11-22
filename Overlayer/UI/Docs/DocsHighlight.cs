using Overlayer.TextEngine.Highlight;
using System.Text;

namespace Overlayer.UI.Docs;

/// <summary>
/// Converts Overlayer tag templates and JS snippets into TMP rich text using
/// Overlayer's own syntax highlighters, safely wrapping plain segments in
/// &lt;noparse&gt; when they contain angle brackets so TMP tags stay literal.
/// </summary>
internal static class DocsHighlight {
    public enum Mode {
        Plain,
        Tag,
        Js,
    }

    public static string Format(string source, Mode mode) {
        if (string.IsNullOrEmpty(source)) {
            return string.Empty;
        }
        if (mode == Mode.Plain) {
            return Escape(source);
        }

        TagSyntaxSpan[] spans;
        try {
            spans = mode == Mode.Js
                ? JsSyntaxHighlighter.GetSpans(source)
                : TagSyntaxHighlighter.GetSpans(source);
        } catch {
            return Escape(source);
        }

        if (spans == null || spans.Length == 0) {
            return Escape(source);
        }

        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(source, spans);
        var sb = new StringBuilder(source.Length * 2);
        int i = 0;
        while (i < source.Length) {
            var kind = kinds[i];
            int j = i + 1;
            while (j < source.Length && kinds[j] == kind) {
                j++;
            }
            string slice = source.Substring(i, j - i);
            if (kind.HasValue) {
                uint rgba = TagSyntaxHighlighter.GetColorRgba(kind.Value);
                // Convert 0xRRGGBBAA to #RRGGBB for TMP rich text.
                uint rgb = (rgba >> 8) & 0xFFFFFFu;
                sb.Append("<color=#").Append(rgb.ToString("X6")).Append('>');
                AppendEscaped(sb, slice);
                sb.Append("</color>");
            } else {
                AppendEscaped(sb, slice);
            }
            i = j;
        }
        return sb.ToString();
    }

    public static string Escape(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }
        if (text.IndexOf('<') < 0 && text.IndexOf('>') < 0) {
            return text;
        }
        var sb = new StringBuilder(text.Length + 24);
        AppendEscaped(sb, text);
        return sb.ToString();
    }

    private static void AppendEscaped(StringBuilder sb, string slice) {
        if (slice.IndexOf('<') < 0) {
            sb.Append(slice);
            return;
        }
        // TMP <noparse> cannot contain literal "</noparse>"; split safely if ever present.
        int start = 0;
        while (start < slice.Length) {
            int stop = slice.IndexOf("</noparse>", start, StringComparison.OrdinalIgnoreCase);
            if (stop < 0) {
                sb.Append("<noparse>").Append(slice, start, slice.Length - start).Append("</noparse>");
                return;
            }
            if (stop > start) {
                sb.Append("<noparse>").Append(slice, start, stop - start).Append("</noparse>");
            }
            sb.Append("<noparse></</noparse><noparse>noparse></noparse>");
            start = stop + "</noparse>".Length;
        }
    }
}
