using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Overlayer.Package;

public sealed class O5cpKeyScanResult {
    public HashSet<string> FontKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SpriteKeys { get; } = new(StringComparer.Ordinal);

    public HashSet<string> GuessedFontKeys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> GuessedSpriteKeys { get; } = new(StringComparer.Ordinal);
}

public static class O5cpKeyScanner {

    private static readonly Regex StringLiteralRegex =
        new(@"'([^'\\]|\\.)*'|""([^""\\]|\\.)*""", RegexOptions.Compiled);

    public static O5cpKeyScanResult Scan(JToken canvas) {
        var result = new O5cpKeyScanResult();
        if (canvas == null) {
            return result;
        }
        Visit(canvas, result);

        result.GuessedFontKeys.ExceptWith(result.FontKeys);
        result.GuessedSpriteKeys.ExceptWith(result.SpriteKeys);
        return result;
    }

    private static void Visit(JToken token, O5cpKeyScanResult result) {
        if (token is JObject obj) {
            foreach (var property in obj.Properties()) {
                if (property.Name == "FontKey" || property.Name == "SpriteKey") {
                    bool isFont = property.Name == "FontKey";
                    CollectKeyToken(property.Value, isFont, result);
                } else {
                    Visit(property.Value, result);
                }
            }
        } else if (token is JArray array) {
            foreach (var item in array) {
                Visit(item, result);
            }
        }
    }

    private static void CollectKeyToken(JToken token, bool isFont, O5cpKeyScanResult result) {
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined) {
            return;
        }
        if (token.Type == JTokenType.String) {
            AddStatic(token.Value<string>(), isFont, result);
            return;
        }
        if (token is not JObject obj) {
            return;
        }

        string expression = obj["Fx"]?.Value<string>();
        JToken staticValue = obj["Value"];
        if (staticValue != null && staticValue.Type == JTokenType.String) {
            AddStatic(staticValue.Value<string>(), isFont, result);
        }
        if (!string.IsNullOrEmpty(expression)) {
            foreach (string literal in ExtractLiterals(expression)) {
                AddGuess(literal, isFont, result);
            }
        }
    }

    private static void AddStatic(string key, bool isFont, O5cpKeyScanResult result) {
        if (string.IsNullOrEmpty(key)) {
            return;
        }
        (isFont ? result.FontKeys : result.SpriteKeys).Add(key);
    }

    private static void AddGuess(string key, bool isFont, O5cpKeyScanResult result) {
        if (string.IsNullOrEmpty(key)) {
            return;
        }
        (isFont ? result.GuessedFontKeys : result.GuessedSpriteKeys).Add(key);
    }

    public static List<string> ExtractLiterals(string expression) {
        var literals = new List<string>();
        if (string.IsNullOrEmpty(expression)) {
            return literals;
        }
        foreach (Match match in StringLiteralRegex.Matches(expression)) {
            string raw = match.Value;
            if (raw.Length < 2) {
                continue;
            }
            string inner = raw[1..^1];
            literals.Add(Unescape(inner));
        }
        return literals;
    }

    private static string Unescape(string value) {
        if (value.IndexOf('\\') < 0) {
            return value;
        }
        return value
            .Replace("\\\\", "\0")
            .Replace("\\'", "'")
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\0", "\\");
    }
}

public static class O5cpKeyMapper {
    public static int RewriteStaticKeys(JToken canvas, Func<string, string> map) {
        int count = 0;
        if (canvas == null || map == null) {
            return count;
        }
        Rewrite(canvas, map, ref count);
        return count;
    }

    private static void Rewrite(JToken token, Func<string, string> map, ref int count) {
        if (token is JObject obj) {
            foreach (var property in obj.Properties()) {
                if (property.Name == "FontKey" || property.Name == "SpriteKey") {
                    if (TryRewriteKey(property.Value, map)) {
                        count++;
                    }
                } else {
                    Rewrite(property.Value, map, ref count);
                }
            }
        } else if (token is JArray array) {
            foreach (var item in array) {
                Rewrite(item, map, ref count);
            }
        }
    }

    private static bool TryRewriteKey(JToken token, Func<string, string> map) {
        if (token == null) {
            return false;
        }
        if (token is JValue value && value.Type == JTokenType.String) {
            string key = value.Value<string>();
            if (string.IsNullOrEmpty(key)) {
                return false;
            }
            value.Value = map(key);
            return true;
        }
        if (token is JObject obj && obj["Value"] is JValue fallback
            && fallback.Type == JTokenType.String) {
            string key = fallback.Value<string>();
            if (string.IsNullOrEmpty(key)) {
                return false;
            }
            fallback.Value = map(key);
            return true;
        }
        return false;
    }

    private static readonly Regex LiteralRegex =
        new(@"'([^'\\]|\\.)*'|""([^""\\]|\\.)*""", RegexOptions.Compiled);

    public static int RewriteKeyLiterals(JToken canvas, Func<string, string> map) {
        int count = 0;
        if (canvas == null || map == null) {
            return count;
        }
        RewriteLiterals(canvas, map, ref count);
        return count;
    }

    private static void RewriteLiterals(JToken token, Func<string, string> map, ref int count) {
        if (token is JObject obj) {
            foreach (var property in obj.Properties()) {
                if ((property.Name == "FontKey" || property.Name == "SpriteKey")
                    && property.Value is JObject keyObj
                    && keyObj["Fx"] is JValue fx
                    && fx.Type == JTokenType.String) {
                    string expression = fx.Value<string>();
                    if (!string.IsNullOrEmpty(expression)) {
                        int[] changed = [0];
                        string rewritten = LiteralRegex.Replace(expression, match => {
                            string replacement = ReplaceLiteral(match.Value, map);
                            if (replacement == null) {
                                return match.Value;
                            }
                            changed[0]++;
                            return replacement;
                        });
                        if (changed[0] > 0) {
                            fx.Value = rewritten;
                            count += changed[0];
                        }
                    }
                } else {
                    RewriteLiterals(property.Value, map, ref count);
                }
            }
        } else if (token is JArray array) {
            foreach (var item in array) {
                RewriteLiterals(item, map, ref count);
            }
        }
    }

    private static string ReplaceLiteral(string raw, Func<string, string> map) {
        if (raw.Length < 2) {
            return null;
        }
        char quote = raw[0];
        string key = UnescapeLiteral(raw[1..^1]);
        string mapped;
        try {
            mapped = map(key);
        } catch {
            return null;
        }
        if (string.IsNullOrEmpty(mapped) || mapped == key) {
            return null;
        }
        return quote + EscapeLiteral(mapped, quote) + quote;
    }

    private static string UnescapeLiteral(string value) {
        if (value.IndexOf('\\') < 0) {
            return value;
        }
        return value
            .Replace("\\\\", "\0")
            .Replace("\\'", "'")
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\0", "\\");
    }

    private static string EscapeLiteral(string value, char quote) {
        return value.Replace("\\", "\\\\").Replace(quote.ToString(), "\\" + quote);
    }
}
