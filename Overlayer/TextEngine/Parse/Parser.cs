using System.Text.RegularExpressions;

namespace Overlayer.TextEngine.Parse;

public static class Parser {
    private static readonly Regex Pattern =
        new(@"\{(?<name>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex ColonPattern =
        new(@"\{(?<name>[A-Za-z0-9_]+):(?<arg>[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex FuncPattern =
        new(@"\{(?<name>[A-Za-z0-9_]+)\((?<args>[^}]*)\)\}", RegexOptions.Compiled);

    public static List<ParsedTag> Parse(string input) {
        var jsRanges = FindJsExprRanges(input);
        var matches = ColonPattern.Matches(input)
            .Cast<Match>()
            .Concat(FuncPattern.Matches(input).Cast<Match>())
            .Concat(Pattern.Matches(input).Cast<Match>())
            .Where(m => !jsRanges.Any(r => m.Index >= r.Index && m.Index + m.Length <= r.Index + r.Length))
            .OrderBy(m => m.Index);

        var tags = (from m in matches
                let name = m.Groups["name"].Value
                let argsRaw = m.Groups["arg"].Success
                    ? m.Groups["arg"].Value
                    : m.Groups["args"].Success ? m.Groups["args"].Value : ""
                let args = (string[])(string.IsNullOrWhiteSpace(argsRaw)
                    ? []
                    : [.. argsRaw.Split(',').Select(s => s.Trim())])
                select new ParsedTag(m.Value, name, args, m.Index, m.Length)).ToList();

        tags.AddRange(jsRanges.Select(r =>
            new ParsedTag(
                input.Substring(r.Index, r.Length),
                r.Name,
                string.IsNullOrWhiteSpace(r.ArgsRaw) ? [] : [r.ArgsRaw],
                r.Index,
                r.Length)));

        tags.Sort((left, right) => left.Index.CompareTo(right.Index));
        return tags;
    }

    private static List<(int Index, int Length, string Name, string ArgsRaw)> FindJsExprRanges(string input) {
        var ranges = new List<(int, int, string, string)>();
        int i = 0;
        while (i < input.Length) {
            if (input[i] != '{') {
                i++;
                continue;
            }

            int nameStart = i + 1;
            int nameEnd = nameStart;
            while (nameEnd < input.Length && (char.IsLetterOrDigit(input[nameEnd]) || input[nameEnd] == '_')) {
                nameEnd++;
            }

            if (nameEnd == nameStart
                || nameEnd >= input.Length
                || (input[nameEnd] != ':' && input[nameEnd] != '(')
                || !input.Substring(nameStart, nameEnd - nameStart).Equals("JSExpr", StringComparison.OrdinalIgnoreCase)) {
                i++;
                continue;
            }

            int close = FindBalancedClose(input, nameEnd + 1);
            if (close < 0) {
                i++;
                continue;
            }

            string argsRaw = input.Substring(nameEnd + 1, close - nameEnd - 1);
            ranges.Add((i, close - i + 1, input.Substring(nameStart, nameEnd - nameStart), argsRaw));
            i = close + 1;
        }

        return ranges;
    }

    private static int FindBalancedClose(string input, int start) {
        int depth = 1;
        int i = start;
        while (i < input.Length) {
            char c = input[i];
            if (c == '\'' || c == '"' || c == '`') {
                i = SkipJsString(input, i);
                continue;
            }

            if (c == '/' && i + 1 < input.Length && input[i + 1] == '/') {
                while (i < input.Length && input[i] != '\n') {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < input.Length && input[i + 1] == '*') {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/')) {
                    i++;
                }

                i = Math.Min(input.Length, i + 2);
                continue;
            }

            if (c == '{') {
                depth++;
            } else if (c == '}') {
                depth--;
                if (depth == 0) {
                    return i;
                }
            }

            i++;
        }

        return -1;
    }

    private static int SkipJsString(string input, int start) {
        char quote = input[start];
        int i = start + 1;
        while (i < input.Length) {
            if (input[i] == '\\') {
                i += 2;
                continue;
            }

            if (input[i] == quote) {
                return i + 1;
            }

            if (quote != '`' && input[i] == '\n') {
                return i;
            }

            i++;
        }

        return input.Length;
    }
}