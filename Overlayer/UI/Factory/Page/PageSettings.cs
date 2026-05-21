using DG.Tweening;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Factory.Page;

internal static class PageSettings {
    private static readonly Dictionary<string, UIObject> objects = [];

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

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pad.AddComponent<UIScrollController>().SetContent(contentRect, viewportRect);

        CoreSettings defSet = new();

        _ = GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>()
            .Init("LANGUAGE", "Language");

        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        var langRow = GenerateUI.Row(content.transform);
        UIDropDown<string> languageDropdown = GenerateUI.DropDown(
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
                MainCore.Config.Language = value;
                MainCore.ConfigFile.RequestSave();
                TextLocalization.RefreshAll();
            },
            "language_dropdown"
        );

        objects[languageDropdown.Id] = languageDropdown;

        UIButton langBtn = GenerateUI.Button(
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
        objects[langBtn.Id] = langBtn;

        _ = GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
           .gameObject.AddComponent<TextLocalization>()
           .Init("OVERLAYER", "Overlayer");

        UIToggle startupToggle = GenerateUI.Toggle(
            GenerateUI.Row(content.transform),
            defSet.ShowOnStartup,
            MainCore.Config.ShowOnStartup,
            toggle => {
                MainCore.Config.ShowOnStartup = toggle;
                MainCore.ConfigFile.RequestSave();
            },
            "Show Overlayer Panel at Startup",
            "show_on_startup"
        );
        startupToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Overlayer Panel at Startup");
        objects[startupToggle.Id] = startupToggle;

        UIToggle tooltipToggle = GenerateUI.Toggle(
            GenerateUI.Row(content.transform),
            defSet.Tooltip,
            MainCore.Config.Tooltip,
            toggle => {
                Tooltip.Hide();
                MainCore.Config.Tooltip = toggle;
                MainCore.ConfigFile.RequestSave();
            },
            "Show Tooltip",
            "show_tooltip"
        );
        tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        tooltipToggle.Rect.AddToolTip(
            "DESC_SHOW_TOOLTIP",
            "This is a Tooltip!"
        );
        objects[tooltipToggle.Id] = tooltipToggle;

        UIToggle middleClickToggle = GenerateUI.Toggle(
            GenerateUI.Row(content.transform),
            defSet.MiddleClickToDefault,
            MainCore.Config.MiddleClickToDefault,
            toggle => {
                MainCore.Config.MiddleClickToDefault = toggle;
                MainCore.ConfigFile.RequestSave();
            },
            "Middle-click to set as default",
            "middle_click_default"
        );
        middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        middleClickToggle.Rect.AddToolTip(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        );
        objects[middleClickToggle.Id] = middleClickToggle;

        static float uiScaleFilter(float v) {
            v = Mathf.Round(v * 100f) / 100f;
            return Mathf.Clamp(v, 0.8f, 1.6f);
        }

        UISlider uiScale = GenerateUI.Slider(
            GenerateUI.Row(content.transform),
            1f,
            0.8f,
            1.6f,
            MainCore.Config.UIScale,
            uiScaleFilter,
            null,
            null,
            "UI Scale",
            "ui_scale"
        );

        uiScale.Format = "0.00x";
        uiScale.OnChanged = value => MainCore.Config.UIScale = value;

        Sequence seq = null;

        uiScale.OnComplete = value => {
            MainCore.Config.UIScale = value;
            MainCore.ConfigFile.RequestSave();

            seq?.Kill();

            float scaleStart = UICore.PanelScale;
            Vector2 targetSize = UICore.DefaultPanelSize;
            UICore.LastPanelSize = targetSize;
            seq = DOTween.Sequence().SetUpdate(true)
                .Append(
                    DOTween.To(
                        () => scaleStart,
                        x => UICore.PanelScale = x,
                        value,
                        0.4f
                    ).SetEase(Ease.OutExpo)
                ).Join(
                    UICore.Panel
                    .DOSizeDelta(targetSize, 0.4f)
                    .SetEase(Ease.OutExpo)
                );
        };
        uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        objects[uiScale.Id] = uiScale;
    }

    internal static void OnTranslatorLoadEnd() {
        if(
            !objects.TryGetValue("language_dropdown", out UIObject obj) ||
            obj is not UIDropDown<string> dropdown
        ) {
            return;
        }

        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        dropdown.SetValues(langs);

        dropdown.Set(
            string.IsNullOrWhiteSpace(MainCore.Config.Language)
                ? Translator.FALLBACK_LANGUAGE
                : MainCore.Config.Language,
            false
        );
    }
}