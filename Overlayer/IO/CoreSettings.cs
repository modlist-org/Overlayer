using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;

namespace Overlayer.IO;

public sealed class CoreSettings : ISettingsFile {
    public bool Active = true;
    public string Language = "en-US";
    public bool IsFirstRun = true;
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;
    public float UIScale = 1.0f;
    public float SliderSensitivity = 1.0f;
    public bool EnableJSScriptWatcher = true;

    public JToken Serialize() {
        return new JObject {
            [nameof(Active)] = Active,
            [nameof(Language)] = Language,
            [nameof(IsFirstRun)] = IsFirstRun,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,
            [nameof(UIScale)] = UIScale,
            [nameof(SliderSensitivity)] = SliderSensitivity,
            [nameof(EnableJSScriptWatcher)] = EnableJSScriptWatcher
        };
    }

    public void Deserialize(JToken token) {
        Active = IOUtils.Read(token, nameof(Active), Active);
        Language = IOUtils.Read(token, nameof(Language), Language);
        IsFirstRun = IOUtils.Read(token, nameof(IsFirstRun), IsFirstRun);
        ShowOnStartup = IOUtils.Read(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = IOUtils.Read(token, nameof(Tooltip), Tooltip);
        MiddleClickToDefault = IOUtils.Read(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        UIScale = IOUtils.Read(token, nameof(UIScale), UIScale);
        SliderSensitivity = IOUtils.Read(token, nameof(SliderSensitivity), SliderSensitivity);
        EnableJSScriptWatcher = IOUtils.Read(token, nameof(EnableJSScriptWatcher), EnableJSScriptWatcher);
    }
}