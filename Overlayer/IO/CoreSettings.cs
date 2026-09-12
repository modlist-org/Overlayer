using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;

namespace Overlayer.IO;

public sealed class CoreSettings : ISettingsFile {
    public FxValue<bool> Active = new(true);
    public FxValue<string> Language = FxValue<string>.FromValue("en-US");
    public FxValue<bool> IsFirstRun = new(true);
    public FxValue<bool> ShowOnStartup = new(false);
    public FxValue<bool> Tooltip = new(true);
    public FxValue<bool> AdvancedTooltip = new(false);
    public FxValue<bool> MiddleClickToDefault = new(true);
    public FxValue<float> UIScale = new(1.0f);
    public FxValue<float> SliderSensitivity = new(1.0f);
    public FxValue<bool> EnableJSScriptWatcher = new(true);

    public JToken Serialize() {
        return new JObject {
            [nameof(Active)] = IOUtils.WriteFx(Active),
            [nameof(Language)] = IOUtils.WriteFx(Language),
            [nameof(IsFirstRun)] = IOUtils.WriteFx(IsFirstRun),
            [nameof(ShowOnStartup)] = IOUtils.WriteFx(ShowOnStartup),
            [nameof(Tooltip)] = IOUtils.WriteFx(Tooltip),
            [nameof(AdvancedTooltip)] = IOUtils.WriteFx(AdvancedTooltip),
            [nameof(MiddleClickToDefault)] = IOUtils.WriteFx(MiddleClickToDefault),
            [nameof(UIScale)] = IOUtils.WriteFx(UIScale),
            [nameof(SliderSensitivity)] = IOUtils.WriteFx(SliderSensitivity),
            [nameof(EnableJSScriptWatcher)] = IOUtils.WriteFx(EnableJSScriptWatcher)
        };
    }

    public void Deserialize(JToken token) {
        Active = IOUtils.ReadFx(token, nameof(Active), Active);
        Language = IOUtils.ReadFx(token, nameof(Language), Language);
        IsFirstRun = IOUtils.ReadFx(token, nameof(IsFirstRun), IsFirstRun);
        ShowOnStartup = IOUtils.ReadFx(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = IOUtils.ReadFx(token, nameof(Tooltip), Tooltip);
        AdvancedTooltip = IOUtils.ReadFx(token, nameof(AdvancedTooltip), AdvancedTooltip);
        MiddleClickToDefault = IOUtils.ReadFx(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        UIScale = IOUtils.ReadFx(token, nameof(UIScale), UIScale);
        SliderSensitivity = IOUtils.ReadFx(token, nameof(SliderSensitivity), SliderSensitivity);
        EnableJSScriptWatcher = IOUtils.ReadFx(token, nameof(EnableJSScriptWatcher), EnableJSScriptWatcher);
    }
}
