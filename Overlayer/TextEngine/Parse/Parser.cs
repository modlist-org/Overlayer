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
        var result = new List<ParsedTag>();

        var matches = ColonPattern.Matches(input)
            .Cast<Match>()
            .Concat(FuncPattern.Matches(input).Cast<Match>())
            .Concat(Pattern.Matches(input).Cast<Match>())
            .OrderBy(m => m.Index);

        foreach(var m in matches) {
            string name = m.Groups["name"].Value;
            string argsRaw = m.Groups["arg"].Success
                ? m.Groups["arg"].Value
                : (m.Groups["args"].Success ? m.Groups["args"].Value : "");

            string[] args = string.IsNullOrWhiteSpace(argsRaw)
                ? [] : [.. argsRaw.Split(',').Select(s => s.Trim())];

            result.Add(new ParsedTag(
                m.Value,
                name,
                args,
                m.Index,
                m.Length
            ));
        }
        return result;
    }
}