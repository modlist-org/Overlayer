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
                        try {
                            foreach (var tag in TextEngine.Parse.Parser.Parse(text)) {
                                if (!string.IsNullOrEmpty(tag.Name)) {
                                    names.Add(tag.Name);
                                }
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
