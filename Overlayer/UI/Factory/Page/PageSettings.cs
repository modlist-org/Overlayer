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
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Factory.Page;

internal static class PageSettings {
    private static readonly Dictionary<TextLocalization, (GameObject LabelRow, GameObject MainRow)> objects = [];
    private static O5Dropdown<string> languageDropdown;

    public static void Create(RectTransform parent) {
        var (_, contentRect, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, parent, 12f, 18f);
        GameObject content = contentRect.gameObject;

        CoreSettings defSet = new();

        var inputRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var findInput =
        O5Factory.Input(O5KitAdapters.Ctx, 
            inputRow,
            null,
            null,
            value => {
                bool isBlank = string.IsNullOrWhiteSpace(value);
                Dictionary<GameObject, bool> labelActivationMap = [];

                foreach(var pair in objects.Where(pair => pair.Value.LabelRow != null)) {
                    labelActivationMap[pair.Value.LabelRow] = isBlank;
                }

                string normalizedQuery = StringUtils.Normalize(value);

                if(MainCore.Conf.Language.Value == "ko-KR") {
                    normalizedQuery = StringUtils.NormalizeToHangulChosung(normalizedQuery);
                }

                foreach(var (labelLoc, valueTuple) in objects) {
                    var (labelRow, mainRow) = valueTuple;

                    if(labelRow == null || mainRow == null) {
                        continue;
                    }

                    string normalizedTarget = labelLoc != null ? StringUtils.Normalize(labelLoc.Value) : string.Empty;
                    if(MainCore.Conf.Language.Value == "ko-KR" && !string.IsNullOrEmpty(normalizedTarget)) {
                        normalizedTarget = StringUtils.NormalizeToHangulChosung(normalizedTarget);
                    }

                    bool isMainMatch = isBlank
                        || (
                            !string.IsNullOrEmpty(normalizedTarget)
                            && normalizedTarget.Contains(normalizedQuery)
                        );

                    mainRow.SetActive(isMainMatch);

                    if(isMainMatch) {
                        labelActivationMap[labelRow] = true;
                    }
                }

                foreach(var kvp in labelActivationMap) {
                    kvp.Key.SetActive(kvp.Value);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            },
            "Find",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "search_find"
        );
        findInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("FIND", "Find");
        findInput.InputField.characterLimit = 22;

        var langLabelRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var langText = O5Factory.ControlTextH1(O5KitAdapters.Ctx, langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");

        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        const float languageReloadWidth = 240f;
        const float languageControlSpacing = 8f;
        var langRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        languageDropdown = O5Factory.DropDown(O5KitAdapters.Ctx, 
            langRow,
            null,
            MainCore.Tr.Language,
            langs,
            lang => {
                if(lang == Translator.FALLBACK_LANGUAGE) {
                    return "DEFAULT";
                }

                string native = MainCore.Tr.GetForLanguage(
                    "0NATIVELANG",
                    lang,
                    lang
                );

                return $"{native} ({lang})";
            },
            value => {
                MainCore.Tr.Language = value;
                MainCore.Conf.Language = value;
                MainCore.ConfMgr.RequestSave();
                TextLocalization.RefreshAll();
            },
            "language_dropdown"
        );
        languageDropdown.Rect.offsetMax = new Vector2(-(languageReloadWidth + languageControlSpacing), 0f);
        var langBtn = O5Factory.Button(O5KitAdapters.Ctx, 
            langRow,
            () => { },
            "Reload",
            "language_reload"
        );
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
        var startupToggle = O5Factory.Toggle(O5KitAdapters.Ctx, 
            startupRow,
            defSet.ShowOnStartup,
            MainCore.Conf.ShowOnStartup,
            toggle => {
                MainCore.Conf.ShowOnStartup = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Overlayer Panel at Startup",
            "show_on_startup"
        );
        var startupToggleTr = startupToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Overlayer Panel at Startup");
        objects[startupToggleTr] = (overlayerText.gameObject, startupRow.gameObject);

        var tooltipRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var tooltipToggle = O5Factory.Toggle(O5KitAdapters.Ctx, tooltipRow, defSet.Tooltip, MainCore.Conf.Tooltip, null, "Show Tooltip", "show_tooltip");
        var advTooltipRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var advTooltipToggle = O5Factory.Toggle(O5KitAdapters.Ctx, advTooltipRow, defSet.AdvancedTooltip, MainCore.Conf.AdvancedTooltip, null, "Show Advanced Tooltip", "show_advanced_tooltip");
        tooltipToggle.OnChanged = toggle => {
            O5KitAdapters.Ctx.Tooltip.Hide();
            MainCore.Conf.Tooltip = toggle;
            MainCore.ConfMgr.RequestSave();

            advTooltipToggle.SetBlocked(!toggle);
        };
        advTooltipToggle.OnChanged = toggle => {
            O5KitAdapters.Ctx.Tooltip.Hide();
            MainCore.Conf.AdvancedTooltip = toggle;
            MainCore.ConfMgr.RequestSave();
        };
        tooltipToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get("DESC_SHOW_TOOLTIP", "This is a Tooltip!"));
        var tooltipToggleTr = tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        objects[tooltipToggleTr] = (overlayerText.gameObject, tooltipRow.gameObject);
        advTooltipToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_SHOW_ADVANCED_TOOLTIP",
            "Additionally displays developer-written notes, technical implementation details, and inner mechanics in tooltips.\nRecommended for anyone curious about how features work under the hood."
        ));
        var advTooltipToggleTr = advTooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_ADVANCED_TOOLTIP", "Show Advanced Tooltip");
        objects[advTooltipToggleTr] = (overlayerText.gameObject, advTooltipRow.gameObject);
        advTooltipToggle.SetBlocked(!MainCore.Conf.Tooltip.Value);

        var middleClickRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        O5Toggle middleClickToggle = O5Factory.Toggle(O5KitAdapters.Ctx, 
            middleClickRow,
            defSet.MiddleClickToDefault,
            MainCore.Conf.MiddleClickToDefault,
            toggle => {
                MainCore.Conf.MiddleClickToDefault = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Middle-click to set as default",
            "middle_click_default"
        );
        middleClickToggle.Rect.AddToolTip(O5KitAdapters.Ctx, () => MainCore.Tr.Get(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        ));
        var middleClickToggleTr = middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        objects[middleClickToggleTr] = (overlayerText.gameObject, middleClickRow.gameObject);

        ITweenHandle scaleSeq = null;

        var uiScaleRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var uiScale = O5Factory.Slider(O5KitAdapters.Ctx, 
            uiScaleRow,
            defSet.UIScale,
            0.8f,
            1.6f,
            MainCore.Conf.UIScale,
            "0.00x",
            ClampMode.All,
            value => MathF.Round(value, 2),
            value => MainCore.Conf.UIScale = value,
            value => {
                MainCore.Conf.UIScale = value;
                MainCore.ConfMgr.RequestSave();

                scaleSeq?.Kill();

                float scaleStart = UICore.PanelScale;

                Vector2 targetSize = UICore.DefaultPanelSize;
                UICore.LastPanelSize = targetSize;

                scaleSeq = O5Seq.New()
                    .Append(done =>
                        O5KitAdapters.Ctx.Tween.TweenFloat(
                            () => scaleStart,
                            x => UICore.PanelScale = x,
                            value,
                            0.4f,
                            done,
                            O5Ease.OutExpo
                        )
                    )
                    .Join(done =>
                        UICore.Panel.TSizeDelta(targetSize, 0.4f, O5Ease.OutExpo, done)
                    ).Play();
            },
            "UI Scale",
            "ui_scale"
        );
        var targetSize = UICore.DefaultPanelSize;
        var uiScaleTr = uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        objects[uiScaleTr] = (overlayerText.gameObject, uiScaleRow.gameObject);

        var sliderSensitivityRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
        var sliderSensitivity = O5Factory.Slider(O5KitAdapters.Ctx, 
            sliderSensitivityRow,
            defSet.SliderSensitivity,
            0.1f,
            2.0f,
            MainCore.Conf.SliderSensitivity,
            "0.00x",
            ClampMode.Slider,
            value => MathF.Round(value, 2),
            value => MainCore.Conf.SliderSensitivity = value,
            value => {
                MainCore.Conf.SliderSensitivity = value;
                MainCore.ConfMgr.RequestSave();
            },
            "Slider Sensitivity",
            "slider_sensitivity"
        );
        var sliderSensitivityTr = sliderSensitivity.Label.gameObject.AddComponent<TextLocalization>().Init("SLIDER_SENSITIVITY", "Slider Sensitivity");
        objects[sliderSensitivityTr] = (overlayerText.gameObject, sliderSensitivityRow.gameObject);
    }

    internal static void OnTranslatorLoadEnd() {
        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        languageDropdown.SetValues(langs);
        languageDropdown.Set(
            string.IsNullOrWhiteSpace(MainCore.Conf.Language)
                ? Translator.FALLBACK_LANGUAGE
                : MainCore.Conf.Language,
            false
        );
    }
}
