using Overlayer.Tween;
using Overlayer.Compat;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Resource;
using O5Kit.Core;
using O5Kit.Factory;
using O5Kit.Control;
using O5Kit.Behaviour;
using Overlayer.Utility;
using Overlayer.IO.Fx;
using Overlayer.IO.User;
using Overlayer.Package;
using Overlayer.Native;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageSettings {
    private const string BuiltinFontOption = "@builtin-default";
    private sealed class FallbackListView {
        public GameObject Root;
        public readonly List<O5Object> Controls = [];
    }

    private static readonly List<Action> fontOptionRefreshers = [];
    private static bool fontRefreshHooked;
    private static readonly Dictionary<TextLocalization, (GameObject LabelRow, GameObject MainRow)> objects = [];
    private static O5Dropdown<string> languageDropdown;
    private static O5Slider uiScaleSlider;
    private static O5Slider sliderSensitivitySlider;
    private static O5Slider animationSpeedSlider;
    private static O5Slider windowOpacitySlider;
    private static O5Slider hudOpacitySlider;
    private static O5Slider tooltipOpacitySlider;
    private static O5Slider soundVolSlider;
    private static O5Slider snapDistanceSlider;
    private static O5Slider gridSizeSlider;
    private static O5Toggle startupToggle;
    private static O5Toggle tooltipToggle;
    private static O5Toggle middleClickToggle;
    private static O5Toggle threeDToggle;
    private static O5Toggle rawInputToggle;
    private static O5Toggle wheelAdjustToggle;
    private static O5Toggle snapToggle;
    private static O5Toggle showInternalImagesToggle;
    private static O5Toggle showInternalFontsToggle;
    private static O5Toggle hideTutorialToggle;
    private static O5Dropdown<TabLabelMode> tabLabelModeDropdown;
    private static O5Dropdown<PanelFadeMode> fadeModeDropdown;
    private static O5Toggle scrollEasingToggle;
    private static O5Dropdown<string> palettePresetDropdown;
    private static O5ColorPicker paletteColorPicker;
    private static O5InputField findInput;
    private static Action refreshWindowKey;
    private static Action refreshHudKey;
    private static Action refreshPaletteLabels;
    private static Action refreshTabLabelDropdown;

    internal static void ClearSearch() {
        if (findInput != null && !string.IsNullOrEmpty(findInput.Value)) {
            findInput.Set(string.Empty, true);
        }
    }

    /// <summary>Shows values changed elsewhere (the setup wizard).</summary>
    internal static void SyncValues() {
        languageDropdown?.Set(MainCore.Tr.Language, false);
        uiScaleSlider?.Set(MainCore.Conf.UIScale, false);
        startupToggle?.Set(MainCore.Conf.ShowOnStartup, false);
        tooltipToggle?.Set(MainCore.Conf.Tooltip, false);
        middleClickToggle?.Set(MainCore.Conf.MiddleClickToDefault, false);
        threeDToggle?.Set(MainCore.Conf.Enable3DFeatures, false);
        rawInputToggle?.Set(MainCore.Conf.UseRawInput, false);
        wheelAdjustToggle?.Set(MainCore.Conf.WheelAdjustSlider, false);
        sliderSensitivitySlider?.Set(MainCore.Conf.SliderSensitivity, false);
        animationSpeedSlider?.Set(MainCore.Conf.AnimationSpeed, false);
        windowOpacitySlider?.Set(MainCore.Conf.WindowOpacity, false);
        hudOpacitySlider?.Set(MainCore.Conf.HudWindowOpacity, false);
        tooltipOpacitySlider?.Set(MainCore.Conf.TooltipOpacity, false);
        tooltipOpacitySlider?.SetBlocked(!MainCore.Conf.Tooltip.Value, true);
        soundVolSlider?.Set(MainCore.Conf.UISoundVolume, false);
        snapToggle?.Set(MainCore.Conf.HudSnap, false);
        snapDistanceSlider?.Set(MainCore.Conf.HudSnapDistance, false);
        gridSizeSlider?.Set(MainCore.Conf.HudGridSize, false);
        showInternalImagesToggle?.Set(MainCore.Conf.ShowInternalImages.Value, false);
        showInternalFontsToggle?.Set(MainCore.Conf.ShowInternalFonts.Value, false);
        hideTutorialToggle?.Set(MainCore.Conf.HideTutorial.Value, false);
        tabLabelModeDropdown?.Set(MainCore.Conf.TabLabelMode.Value, false);
        fadeModeDropdown?.Set(MainCore.Conf.PanelFadeMode.Value, false);
        scrollEasingToggle?.Set(MainCore.Conf.ScrollEasing.Value, false);
        string configuredColor = MainCore.Conf.UIBaseColor.Value;
        Color baseColor = UIColors.IsStaticToken(configuredColor)
            ? UIColors.StaticDisplayColor
            : UIColors.ParseBaseColor(configuredColor);
        paletteColorPicker?.Set(baseColor, false);
        palettePresetDropdown?.Set(UIColors.MatchConfiguredPresetId(configuredColor), false);
        refreshWindowKey?.Invoke();
        refreshHudKey?.Invoke();
        refreshPaletteLabels?.Invoke();
        refreshTabLabelDropdown?.Invoke();
    }

    public static void Create(RectTransform parent) {
        objects.Clear();
        var (_, contentRect, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, parent, 12f, 18f);
        GameObject content = contentRect.gameObject;

        CoreSettings defSet = new();

        var inputRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        findInput =
        O5Factory.Input(O5KitAdapters.Ctx, inputRow, "search_find", new O5InputOptions { OnChanged = value => {
                bool isBlank = string.IsNullOrWhiteSpace(value);
                Dictionary<GameObject, bool> labelActivationMap = [];

                foreach (var pair in objects.Where(pair => pair.Value.LabelRow != null)) {
                    labelActivationMap[pair.Value.LabelRow] = isBlank;
                }

                string normalizedQuery = StringUtils.Normalize(value);

                if (MainCore.Conf.Language.Value == "ko-KR") {
                    normalizedQuery = StringUtils.NormalizeToHangulChosung(normalizedQuery);
                }

                foreach (var (labelLoc, valueTuple) in objects) {
                    var (labelRow, mainRow) = valueTuple;

                    if (labelRow == null || mainRow == null) {
                        continue;
                    }

                    string normalizedTarget = labelLoc != null ? StringUtils.Normalize(labelLoc.Value) : string.Empty;
                    if (MainCore.Conf.Language.Value == "ko-KR" && !string.IsNullOrEmpty(normalizedTarget)) {
                        normalizedTarget = StringUtils.NormalizeToHangulChosung(normalizedTarget);
                    }

                    bool isMainMatch = isBlank
                        || (
                            !string.IsNullOrEmpty(normalizedTarget)
                            && normalizedTarget.Contains(normalizedQuery)
                        );

                    mainRow.SetActive(isMainMatch);

                    if (isMainMatch) {
                        labelActivationMap[labelRow] = true;
                    }
                }

                foreach (var kvp in labelActivationMap) {
                    kvp.Key.SetActive(kvp.Value);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }, Placeholder = "Find", Icon = MainCore.Icons.Get(UIIcon.MagnifyingGlass) });
        findInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("FIND", "Find");
        findInput.InputField.characterLimit = 22;

        var langLabelRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var langText = O5Factory.ControlTextH1(O5KitAdapters.Ctx, langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");

        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        const float languageReloadWidth = 240f;
        const float languageControlSpacing = 8f;
        var langRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        languageDropdown = O5Factory.DropDown(O5KitAdapters.Ctx, langRow, "language_dropdown", new O5DropdownOptions<string> { Value = MainCore.Tr.Language, Values = langs, Display = lang => {
                if (lang == Translator.FALLBACK_LANGUAGE) {
                    return "DEFAULT";
                }

                string native = MainCore.Tr.GetForLanguage(
                    "0NATIVELANG",
                    lang,
                    lang
                );

                return $"{native} ({lang})";
            }, OnChanged = value => {
                MainCore.Tr.Language = value;
                MainCore.Conf.Language = value;
                MainCore.ConfMgr.RequestSave();
                TextLocalization.RefreshAll();
                refreshWindowKey?.Invoke();
                refreshHudKey?.Invoke();
                refreshPaletteLabels?.Invoke();
                foreach (var refresh in fontOptionRefreshers.ToArray()) {
                    refresh();
                }
            } });
        languageDropdown.Rect.offsetMax = new Vector2(-(languageReloadWidth + languageControlSpacing), 0f);
        var langBtn = O5Factory.Button(O5KitAdapters.Ctx, langRow, "language_reload", new O5ButtonOptions { Text = "Reload", OnClick = () => { } });
        langBtn.OnClick = async () => {
            languageDropdown.SetExpanded(false);
            languageDropdown.SetBlocked(true);
            langBtn.SetBlocked(true);
            langBtn.Label.text = "...";
            _ = Task.Run(async () => {
                await MainCore.Tr.Load(MainCore.Paths.LangPath);
                MainThread.Enqueue(() => {
                    languageDropdown.SetBlocked(false);
                    langBtn.SetBlocked(false);
                    TextLocalization.RefreshAll();
                });
            });
        };
        {
            var br = langBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.anchoredPosition = Vector2.zero;
            br.sizeDelta = new(languageReloadWidth, 50f);
        }
        langBtn.Label.gameObject.AddComponent<TextLocalization>().Init("RELOAD", "Reload");

        objects[langTextTr] = (langLabelRow.gameObject, langRow.gameObject);

        var overlayerText = O5Factory.ControlTextH1(O5KitAdapters.Ctx, O5Factory.Row(O5KitAdapters.Ctx, content.transform));
        var overlayerTextTr = overlayerText.gameObject.AddComponent<TextLocalization>().Init("OVERLAYER", "Overlayer");

        var startupRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        startupToggle = O5Factory.Toggle(O5KitAdapters.Ctx, startupRow, "show_on_startup", new O5ToggleOptions { DefaultValue = defSet.ShowOnStartup, Value = MainCore.Conf.ShowOnStartup, OnChanged = toggle => {
                MainCore.Conf.ShowOnStartup = toggle;
                MainCore.ConfMgr.RequestSave();
            }, Text = "Show Overlayer Panel at Startup" });
        var startupToggleTr = startupToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Overlayer Panel at Startup");
        objects[startupToggleTr] = (overlayerText.gameObject, startupRow.gameObject);

        var setupRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var setupButton = O5Factory.Button(O5KitAdapters.Ctx, setupRow, "run_setup", new O5ButtonOptions { Text = "Run Setup Again", OnClick = () => {
                // The intro belongs to the first run; Shift replays it.
                if (O5Kit.Input.O5Input.GetKey(KeyCode.LeftShift) || O5Kit.Input.O5Input.GetKey(KeyCode.RightShift)) {
                    Setup.SetupIntro.Play();
                } else {
                    Setup.SetupWizard.Open();
                }
            } });
        var setupButtonTr = setupButton.Label.gameObject.AddComponent<TextLocalization>().Init("SETUP_RUN_AGAIN", "Run Setup Again");
        objects[setupButtonTr] = (overlayerText.gameObject, setupRow.gameObject);

        var windowKeyRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        CreateKeyBindButton(
            windowKeyRow,
            "window_key",
            "WINDOW_KEY",
            "Shortcut",
            "DESC_WINDOW_KEY",
            "Click, then press a key (hold Ctrl / Shift / Alt for a combination). Hold to reset window position and size. Esc cancels.",
            () => UICore.WindowKey,
            b => {
                MainCore.Conf.WindowKey = b.ToString();
                MainCore.ConfMgr.RequestSave();
                UICore.RegisterToggleShortcut();
                PageDocs.RefreshHotkeys();
                Tutorial.Tutorial.RefreshTexts();
            },
            out refreshWindowKey);

        var tooltipRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        tooltipToggle = O5Factory.Toggle(O5KitAdapters.Ctx, tooltipRow, "show_tooltip", new O5ToggleOptions { DefaultValue = defSet.Tooltip, Value = MainCore.Conf.Tooltip, Text = "Show Tooltip" });
        tooltipToggle.OnChanged = toggle => {
            O5KitAdapters.Ctx.Tooltip.Hide();
            MainCore.Conf.Tooltip = toggle;
            O5KitAdapters.SyncConfig();
            MainCore.ConfMgr.RequestSave();
            tooltipOpacitySlider?.SetBlocked(!toggle);
        };
        tooltipToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get("DESC_SHOW_TOOLTIP", "This is a Tooltip!"));
        var tooltipToggleTr = tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        objects[tooltipToggleTr] = (overlayerText.gameObject, tooltipRow.gameObject);

        var middleClickRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        middleClickToggle = O5Factory.Toggle(O5KitAdapters.Ctx, middleClickRow, "middle_click_default", new O5ToggleOptions { DefaultValue = defSet.MiddleClickToDefault, Value = MainCore.Conf.MiddleClickToDefault, OnChanged = toggle => {
                MainCore.Conf.MiddleClickToDefault = toggle;
                MainCore.ConfMgr.RequestSave();
            }, Text = "Middle-click to set as default" });
        middleClickToggle.OnChanged += _ => O5KitAdapters.SyncConfig();
        middleClickToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        ));
        var middleClickToggleTr = middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        objects[middleClickToggleTr] = (overlayerText.gameObject, middleClickRow.gameObject);

        var threeDRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        threeDToggle = O5Factory.Toggle(O5KitAdapters.Ctx, threeDRow, "enable_3d_features", new O5ToggleOptions { DefaultValue = defSet.Enable3DFeatures, Value = MainCore.Conf.Enable3DFeatures, OnChanged = toggle => {
                MainCore.Conf.Enable3DFeatures = toggle;
                MainCore.ConfMgr.RequestSave();
            }, Text = "Enable 3D Features" });
        threeDToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_ENABLE_3D_FEATURES",
            "Shows 3D object creation and 3D components (Mesh, Colliders, Rigidbody, Light).\nNew empty objects are created without RectTransform, and top-level objects can live outside any canvas."
        ));
        var threeDToggleTr = threeDToggle.Label.gameObject.AddComponent<TextLocalization>().Init("ENABLE_3D_FEATURES", "Enable 3D Features");
        objects[threeDToggleTr] = (overlayerText.gameObject, threeDRow.gameObject);

        var rawInputRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        rawInputToggle = O5Factory.Toggle(O5KitAdapters.Ctx, rawInputRow, "use_raw_input", new O5ToggleOptions { DefaultValue = defSet.UseRawInput, Value = MainCore.Conf.UseRawInput, OnChanged = toggle => {
                MainCore.Conf.UseRawInput = toggle;
                MainCore.ConfMgr.RequestSave();
                if (toggle) {
                    OvRawInput.Start();
                } else {
                    OvRawInput.Stop();
                }
            }, Text = "Use OS Raw Input" });
        rawInputToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_USE_RAW_INPUT",
            "Collects keyboard input at OS level in the background with lower latency and higher accuracy (Windows/Linux only).\nDisable to fall back to standard Unity input polling."
        ));
        var rawInputToggleTr = rawInputToggle.Label.gameObject.AddComponent<TextLocalization>().Init("USE_RAW_INPUT", "Use OS Raw Input");
        objects[rawInputToggleTr] = (overlayerText.gameObject, rawInputRow.gameObject);

        var internalImgRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        showInternalImagesToggle = O5Factory.Toggle(O5KitAdapters.Ctx, internalImgRow, "show_internal_images", new O5ToggleOptions { DefaultValue = defSet.ShowInternalImages, Value = MainCore.Conf.ShowInternalImages, OnChanged = toggle => {
                MainCore.Conf.ShowInternalImages = toggle;
                MainCore.ConfMgr.RequestSave();
                PageResources.RefreshList();
                UserResourceManager.NotifyChanged();
            }, Text = "Show Internal Images" });
        showInternalImagesToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_SHOW_INTERNAL_IMAGES",
            "Show Overlayer's built-in images in the Resources tab and image pickers."
        ));
        var internalImgTr = showInternalImagesToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_INTERNAL_IMAGES", "Show Internal Images");
        objects[internalImgTr] = (overlayerText.gameObject, internalImgRow.gameObject);

        var internalFontRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        showInternalFontsToggle = O5Factory.Toggle(O5KitAdapters.Ctx, internalFontRow, "show_internal_fonts", new O5ToggleOptions { DefaultValue = defSet.ShowInternalFonts, Value = MainCore.Conf.ShowInternalFonts, OnChanged = toggle => {
                MainCore.Conf.ShowInternalFonts = toggle;
                MainCore.ConfMgr.RequestSave();
                PageResources.RefreshList();
                UserResourceManager.NotifyChanged();
                foreach (var refresh in fontOptionRefreshers.ToArray()) {
                    refresh();
                }
            }, Text = "Show Internal Fonts" });
        showInternalFontsToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_SHOW_INTERNAL_FONTS",
            "Show Overlayer's built-in fonts in the Resources tab and font pickers."
        ));
        var internalFontTr = showInternalFontsToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_INTERNAL_FONTS", "Show Internal Fonts");
        objects[internalFontTr] = (overlayerText.gameObject, internalFontRow.gameObject);

        var hideTutorialRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        hideTutorialToggle = O5Factory.Toggle(O5KitAdapters.Ctx, hideTutorialRow, "hide_tutorial", new O5ToggleOptions { DefaultValue = defSet.HideTutorial, Value = MainCore.Conf.HideTutorial, OnChanged = toggle => {
                MainCore.Conf.HideTutorial.Value = toggle;
                MainCore.ConfMgr.RequestSave();
                MenuFactory.SetItemVisible((int)OriginalMenuState.Tutorial, !toggle);
            }, Text = "Hide Tutorial" });
        hideTutorialToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_HIDE_TUTORIAL",
            "Hide the Tutorial tab from the left menu."
        ));
        var hideTutorialTr = hideTutorialToggle.Label.gameObject.AddComponent<TextLocalization>().Init("HIDE_TUTORIAL", "Hide Tutorial");
        objects[hideTutorialTr] = (overlayerText.gameObject, hideTutorialRow.gameObject);

        var tabLabelRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        string TabLabelModeDisplay(TabLabelMode mode) {
            string modeLabel = mode switch {
                TabLabelMode.Show => MainCore.Tr.Get("TAB_LABELS_SHOW", "Show Always"),
                TabLabelMode.Hover => MainCore.Tr.Get("TAB_LABELS_HOVER", "Show when Hover"),
                TabLabelMode.Hide => MainCore.Tr.Get("TAB_LABELS_HIDE", "Hide Always"),
                _ => mode.ToString(),
            };
            return $"{MainCore.Tr.Get("TAB_LABELS", "Tab Labels")}: {modeLabel}";
        }
        tabLabelModeDropdown = O5Factory.DropDown(O5KitAdapters.Ctx, tabLabelRow, "tab_label_mode", new O5DropdownOptions<TabLabelMode> {
            DefaultValue = defSet.TabLabelMode.Value,
            Value = MainCore.Conf.TabLabelMode.Value,
            Values = [TabLabelMode.Hover, TabLabelMode.Show, TabLabelMode.Hide],
            Display = TabLabelModeDisplay,
            OnChanged = mode => {
                MainCore.Conf.TabLabelMode.Value = mode;
                MainCore.ConfMgr.RequestSave();
                UICore.ApplyTabMode(mode);
            }
        });
        tabLabelModeDropdown.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_TAB_LABELS",
            "Controls whether the left menu shows tab labels always, only on hover, or stays icons-only."
        ));
        var tabLabelTr = tabLabelModeDropdown.Label.gameObject.AddComponent<TextLocalization>();
        // The label shows "Tab Labels: <mode>"; without Format, OnEnable/RefreshAll would reset it to just "Tab Labels".
        tabLabelTr.Format = _ => TabLabelModeDisplay(tabLabelModeDropdown.Value);
        tabLabelTr.Init("TAB_LABELS", "Tab Labels");
        refreshTabLabelDropdown = () => {
            if (tabLabelModeDropdown != null) {
                tabLabelModeDropdown.RebuildList();
                tabLabelModeDropdown.Label.text = TabLabelModeDisplay(tabLabelModeDropdown.Value);
            }
        };
        refreshTabLabelDropdown();
        objects[tabLabelTr] = (overlayerText.gameObject, tabLabelRow.gameObject);

        var fadeModeRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        static string FadeModeDisplay(PanelFadeMode mode) {
            string modeLabel = mode switch {
                PanelFadeMode.FlyIn => "Fly In",
                PanelFadeMode.Fade => "Fade",
                PanelFadeMode.DiagonalSwipe => "Diagonal Swipe",
                PanelFadeMode.Taskbar => "Taskbar",
                PanelFadeMode.Clock => "Clock",
                _ => mode.ToString(),
            };
            return $"Fade Mode: {modeLabel}";
        }
        fadeModeDropdown = O5Factory.DropDown(O5KitAdapters.Ctx, fadeModeRow, "panel_fade_mode", new O5DropdownOptions<PanelFadeMode> {
            DefaultValue = defSet.PanelFadeMode.Value,
            Value = MainCore.Conf.PanelFadeMode.Value,
            Values = [PanelFadeMode.FlyIn, PanelFadeMode.Fade, PanelFadeMode.DiagonalSwipe, PanelFadeMode.Taskbar, PanelFadeMode.Clock],
            Display = FadeModeDisplay,
            OnChanged = mode => {
                MainCore.Conf.PanelFadeMode.Value = mode;
                MainCore.ConfMgr.RequestSave();
            }
        });
        fadeModeDropdown.Rect.AddToolTip(O5KitAdapters.Ctx, () => "How the Overlayer panel appears and disappears when toggled.");
        var fadeModeTr = fadeModeDropdown.Label.gameObject.AddComponent<TextLocalization>();
        fadeModeTr.Format = _ => FadeModeDisplay(fadeModeDropdown.Value);
        fadeModeTr.Init("FADE_MODE", "Fade Mode");
        objects[fadeModeTr] = (overlayerText.gameObject, fadeModeRow.gameObject);


        var uiScaleRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var uiScale = O5Factory.Slider(O5KitAdapters.Ctx, uiScaleRow, "ui_scale", new O5SliderOptions { DefaultValue = defSet.UIScale, Min = 0.8f, Max = 1.6f, Value = MainCore.Conf.UIScale, Format = "0.00x", Label = "UI Scale", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.UIScale = (float)value;
                O5KitAdapters.SyncConfig();
            }, OnComplete = value => {
                MainCore.Conf.UIScale = (float)value;
                O5KitAdapters.SyncConfig();
                MainCore.ConfMgr.RequestSave();
                UICore.AnimateUiScale((float)value);
            } });
        uiScaleSlider = uiScale;
        var targetSize = UICore.DefaultPanelSize;
        var uiScaleTr = uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        objects[uiScaleTr] = (overlayerText.gameObject, uiScaleRow.gameObject);

        var sliderSensitivityRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        sliderSensitivitySlider = O5Factory.Slider(O5KitAdapters.Ctx, sliderSensitivityRow, "slider_sensitivity", new O5SliderOptions { DefaultValue = defSet.SliderSensitivity, Min = 0.1f, Max = 2.0f, Value = MainCore.Conf.SliderSensitivity, Format = "0.00x", Label = "Slider Sensitivity", ClampDrag = true, ClampInput = false, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.SliderSensitivity = (float)value;
                O5KitAdapters.SyncConfig();
            }, OnComplete = value => {
                MainCore.Conf.SliderSensitivity = (float)value;
                O5KitAdapters.SyncConfig();
                MainCore.ConfMgr.RequestSave();
            } });
        var sliderSensitivityTr = sliderSensitivitySlider.Label.gameObject.AddComponent<TextLocalization>().Init("SLIDER_SENSITIVITY", "Slider Sensitivity");
        objects[sliderSensitivityTr] = (overlayerText.gameObject, sliderSensitivityRow.gameObject);

        var wheelAdjustRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        wheelAdjustToggle = O5Factory.Toggle(O5KitAdapters.Ctx, wheelAdjustRow, "wheel_adjust_slider", new O5ToggleOptions { DefaultValue = defSet.WheelAdjustSlider, Value = MainCore.Conf.WheelAdjustSlider, OnChanged = toggle => {
                MainCore.Conf.WheelAdjustSlider = toggle;
                MainCore.ConfMgr.RequestSave();
            }, Text = "Adjust Sliders & Dropdowns with Scroll" });
        var wheelAdjustTr = wheelAdjustToggle.Label.gameObject.AddComponent<TextLocalization>().Init("WHEEL_ADJUST_SLIDER", "Adjust Sliders & Dropdowns with Scroll");
        objects[wheelAdjustTr] = (overlayerText.gameObject, wheelAdjustRow.gameObject);

        var animationSpeedRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        animationSpeedSlider = O5Factory.Slider(O5KitAdapters.Ctx, animationSpeedRow, "animation_speed", new O5SliderOptions { DefaultValue = defSet.AnimationSpeed, Min = 0.0f, Max = 3.0f, Value = MainCore.Conf.AnimationSpeed, Format = "0.00x", Label = "Animation Speed", ClampDrag = true, ClampInput = false, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.AnimationSpeed = (float)value;
                O5KitAdapters.SyncConfig();
            }, OnComplete = value => {
                MainCore.Conf.AnimationSpeed = (float)value;
                O5KitAdapters.SyncConfig();
                MainCore.ConfMgr.RequestSave();
            } });
        var animationSpeedTr = animationSpeedSlider.Label.gameObject.AddComponent<TextLocalization>().Init("ANIMATION_SPEED", "Animation Speed");
        objects[animationSpeedTr] = (overlayerText.gameObject, animationSpeedRow.gameObject);

        var windowOpacityRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        windowOpacitySlider = O5Factory.Slider(O5KitAdapters.Ctx, windowOpacityRow, "window_opacity", new O5SliderOptions { DefaultValue = defSet.WindowOpacity, Min = 0.2f, Max = 1.0f, Value = MainCore.Conf.WindowOpacity, Format = "0.00", Label = "Window Opacity", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.WindowOpacity = (float)value;
                PanelTransition.ApplyOpacity();
            }, OnComplete = value => {
                MainCore.Conf.WindowOpacity = (float)value;
                PanelTransition.ApplyOpacity();
                MainCore.ConfMgr.RequestSave();
            } });
        windowOpacitySlider.Rect.AddToolTip(O5KitAdapters.Ctx, () => "Opacity of the Overlayer window.");
        objects[windowOpacitySlider.Label.gameObject.AddComponent<TextLocalization>().Init("WINDOW_OPACITY", "Window Opacity")] = (overlayerText.gameObject, windowOpacityRow.gameObject);

        var tooltipOpacityRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        tooltipOpacitySlider = O5Factory.Slider(O5KitAdapters.Ctx, tooltipOpacityRow, "tooltip_opacity", new O5SliderOptions { DefaultValue = defSet.TooltipOpacity, Min = 0.2f, Max = 1.0f, Value = MainCore.Conf.TooltipOpacity, Format = "0.00", Label = "Tooltip Opacity", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.TooltipOpacity = (float)value;
                O5KitAdapters.SyncConfig();
            }, OnComplete = value => {
                MainCore.Conf.TooltipOpacity = (float)value;
                O5KitAdapters.SyncConfig();
                MainCore.ConfMgr.RequestSave();
            } });
        tooltipOpacitySlider.SetBlocked(!MainCore.Conf.Tooltip.Value, true);
        tooltipOpacitySlider.Rect.AddToolTip(O5KitAdapters.Ctx, () => "Opacity of tooltips.");
        objects[tooltipOpacitySlider.Label.gameObject.AddComponent<TextLocalization>().Init("TOOLTIP_OPACITY", "Tooltip Opacity")] = (overlayerText.gameObject, tooltipOpacityRow.gameObject);


        var scrollEasingRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        scrollEasingToggle = O5Factory.Toggle(O5KitAdapters.Ctx, scrollEasingRow, "scroll_easing", new O5ToggleOptions {
            DefaultValue = defSet.ScrollEasing,
            Value = MainCore.Conf.ScrollEasing,
            OnChanged = toggle => {
                MainCore.Conf.ScrollEasing = toggle;
                O5KitAdapters.SyncConfig();
                MainCore.ConfMgr.RequestSave();
            },
            Text = "Scroll Easing"
        });
        scrollEasingToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_SCROLL_EASING",
            "Smoothly eases wheel and right-click drag scrolling. Affected by Animation Speed."
        ));
        var scrollEasingTr = scrollEasingToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SCROLL_EASING", "Scroll Easing");
        objects[scrollEasingTr] = (overlayerText.gameObject, scrollEasingRow.gameObject);

        var soundVolRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        soundVolSlider = O5Factory.Slider(O5KitAdapters.Ctx, soundVolRow, "ui_sound_volume", new O5SliderOptions { DefaultValue = defSet.UISoundVolume, Min = 0.0f, Max = 1.0f, Value = MainCore.Conf.UISoundVolume, Format = "F2", Label = "UI Sound Volume", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.UISoundVolume = Mathf.Clamp01((float)value);
            }, OnComplete = value => {
                MainCore.Conf.UISoundVolume = Mathf.Clamp01((float)value);
                MainCore.ConfMgr.RequestSave();
            } });
        var soundVolTr = soundVolSlider.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SOUND_VOLUME", "UI Sound Volume");
        objects[soundVolTr] = (overlayerText.gameObject, soundVolRow.gameObject);

        // Split the long Overlayer block into sections (rows are re-ordered under new headers).
        MoveToSection(content, "INTERFACE", "Interface", [
            tabLabelRow, fadeModeRow, uiScaleRow, windowOpacityRow, tooltipOpacityRow,
            animationSpeedRow, scrollEasingRow, soundVolRow]);
        MoveToSection(content, "INPUT", "Input", [
            middleClickRow, rawInputRow, sliderSensitivityRow, wheelAdjustRow]);

        BuildPaletteSettings(content, overlayerText);

        BuildFontPickers(content, objects, overlayerText);

        BuildHudEditor(content, defSet);

        BuildResetAll(content);
    }

    // Appends an H1 header plus the given rows (in order) to the end of
    // content, and points their search entries at the new header.
    private static void MoveToSection(GameObject content, string key, string title, RectTransform[] rows) {
        var headerRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        O5Factory.ControlTextH1(O5KitAdapters.Ctx, headerRow).gameObject.AddComponent<TextLocalization>().Init(key, title);
        var moved = new HashSet<GameObject>();
        foreach (var row in rows) {
            row.SetAsLastSibling();
            moved.Add(row.gameObject);
        }
        foreach (var loc in objects.Keys.ToList()) {
            if (moved.Contains(objects[loc].MainRow)) {
                objects[loc] = (headerRow.gameObject, objects[loc].MainRow);
            }
        }
    }

    private static void BuildPaletteSettings(GameObject content, TextMeshProUGUI overlayerText) {
        var ctx = O5KitAdapters.Ctx;
        var paletteLabelRow = O5Factory.Row(ctx, content.transform);
        var paletteLabel = O5Factory.ControlTextH1(ctx, paletteLabelRow);
        var paletteLabelTr = paletteLabel.gameObject.AddComponent<TextLocalization>().Init("UI_PALETTE", "UI Palette");

        string[] presetIds = [.. UIColors.Presets.Select(p => p.Id), UIColors.CustomPresetId];
        string PresetDisplay(string id) {
            for (int i = 0; i < UIColors.Presets.Length; i++) {
                if (UIColors.Presets[i].Id == id) {
                    return MainCore.Tr.Get(UIColors.Presets[i].LangKey, UIColors.Presets[i].FallbackName);
                }
            }
            return MainCore.Tr.Get("UI_PALETTE_CUSTOM", "Custom");
        }

        string configuredColor = MainCore.Conf.UIBaseColor.Value;
        bool isStatic = UIColors.IsStaticToken(configuredColor);
        Color initialColor = isStatic ? UIColors.StaticDisplayColor : UIColors.ParseBaseColor(configuredColor);
        string initialPreset = UIColors.MatchConfiguredPresetId(configuredColor);

        var presetRow = O5Factory.Row(ctx, content.transform);
        palettePresetDropdown = O5Factory.DropDown(ctx, presetRow, "ui_palette_preset", new O5DropdownOptions<string> {
            DefaultValue = UIColors.Presets[0].Id,
            Value = initialPreset,
            Values = presetIds,
            Display = PresetDisplay,
            OnChanged = id => {
                if (id == UIColors.StaticPresetId) {
                    paletteColorPicker?.Set(UIColors.StaticDisplayColor, false);
                    MainCore.Conf.UIBaseColor.Value = UIColors.StaticConfigToken;
                    UIColors.ApplyStatic(true);
                    MainCore.ConfMgr.RequestSave();
                } else if (UIColors.TryGetPresetColor(id, out Color presetColor)) {
                    paletteColorPicker?.Set(presetColor, false);
                    MainCore.Conf.UIBaseColor.Value = UIColors.ToHexRgb(presetColor);
                    UIColors.ApplyBaseColor(presetColor, true);
                    MainCore.ConfMgr.RequestSave();
                } else if (id != UIColors.CustomPresetId) {
                    paletteColorPicker?.Set(UIColors.DefaultBaseColor, false);
                    MainCore.Conf.UIBaseColor.Value = UIColors.DefaultBaseHex;
                    UIColors.ApplyBaseColor(UIColors.DefaultBaseColor, true);
                    MainCore.ConfMgr.RequestSave();
                }
            }
        });
        palettePresetDropdown.Rect.AddToolTip(ctx, () => MainCore.Tr.Get(
            "DESC_UI_PALETTE",
            "Derives the entire UI color palette from a single base hue using balanced saturation and brightness."
        ));
        objects[paletteLabelTr] = (paletteLabelRow.gameObject, presetRow.gameObject);

        var colorRow = O5Factory.Row(ctx, content.transform);
        paletteColorPicker = O5Factory.ColorPicker(ctx, colorRow, "ui_palette_color", new O5ColorPickerOptions {
            CanvasRect = UICore.CanvasObj.GetComponent<RectTransform>(),
            CanvasCamera = UICore.Canvas ? UICore.Canvas.worldCamera : null,
            DefaultValue = UIColors.DefaultBaseColor,
            Value = initialColor,
            Label = MainCore.Tr.Get("UI_PALETTE_COLOR", "Base Color"),
            OnChanged = color => {
                Color opaque = new(color.r, color.g, color.b, 1f);
                UIColors.ApplyBaseColor(opaque, true);
                palettePresetDropdown?.Set(UIColors.MatchPresetId(opaque), false);
            },
            OnComplete = color => {
                Color opaque = new(color.r, color.g, color.b, 1f);
                MainCore.Conf.UIBaseColor.Value = UIColors.ToHexRgb(opaque);
                UIColors.ApplyBaseColor(opaque, true);
                palettePresetDropdown?.Set(UIColors.MatchPresetId(opaque), false);
                MainCore.ConfMgr.RequestSave();
            }
        });
        paletteColorPicker.Rect.AddToolTip(ctx, () => MainCore.Tr.Get(
            "DESC_UI_PALETTE_COLOR",
            "Customize Overlayer with a monochrome palette using your chosen color."
        ));
        var colorLabel = paletteColorPicker.Rect.Find("Header/Preview/ColorLabel")?.GetComponent<TMP_Text>();
        if (colorLabel != null) {
            var colorLabelTr = colorLabel.gameObject.AddComponent<TextLocalization>().Init("UI_PALETTE_COLOR", "Base Color");
            objects[colorLabelTr] = (paletteLabelRow.gameObject, colorRow.gameObject);
        }

        refreshPaletteLabels = () => {
            if (palettePresetDropdown != null && !palettePresetDropdown.IsDisposed && palettePresetDropdown.Rect) {
                string cur = palettePresetDropdown.Value;
                palettePresetDropdown.SetValues(presetIds);
                palettePresetDropdown.Set(cur, false);
            }
        };
    }

    private static void BuildHudEditor(GameObject content, CoreSettings defSet) {
        var ctx = O5KitAdapters.Ctx;
        var headerRow = O5Factory.Row(ctx, content.transform);
        var header = O5Factory.ControlTextH1(ctx, headerRow);
        header.gameObject.AddComponent<TextLocalization>().Init("HUD_EDITOR", "HUD Editor");

        var keyRow = O5Factory.Row(ctx, content.transform);
        CreateKeyBindButton(
            keyRow,
            "hud_editor_key",
            "HUD_EDITOR_KEY",
            "Shortcut",
            "DESC_HUD_EDITOR_KEY",
            "Click, then press a key (hold Ctrl / Shift / Alt for a combination). Esc cancels.",
            () => HudEdit.HudEditor.Key,
            b => {
                MainCore.Conf.HudEditorKey = b.ToString();
                MainCore.ConfMgr.RequestSave();
                HudEdit.HudEditor.RegisterShortcut();
                PageDocs.RefreshHotkeys();
                Tutorial.Tutorial.RefreshTexts();
            },
            out refreshHudKey);

        var snapRow = O5Factory.Row(ctx, content.transform);
        snapToggle = O5Factory.Toggle(ctx, snapRow, "hud_snap", new O5ToggleOptions { DefaultValue = defSet.HudSnap, Value = MainCore.Conf.HudSnap, OnChanged = toggle => {
                MainCore.Conf.HudSnap = toggle;
                MainCore.ConfMgr.RequestSave();
            }, Text = "Snap to Edges and Other Objects" });
        snapToggle.Rect.AddToolTip(ctx, () => MainCore.Tr.Get("DESC_HUD_SNAP", "Hold Alt while dragging to temporarily disable snapping."));
        var snapTr = snapToggle.Label.gameObject.AddComponent<TextLocalization>().Init("HUD_SNAP", "Snap to Edges and Other Objects");
        objects[snapTr] = (headerRow.gameObject, snapRow.gameObject);

        var distanceRow = O5Factory.Row(ctx, content.transform);
        snapDistanceSlider = O5Factory.Slider(ctx, distanceRow, "hud_snap_distance", new O5SliderOptions { DefaultValue = defSet.HudSnapDistance, Min = 2f, Max = 32f, Value = MainCore.Conf.HudSnapDistance, Format = "0 px", Label = "Snap Distance", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value), OnComplete = value => {
                MainCore.Conf.HudSnapDistance = (float)value;
                MainCore.ConfMgr.RequestSave();
            } });
        var distanceTr = snapDistanceSlider.Label.gameObject.AddComponent<TextLocalization>().Init("HUD_SNAP_DISTANCE", "Snap Distance");
        objects[distanceTr] = (headerRow.gameObject, distanceRow.gameObject);

        var gridRow = O5Factory.Row(ctx, content.transform);
        gridSizeSlider = O5Factory.Slider(ctx, gridRow, "hud_grid_size", new O5SliderOptions { DefaultValue = defSet.HudGridSize, Min = 8f, Max = 200f, Value = MainCore.Conf.HudGridSize, Format = "0", Label = "Grid Size", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value), OnComplete = value => {
                MainCore.Conf.HudGridSize = (float)value;
                MainCore.ConfMgr.RequestSave();
            } });
        gridSizeSlider.Rect.AddToolTip(ctx, () => MainCore.Tr.Get("DESC_HUD_GRID_SIZE", "Spacing of the HUD editor grid, in HUD units."));
        var gridTr = gridSizeSlider.Label.gameObject.AddComponent<TextLocalization>().Init("HUD_GRID_SIZE", "Grid Size");
        objects[gridTr] = (headerRow.gameObject, gridRow.gameObject);

        var hudOpacityRow = O5Factory.Row(ctx, content.transform);
        hudOpacitySlider = O5Factory.Slider(ctx, hudOpacityRow, "hud_window_opacity", new O5SliderOptions { DefaultValue = defSet.HudWindowOpacity, Min = 0.2f, Max = 1.0f, Value = MainCore.Conf.HudWindowOpacity, Format = "0.00", Label = "HUD Window Opacity", ClampDrag = true, ClampInput = true, DragFilter = value => Math.Round(value, 2), InputFilter = value => Math.Round(value, 2), OnChanged = value => {
                MainCore.Conf.HudWindowOpacity = (float)value;
            }, OnComplete = value => {
                MainCore.Conf.HudWindowOpacity = (float)value;
                MainCore.ConfMgr.RequestSave();
            } });
        hudOpacitySlider.Rect.AddToolTip(ctx, () => "Opacity of the HUD editor's toolbar, panels and bottom bar.");
        objects[hudOpacitySlider.Label.gameObject.AddComponent<TextLocalization>().Init("HUD_WINDOW_OPACITY", "HUD Window Opacity")] = (headerRow.gameObject, hudOpacityRow.gameObject);
    }

    private static void BuildResetAll(GameObject content) {
        var ctx = O5KitAdapters.Ctx;
        var headerRow = O5Factory.Row(ctx, content.transform);
        var header = O5Factory.ControlTextH1(ctx, headerRow);
        var headerTr = header.gameObject.AddComponent<TextLocalization>().Init("RESET", "Reset");

        var btnRow = O5Factory.Row(ctx, content.transform);
        var btn = O5Factory.Button(ctx, btnRow, "reset_all_settings",
            new O5ButtonOptions { Text = MainCore.Tr.Get("RESET_ALL_SETTINGS", "Reset All Settings"), OnClick = () => { } });
        var btnTr = btn.Label.gameObject.AddComponent<TextLocalization>().Init("RESET_ALL_SETTINGS", "Reset All Settings");
        btn.Rect.AddToolTip(ctx, () => MainCore.Tr.Get(
            "DESC_RESET_ALL_SETTINGS",
            "Restores every setting to its default. Hold to confirm."
        ));
        HoldToConfirm.Attach(btn, ResetAllSettings);
        objects[headerTr] = (headerRow.gameObject, btnRow.gameObject);
        objects[btnTr] = (headerRow.gameObject, btnRow.gameObject);
    }

    private static void ResetAllSettings() {
        var def = new CoreSettings();
        MainCore.Conf.Deserialize(def.Serialize());
        // shortcut: state flags, not settings; keeps the setup wizard from popping on the next scene load.
        MainCore.Conf.IsFirstRun = false;
        MainCore.ConfMgr.RequestSave();
        MainCore.Tr.Language = MainCore.Conf.Language.Value;
        O5KitAdapters.SyncConfig();
        UIColors.ApplyConfigured(MainCore.Conf.UIBaseColor.Value, true);
        O5KitAdapters.RefreshFonts();
        UICore.RegisterToggleShortcut();
        HudEdit.HudEditor.RegisterShortcut();
        if (MainCore.Conf.UseRawInput.Value) {
            OvRawInput.Start();
        } else {
            OvRawInput.Stop();
        }
        UICore.AnimateUiScale(MainCore.Conf.UIScale.Value);
        TextLocalization.RefreshAll();
        SyncValues();
        foreach (var refresh in fontOptionRefreshers.ToArray()) {
            refresh();
        }
        PageDocs.RefreshHotkeys();
        Tutorial.Tutorial.RefreshTexts();
    }

    private static void BuildFontPickers(GameObject content, Dictionary<TextLocalization, (GameObject, GameObject)> objects, TextMeshProUGUI overlayerText) {
        fontOptionRefreshers.Clear();
        if (!fontRefreshHooked) {
            fontRefreshHooked = true;
            Overlayer.UI.Factory.MenuFactory.OnStateChanged += state => {
                if (state == (int)Overlayer.UI.OriginalMenuState.Settings) {
                    foreach (var refresh in fontOptionRefreshers.ToArray()) {
                        refresh();
                    }
                }
            };
        }
        BuildSystemFontPickers(content, objects, overlayerText);
        BuildFontPicker(content, objects, overlayerText,
            "CODE_FONT", "Code Font", MainCore.Conf.CodeFontKey, MainCore.Conf.CodeFontFallbacks,
            "CODE_FONT_FALLBACKS", "Code Font Fallbacks");
    }

    private static void BuildSystemFontPickers(GameObject content, Dictionary<TextLocalization, (GameObject, GameObject)> objects,
        TextMeshProUGUI overlayerText) {
        GameObject warnRow = null;
        TextMeshProUGUI warnText = null;
        void RefreshWarning() {
            if (warnRow == null || warnText == null) {
                return;
            }
            bool hasReg = !string.IsNullOrEmpty(MainCore.Conf.SystemFontKey.Value);
            bool hasMed = !string.IsNullOrEmpty(MainCore.Conf.SystemMediumFontKey.Value);
            if (hasReg && !hasMed) {
                warnRow.SetActive(true);
                warnText.text = MainCore.Tr.Get("WARN_SYSTEM_MEDIUM_DEFAULT",
                    "Medium font is set to Default, so bold text will not appear bold.");
            } else if (!hasReg && hasMed) {
                warnRow.SetActive(true);
                warnText.text = MainCore.Tr.Get("WARN_SYSTEM_MEDIUM_ONLY",
                    "Medium font alone cannot be applied. Change the Regular font to change standard text.");
            } else {
                warnRow.SetActive(false);
            }
        }

        var colsObj = new GameObject("SystemFontColumns");
        colsObj.transform.SetParent(content.transform, false);
        colsObj.AddComponent<RectTransform>();
        var colsLayout = colsObj.AddComponent<HorizontalLayoutGroup>();
        colsLayout.spacing = 16f;
        colsLayout.childControlWidth = true;
        colsLayout.childControlHeight = true;
        colsLayout.childForceExpandWidth = true;
        colsLayout.childForceExpandHeight = false;
        colsLayout.childAlignment = TextAnchor.UpperLeft;
        colsObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var colsLe = colsObj.AddComponent<LayoutElement>();
        colsLe.minWidth = 0f;
        colsLe.flexibleWidth = 1f;

        GameObject MakeColumn(string name) {
            var col = new GameObject(name);
            col.transform.SetParent(colsObj.transform, false);
            col.AddComponent<RectTransform>();
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = col.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.flexibleWidth = 1f;
            return col;
        }

        var regCol = MakeColumn("RegularCol");
        var regFallbacks = BuildFontDropdown(regCol, objects, overlayerText,
            "SYSTEM_FONT", "System Font (Regular)", MainCore.Conf.SystemFontKey, MainCore.Conf.SystemFontFallbacks,
            "DESC_SYSTEM_FONT", "UI font (Regular), loaded from Resources. Empty means the built-in default.",
            onChanged: RefreshWarning);
        BuildFallbackSection(regCol, "SYSTEM_FONT", MainCore.Conf.SystemFontKey, MainCore.Conf.SystemFontFallbacks,
            "SYSTEM_FONT_FALLBACKS", "System Font Fallbacks", regFallbacks);

        var medCol = MakeColumn("MediumCol");
        var medFallbacks = BuildFontDropdown(medCol, objects, overlayerText,
            "SYSTEM_MEDIUM_FONT", "System Font (Medium)", MainCore.Conf.SystemMediumFontKey, MainCore.Conf.SystemMediumFontFallbacks,
            "DESC_SYSTEM_MEDIUM_FONT", "Bold/header UI font (Medium), loaded from Resources. Empty means the built-in default.",
            onChanged: RefreshWarning);
        BuildFallbackSection(medCol, "SYSTEM_MEDIUM_FONT", MainCore.Conf.SystemMediumFontKey, MainCore.Conf.SystemMediumFontFallbacks,
            "SYSTEM_FONT_FALLBACKS", "System Font Fallbacks", medFallbacks);

        var warnRect = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 44f);
        warnRow = warnRect.gameObject;
        warnText = O5Factory.ControlText(O5KitAdapters.Ctx, warnRect, 16f, true);
        warnText.color = new Color(1f, 0.78f, 0.32f, 1f);
        warnText.enableWordWrapping = true;
        RefreshWarning();
        fontOptionRefreshers.Add(RefreshWarning);
    }

    private static void BuildFontPicker(GameObject content, Dictionary<TextLocalization, (GameObject, GameObject)> objects,
        TextMeshProUGUI overlayerText, string key, string label, FxValue<string> fx,
        FxValue<List<string>> fallbacks, string fallbacksKey, string fallbacksLabel) {
        var fallbackList = BuildFontDropdown(content, objects, overlayerText, key, label, fx, fallbacks,
            "DESC_CODE_FONT", "Code and numeric font, loaded from Resources. Empty means the built-in default.");
        BuildFallbackSection(content, key, fx, fallbacks, fallbacksKey, fallbacksLabel, fallbackList);
    }

    private static FallbackListView BuildFontDropdown(GameObject content, Dictionary<TextLocalization, (GameObject, GameObject)> objects,
        TextMeshProUGUI overlayerText, string key, string label, FxValue<string> fx,
        FxValue<List<string>> fallbacks, string descKey, string descFallback, Action onChanged = null) {
        var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var text = O5Factory.ControlTextH1(O5KitAdapters.Ctx, row);
        var textTr = text.gameObject.AddComponent<TextLocalization>().Init(key, label);

        var fallbackList = new FallbackListView();

        var pickerRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var dropdown = O5Factory.DropDown(O5KitAdapters.Ctx, pickerRow, key.ToLowerInvariant() + "_dropdown", new O5DropdownOptions<string> { Value = string.IsNullOrEmpty(fx.Value) ? BuiltinFontOption : fx.Value, Values = FontOptions(includeBuiltin: true, includeKey: fx.Value), Display = FontOptionLabel, OnChanged = value => {
                fx.Value = value == BuiltinFontOption ? null : value;
                if (fallbacks.Value != null) {
                    fallbacks.Value.RemoveAll(k => k == fx.Value);
                }
                MainCore.ConfMgr.RequestSave();
                O5KitAdapters.RefreshFonts();
                TextLocalization.RefreshAll();
                RefreshFallbackRows(fallbackList, fallbacks, fx.Value);
                onChanged?.Invoke();
            } });
        dropdown.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(descKey, descFallback));
        objects[textTr] = (overlayerText.gameObject, row.gameObject);

        fontOptionRefreshers.Add(() => {
            if (dropdown != null) {
                dropdown.SetValues(FontOptions(includeBuiltin: true, includeKey: fx.Value));
                dropdown.Set(string.IsNullOrEmpty(fx.Value) ? BuiltinFontOption : fx.Value, false);
            }
        });
        return fallbackList;
    }

    private static void BuildFallbackSection(GameObject content, string key, FxValue<string> fx,
        FxValue<List<string>> fallbacks, string fallbacksKey, string fallbacksLabel, FallbackListView fallbackList = null) {
        fallbackList ??= new FallbackListView();

        var fallbacksRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var fallbacksText = O5Factory.ControlText(O5KitAdapters.Ctx, fallbacksRow, 22f, true);
        fallbacksText.text = MainCore.Tr.Get(fallbacksKey, fallbacksLabel);
        fallbacksText.gameObject.AddComponent<TextLocalization>().Init(fallbacksKey, fallbacksLabel);

        fallbackList.Root = new GameObject(fallbacksKey + "_List");
        fallbackList.Root.transform.SetParent(content.transform, false);
        fallbackList.Root.AddComponent<RectTransform>();
        var fallbackLayout = fallbackList.Root.AddComponent<VerticalLayoutGroup>();
        fallbackLayout.spacing = 6f;
        fallbackLayout.childControlWidth = true;
        fallbackLayout.childControlHeight = true;
        fallbackLayout.childForceExpandWidth = true;
        fallbackLayout.childForceExpandHeight = false;
        var fallbackFitter = fallbackList.Root.AddComponent<ContentSizeFitter>();
        fallbackFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var fallbackElement = fallbackList.Root.AddComponent<LayoutElement>();
        fallbackElement.minWidth = 0f;
        fallbackElement.flexibleWidth = 1f;
        RefreshFallbackRows(fallbackList, fallbacks, fx.Value);

        var addRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 44f);
        var addFallbackBtn = O5Factory.Button(O5KitAdapters.Ctx, addRow, key.ToLowerInvariant() + "_fallback_add", new O5ButtonOptions { Text = MainCore.Tr.Get("ADD_FALLBACK", "Add Fallback"), OnClick = () => {
            var chain = fallbacks.Value ?? [];
            var available = UserResourceManager.Fnt.Keys
                .Where(k => !O5cpFormat.IsPackageKey(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .AsEnumerable();
            if (MainCore.Conf.ShowInternalFonts.Value) {
                available = available.Concat(InternalResources.FontKeys);
            }
            string first = available.FirstOrDefault(k => k != fx.Value && !chain.Contains(k));
            if (first == null) {
                if (chain.Contains(BuiltinFontOption)) {
                    return;
                }
                first = BuiltinFontOption;
            }
            chain.Add(first);
            fallbacks.Value = chain;
            MainCore.ConfMgr.RequestSave();
            O5KitAdapters.RefreshFonts();
            TextLocalization.RefreshAll();
            RefreshFallbackRows(fallbackList, fallbacks, fx.Value);
        }, Height = 44f });
        addFallbackBtn.Label.gameObject.AddComponent<TextLocalization>().Init("ADD_FALLBACK", "Add Fallback");

        fontOptionRefreshers.Add(() => {
            if (fallbackList.Root != null) {
                RefreshFallbackRows(fallbackList, fallbacks, fx.Value);
            }
        });
    }

    private static void RefreshFallbackRows(FallbackListView list, FxValue<List<string>> fallbacks, string primaryKey) {
        if (list?.Root == null) {
            return;
        }
        foreach (var control in list.Controls) {
            control?.Dispose();
        }
        list.Controls.Clear();
        GameObject listBox = list.Root;
        for (int i = listBox.transform.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(listBox.transform.GetChild(i).gameObject);
        }
        var chain = fallbacks.Value ?? [];
        for (int i = 0; i < chain.Count; i++) {
            int index = i;
            RectTransform row = O5Factory.Row(O5KitAdapters.Ctx, listBox.transform, 44f);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            var dropdown = O5Factory.DropDown(O5KitAdapters.Ctx, row, $"fallback_row_{index}", new O5DropdownOptions<string> { Value = FontOptionLabel(chain[index]), Values = FontOptions(includeBuiltin: true, includeKey: chain[index]), Display = FontOptionLabel, OnChanged = value => {
                    var next = fallbacks.Value ?? [];
                    string selected = value;
                    if (index < next.Count && selected != primaryKey &&
                        !next.Where((_, i) => i != index).Contains(selected)) {
                        next[index] = selected;
                        fallbacks.Value = next;
                        MainCore.ConfMgr.RequestSave();
                        O5KitAdapters.RefreshFonts();
                        TextLocalization.RefreshAll();
                        RefreshFallbackRows(list, fallbacks, primaryKey);
                    } else {
                        RefreshFallbackRows(list, fallbacks, primaryKey);
                    }
                } });
            list.Controls.Add(dropdown);
            var dropLe = dropdown.Rect.gameObject.GetComponent<LayoutElement>();
            if (dropLe == null) {
                dropLe = dropdown.Rect.gameObject.AddComponent<LayoutElement>();
            }
            dropLe.flexibleWidth = 1f;
            list.Controls.Add(IconButton(row, UIIcon.Triangle, 180f, $"fallback_up_{index}", () => MoveFallback(list, fallbacks, index, -1, primaryKey)));
            list.Controls.Add(IconButton(row, UIIcon.Triangle, 0f, $"fallback_down_{index}", () => MoveFallback(list, fallbacks, index, 1, primaryKey)));
            var delBtn = IconButton(row, UIIcon.X, 0f, $"fallback_del_{index}", () => { });
            HoldToConfirm.Attach(delBtn, () => {
                var next = fallbacks.Value ?? [];
                if (index < next.Count) {
                    next.RemoveAt(index);
                    fallbacks.Value = next;
                    MainCore.ConfMgr.RequestSave();
                    O5KitAdapters.RefreshFonts();
                    TextLocalization.RefreshAll();
                    RefreshFallbackRows(list, fallbacks, primaryKey);
                }
            });
            list.Controls.Add(delBtn);
        }
    }

    private static void MoveFallback(FallbackListView list, FxValue<List<string>> fallbacks, int index, int dir, string primaryKey) {
        var next = fallbacks.Value ?? [];
        int other = index + dir;
        if (index < 0 || index >= next.Count || other < 0 || other >= next.Count) {
            return;
        }
        (next[index], next[other]) = (next[other], next[index]);
        fallbacks.Value = next;
        MainCore.ConfMgr.RequestSave();
        O5KitAdapters.RefreshFonts();
        TextLocalization.RefreshAll();
        RefreshFallbackRows(list, fallbacks, primaryKey);
    }

    private static O5Button IconButton(Transform parent, UIIcon icon, float rotationDeg, string id, Action onClick) {
        var button = O5Factory.Button(O5KitAdapters.Ctx, parent, id, new O5ButtonOptions { Icon = MainCore.Icons.Get(icon), OnClick = onClick, IconPadding = 5f, Height = 44f });
        if (rotationDeg != 0f && button.Icon != null) {
            button.Icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotationDeg);
        }
        var le = button.Rect.gameObject.GetComponent<LayoutElement>();
        le.minWidth = 52f;
        le.preferredWidth = 52f;
        le.flexibleWidth = 0f;
        return button;
    }

    private static string DefaultFontName() => MainCore.Tr.Get("DEFAULT_FONT", "Default");

    private static string FontOptionLabel(string key) => key == BuiltinFontOption
        ? DefaultFontName()
        : InternalResources.IsInternalKey(key)
            ? InternalResources.DisplayName(key)
            : UserResourceSettings.FullName(UserResourceManager.Config.Data.FontFolders, key);

    private static string[] FontOptions(bool includeBuiltin = false, string includeKey = null) {
        var folders = UserResourceManager.Config.Data.FontFolders;
        List<string> keys = [.. UserResourceManager.Fnt.Keys
            .Where(k => !O5cpFormat.IsPackageKey(k))
            .OrderBy(k => UserResourceSettings.FolderOf(folders, k), StringComparer.OrdinalIgnoreCase)
            .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)];
        if (MainCore.Conf.ShowInternalFonts.Value) {
            keys.AddRange(InternalResources.FontKeys);
        }
        if (includeBuiltin) {
            keys.Insert(0, BuiltinFontOption);
        }
        if (!string.IsNullOrEmpty(includeKey) && !keys.Contains(includeKey)) {
            keys.Add(includeKey);
        }
        return [.. keys];
    }

    private static O5Button CreateKeyBindButton(
        Transform row,
        string id,
        string labelKey,
        string labelFallback,
        string descKey,
        string descFallback,
        Func<KeyBind> getBind,
        Action<KeyBind> applyBind,
        out Action refresh) {
        var ctx = O5KitAdapters.Ctx;
        O5Button keyButton = null;
        string KeyCaption() => $"{MainCore.Tr.Get(labelKey, labelFallback)}: {getBind().ToDisplayString()}";
        keyButton = O5Factory.Button(ctx, row, id, new O5ButtonOptions {
            Text = KeyCaption(),
            OnClick = () => KeyCapture.Begin(
                () => keyButton.Text = $"{MainCore.Tr.Get(labelKey, labelFallback)}: ...",
                bind => {
                    if (bind is { IsNone: false } b) {
                        applyBind(b);
                    }
                    keyButton.Text = KeyCaption();
                })
        });
        refresh = () => keyButton.Text = KeyCaption();
        keyButton.Rect.AddToolTip(ctx, () => MainCore.Tr.Get(descKey, descFallback));
        return keyButton;
    }

    internal static void OnTranslatorLoadEnd() {
        if (languageDropdown == null || languageDropdown.IsDisposed || !languageDropdown.Rect) {
            return;
        }
        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        languageDropdown.SetValues(langs);
        languageDropdown.Set(
            string.IsNullOrWhiteSpace(MainCore.Conf.Language)
                ? Translator.FALLBACK_LANGUAGE
                : MainCore.Conf.Language,
            false
        );
        refreshWindowKey?.Invoke();
        refreshHudKey?.Invoke();
        refreshPaletteLabels?.Invoke();
        refreshTabLabelDropdown?.Invoke();
        foreach (var refresh in fontOptionRefreshers.ToArray()) {
            refresh();
        }
    }
}
