using Newtonsoft.Json.Linq;

namespace Overlayer.Package;

public sealed class O5cpProps {
    public const string JsonKey = "O5cpProps";

    public string ThumbnailPath = string.Empty;
    public string Author = string.Empty;
    public string Description = string.Empty;
    public string Version = "1.0.0";
    public string License = string.Empty;
    public List<string> ExtraScripts = [];

    public O5cpProps Copy() {
        return new O5cpProps {
            ThumbnailPath = ThumbnailPath,
            Author = Author,
            Description = Description,
            Version = Version,
            License = License,
            ExtraScripts = [.. ExtraScripts],
        };
    }

    public JToken Serialize() {
        return new JObject {
            ["thumbnailPath"] = ThumbnailPath ?? string.Empty,
            ["author"] = Author ?? string.Empty,
            ["description"] = Description ?? string.Empty,
            ["version"] = Version ?? "1.0.0",
            ["license"] = License ?? string.Empty,
            ["extraScripts"] = new JArray((ExtraScripts ?? []).Where(s => !string.IsNullOrWhiteSpace(s))),
        };
    }

    public static O5cpProps Parse(JToken token) {
        var props = new O5cpProps();
        if (token is not JObject obj) {
            return props;
        }
        props.ThumbnailPath = (string)obj["thumbnailPath"] ?? string.Empty;
        props.Author = (string)obj["author"] ?? string.Empty;
        props.Description = (string)obj["description"] ?? string.Empty;
        props.Version = (string)obj["version"] ?? "1.0.0";
        props.License = (string)obj["license"] ?? string.Empty;
        if (obj["extraScripts"] is JArray arr) {
            foreach (var item in arr) {
                string path = item?.Value<string>();
                if (!string.IsNullOrWhiteSpace(path) && !props.ExtraScripts.Contains(path)) {
                    props.ExtraScripts.Add(path);
                }
            }
        }
        return props;
    }
}
