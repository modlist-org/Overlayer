using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.UI.Utility;
using Overlayer.V8.Scripting.Patch;
using O5Kit.Control;
using O5Kit.Factory;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageJS {
    private static RectTransform listContent;
    private static TextMeshProUGUI diagText;
    private static GameObject disabledPanel;

    public static void Create(RectTransform parent) {
        RectTransform root = CreateStretch(parent, "JSRoot");

        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 12);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = CreateText(root, T("JS", "JavaScript"), 30f, TextAlignmentOptions.Left);
        var titleLe = title.gameObject.AddComponent<LayoutElement>();
        titleLe.minHeight = 46f;
        titleLe.flexibleHeight = 0f;

        RectTransform toolbar = O5Factory.Row(O5KitAdapters.Ctx, root, 44f);
        var toolbarLayout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.spacing = 8f;
        toolbarLayout.childControlWidth = true;
        toolbarLayout.childControlHeight = true;
        toolbarLayout.childForceExpandWidth = false;
        toolbarLayout.childForceExpandHeight = true;
        toolbarLayout.childAlignment = TextAnchor.MiddleLeft;
        ToolbarButton(toolbar, T("JS_RELOAD_ALL", "Reload All"), "js_reload_all", 150f, ReloadAll);
        ToolbarButton(toolbar, T("JS_OPEN_FOLDER", "Open Folder"), "js_open_folder", 150f, OpenFolder);
        ToolbarButton(toolbar, T("JS_REFRESH", "Refresh"), "js_refresh", 110f, Refresh);

        RectTransform toggleRow = O5Factory.Row(O5KitAdapters.Ctx, root, 50f);
        CoreSettings defSet = new();
        var autoToggle = O5Factory.Toggle(O5KitAdapters.Ctx,
            toggleRow,
            defSet.EnableJSScriptWatcher,
            MainCore.Conf.EnableJSScriptWatcher,
            toggle => {
                MainCore.Conf.EnableJSScriptWatcher = toggle;
                MainCore.ConfMgr.RequestSave();
                MainCore.V8.UpdateWatcher();
            },
            T("JS_AUTO_RELOAD", "Auto Reload"),
            "js_auto_reload"
        );
        autoToggle.Label.gameObject.AddComponent<TextLocalization>().Init("JS_AUTO_RELOAD", "Auto Reload");

        RectTransform diagBox = O5Factory.Row(O5KitAdapters.Ctx, root, 120f);
        var (_, diagContent, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, diagBox, expandLayout: true);
        diagText = CreateText(diagContent, string.Empty, 18f, TextAlignmentOptions.TopLeft);

        var (_, contentRect, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, root, expandLayout: true);
        listContent = contentRect;

        CreateDisabledPanel(root);
        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) {
                ToggleUIStateByMod(isEnabled);
            }
        };
        ToggleUIStateByMod(MainCore.IsModEnabled);

        MenuFactory.OnStateChanged += state => {
            if(state == (int)OriginalMenuState.JS) {
                Refresh();
            }
        };

        Refresh();
    }

    private static void ToolbarButton(Transform parent, string text, string id, float width, Action onClick) {
        var button = O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, text, id, 44f);
        var le = button.Rect.gameObject.GetComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        le.flexibleWidth = 0f;
    }

    private static void ToggleUIStateByMod(bool isEnabled) {
        if(disabledPanel == null) {
            return;
        }
        disabledPanel.SetActive(!isEnabled);
    }

    private static void CreateDisabledPanel(RectTransform parent) {
        disabledPanel = new GameObject("DisabledJSPanel");
        disabledPanel.transform.SetParent(parent, false);

        RectTransform rect = disabledPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = disabledPanel.AddComponent<Image>();
        image.color = UIColors.PanelBG;
        image.raycastTarget = true;

        TextMeshProUGUI message = CreateText(disabledPanel.transform,
            T("ONLY_AVAILABLE_WHEN_ENABLED", "Only available when the Mod is Enabled!"),
            24f, TextAlignmentOptions.Center);
        message.rectTransform.anchorMin = Vector2.zero;
        message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = Vector2.zero;
        message.rectTransform.offsetMax = Vector2.zero;
        message.gameObject.AddComponent<TextLocalization>().Init(
            "ONLY_AVAILABLE_WHEN_ENABLED",
            "Only available when the Mod is Enabled!"
        );

        disabledPanel.SetActive(false);
    }

    private static void ReloadAll() {
        _ = MainCore.V8.ReloadScriptsAsync().ContinueWith(_ => MainThread.Enqueue(Refresh));
    }

    private static void OpenFolder() {
        try {
            Application.OpenURL("file://" + MainCore.V8.ScriptFolderPath);
        } catch(Exception e) {
            MainCore.Log.Err($"[JS] Cannot open script folder: {e.Message}");
        }
    }

    public static void Refresh() {
        if(listContent == null) {
            return;
        }
        for(int i = listContent.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(listContent.GetChild(i).gameObject);
        }
        foreach(string file in MainCore.V8.ScriptFiles) {
            BuildFileRow(file);
        }
        if(diagText != null) {
            var diags = MainCore.V8.LoaderDiagnostics;
            diagText.text = diags.Count == 0
                ? T("JS_NO_ERRORS", "No script errors.")
                : string.Join("\n", diags.TakeLast(6).Select(d => d.ToString()));
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    private static void BuildFileRow(string file) {
        int tags;
        int patches;
        try {
            tags = MainCore.V8.ScriptFileTags(file).Count;
            patches = JSPatchManager.GetFilePatches(file).Count;
        } catch {
            tags = 0;
            patches = 0;
        }
        RectTransform row = O5Factory.Row(O5KitAdapters.Ctx, listContent, 44f);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        var name = O5Factory.ControlText(O5KitAdapters.Ctx, row, 22f, true);
        name.text = Path.GetFileName(file);
        name.alignment = TextAlignmentOptions.Left;
        var nameLe = name.gameObject.AddComponent<LayoutElement>();
        nameLe.flexibleWidth = 1f;

        var counts = O5Factory.ControlText(O5KitAdapters.Ctx, row, 22f, true);
        counts.text = $"{tags} tags · {patches} patches";
        counts.alignment = TextAlignmentOptions.Right;
        var countsLe = counts.gameObject.AddComponent<LayoutElement>();
        countsLe.minWidth = 190f;
        countsLe.flexibleWidth = 0f;

        string captured = file;
        var reload = O5Factory.Button(O5KitAdapters.Ctx, row, () => {
            try {
                MainCore.V8.ReloadScriptFile(captured);
            } finally {
                Refresh();
            }
        }, T("JS_RELOAD", "Reload"), "js_reload_file", 44f);
        var reloadLe = reload.Rect.gameObject.GetComponent<LayoutElement>();
        reloadLe.minWidth = 110f;
        reloadLe.preferredWidth = 110f;
        reloadLe.flexibleWidth = 0f;
    }

    private static RectTransform CreateStretch(Transform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions alignment) {
        TextMeshProUGUI label = O5Factory.ControlText(O5KitAdapters.Ctx, parent, 24f, true);
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        return label;
    }

    private static string T(string key, string defaultValue, params object[] args) => string.Format(MainCore.Tr.Get(key, defaultValue), args);
}
