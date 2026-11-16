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
    public FxValue<bool> Enable3DFeatures = new(false);
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
    public FxValue<bool> UseRawInput = new(true);
    public FxValue<string> HudEditorKey = FxValue<string>.FromValue("Alt+H");
    public FxValue<bool> HudEditorInPlay = new(false);
    public FxValue<bool> HudSnap = new(true);
    public FxValue<float> HudSnapDistance = new(8f);
    public FxValue<bool> HudShowHints = new(true);
    public FxValue<string> HudMainCanvas = FxValue<string>.FromValue("HUD");
    public FxValue<string> HudPlacement = FxValue<string>.FromValue("MainCanvas");
    public FxValue<float> HudPanelWidth = new(520f);
    public FxValue<float> HudPanelHeight = new(0f);
    public FxValue<bool> HudPanelPinned = new(true);
    public FxValue<bool> HudGrid = new(false);
    public FxValue<float> HudGridSize = new(40f);
    public FxValue<List<string>> HudLocked = new([]);
    public FxValue<bool> HudLayersShown = new(true);
    public FxValue<float> HudLayersWidth = new(300f);
    public FxValue<bool> HudRulers = new(true);
    public FxValue<List<string>> HudGuides = new([]);
    public FxValue<float> HudPreviewWidth = new(760f);
    public FxValue<float> HudPreviewHeight = new(520f);
    public FxValue<List<string>> HudDetached = new([]);
    public FxValue<List<string>> TutorialDone = new([]);
    public FxValue<bool> TutorialOffered = new(false);

    public JToken Serialize() {
        return new JObject {
            [nameof(Active)] = IOUtils.WriteFx(Active),
            [nameof(Language)] = IOUtils.WriteFx(Language),
            [nameof(IsFirstRun)] = IOUtils.WriteFx(IsFirstRun),
            [nameof(ShowOnStartup)] = IOUtils.WriteFx(ShowOnStartup),
            [nameof(Tooltip)] = IOUtils.WriteFx(Tooltip),
            [nameof(AdvancedTooltip)] = IOUtils.WriteFx(AdvancedTooltip),
            [nameof(MiddleClickToDefault)] = IOUtils.WriteFx(MiddleClickToDefault),
            [nameof(Enable3DFeatures)] = IOUtils.WriteFx(Enable3DFeatures),
            [nameof(UIScale)] = IOUtils.WriteFx(UIScale),
            [nameof(SliderSensitivity)] = IOUtils.WriteFx(SliderSensitivity),
            [nameof(AnimationSpeed)] = IOUtils.WriteFx(AnimationSpeed),
            [nameof(EnableJSScriptWatcher)] = IOUtils.WriteFx(EnableJSScriptWatcher),
            [nameof(AutoUpdate)] = IOUtils.WriteFx(AutoUpdate),
            [nameof(UpdateBeta)] = IOUtils.WriteFx(UpdateBeta),
            [nameof(SystemFontKey)] = IOUtils.WriteFx(SystemFontKey),
            [nameof(CodeFontKey)] = IOUtils.WriteFx(CodeFontKey),
            [nameof(SystemFontFallbacks)] = IOUtils.WriteFx(SystemFontFallbacks),
            [nameof(CodeFontFallbacks)] = IOUtils.WriteFx(CodeFontFallbacks),
            [nameof(UseRawInput)] = IOUtils.WriteFx(UseRawInput),
            [nameof(HudEditorKey)] = IOUtils.WriteFx(HudEditorKey),
            [nameof(HudEditorInPlay)] = IOUtils.WriteFx(HudEditorInPlay),
            [nameof(HudSnap)] = IOUtils.WriteFx(HudSnap),
            [nameof(HudSnapDistance)] = IOUtils.WriteFx(HudSnapDistance),
            [nameof(HudShowHints)] = IOUtils.WriteFx(HudShowHints),
            [nameof(HudMainCanvas)] = IOUtils.WriteFx(HudMainCanvas),
            [nameof(HudPlacement)] = IOUtils.WriteFx(HudPlacement),
            [nameof(HudPanelWidth)] = IOUtils.WriteFx(HudPanelWidth),
            [nameof(HudPanelHeight)] = IOUtils.WriteFx(HudPanelHeight),
            [nameof(HudPanelPinned)] = IOUtils.WriteFx(HudPanelPinned),
            [nameof(HudGrid)] = IOUtils.WriteFx(HudGrid),
            [nameof(HudGridSize)] = IOUtils.WriteFx(HudGridSize),
            [nameof(HudLocked)] = IOUtils.WriteFx(HudLocked),
            [nameof(HudLayersShown)] = IOUtils.WriteFx(HudLayersShown),
            [nameof(HudLayersWidth)] = IOUtils.WriteFx(HudLayersWidth),
            [nameof(HudRulers)] = IOUtils.WriteFx(HudRulers),
            [nameof(HudGuides)] = IOUtils.WriteFx(HudGuides),
            [nameof(HudPreviewWidth)] = IOUtils.WriteFx(HudPreviewWidth),
            [nameof(HudPreviewHeight)] = IOUtils.WriteFx(HudPreviewHeight),
            [nameof(HudDetached)] = IOUtils.WriteFx(HudDetached),
            [nameof(TutorialDone)] = IOUtils.WriteFx(TutorialDone),
            [nameof(TutorialOffered)] = IOUtils.WriteFx(TutorialOffered)
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
        Enable3DFeatures = IOUtils.ReadFx(token, nameof(Enable3DFeatures), Enable3DFeatures);
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
        UseRawInput = IOUtils.ReadFx(token, nameof(UseRawInput), UseRawInput);
        HudEditorKey = IOUtils.ReadFx(token, nameof(HudEditorKey), HudEditorKey);
        HudEditorInPlay = IOUtils.ReadFx(token, nameof(HudEditorInPlay), HudEditorInPlay);
        HudSnap = IOUtils.ReadFx(token, nameof(HudSnap), HudSnap);
        HudSnapDistance = IOUtils.ReadFx(token, nameof(HudSnapDistance), HudSnapDistance);
        HudShowHints = IOUtils.ReadFx(token, nameof(HudShowHints), HudShowHints);
        HudMainCanvas = IOUtils.ReadFx(token, nameof(HudMainCanvas), HudMainCanvas);
        HudPlacement = IOUtils.ReadFx(token, nameof(HudPlacement), HudPlacement);
        HudPanelWidth = IOUtils.ReadFx(token, nameof(HudPanelWidth), HudPanelWidth);
        HudPanelHeight = IOUtils.ReadFx(token, nameof(HudPanelHeight), HudPanelHeight);
        HudPanelPinned = IOUtils.ReadFx(token, nameof(HudPanelPinned), HudPanelPinned);
        HudGrid = IOUtils.ReadFx(token, nameof(HudGrid), HudGrid);
        HudGridSize = IOUtils.ReadFx(token, nameof(HudGridSize), HudGridSize);
        HudLocked = IOUtils.ReadFx(token, nameof(HudLocked), HudLocked);
        HudLayersShown = IOUtils.ReadFx(token, nameof(HudLayersShown), HudLayersShown);
        HudLayersWidth = IOUtils.ReadFx(token, nameof(HudLayersWidth), HudLayersWidth);
        HudRulers = IOUtils.ReadFx(token, nameof(HudRulers), HudRulers);
        HudGuides = IOUtils.ReadFx(token, nameof(HudGuides), HudGuides);
        HudPreviewWidth = IOUtils.ReadFx(token, nameof(HudPreviewWidth), HudPreviewWidth);
        HudPreviewHeight = IOUtils.ReadFx(token, nameof(HudPreviewHeight), HudPreviewHeight);
        HudDetached = IOUtils.ReadFx(token, nameof(HudDetached), HudDetached);
        TutorialDone = IOUtils.ReadFx(token, nameof(TutorialDone), TutorialDone);
        TutorialOffered = IOUtils.ReadFx(token, nameof(TutorialOffered), TutorialOffered);
    }
}
