namespace Overlayer.Utility;

public static class StringUtils {
    public static List<string> Search(string query, IEnumerable<string> source) {
        if(string.IsNullOrWhiteSpace(query)) {
            return [.. source];
        }

        string q = Normalize(query);

        if(string.IsNullOrEmpty(q)) {
            return [];
        }

        return [..
            source
                .Select(original => new {
                    Original = original,
                    Normalized = Normalize(original)
                })
                .Select(x => new {
                    x.Original,
                    Score = ScoreMatch(x.Normalized, q)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Original)
        ];
    }

    private static int ScoreMatch(string normalizedValue, string normalizedQuery) {
        if(normalizedValue == normalizedQuery) {
            return 100;
        }

        if(normalizedValue.StartsWith(normalizedQuery)) {
            return 80;
        }

        return normalizedValue.Contains(normalizedQuery) ? 50 : 0;
    }

    public static string Normalize(string input) {
        if(string.IsNullOrEmpty(input)) {
            return string.Empty;
        }

        char[] chars = [.. input.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)];
        return new string(chars);
    }   
}