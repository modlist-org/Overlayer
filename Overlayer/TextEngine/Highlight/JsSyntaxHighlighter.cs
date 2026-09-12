using Esprima;

namespace Overlayer.TextEngine.Highlight;

public static class JsSyntaxHighlighter {
    private static readonly ParserOptions Options = new() {
        Tokens = true,
        Comments = true,
        Tolerant = true
    };

    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal) {
        "break", "case", "catch", "continue", "debugger", "default", "delete",
        "do", "else", "finally", "for", "if", "return", "switch", "throw",
        "try", "while", "with", "yield", "await"
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal) {
        "Math", "JSON"
    };

    private static readonly HashSet<string> ConstantNames = new(StringComparer.Ordinal) {
        "Infinity", "NaN"
    };

    private static readonly HashSet<string> TypeContexts = new(StringComparer.Ordinal) {
        "new", "class", "extends", "instanceof"
    };

    public static TagSyntaxSpan[] GetSpans(string source) {
        if (string.IsNullOrEmpty(source)) {
            return [];
        }

        try {
            var program = new JavaScriptParser(Options).ParseScript(source);
            var tokens = new List<(TokenType Type, int Start, int End)>();
            foreach (var token in program.Tokens) {
                if (token.Type == TokenType.EOF) {
                    continue;
                }

                tokens.Add((token.Type, token.Range.Start, token.Range.End));
            }

            var spans = new List<TagSyntaxSpan>();
            for (int k = 0; k < tokens.Count; k++) {
                AddToken(spans, source, tokens, k);
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

    private static void AddToken(
        List<TagSyntaxSpan> spans, string source,
        List<(TokenType Type, int Start, int End)> tokens, int index) {
        var (type, start, end) = tokens[index];
        if (end <= start) {
            return;
        }

        switch (type) {
            case TokenType.Keyword:
            case TokenType.BooleanLiteral:
            case TokenType.NullLiteral: {
                string word = source[start..end];
                spans.Add(new(start, end - start,
                    ControlKeywords.Contains(word) ? TagSyntaxKind.JsControl : TagSyntaxKind.JsKeyword));
                return;
            }
            case TokenType.StringLiteral:
                AddStringWithEscapes(spans, source, start, end);
                return;
            case TokenType.Template:
                AddTemplateQuasi(spans, source, tokens, index);
                return;
            case TokenType.RegularExpression:
                spans.Add(new(start, end - start, TagSyntaxKind.JsRegexp));
                return;
            case TokenType.NumericLiteral:
            case TokenType.BigIntLiteral:
                spans.Add(new(start, end - start, TagSyntaxKind.JsNumber));
                return;
            case TokenType.Identifier: {
                var kind = ClassifyIdentifier(source, tokens, index);
                if (kind.HasValue) {
                    spans.Add(new(start, end - start, kind.Value));
                }

                return;
            }
        }
    }

    private static TagSyntaxKind? ClassifyIdentifier(
        string source, List<(TokenType Type, int Start, int End)> tokens, int index) {
        var (_, start, end) = tokens[index];
        string word = source[start..end];
        if (TypeNames.Contains(word)) {
            return TagSyntaxKind.JsType;
        }

        if (ConstantNames.Contains(word)) {
            return TagSyntaxKind.JsConstant;
        }

        if (index > 0) {
            var prev = tokens[index - 1];
            if (prev.Type == TokenType.Keyword
                && TypeContexts.Contains(source[prev.Start..prev.End])) {
                return TagSyntaxKind.JsType;
            }
        }

        if (index + 1 < tokens.Count
            && tokens[index + 1].Start < source.Length
            && source[tokens[index + 1].Start] == '('
            && IsGapWhitespace(source, end, tokens[index + 1].Start)) {
            return TagSyntaxKind.JsFunction;
        }

        if (index > 0
            && tokens[index - 1].End - tokens[index - 1].Start == 1
            && source[tokens[index - 1].Start] == '.'
            && IsGapWhitespace(source, tokens[index - 1].End, start)) {
            return TagSyntaxKind.JsProperty;
        }

        return null;
    }

    private static bool IsGapWhitespace(string source, int start, int end) {
        for (int i = Math.Max(0, start); i < end && i < source.Length; i++) {
            if (!char.IsWhiteSpace(source[i])) {
                return false;
            }
        }

        return true;
    }

    private static void AddStringWithEscapes(List<TagSyntaxSpan> spans, string source, int start, int end) {
        if (end > start) {
            spans.Add(new(start, end - start, TagSyntaxKind.JsString));
        }

        int i = start;
        while (i + 1 < end) {
            if (source[i] == '\\') {
                spans.Add(new(i, 2, TagSyntaxKind.JsEscape));
                i += 2;
            } else {
                i++;
            }
        }
    }

    private static void AddTemplateQuasi(
        List<TagSyntaxSpan> spans, string source,
        List<(TokenType Type, int Start, int End)> tokens, int index) {
        var (_, start, end) = tokens[index];
        int contentStart = start;
        if (contentStart < end && source[contentStart] == '}') {
            spans.Add(new(contentStart, 1, TagSyntaxKind.JsKeyword));
            contentStart++;
        }

        int contentEnd = end;
        if (contentEnd - 2 >= contentStart
            && source[contentEnd - 2] == '$' && source[contentEnd - 1] == '{') {
            contentEnd -= 2;
        }

        AddStringWithEscapes(spans, source, contentStart, contentEnd);
        if (contentEnd < end) {
            spans.Add(new(contentEnd, end - contentEnd, TagSyntaxKind.JsKeyword));
            PairInterpolationClose(spans, source, tokens, index);
        }
    }

    private static void PairInterpolationClose(
        List<TagSyntaxSpan> spans, string source,
        List<(TokenType Type, int Start, int End)> tokens, int index) {
        int depth = 1;
        for (int k = index + 1; k < tokens.Count; k++) {
            var (type, start, end) = tokens[k];
            if (type == TokenType.Template) {
                if (end - 2 >= start && source[end - 2] == '$' && source[end - 1] == '{') {
                    depth++;
                }

                continue;
            }

            if (type != TokenType.Punctuator || end - start != 1) {
                continue;
            }

            if (source[start] == '{') {
                depth++;
            } else if (source[start] == '}') {
                depth--;
                if (depth == 0) {
                    spans.Add(new(start, 1, TagSyntaxKind.JsKeyword));
                    return;
                }
            }
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

                if (c == '/' && IsRegexStart(source, i)) {
                    i = AddRegex(spans, source, i);
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

        private static bool IsRegexStart(string source, int index) {
            int i = index - 1;
            while (i >= 0 && char.IsWhiteSpace(source[i])) {
                i--;
            }

            if (i < 0) {
                return true;
            }

            char prev = source[i];
            if (char.IsLetterOrDigit(prev) || prev == '_' || prev == '$' || prev == ')' || prev == ']') {
                int end = i + 1;
                while (i >= 0 && (char.IsLetterOrDigit(source[i]) || source[i] == '_' || source[i] == '$')) {
                    i--;
                }

                return Keywords.Contains(source[(i + 1)..end]);
            }

            return true;
        }

        private static int AddRegex(List<TagSyntaxSpan> spans, string source, int start) {
            int i = start + 1;
            bool inClass = false;
            while (i < source.Length) {
                char c = source[i];
                if (c == '\\') {
                    i += 2;
                    continue;
                }

                if (c == '[') {
                    inClass = true;
                } else if (c == ']') {
                    inClass = false;
                } else if (c == '/' && !inClass) {
                    i++;
                    while (i < source.Length && char.IsLetter(source[i])) {
                        i++;
                    }

                    break;
                } else if (c == '\n') {
                    break;
                }

                i++;
            }

            spans.Add(new(start, i - start, TagSyntaxKind.JsRegexp));
            return i;
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
                    if (i + 1 < source.Length) {
                        spans.Add(new(i, 2, TagSyntaxKind.JsEscape));
                    }

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

            spans.Insert(FindInsertIndex(spans, start), new(start, i - start, TagSyntaxKind.JsString));
            return i;
        }

        private static int FindInsertIndex(List<TagSyntaxSpan> spans, int start) {
            int index = spans.Count;
            while (index > 0 && spans[index - 1].Index > start) {
                index--;
            }

            return index;
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

            string word = source[start..i];
            if (Keywords.Contains(word)) {
                spans.Add(new(start, i - start,
                    ControlKeywords.Contains(word) ? TagSyntaxKind.JsControl : TagSyntaxKind.JsKeyword));
                return i;
            }

            if (TypeNames.Contains(word)) {
                spans.Add(new(start, i - start, TagSyntaxKind.JsType));
                return i;
            }

            if (ConstantNames.Contains(word)) {
                spans.Add(new(start, i - start, TagSyntaxKind.JsConstant));
                return i;
            }

            if (IsPreviousWord(source, start, TypeContexts)) {
                spans.Add(new(start, i - start, TagSyntaxKind.JsType));
                return i;
            }

            int j = i;
            while (j < source.Length && char.IsWhiteSpace(source[j])) {
                j++;
            }

            if (j < source.Length && source[j] == '(') {
                spans.Add(new(start, i - start, TagSyntaxKind.JsFunction));
                return i;
            }

            int k = start - 1;
            while (k >= 0 && char.IsWhiteSpace(source[k])) {
                k--;
            }

            if (k >= 0 && source[k] == '.') {
                spans.Add(new(start, i - start, TagSyntaxKind.JsProperty));
            }

            return i;
        }

        private static bool IsPreviousWord(string source, int start, HashSet<string> words) {
            int k = start - 1;
            while (k >= 0 && char.IsWhiteSpace(source[k])) {
                k--;
            }

            if (k < 0) {
                return false;
            }

            int end = k + 1;
            while (k >= 0 && (char.IsLetterOrDigit(source[k]) || source[k] == '_' || source[k] == '$')) {
                k--;
            }

            return words.Contains(source[(k + 1)..end]);
        }
    }
}
