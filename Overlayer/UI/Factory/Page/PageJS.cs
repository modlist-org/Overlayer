using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Package;
using Overlayer.UI.Utility;
using Overlayer.V8.Scripting.Patch;
using Overlayer.V8.Scripting.Tag;
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
    private static int refreshQueued;
    private static readonly Dictionary<string, TextMeshProUGUI> scriptStatus = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> collapsedFolders = new(StringComparer.OrdinalIgnoreCase);

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

        RectTransform toolbar = O5Factory.Row(O5KitAdapters.Ctx, root);
        var toolbarLayout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.spacing = 8f;
        toolbarLayout.childControlWidth = true;
        toolbarLayout.childControlHeight = true;
        toolbarLayout.childForceExpandWidth = true;
        toolbarLayout.childForceExpandHeight = false;
        toolbarLayout.childAlignment = TextAnchor.MiddleLeft;
        ToolbarButton(toolbar, T("JS_RELOAD_ALL", "Reload All"), "js_reload_all", ReloadAll);
        ToolbarButton(toolbar, T("JS_OPEN_FOLDER", "Open Folder"), "js_open_folder", OpenFolder);
        ToolbarButton(toolbar, T("JS_REFRESH", "Refresh"), "js_refresh", Refresh);

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

        RectTransform diagBox = O5Factory.Row(O5KitAdapters.Ctx, root, 40f);
        diagText = CreateText(diagBox, string.Empty, 20f, TextAlignmentOptions.Left);

        var (_, contentRect, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, root, expandLayout: true);
        listContent = contentRect;

        CreateDisabledPanel(root);
        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if (!isDispose) {
                ToggleUIStateByMod(isEnabled);
            }
        };
        ToggleUIStateByMod(MainCore.IsModEnabled);

        MenuFactory.OnStateChanged += state => {
            if (state == (int)OriginalMenuState.JS) {
                Refresh();
            }
        };

        Refresh();
    }

    private static void ToolbarButton(Transform parent, string text, string id, Action onClick) {
        O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, text, id);
    }

    private static void ToggleUIStateByMod(bool isEnabled) {
        if (disabledPanel == null || disabledPanel.Equals(null)) {
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
        _ = ReloadAllAsync();
    }

    private static async Task ReloadAllAsync() {
        try {
            await MainCore.V8.ReloadScriptsAsync();
        } catch (Exception e) {
            MainCore.Log.Err($"[JS] Reload failed: {e.Message}");
        } finally {
            QueueRefresh();
        }
    }

    private static async Task SetScriptEnabledAsync(string file, bool enabled) {
        bool success = false;
        try {
            await MainCore.V8.SetScriptEnabledAsync(file, enabled);
            success = true;
        } catch (Exception e) {
            MainCore.Log.Err($"[JS] Script toggle failed: {e.Message}");
        } finally {
            if (success) {
                MainThread.Enqueue(() => RefreshFileStatus(file));
            } else {
                QueueRefresh();
            }
        }
    }

    private static void RefreshFileStatus(string file) {
        if (listContent == null || listContent.Equals(null)) {
            return;
        }
        if (!scriptStatus.TryGetValue(file, out var status) || status == null) {
            Refresh();
            return;
        }
        if (!MainCore.V8.IsScriptEnabled(file)) {
            status.text = T("JS_DISABLED", "Disabled");
            return;
        }
        int tags = MainCore.V8.ScriptFileTags(file).Count;
        int patches = JSPatchManager.GetFilePatches(file).Count;
        status.text = $"{tags} tags · {patches} patches";
    }

    private static void QueueRefresh() {
        if (Interlocked.Exchange(ref refreshQueued, 1) != 0) {
            return;
        }
        MainThread.Enqueue(() => {
            Interlocked.Exchange(ref refreshQueued, 0);
            Refresh();
        });
    }

    private static void OpenFolder() {
        try {
            Application.OpenURL("file://" + MainCore.V8.ScriptFolderPath);
        } catch (Exception e) {
            MainCore.Log.Err($"[JS] Cannot open script folder: {e.Message}");
        }
    }

    public static void Refresh() {
        if (listContent == null || listContent.Equals(null)) {
            return;
        }
        for (int i = listContent.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(listContent.GetChild(i).gameObject);
        }
        scriptStatus.Clear();
        string root = MainCore.V8.ScriptFolderPath;
        string[] files;
        try {
            files = [.. JSScriptLoader.FindScripts(root).Where(file => !O5cpFormat.IsPackageScript(file))];
        } catch {
            files = [];
        }
        var folders = files
            .GroupBy(file => FolderOf(JSScriptLoader.RelativeName(root, file)), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders) {
            string[] folderFiles = [.. folder.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];
            bool inFolder = folder.Key.Length > 0;
            if (inFolder) {
                BuildFolderRow(folder.Key, folderFiles);
                if (collapsedFolders.Contains(folder.Key)) {
                    continue;
                }
            }
            foreach (string file in folderFiles) {
                BuildFileRow(file, MainCore.V8.IsScriptEnabled(file), inFolder);
            }
        }
        if (diagText != null) {
            int errors = MainCore.V8.LoaderDiagnostics.Count(d => !O5cpFormat.IsPackageScript(d.FilePath));
            diagText.text = errors == 0
                ? T("JS_NO_ERRORS", "No script errors.")
                : string.Format(T("JS_ERROR_COUNT", "{0} script errors (see log)."), errors);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    private static string FolderOf(string relativeName) {
        int slash = relativeName.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativeName[..slash];
    }

    private static void BuildFolderRow(string folder, string[] files) {
        RectTransform row = O5Factory.Row(O5KitAdapters.Ctx, listContent, 40f);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        bool allEnabled = files.All(MainCore.V8.IsScriptEnabled);
        BuildToggle(row, allEnabled, value => {
            _ = Task.WhenAll(files.Select(file => SetScriptEnabledAsync(file, value))).ContinueWith(_ => QueueRefresh());
        }, $"js_folder_enabled_{folder}", T("JS_TOGGLE_FOLDER", "Enable/disable all scripts in folder"));

        var header = FolderHeader.Create(row, folder, files.Length, collapsedFolders.Contains(folder), () => {
            if (!collapsedFolders.Remove(folder)) {
                collapsedFolders.Add(folder);
            }
            Refresh();
        }, $"js_folder_{folder}");
        header.Rect.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private static void BuildFileRow(string file, bool enabled, bool indent) {
        int tags;
        int patches;
        try {
            tags = enabled ? MainCore.V8.ScriptFileTags(file).Count : 0;
            patches = enabled ? JSPatchManager.GetFilePatches(file).Count : 0;
        } catch {
            tags = 0;
            patches = 0;
        }
        RectTransform row = O5Factory.Row(O5KitAdapters.Ctx, listContent, 44f);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(indent ? 28 : 0, 0, 0, 0);

        BuildToggle(row, enabled, value => _ = SetScriptEnabledAsync(file, value),
            $"js_script_enabled_{JSScriptLoader.RelativeName(MainCore.V8.ScriptFolderPath, file)}",
            T("JS_TOGGLE_SCRIPT", "Enable/disable script"));

        var name = O5Factory.ControlText(O5KitAdapters.Ctx, row, 22f, true);
        name.text = Path.GetFileName(file);
        name.alignment = TextAlignmentOptions.Left;
        var nameLe = name.gameObject.AddComponent<LayoutElement>();
        nameLe.flexibleWidth = 1f;

        var counts = O5Factory.ControlText(O5KitAdapters.Ctx, row, 22f, true);
        counts.text = enabled
            ? $"{tags} tags · {patches} patches"
            : T("JS_DISABLED", "Disabled");
        counts.alignment = TextAlignmentOptions.Right;
        var countsLe = counts.gameObject.AddComponent<LayoutElement>();
        countsLe.minWidth = 190f;
        countsLe.flexibleWidth = 0f;
        scriptStatus[file] = counts;
    }

    private static void BuildToggle(RectTransform row, bool enabled, Action<bool> onChange, string id, string tip) {
        GameObject toggleGo = new("ScriptToggle");
        toggleGo.transform.SetParent(row, false);
        toggleGo.AddComponent<RectTransform>();
        var toggleLe = toggleGo.AddComponent<LayoutElement>();
        toggleLe.minWidth = 40f;
        toggleLe.preferredWidth = 40f;
        toggleLe.minHeight = 34f;
        toggleLe.preferredHeight = 34f;
        toggleLe.flexibleWidth = 0f;
        toggleLe.flexibleHeight = 0f;

        var toggle = O5Factory.Toggle(O5KitAdapters.Ctx,
            toggleGo.transform,
            null,
            enabled,
            onChange,
            string.Empty,
            id);
        var hoverOutline = toggle.Rect.transform.Find("Hover");
        if (hoverOutline != null) {
            UnityEngine.Object.Destroy(hoverOutline.gameObject);
        }
        var toggleBg = toggle.Rect.GetComponent<Image>();
        if (toggleBg != null) {
            toggleBg.color = Color.clear;
        }
        toggleGo.transform.AddToolTip(O5KitAdapters.Ctx, () => tip);
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
