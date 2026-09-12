using Esprima;

namespace Overlayer.TextEngine.Highlight;

public static class JsSyntaxHighlighter {
    private static readonly ParserOptions Options = new() {
        Tokens = true,
        Comments = true,
        Tolerant = true
    };

    public static TagSyntaxSpan[] GetSpans(string source) {
        if (string.IsNullOrEmpty(source)) {
            return [];
        }

        try {
            var program = new JavaScriptParser(Options).ParseScript(source);
            var spans = new List<TagSyntaxSpan>();
            foreach (var token in program.Tokens) {
                TagSyntaxKind? kind = token.Type switch {
                    TokenType.Keyword or TokenType.BooleanLiteral or TokenType.NullLiteral
                        => TagSyntaxKind.JsKeyword,
                    TokenType.StringLiteral or TokenType.Template or TokenType.RegularExpression
                        => TagSyntaxKind.JsString,
                    TokenType.NumericLiteral or TokenType.BigIntLiteral
                        => TagSyntaxKind.JsNumber,
                    _ => null
                };

                if (kind.HasValue && token.Range.End > token.Range.Start) {
                    spans.Add(new(token.Range.Start, token.Range.End - token.Range.Start, kind.Value));
                }
            }

            foreach (var comment in program.Comments) {
                if (comment.Range.End > comment.Range.Start) {
                    spans.Add(new(comment.Range.Start, comment.Range.End - comment.Range.Start, TagSyntaxKind.JsComment));
                }
            }

            return [.. spans];
        } catch {
            return Fallback.GetSpans(source);
        }
    }

    public static bool TryParse(string source, out int errorIndex) {
        errorIndex = -1;
        if (string.IsNullOrEmpty(source)) {
            return true;
        }

        try {
            new JavaScriptParser(Options).ParseScript(source);
            return true;
        } catch (ParserException ex) {
            errorIndex = ex.Index;
            return false;
        } catch {
            return false;
        }
    }

    private static class Fallback {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) {
            "break", "case", "catch", "class", "const", "continue", "debugger",
            "default", "delete", "do", "else", "export", "extends", "false",
            "finally", "for", "function", "if", "import", "in", "instanceof",
            "let", "new", "null", "return", "super", "switch", "this",
            "throw", "true", "try", "typeof", "undefined", "var", "void",
            "while", "with", "yield", "of", "from", "as", "async", "await",
            "static", "get", "set"
        };

        public static TagSyntaxSpan[] GetSpans(string source) {
            if (string.IsNullOrEmpty(source)) {
                return [];
            }

            var spans = new List<TagSyntaxSpan>();
            int i = 0;
            while (i < source.Length) {
                char c = source[i];
                if (char.IsWhiteSpace(c)) {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && (source[i + 1] == '/' || source[i + 1] == '*')) {
                    i = AddComment(spans, source, i);
                    continue;
                }

                if (c == '\'' || c == '"' || c == '`') {
                    i = AddString(spans, source, i);
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1]))) {
                    i = AddNumber(spans, source, i);
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '$') {
                    i = AddWord(spans, source, i);
                    continue;
                }

                i++;
            }

            return [.. spans];
        }

        private static int AddComment(List<TagSyntaxSpan> spans, string source, int start) {
            int i = start + 2;
            if (source[start + 1] == '/') {
                while (i < source.Length && source[i] != '\n') {
                    i++;
                }
            } else {
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) {
                    i++;
                }

                i = Math.Min(source.Length, i + 2);
            }

            spans.Add(new(start, i - start, TagSyntaxKind.JsComment));
            return i;
        }

        private static int AddString(List<TagSyntaxSpan> spans, string source, int start) {
            char quote = source[start];
            int i = start + 1;
            while (i < source.Length) {
                if (source[i] == '\\') {
                    i += 2;
                    continue;
                }

                if (source[i] == quote) {
                    i++;
                    break;
                }

                if (quote != '`' && source[i] == '\n') {
                    break;
                }

                i++;
            }

            spans.Add(new(start, i - start, TagSyntaxKind.JsString));
            return i;
        }

        private static int AddNumber(List<TagSyntaxSpan> spans, string source, int start) {
            int i = start;
            if (source[i] == '0' && i + 1 < source.Length && (source[i + 1] == 'x' || source[i + 1] == 'X')) {
                i += 2;
                while (i < source.Length && Uri.IsHexDigit(source[i])) {
                    i++;
                }
            } else {
                while (i < source.Length && char.IsDigit(source[i])) {
                    i++;
                }

                if (i < source.Length && source[i] == '.') {
                    i++;
                    while (i < source.Length && char.IsDigit(source[i])) {
                        i++;
                    }
                }

                if (i < source.Length && (source[i] == 'e' || source[i] == 'E')) {
                    int j = i + 1;
                    if (j < source.Length && (source[j] == '+' || source[j] == '-')) {
                        j++;
                    }

                    if (j < source.Length && char.IsDigit(source[j])) {
                        i = j;
                        while (i < source.Length && char.IsDigit(source[i])) {
                            i++;
                        }
                    }
                }
            }

            spans.Add(new(start, i - start, TagSyntaxKind.JsNumber));
            return i;
        }

        private static int AddWord(List<TagSyntaxSpan> spans, string source, int start) {
            int i = start;
            while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_' || source[i] == '$')) {
                i++;
            }

            if (Keywords.Contains(source[start..i])) {
                spans.Add(new(start, i - start, TagSyntaxKind.JsKeyword));
            }

            return i;
        }
    }
}
