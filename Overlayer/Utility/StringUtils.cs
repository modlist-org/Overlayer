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

        var sb = new System.Text.StringBuilder(input.Length);
        foreach(char c in input) {
            if(char.IsLetterOrDigit(c)) {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static readonly string[] ChosungTable = [
        "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ",
        "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"
    ];
    /// <summary>
    /// 한국기업이좋아하는 초★성★변★환★기
    /// (Magic Hangul Tool)
    /// </summary>
    public static string NormalizeToHangulChosung(string input) {
        if(string.IsNullOrEmpty(input)) {
            return input;
        }

        var result = new System.Text.StringBuilder();

        foreach(char c in input) {
            if(c is >= (char)0xAC00 and <= (char)0xD7A3) {
                int index = (c - 0xAC00) / 588; // ㅋ (lol)
                result.Append(ChosungTable[index]);
            } else {
                result.Append(c); // not Hangul :(
            }
        }

        return result.ToString();
    }
}