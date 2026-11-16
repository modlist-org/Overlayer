using Newtonsoft.Json.Linq;

namespace Overlayer.Package;

public static class O5cpTagScanner {
    private static readonly HashSet<string> TagTextKeys = new(StringComparer.Ordinal) {
        "Text", "PlayingText", "NotPlayingText", "JsCode",
    };

    public static HashSet<string> Scan(JToken canvas) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (canvas == null) {
            return names;
        }
        Visit(canvas, names);
        return names;
    }

    private static void Visit(JToken token, HashSet<string> names) {
        if (token is JObject obj) {
            foreach (var property in obj.Properties()) {
                if (TagTextKeys.Contains(property.Name)) {
                    foreach (string text in CandidateTexts(property.Value)) {
                        // JS code isn't a template: tags are called there like functions.
                        if (property.Name == "JsCode") {
                            AddIdentifiers(text, names, callsOnly: true);
                            continue;
                        }
                        try {
                            foreach (var tag in TextEngine.Parse.Parser.Parse(text)) {
                                // Every name in the placeholder, not just the outer one:
                                // {EasedValue(Combo,0,500)} uses Combo too, {Combo * 2} is an expression.
                                AddIdentifiers(tag.Expression, names);
                                AddIdentifiers(tag.Format, names);
                            }
                        } catch {
                        }
                    }
                } else {
                    Visit(property.Value, names);
                }
            }
        } else if (token is JArray array) {
            foreach (var item in array) {
                Visit(item, names);
            }
        }
    }

    // Words that could be tag names; the caller keeps only real tags. callsOnly: only name(...)
    // that isn't a member (Combo() yes, Math.sin() and plain variables no).
    private static void AddIdentifiers(string text, HashSet<string> names, bool callsOnly = false) {
        if (string.IsNullOrEmpty(text)) {
            return;
        }
        int i = 0;
        while (i < text.Length) {
            if (char.IsLetter(text[i]) || text[i] == '_') {
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) {
                    i++;
                }
                int next = i;
                while (next < text.Length && text[next] == ' ') {
                    next++;
                }
                bool call = next < text.Length && text[next] == '(';
                bool member = start > 0 && text[start - 1] == '.';
                if (!callsOnly || (call && !member)) {
                    names.Add(text[start..i]);
                }
            } else {
                i++;
            }
        }
    }

    private static List<string> CandidateTexts(JToken token) {
        var texts = new List<string>();
        if (token == null) {
            return texts;
        }
        if (token.Type == JTokenType.String) {
            texts.Add(token.Value<string>());
            return texts;
        }
        if (token is JObject obj) {
            string expression = obj["Fx"]?.Value<string>();
            if (!string.IsNullOrEmpty(expression)) {
                texts.Add(expression);
            }
            if (obj["Value"] is JValue fallback && fallback.Type == JTokenType.String) {
                texts.Add(fallback.Value<string>());
            }
        }
        return texts;
    }
}
