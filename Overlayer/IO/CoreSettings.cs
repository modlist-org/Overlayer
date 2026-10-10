using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using System.Collections.Generic;

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
    public FxValue<float> AnimationSpeed = new(1.0f);
    public FxValue<bool> EnableJSScriptWatcher = new(true);
    public FxValue<bool> AutoUpdate = new(false);
    public FxValue<bool> UpdateBeta = new(false);
    public FxValue<string> SystemFontKey = FxValue<string>.FromValue(null);
    public FxValue<string> CodeFontKey = FxValue<string>.FromValue(null);
    public FxValue<List<string>> SystemFontFallbacks = new([]);
    public FxValue<List<string>> CodeFontFallbacks = new([]);

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
            [nameof(AnimationSpeed)] = IOUtils.WriteFx(AnimationSpeed),
            [nameof(EnableJSScriptWatcher)] = IOUtils.WriteFx(EnableJSScriptWatcher),
            [nameof(AutoUpdate)] = IOUtils.WriteFx(AutoUpdate),
            [nameof(UpdateBeta)] = IOUtils.WriteFx(UpdateBeta),
            [nameof(SystemFontKey)] = IOUtils.WriteFx(SystemFontKey),
            [nameof(CodeFontKey)] = IOUtils.WriteFx(CodeFontKey),
            [nameof(SystemFontFallbacks)] = IOUtils.WriteFx(SystemFontFallbacks),
            [nameof(CodeFontFallbacks)] = IOUtils.WriteFx(CodeFontFallbacks)
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
        AnimationSpeed = IOUtils.ReadFx(token, nameof(AnimationSpeed), AnimationSpeed);
        EnableJSScriptWatcher = IOUtils.ReadFx(token, nameof(EnableJSScriptWatcher), EnableJSScriptWatcher);
        AutoUpdate = IOUtils.ReadFx(token, nameof(AutoUpdate), AutoUpdate);
        UpdateBeta = IOUtils.ReadFx(token, nameof(UpdateBeta), UpdateBeta);
        SystemFontKey = IOUtils.ReadFx(token, nameof(SystemFontKey), SystemFontKey);
        CodeFontKey = IOUtils.ReadFx(token, nameof(CodeFontKey), CodeFontKey);
        SystemFontFallbacks = IOUtils.ReadFx(token, nameof(SystemFontFallbacks), SystemFontFallbacks);
        CodeFontFallbacks = IOUtils.ReadFx(token, nameof(CodeFontFallbacks), CodeFontFallbacks);
    }
}
