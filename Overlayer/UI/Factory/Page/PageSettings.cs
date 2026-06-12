using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Newtonsoft.Json.Linq;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.Tween;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
using Overlayer.Utility;
using UnityEngine;
using UnityEngine.UI;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

namespace Overlayer.UI.Factory.Page;

internal static class PageSettings {
    private static readonly Dictionary<TextLocalization, (GameObject LabelRow, GameObject MainRow)> objects = [];
    private static UIDropDown<string> languageDropdown;

    public static void Create(RectTransform parent) {
        GameObject pad = new("Pad");
        pad.transform.SetParent(parent, false);

        RectTransform padRect = pad.AddComponent<RectTransform>();
        padRect.anchorMin = Vector2.zero;
        padRect.anchorMax = Vector2.one;
        padRect.pivot = new Vector2(0.5f, 0.5f);
        padRect.offsetMin = new Vector2(18f, 18f);
        padRect.offsetMax = new Vector2(-18f, -18f);

        GameObject viewport = new("Viewport");
        viewport.transform.SetParent(pad.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);

        viewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        GameObject content = new("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pad.AddComponent<UIScrollController>().SetContent(contentRect, viewportRect);

        CoreSettings defSet = new();

        var inputRow = GenerateUI.Row(content.transform);
        var findInput =
        GenerateUI.Input(
            inputRow,
            null,
            null,
            value => {
                bool isBlank = string.IsNullOrWhiteSpace(value);
                Dictionary<GameObject, bool> labelActivationMap = [];

                foreach (var pair in objects.Where(pair => pair.Value.LabelRow != null)) {
                    labelActivationMap[pair.Value.LabelRow] = isBlank;
                }

                string normalizedQuery = StringUtils.Normalize(value);

                if (MainCore.Conf.Language == "ko-KR") {
                    normalizedQuery = StringUtils.NormalizeToHangulChosung(normalizedQuery);
                }

                foreach(var (labelLoc, valueTuple) in objects) {
                    var (labelRow, mainRow) = valueTuple;

                    if(labelRow == null || mainRow == null) {
                        continue;
                    }

                    string normalizedTarget = labelLoc != null ? StringUtils.Normalize(labelLoc.Value) : string.Empty;
                    if (MainCore.Conf.Language == "ko-KR" && !string.IsNullOrEmpty(normalizedTarget)) {
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

        var langLabelRow = GenerateUI.Row(content.transform);
        var langText = GenerateUI.AddTextH1(langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");

        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        var langRow = GenerateUI.Row(content.transform);
        languageDropdown = GenerateUI.DropDown(
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

        var langBtn = GenerateUI.Button(
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
            br.sizeDelta = new(114f, 50f);
            br.offsetMax = Vector2.zero;
        }
        langBtn.Label.gameObject.AddComponent<TextLocalization>().Init("RELOAD", "Reload");

        objects[langTextTr] = (langLabelRow.gameObject, langRow.gameObject);

        var overlayerText = GenerateUI.AddTextH1(GenerateUI.Row(content.transform));
        var overlayerTextTr = overlayerText.gameObject.AddComponent<TextLocalization>().Init("OVERLAYER", "Overlayer");

        var startupRow = GenerateUI.Row(content.transform);
        var startupToggle = GenerateUI.Toggle(
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

        var tooltipRow = GenerateUI.Row(content.transform);
        var tooltipToggle = GenerateUI.Toggle(
            tooltipRow,
            defSet.Tooltip,
            MainCore.Conf.Tooltip,
            toggle => {
                Tooltip.Hide();
                MainCore.Conf.Tooltip = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Tooltip",
            "show_tooltip"
        );
        tooltipToggle.Rect.AddToolTip(
            "DESC_SHOW_TOOLTIP",
            "This is a Tooltip!"
        );
        var tooltipToggleTr = tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        objects[tooltipToggleTr] = (overlayerText.gameObject, tooltipRow.gameObject);

        var middleClickRow = GenerateUI.Row(content.transform);
        UIToggle middleClickToggle = GenerateUI.Toggle(
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
        middleClickToggle.Rect.AddToolTip(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        );
        var middleClickToggleTr = middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        objects[middleClickToggleTr] = (overlayerText.gameObject, middleClickRow.gameObject);

        GTween scaleSeq = null;

        var uiScaleRow = GenerateUI.Row(content.transform);
        var uiScale = GenerateUI.Slider(
            uiScaleRow,
            defSet.UIScale,
            0.8f,
            1.6f,
            MainCore.Conf.UIScale,
            "0.00x",
            true,
            value => MathF.Round(value, 2),
            value => MainCore.Conf.UIScale = value,
            value => {
                MainCore.Conf.UIScale = value;
                MainCore.ConfMgr.RequestSave();

                scaleSeq?.Kill();

                float scaleStart = UICore.PanelScale;

                Vector2 targetSize = UICore.DefaultPanelSize;
                UICore.LastPanelSize = targetSize;

                scaleSeq = GTweenSequenceBuilder.New()
                    .Append(
                        GTweenExtensions.Tween(
                            () => scaleStart,
                            x => UICore.PanelScale = x,
                            value,
                            0.4f
                        ).SetEasing(Easing.OutExpo)
                    )
                    .Join(
                        UICore.Panel.GTSizeDelta(targetSize, 0.4f)
                        .SetEasing(Easing.OutExpo)
                    ).Build();

                MainCore.TC.Play(scaleSeq);
            },
            "UI Scale",
            "ui_scale"
        );
        var targetSize = UICore.DefaultPanelSize;
        var uiScaleTr = uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        objects[uiScaleTr] = (overlayerText.gameObject, uiScaleRow.gameObject);

        var sliderSensitivityRow = GenerateUI.Row(content.transform);
        var sliderSensitivity = GenerateUI.Slider(
            sliderSensitivityRow,
            defSet.SliderSensitivity,
            0.1f,
            2.0f,
            MainCore.Conf.SliderSensitivity,
            "0.00x",
            false,
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