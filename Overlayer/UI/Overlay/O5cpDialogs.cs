using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Overlay;

internal static class O5cpDialogs {
    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    public sealed class Modal {
        public GameObject Root;
        public void Close() {
            if(Root) {
                UnityEngine.Object.Destroy(Root);
            }
        }
    }

    private static Modal ShowModal(string name, Vector2 size, string title) {
        var root = new GameObject(name);
        root.transform.SetParent(UICore.CanvasObj.transform, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(root.transform, false);
        var blockerRect = blocker.AddComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        var blockerImg = blocker.AddComponent<Image>();
        blockerImg.color = new Color(0f, 0f, 0f, 0.55f);
        blockerImg.raycastTarget = true;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        panelImg.type = Image.Type.Sliced;
        panelImg.color = UIColors.PanelBG;
        panelImg.raycastTarget = true;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = Vector2.one;
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(16f, -52f);
        titleRect.offsetMax = new Vector2(-16f, -16f);
        var titleText = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = title;
        titleText.font = MainCore.Res.Get<TMPro.TMP_FontAsset>(Asset.SUIT_Medium);
        titleText.fontSize = 24;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;

        root.transform.SetAsLastSibling();
        return new Modal { Root = root };
    }

    private static RectTransform BodyArea(GameObject panel, float bottomBarHeight) {
        var body = new GameObject("Body");
        body.transform.SetParent(panel.transform, false);
        var rect = body.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16f, bottomBarHeight + 8f);
        rect.offsetMax = new Vector2(-16f, -60f);
        var layout = body.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static RectTransform ButtonBar(GameObject panel, float height) {
        var bar = new GameObject("Buttons");
        bar.transform.SetParent(panel.transform, false);
        var rect = bar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(16f, 12f);
        rect.offsetMax = new Vector2(-16f, 12f + height);
        return rect;
    }

    private static O5Button BarButtonLeft(RectTransform bar, string text, float offsetX, float y, float width, Action onClick, string id) {
        var btn = O5Factory.Button(O5KitAdapters.Ctx, bar, onClick, text, id, 40f);
        var rect = btn.Rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(offsetX, y);
        rect.sizeDelta = new Vector2(width, 40f);
        return btn;
    }

    private static O5Button BarButtonRight(RectTransform bar, string text, float offsetXFromRight, float y, float width, Action onClick, string id) {
        var btn = O5Factory.Button(O5KitAdapters.Ctx, bar, onClick, text, id, 40f);
        var rect = btn.Rect;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-offsetXFromRight, y);
        rect.sizeDelta = new Vector2(width, 40f);
        return btn;
    }

    public static void ShowExportOptions(Action<Package.O5cpExportOptions> onExport) {
        var options = new Package.O5cpExportOptions();
        var modal = ShowModal("O5cpExportDialog", new Vector2(480f, 400f),
            T("O5CP_EXPORT_TITLE", "Export Package"));
        var panel = modal.Root.transform.Find("Panel").gameObject;
        var body = BodyArea(panel, 64f);
        var bar = ButtonBar(panel, 40f);

        var imgRow = O5Factory.Row(O5KitAdapters.Ctx, body);
        O5Factory.Toggle(O5KitAdapters.Ctx, imgRow, true, options.IncludeImages,
            toggle => options.IncludeImages = toggle,
            T("O5CP_INCLUDE_IMAGES", "Include images"), "o5cp_include_images");
        var fontRow = O5Factory.Row(O5KitAdapters.Ctx, body);
        O5Factory.Toggle(O5KitAdapters.Ctx, fontRow, true, options.IncludeFonts,
            toggle => options.IncludeFonts = toggle,
            T("O5CP_INCLUDE_FONTS", "Include fonts"), "o5cp_include_fonts");
        var jsRow = O5Factory.Row(O5KitAdapters.Ctx, body);
        O5Factory.Toggle(O5KitAdapters.Ctx, jsRow, true, options.IncludeScripts,
            toggle => options.IncludeScripts = toggle,
            T("O5CP_INCLUDE_JS", "Include JS (auto tracked)"), "o5cp_include_js");

        var noteRow = O5Factory.Row(O5KitAdapters.Ctx, body, 80f);
        var note = O5Factory.ControlText(O5KitAdapters.Ctx, noteRow, 16f, true);
        note.text = T("O5CP_EXPORT_NOTE",
            "Unchecked resources are recorded as missing and warn on import.");
        note.color = new Color(1f, 1f, 1f, 0.6f);

        BarButtonLeft(bar, T("O5CP_EXPORT", "Export"), 0f, 0f, 150f, () => {
            modal.Close();
            onExport?.Invoke(options);
        }, "o5cp_do_export");
        BarButtonRight(bar, T("CANCEL", "Cancel"), 0f, 0f, 150f, modal.Close, "o5cp_cancel_export");
    }

    public static void ShowWarnings(string title, List<string> warnings) {
        var modal = ShowModal("O5cpWarningsDialog", new Vector2(520f, 420f), title);
        var panel = modal.Root.transform.Find("Panel").gameObject;
        var body = BodyArea(panel, 64f);
        var bar = ButtonBar(panel, 40f);

        var (_, content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, body, 8f, 8f, true);
        if(warnings == null || warnings.Count == 0) {
            var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
            var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 18f, true);
            label.text = T("O5CP_NO_WARNINGS", "No warnings.");
        } else {
            foreach(string warning in warnings) {
                var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 30f);
                var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 17f, true);
                label.text = "• " + warning;
                label.color = new Color(1f, 0.85f, 0.5f);
            }
        }
        BarButtonLeft(bar, T("CANCEL", "Close"), 0f, 0f, 150f, modal.Close, "o5cp_close_warnings");
    }

    public static void ShowPresets(Action<Package.PackagePreset> onPick) {
        Package.PresetStore.RefreshFilePresets();
        var presets = Package.PresetStore.Presets;
        var modal = ShowModal("O5cpPresetDialog", new Vector2(560f, 480f),
            T("O5CP_PRESETS_TITLE", "Presets"));
        var panel = modal.Root.transform.Find("Panel").gameObject;
        var body = BodyArea(panel, 64f);
        var bar = ButtonBar(panel, 40f);

        var (_, content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, body, 8f, 8f, true);
        if(presets.Count == 0) {
            var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform);
            var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 18f, true);
            label.text = T("O5CP_PRESET_EMPTY", "No presets.");
        } else {
            foreach(var preset in presets) {
                var captured = preset;
                var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 44f);
                var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 17f, true);
                string source = string.IsNullOrEmpty(captured.Module)
                    ? T("O5CP_PRESET_FILE", "File")
                    : captured.Module;
                label.text = $"{captured.Name}  ·  {source}";
                label.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                label.rectTransform.offsetMax = new Vector2(-8f, 0f);

                var importBtn = O5Factory.Button(O5KitAdapters.Ctx, row, () => { },
                    T("O5CP_PRESET_IMPORT", "Import"), $"o5cp_preset_{captured.Id}", 36f);
                var importRect = importBtn.Rect;
                importRect.anchorMin = new Vector2(0.62f, 0f);
                importRect.anchorMax = new Vector2(1f, 1f);
                importRect.pivot = new Vector2(1f, 0.5f);
                importRect.offsetMin = new Vector2(0f, 4f);
                importRect.offsetMax = new Vector2(0f, -4f);
                importBtn.OnClick = () => {
                    modal.Close();
                    onPick?.Invoke(captured);
                };
            }
        }
        BarButtonLeft(bar, T("CANCEL", "Close"), 0f, 0f, 150f, modal.Close, "o5cp_close_presets");
    }

    public static void ShowPromoteConflicts(
        Package.PackageEntry entry,
        Package.O5cpPromotePlan plan,
        Action onApply
    ) {
        string title = T("O5CP_PROMOTE_TITLE", "Copy to editable canvas");
        if(entry?.Manifest?.Package?.Name is string pkgName && !string.IsNullOrEmpty(pkgName)) {
            title += $" — {pkgName}";
        }
        var modal = ShowModal("O5cpPromoteDialog", new Vector2(660f, 580f), title);
        var panel = modal.Root.transform.Find("Panel").gameObject;
        var body = BodyArea(panel, 110f);
        var bar = ButtonBar(panel, 96f);

        var (_, content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, body, 8f, 8f, true);
        var renameInputs = new Dictionary<Package.O5cpPromoteItem, O5InputField>();
        foreach(var item in plan.Items) {
            if(!item.IsConflict) {
                var infoRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 30f);
                var info = O5Factory.ControlText(O5KitAdapters.Ctx, infoRow, 16f, true);
                info.text = $"[{item.Kind}] {item.Key} — " + (item.Choice == Package.O5cpConflictChoice.Reuse
                    ? T("O5CP_REUSE_AUTO", "auto: reuse")
                    : T("O5CP_IMPORT_AUTO", "auto: import"));
                info.color = new Color(1f, 1f, 1f, 0.55f);
                continue;
            }
            var row = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 44f);
            var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 17f, true);
            label.text = $"[{item.Kind}] {item.Key}";
            label.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var choiceBtn = O5Factory.Button(O5KitAdapters.Ctx, row, () => { },
                ChoiceText(item.Choice), $"o5cp_choice_{item.Kind}_{item.Key}", 36f);
            var choiceRect = choiceBtn.Rect;
            choiceRect.anchorMin = new Vector2(0.62f, 0f);
            choiceRect.anchorMax = new Vector2(1f, 1f);
            choiceRect.pivot = new Vector2(1f, 0.5f);
            choiceRect.offsetMin = new Vector2(0f, 4f);
            choiceRect.offsetMax = new Vector2(0f, -4f);
            choiceBtn.OnClick = () => {
                item.Choice = item.Choice switch {
                    Package.O5cpConflictChoice.Overwrite => Package.O5cpConflictChoice.Rename,
                    Package.O5cpConflictChoice.Rename => Package.O5cpConflictChoice.Skip,
                    _ => Package.O5cpConflictChoice.Overwrite,
                };
                choiceBtn.Label.text = ChoiceText(item.Choice);
                if(renameInputs.TryGetValue(item, out var input)) {
                    input.Rect.gameObject.SetActive(item.Choice == Package.O5cpConflictChoice.Rename);
                }
            };

            var renameRow = O5Factory.Row(O5KitAdapters.Ctx, content.transform, 44f);
            var renameInput = O5Factory.Input(O5KitAdapters.Ctx, renameRow, null,
                item.RenameTo, value => item.RenameTo = value,
                T("O5CP_RENAME_TO", "New name..."), null, $"o5cp_rename_{item.Kind}_{item.Key}");
            renameRow.gameObject.SetActive(item.Choice == Package.O5cpConflictChoice.Rename);
            renameInputs[item] = renameInput;
        }

        BarButtonLeft(bar, T("O5CP_ALL_OVERWRITE", "All: overwrite"), 0f, 24f, 160f, () => {
            foreach(var item in plan.Items.Where(i => i.IsConflict)) {
                item.Choice = Package.O5cpConflictChoice.Overwrite;
            }
            modal.Close();
            ShowPromoteConflicts(entry, plan, onApply);
        }, "o5cp_bulk_overwrite");
        BarButtonLeft(bar, T("O5CP_ALL_RENAME", "All: rename"), 170f, 24f, 150f, () => {
            foreach(var item in plan.Items.Where(i => i.IsConflict)) {
                item.Choice = Package.O5cpConflictChoice.Rename;
            }
            modal.Close();
            ShowPromoteConflicts(entry, plan, onApply);
        }, "o5cp_bulk_rename");
        BarButtonLeft(bar, T("O5CP_ALL_SKIP", "All: skip"), 330f, 24f, 140f, () => {
            foreach(var item in plan.Items.Where(i => i.IsConflict)) {
                item.Choice = Package.O5cpConflictChoice.Skip;
            }
            modal.Close();
            ShowPromoteConflicts(entry, plan, onApply);
        }, "o5cp_bulk_skip");

        BarButtonLeft(bar, T("O5CP_IMPORT_ACTION", "Copy"), 0f, -24f, 150f, () => {
            modal.Close();
            onApply?.Invoke();
        }, "o5cp_do_promote");
        BarButtonRight(bar, T("CANCEL", "Cancel"), 0f, -24f, 150f, modal.Close, "o5cp_cancel_promote");
    }

    private static string ChoiceText(Package.O5cpConflictChoice choice) => choice switch {
        Package.O5cpConflictChoice.Overwrite => T("O5CP_OVERWRITE", "Overwrite"),
        Package.O5cpConflictChoice.Rename => T("O5CP_RENAME", "Rename"),
        Package.O5cpConflictChoice.Skip => T("O5CP_SKIP", "Skip"),
        Package.O5cpConflictChoice.Reuse => T("O5CP_REUSE_AUTO", "auto: reuse"),
        _ => T("O5CP_IMPORT_AUTO", "auto: import"),
    };
}
