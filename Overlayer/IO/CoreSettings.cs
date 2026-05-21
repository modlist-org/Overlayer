using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;

namespace Overlayer.IO;

public sealed class CoreSettings : ISettingsFile {
    public bool Active = true;
    public string Language = "en-US";
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;
    public float UIScale = 1.0f;

    public JToken Serialize() {
        return new JObject {
            [nameof(Active)] = ShowOnStartup,
            [nameof(Language)] = Language,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,
            [nameof(UIScale)] = UIScale
        };
    }

    public void Deserialize(
        JToken token
    ) {
        Active = Read(token, nameof(Active), Active);
        Language = Read(token, nameof(Language), Language);
        ShowOnStartup = Read(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = Read(token, nameof(Tooltip), Tooltip);
        MiddleClickToDefault = Read(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        UIScale = Read(token, nameof(UIScale), UIScale);
    }

    private static T Read<T>(JToken token, string key, T fallback) {
        var value = token[key];

        if(value == null) {
            return fallback;
        }

        try {
            return value.Value<T>();
        } catch {
            return fallback;
        }
    }
}