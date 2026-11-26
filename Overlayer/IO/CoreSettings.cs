using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using System.Collections.Generic;
using System.IO;

namespace Overlayer.IO;

public enum TabLabelMode {
    Show,
    Hover,
    Hide,
}

public enum PanelFadeMode {
    FlyIn,
    Fade,
    DiagonalSwipe,
    Taskbar,
    Clock,
}

public sealed class CoreSettings : ISettingsFile {
    public static string DefaultWindowKey =>
        Path.DirectorySeparatorChar == '/' && !File.Exists("/System/Library/CoreServices/SystemVersion.plist")
            ? "Ctrl+BackQuote"
            : "Alt+BackQuote";

    public FxValue<bool> Active = new(true);
    public FxValue<string> Language = FxValue<string>.FromValue("en-US");
    public FxValue<bool> IsFirstRun = new(true);
    public FxValue<bool> ShowOnStartup = new(false);
    public FxValue<bool> Tooltip = new(true);
    public FxValue<bool> MiddleClickToDefault = new(true);
    public FxValue<bool> Enable3DFeatures = new(false);
    public FxValue<bool> ShowInternalImages = new(false);
    public FxValue<bool> ShowInternalFonts = new(false);
    public FxValue<bool> ReferenceResources = new(false);
    public FxValue<float> UIScale = new(1.0f);
    public FxValue<string> UIBaseColor = FxValue<string>.FromValue(O5Kit.Core.O5Palette.DefaultBaseHex);
    public FxValue<float> SliderSensitivity = new(1.0f);
    public FxValue<float> ScrollSensitivity = new(1.0f);
    public FxValue<bool> WheelAdjustSlider = new(true);
    public FxValue<float> AnimationSpeed = new(1.0f);
    public FxValue<float> WindowOpacity = new(1.0f);
    public FxValue<float> HudWindowOpacity = new(1.0f);
    public FxValue<float> TooltipOpacity = new(1.0f);
    public FxValue<bool> ScrollEasing = new(true);
    public FxValue<float> UISoundVolume = new(0.8f);
    public FxValue<bool> EnableJSScriptWatcher = new(true);
    public FxValue<string> SystemFontKey = FxValue<string>.FromValue(null);
    public FxValue<string> SystemMediumFontKey = FxValue<string>.FromValue(null);
    public FxValue<string> CodeFontKey = FxValue<string>.FromValue(null);
    public FxValue<List<string>> SystemFontFallbacks = new([]);
    public FxValue<List<string>> SystemMediumFontFallbacks = new([]);
    public FxValue<List<string>> CodeFontFallbacks = new([]);
    public FxValue<bool> UseRawInput = new(true);
    public FxValue<string> WindowKey = FxValue<string>.FromValue(DefaultWindowKey);
    public FxValue<string> HudEditorKey = FxValue<string>.FromValue("Alt+H");
    public FxValue<bool> HudSnap = new(true);
    public FxValue<float> HudSnapDistance = new(8f);
    public FxValue<string> HudMainCanvas = FxValue<string>.FromValue("Canvas");
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
    public FxValue<bool> HudAutoAnchor = new(true);
    public FxValue<List<string>> HudGuides = new([]);
    public FxValue<List<string>> HudDetached = new([]);
    public FxValue<List<string>> TutorialDone = new([]);
    public FxValue<bool> TutorialOffered = new(false);
    public FxValue<bool> HideTutorial = new(false);
    public FxValue<TabLabelMode> TabLabelMode = new(IO.TabLabelMode.Hover);
    public FxValue<PanelFadeMode> PanelFadeMode = new(IO.PanelFadeMode.FlyIn);
    public FxValue<bool> SetupPending = new(false);
    public FxValue<bool> LinuxTextInputFix = new(true);

    public JToken Serialize() {
        return new JObject {
            [nameof(Active)] = IOUtils.WriteFx(Active),
            [nameof(Language)] = IOUtils.WriteFx(Language),
            [nameof(IsFirstRun)] = IOUtils.WriteFx(IsFirstRun),
            [nameof(ShowOnStartup)] = IOUtils.WriteFx(ShowOnStartup),
            [nameof(Tooltip)] = IOUtils.WriteFx(Tooltip),
            [nameof(MiddleClickToDefault)] = IOUtils.WriteFx(MiddleClickToDefault),
            [nameof(Enable3DFeatures)] = IOUtils.WriteFx(Enable3DFeatures),
            [nameof(ShowInternalImages)] = IOUtils.WriteFx(ShowInternalImages),
            [nameof(ShowInternalFonts)] = IOUtils.WriteFx(ShowInternalFonts),
            [nameof(ReferenceResources)] = IOUtils.WriteFx(ReferenceResources),
            [nameof(UIScale)] = IOUtils.WriteFx(UIScale),
            [nameof(UIBaseColor)] = IOUtils.WriteFx(UIBaseColor),
            [nameof(SliderSensitivity)] = IOUtils.WriteFx(SliderSensitivity),
            [nameof(ScrollSensitivity)] = IOUtils.WriteFx(ScrollSensitivity),
            [nameof(WheelAdjustSlider)] = IOUtils.WriteFx(WheelAdjustSlider),
            [nameof(AnimationSpeed)] = IOUtils.WriteFx(AnimationSpeed),
            [nameof(WindowOpacity)] = IOUtils.WriteFx(WindowOpacity),
            [nameof(HudWindowOpacity)] = IOUtils.WriteFx(HudWindowOpacity),
            [nameof(TooltipOpacity)] = IOUtils.WriteFx(TooltipOpacity),
            [nameof(ScrollEasing)] = IOUtils.WriteFx(ScrollEasing),
            [nameof(UISoundVolume)] = IOUtils.WriteFx(UISoundVolume),
            [nameof(EnableJSScriptWatcher)] = IOUtils.WriteFx(EnableJSScriptWatcher),
            [nameof(SystemFontKey)] = IOUtils.WriteFx(SystemFontKey),
            [nameof(SystemMediumFontKey)] = IOUtils.WriteFx(SystemMediumFontKey),
            [nameof(CodeFontKey)] = IOUtils.WriteFx(CodeFontKey),
            [nameof(SystemFontFallbacks)] = IOUtils.WriteFx(SystemFontFallbacks),
            [nameof(SystemMediumFontFallbacks)] = IOUtils.WriteFx(SystemMediumFontFallbacks),
            [nameof(CodeFontFallbacks)] = IOUtils.WriteFx(CodeFontFallbacks),
            [nameof(UseRawInput)] = IOUtils.WriteFx(UseRawInput),
            [nameof(WindowKey)] = IOUtils.WriteFx(WindowKey),
            [nameof(HudEditorKey)] = IOUtils.WriteFx(HudEditorKey),
            [nameof(HudSnap)] = IOUtils.WriteFx(HudSnap),
            [nameof(HudSnapDistance)] = IOUtils.WriteFx(HudSnapDistance),
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
            [nameof(HudAutoAnchor)] = IOUtils.WriteFx(HudAutoAnchor),
            [nameof(HudGuides)] = IOUtils.WriteFx(HudGuides),
            [nameof(HudDetached)] = IOUtils.WriteFx(HudDetached),
            [nameof(TutorialDone)] = IOUtils.WriteFx(TutorialDone),
            [nameof(TutorialOffered)] = IOUtils.WriteFx(TutorialOffered),
            [nameof(HideTutorial)] = IOUtils.WriteFx(HideTutorial),
            [nameof(TabLabelMode)] = IOUtils.WriteFx(TabLabelMode),
            [nameof(PanelFadeMode)] = IOUtils.WriteFx(PanelFadeMode),
            [nameof(SetupPending)] = IOUtils.WriteFx(SetupPending),
            [nameof(LinuxTextInputFix)] = IOUtils.WriteFx(LinuxTextInputFix)
        };
    }

    public void Deserialize(JToken token) {
        Active = IOUtils.ReadFx(token, nameof(Active), Active);
        Language = IOUtils.ReadFx(token, nameof(Language), Language);
        IsFirstRun = IOUtils.ReadFx(token, nameof(IsFirstRun), IsFirstRun);
        ShowOnStartup = IOUtils.ReadFx(token, nameof(ShowOnStartup), ShowOnStartup);
        Tooltip = IOUtils.ReadFx(token, nameof(Tooltip), Tooltip);
        MiddleClickToDefault = IOUtils.ReadFx(token, nameof(MiddleClickToDefault), MiddleClickToDefault);
        Enable3DFeatures = IOUtils.ReadFx(token, nameof(Enable3DFeatures), Enable3DFeatures);
        ShowInternalImages = IOUtils.ReadFx(token, nameof(ShowInternalImages), ShowInternalImages);
        ShowInternalFonts = IOUtils.ReadFx(token, nameof(ShowInternalFonts), ShowInternalFonts);
        if (token is JObject obj && obj[nameof(ReferenceResources)] != null) {
            ReferenceResources = IOUtils.ReadFx(token, nameof(ReferenceResources), ReferenceResources);
        } else {
            var legacyImg = IOUtils.ReadFx(token, "ReferenceImages", new FxValue<bool>(false));
            var legacyFont = IOUtils.ReadFx(token, "ReferenceFonts", new FxValue<bool>(false));
            ReferenceResources = new FxValue<bool>(legacyImg.Value || legacyFont.Value);
        }
        UIScale = IOUtils.ReadFx(token, nameof(UIScale), UIScale);
        UIBaseColor = IOUtils.ReadFx(token, nameof(UIBaseColor), UIBaseColor);
        SliderSensitivity = IOUtils.ReadFx(token, nameof(SliderSensitivity), SliderSensitivity);
        ScrollSensitivity = IOUtils.ReadFx(token, nameof(ScrollSensitivity), ScrollSensitivity);
        WheelAdjustSlider = IOUtils.ReadFx(token, nameof(WheelAdjustSlider), WheelAdjustSlider);
        AnimationSpeed = IOUtils.ReadFx(token, nameof(AnimationSpeed), AnimationSpeed);
        WindowOpacity = IOUtils.ReadFx(token, nameof(WindowOpacity), WindowOpacity);
        HudWindowOpacity = IOUtils.ReadFx(token, nameof(HudWindowOpacity), HudWindowOpacity);
        TooltipOpacity = IOUtils.ReadFx(token, nameof(TooltipOpacity), TooltipOpacity);
        ScrollEasing = IOUtils.ReadFx(token, nameof(ScrollEasing), ScrollEasing);
        UISoundVolume = IOUtils.ReadFx(token, nameof(UISoundVolume), UISoundVolume);
        EnableJSScriptWatcher = IOUtils.ReadFx(token, nameof(EnableJSScriptWatcher), EnableJSScriptWatcher);
        SystemFontKey = IOUtils.ReadFx(token, nameof(SystemFontKey), SystemFontKey);
        SystemMediumFontKey = IOUtils.ReadFx(token, nameof(SystemMediumFontKey), SystemMediumFontKey);
        CodeFontKey = IOUtils.ReadFx(token, nameof(CodeFontKey), CodeFontKey);
        SystemFontFallbacks = IOUtils.ReadFx(token, nameof(SystemFontFallbacks), SystemFontFallbacks);
        SystemMediumFontFallbacks = IOUtils.ReadFx(token, nameof(SystemMediumFontFallbacks), SystemMediumFontFallbacks);
        CodeFontFallbacks = IOUtils.ReadFx(token, nameof(CodeFontFallbacks), CodeFontFallbacks);
        UseRawInput = IOUtils.ReadFx(token, nameof(UseRawInput), UseRawInput);
        WindowKey = IOUtils.ReadFx(token, nameof(WindowKey), WindowKey);
        HudEditorKey = IOUtils.ReadFx(token, nameof(HudEditorKey), HudEditorKey);
        HudSnap = IOUtils.ReadFx(token, nameof(HudSnap), HudSnap);
        HudSnapDistance = IOUtils.ReadFx(token, nameof(HudSnapDistance), HudSnapDistance);
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
        HudAutoAnchor = IOUtils.ReadFx(token, nameof(HudAutoAnchor), HudAutoAnchor);
        HudGuides = IOUtils.ReadFx(token, nameof(HudGuides), HudGuides);
        HudDetached = IOUtils.ReadFx(token, nameof(HudDetached), HudDetached);
        TutorialDone = IOUtils.ReadFx(token, nameof(TutorialDone), TutorialDone);
        TutorialOffered = IOUtils.ReadFx(token, nameof(TutorialOffered), TutorialOffered);
        HideTutorial = IOUtils.ReadFx(token, nameof(HideTutorial), HideTutorial);
        TabLabelMode = IOUtils.ReadFx(token, nameof(TabLabelMode), TabLabelMode);
        PanelFadeMode = IOUtils.ReadFx(token, nameof(PanelFadeMode), PanelFadeMode);
        SetupPending = IOUtils.ReadFx(token, nameof(SetupPending), SetupPending);
        LinuxTextInputFix = IOUtils.ReadFx(token, nameof(LinuxTextInputFix), LinuxTextInputFix);
    }
}
