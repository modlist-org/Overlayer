using System.Text;
using System.Text.RegularExpressions;

namespace Overlayer.Tutorial;

// Balances line breaks inside Olly's speech bubble so Korean/English lines break at natural
// clauses instead of stranding one-word orphans or splitting mid-syllable on older TMP builds.
internal static class TutorialWrap {
    private static readonly Regex CodeMark = new("«([^»]*)»");
    private static readonly Regex FaceMark = new(@"\[\[(\w+)\]\]");
    private static readonly Regex BoldMark = new(@"<b>(.*?)</b>");

    internal static Func<string> CodeColorHex { get; set; }

    private readonly struct Token(string leadingSep, string raw, string plain, bool isCode) {
        public readonly string LeadingSep = leadingSep;
        public readonly string Raw = raw;
        public readonly string Plain = plain;
        public readonly bool IsCode = isCode;
    }

    // «code» shows as code, as typed (tags and formatting not run); <b> highlights without TMP boldSpacing bloat.
    internal static string Styled(string text) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }
        text = FaceMark.Replace(text, string.Empty);
        string codeHex = CodeColorHex?.Invoke();
        if (string.IsNullOrEmpty(codeHex)) {
            codeHex = "AEB3FF";
        }
        text = CodeMark.Replace(text, m => $"<color=#{codeHex}><noparse>{m.Groups[1].Value}</noparse></color>");
        return BoldMark.Replace(text, m => $"<color=#FFFFFF>{m.Groups[1].Value}</color>");
    }

    internal static float NeededWidth(string rawText, Func<string, float> measure) {
        if (string.IsNullOrEmpty(rawText) || measure == null) {
            return 0f;
        }
        string clean = FaceMark.Replace(rawText, string.Empty);
        float max = 0f;
        foreach (string para in clean.Split('\n')) {
            foreach (var tok in Tokenize(para)) {
                if (tok.IsCode || !ContainsCjk(tok.Raw)) {
                    float w = measure(Styled(tok.Raw));
                    if (w > max) {
                        max = w;
                    }
                }
            }
        }
        return max;
    }

    internal static string Format(string rawText, float maxWidth, Func<string, float> measure) {
        if (string.IsNullOrEmpty(rawText) || measure == null || maxWidth <= 0f) {
            return Styled(rawText);
        }
        string clean = FaceMark.Replace(rawText, string.Empty);
        string[] paras = clean.Split('\n');
        var sepCache = new Dictionary<string, float>(StringComparer.Ordinal);
        for (int i = 0; i < paras.Length; i++) {
            paras[i] = FormatParagraph(paras[i], maxWidth, measure, sepCache);
        }
        return string.Join("\n", paras);
    }

    private static string FormatParagraph(string para, float maxWidth, Func<string, float> measure, Dictionary<string, float> sepCache) {
        string styled = Styled(para);
        if (string.IsNullOrEmpty(para) || ContainsCjk(para) || measure(styled) <= maxWidth) {
            return styled;
        }

        var tokens = Tokenize(para);
        int n = tokens.Count;
        if (n <= 1) {
            return styled;
        }

        float[] tokW = new float[n];
        for (int k = 0; k < n; k++) {
            tokW[k] = measure(Styled(tokens[k].Raw));
        }

        float SepWidth(string sep) {
            if (string.IsNullOrEmpty(sep)) {
                return 0f;
            }
            if (!sepCache.TryGetValue(sep, out float sw)) {
                float x = measure("x");
                sw = Math.Max(0f, measure("x" + sep + "x") - 2f * x);
                sepCache[sep] = sw;
            }
            return sw;
        }

        // Small safety pad prevents TMP from triggering a secondary wrap on a line we emitted.
        float limit = Math.Max(1f, maxWidth - 2f);

        int[] minLines = new int[n + 1];
        for (int i = n - 1; i >= 0; i--) {
            minLines[i] = int.MaxValue / 2;
            float w = i == 0 ? SepWidth(tokens[0].LeadingSep) : 0f;
            for (int j = i + 1; j <= n; j++) {
                w += j == i + 1 ? tokW[j - 1] : SepWidth(tokens[j - 1].LeadingSep) + tokW[j - 1];
                if (j > i + 1 && w > limit) {
                    break;
                }
                int lines = 1 + minLines[j];
                if (lines < minLines[i]) {
                    minLines[i] = lines;
                }
            }
        }

        int targetLines = minLines[0];
        if (targetLines <= 1) {
            return styled;
        }

        float totalW = SepWidth(tokens[0].LeadingSep);
        for (int k = 0; k < n; k++) {
            if (k > 0) {
                totalW += SepWidth(tokens[k].LeadingSep);
            }
            totalW += tokW[k];
        }
        float idealW = totalW / targetLines;

        float[] dpCost = new float[n + 1];
        int[] nextBreak = new int[n + 1];
        nextBreak[n] = -1;
        for (int i = n - 1; i >= 0; i--) {
            dpCost[i] = float.PositiveInfinity;
            nextBreak[i] = -1;
            float w = i == 0 ? SepWidth(tokens[0].LeadingSep) : 0f;
            for (int j = i + 1; j <= n; j++) {
                w += j == i + 1 ? tokW[j - 1] : SepWidth(tokens[j - 1].LeadingSep) + tokW[j - 1];
                if (j > i + 1 && w > limit) {
                    break;
                }
                if (1 + minLines[j] != minLines[i]) {
                    continue;
                }

                float diff = (w - idealW) / maxWidth;
                float cost = diff * diff * 100f;
                if (j < n) {
                    string lastPlain = tokens[j - 1].Plain;
                    string nextPlain = tokens[j].Plain;
                    if (EndsWithAny(lastPlain, '.', '?', '!')) {
                        cost -= 45f;
                    } else if (EndsWithAny(lastPlain, '·', '→', '|')) {
                        cost -= 28f;
                    } else if (EndsWithAny(lastPlain, ',', ':', ';', ')', '—')) {
                        cost -= 20f;
                    }
                    if (lastPlain.IndexOf('(') >= 0 && lastPlain.IndexOf(')') < 0) {
                        cost += 35f;
                    }
                    if (nextPlain == "수" || nextPlain == "때" || nextPlain == "것"
                        || nextPlain.StartsWith("있어요", StringComparison.Ordinal)
                        || nextPlain.StartsWith("있습니다", StringComparison.Ordinal)
                        || nextPlain.StartsWith("있죠", StringComparison.Ordinal)
                        || nextPlain.StartsWith("돼요", StringComparison.Ordinal)) {
                        cost += 30f;
                    }
                    if (lastPlain == "수" && nextPlain.StartsWith("있", StringComparison.Ordinal)) {
                        cost += 40f;
                    }
                    if (j == n - 1 && tokW[n - 1] < maxWidth * 0.25f) {
                        cost += 50f;
                    }
                } else if (w < maxWidth * 0.25f) {
                    cost += 35f;
                }

                float total = cost + dpCost[j];
                if (total < dpCost[i]) {
                    dpCost[i] = total;
                    nextBreak[i] = j;
                }
            }
        }

        var sb = new StringBuilder(para.Length + targetLines);
        int idx = 0;
        while (idx < n) {
            int nxt = nextBreak[idx];
            if (nxt <= idx) {
                nxt = idx + 1;
            }
            if (idx > 0) {
                sb.Append('\n');
            }
            for (int k = idx; k < nxt; k++) {
                if (k > idx || k == 0) {
                    sb.Append(tokens[k].LeadingSep);
                }
                sb.Append(tokens[k].Raw);
            }
            idx = nxt;
        }
        return Styled(sb.ToString());
    }

    private static List<Token> Tokenize(string para) {
        var tokens = new List<Token>();
        int i = 0;
        while (i < para.Length) {
            int sepStart = i;
            while (i < para.Length && para[i] == ' ') {
                i++;
            }
            if (i >= para.Length) {
                break;
            }
            string sep = i > sepStart ? para.Substring(sepStart, i - sepStart) : string.Empty;
            int tokStart = i;
            bool inCode = false, inBold = false, hasCode = false;
            while (i < para.Length) {
                char c = para[i];
                if (c == '«') {
                    inCode = true;
                    hasCode = true;
                    i++;
                } else if (c == '»' && inCode) {
                    inCode = false;
                    i++;
                } else if (!inCode && string.CompareOrdinal(para, i, "<b>", 0, 3) == 0) {
                    inBold = true;
                    i += 3;
                } else if (!inCode && inBold && string.CompareOrdinal(para, i, "</b>", 0, 4) == 0) {
                    inBold = false;
                    i += 4;
                } else if (c == ' ' && !inCode && !inBold) {
                    break;
                } else {
                    i++;
                }
            }
            string raw = para.Substring(tokStart, i - tokStart);
            tokens.Add(new Token(sep, raw, Plain(raw), hasCode));
        }
        return tokens;
    }

    private static string Plain(string raw) =>
        raw.Replace("«", string.Empty)
           .Replace("»", string.Empty)
           .Replace("<b>", string.Empty)
           .Replace("</b>", string.Empty);

    private static bool EndsWithAny(string s, params char[] chars) {
        if (string.IsNullOrEmpty(s)) {
            return false;
        }
        char last = s[s.Length - 1];
        for (int i = 0; i < chars.Length; i++) {
            if (last == chars[i]) {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsCjk(string s) {
        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            if ((c >= '\u3040' && c <= '\u30FF')
                || (c >= '\u3400' && c <= '\u4DBF')
                || (c >= '\u4E00' && c <= '\u9FFF')
                || (c >= '\uF900' && c <= '\uFAFF')) {
                return true;
            }
        }
        return false;
    }
}
