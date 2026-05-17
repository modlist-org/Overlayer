using Overlayer.Async;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects;
using Overlayer.UI.Objects.Impl;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Factory.Page;

internal static class PageSettings {
    private static readonly Dictionary<string, UIObject> objects = [];

    public static void Create(RectTransform parent) {
        GameObject pad = new("Pad");
        pad.transform.SetParent(parent, false);

        RectTransform rect = pad.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = new(18f, 18f);
        rect.offsetMax = new(-18f, -18f);

        ScrollRect scroll = pad.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = new("Viewport");
        viewport.transform.SetParent(pad.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<RectMask2D>();

        GameObject content = new("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new(0f, 1f);
        contentRect.anchorMax = new(1f, 1f);
        contentRect.pivot = new(0.5f, 1f);
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

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        Settings defSet = new();

        _ = GenerateUI.AddTextH1(GenerateUI.Row(content.transform))
            .gameObject.AddComponent<TextLocalization>()
            .Init("LANGUAGE", "Language");

        string[] langs = Core.Tr.GetLanguages()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var langRow = GenerateUI.Row(content.transform);
        UIDropDown<string> languageDropdown = GenerateUI.DropDown(
            langRow,
            null,
            Core.Tr.Language,
            langs,
            lang => {
                if(lang == Translator.FALLBACK_LANGUAGE) {
                    return "DEFAULT";
                }

                string native = Core.Tr.GetForLanguage(
                    "0NATIVELANG",
                    lang,
                    lang
                );

                return $"{native} ({lang})";
            },
            value => {
                Core.Tr.Language = value;
                Core.Config.Language = value;
                Core.Config.RequestSave();
                TextLocalization.RefreshAll();
            },
            "language_dropdown"
        );

        objects[languageDropdown.Id] = languageDropdown;

        UIButton langBtn = GenerateUI.Button(
            langRow,
            () => {},
            "Reload",
            "language_reload"
        );
        langBtn.OnClick = async () => {
            languageDropdown.SetExpanded(false);
            languageDropdown.SetBlocked(true);
            langBtn.SetBlocked(true);
            langBtn.Text.text = "...";
            _ = Task.Run(async () => {
                await Core.Tr.Load(Path.Combine(Core.OverlayerPath, "Lang"));
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
            br.sizeDelta = new(140f, 50f);
            br.offsetMax = Vector2.zero;
        }
        langBtn.Text.gameObject.AddComponent<TextLocalization>().Init("RELOAD", "Reload");
        objects[langBtn.Id] = langBtn;

        _ = GenerateUI.AddTextH1(GenerateUI.Row(content.transform)).text = "Overlayer";

        UIToggle startupToggle = GenerateUI.Toggle(
            GenerateUI.Row(content.transform),
            defSet.ShowOnStartup,
            Core.Config.ShowOnStartup,
            toggle => {
                Core.Config.ShowOnStartup = toggle;
                Core.Config.RequestSave();
            },
            "Show Overlayer Panel at Startup",
            "show_on_startup"
        );
        startupToggle.Text.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Overlayer Panel at Startup");
        objects[startupToggle.Id] = startupToggle;

        UIToggle middleClickToggle = GenerateUI.Toggle(
            GenerateUI.Row(content.transform),
            defSet.MiddleClickToDefault,
            Core.Config.MiddleClickToDefault,
            toggle => {
                Core.Config.MiddleClickToDefault = toggle;
                Core.Config.RequestSave();
            },
            "Middle-click to set as default",
            "middle_click_default"
        );
        middleClickToggle.Text.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        middleClickToggle.Rect.AddToolTip(
            "DESC_MIDDLECLICKTODEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item."
        );
        objects[middleClickToggle.Id] = middleClickToggle;

    }

    internal static void OnTranslatorInitialize() {
        if(
            !objects.TryGetValue("language_dropdown", out UIObject obj) ||
            obj is not UIDropDown<string> dropdown
        ) {
            return;
        }

        string[] langs = [.. Core.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        dropdown.SetValues(langs);

        dropdown.Set(
            string.IsNullOrWhiteSpace(Core.Config.Language)
                ? Translator.FALLBACK_LANGUAGE
                : Core.Config.Language,
            false
        );
    }
}