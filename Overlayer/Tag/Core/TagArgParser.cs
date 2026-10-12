namespace Overlayer.Tag.Core;

internal static class TagArgParser {
    public static bool TrySplit(string expr, out string name, out string[] args) {
        name = (expr ?? string.Empty).Trim();
        args = [];
        if (name.Length == 0) {
            return false;
        }

        int open = name.IndexOf('(');
        if (open < 0) {
            return name.IndexOf(')') < 0;
        }

        if (!name.EndsWith(")")) {
            return false;
        }

        string inner = name.Substring(open + 1, name.Length - open - 2);
        name = name.Substring(0, open).Trim();
        if (name.Length == 0) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(inner)) {
            return true;
        }

        var parts = new System.Collections.Generic.List<string>();
        int depth = 0;
        char quote = '\0';
        int start = 0;
        for (int i = 0; i < inner.Length; i++) {
            char c = inner[i];
            if (quote != '\0') {
                if (c == quote) {
                    quote = '\0';
                }
                continue;
            }
            if (c == '\'' || c == '"') {
                quote = c;
            } else if (c == '(') {
                depth++;
            } else if (c == ')') {
                if (depth == 0) {
                    return false;
                }
                depth--;
            } else if (c == ',' && depth == 0) {
                parts.Add(inner.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (quote != '\0' || depth != 0) {
            return false;
        }
        parts.Add(inner.Substring(start).Trim());
        args = parts.ToArray();
        return true;
    }
}
