using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.Overlay;
using Overlayer.Package;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Overlay;

public sealed class OvCanvasPropsPage : IDisposable {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;

    private readonly Action onBackAction;

    private OvCanvas currentCanvas;
    private O5cpProps draft = new();
    private Texture2D previewTexture;
    private Image previewImage;
    private O5InputField authorInput;
    private O5InputField descInput;
    private O5InputField versionInput;
    private O5InputField licenseInput;
    private RectTransform scriptList;
    private TMPro.TextMeshProUGUI titleText;

    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    public OvCanvasPropsPage(Transform parent, Action onBack) {
        onBackAction = onBack;

        GameObject = new(nameof(OvCanvasPropsPage));
        GameObject.transform.SetParent(parent, false);

        RectTransform = GameObject.AddComponent<RectTransform>();
        RectTransform.anchorMin = Vector2.zero;
        RectTransform.anchorMax = Vector2.one;
        RectTransform.offsetMin = Vector2.zero;
        RectTransform.offsetMax = Vector2.zero;
        GameObject.SetActive(false);

        Build();
    }

    private void Build() {
        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(GameObject.transform, false);
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = new Vector2(0f, -60f);
        headerRect.offsetMax = Vector2.zero;

        var backBtn = O5Factory.Button(O5KitAdapters.Ctx, headerGo.transform,
            () => Close(), T("O5CP_BACK", "Back"), "props_back", 40f);
        backBtn.Rect.anchorMin = new Vector2(0f, 0.5f);
        backBtn.Rect.anchorMax = new Vector2(0f, 0.5f);
        backBtn.Rect.pivot = new Vector2(0f, 0.5f);
        backBtn.Rect.anchoredPosition = new Vector2(12f, 0f);
        backBtn.Rect.sizeDelta = new Vector2(120f, 40f);

        var saveBtn = O5Factory.Button(O5KitAdapters.Ctx, headerGo.transform,
            () => Save(), T("SAVE", "Save"), "props_save", 40f);
        saveBtn.Rect.anchorMin = new Vector2(1f, 0.5f);
        saveBtn.Rect.anchorMax = new Vector2(1f, 0.5f);
        saveBtn.Rect.pivot = new Vector2(1f, 0.5f);
        saveBtn.Rect.anchoredPosition = new Vector2(-12f, 0f);
        saveBtn.Rect.sizeDelta = new Vector2(120f, 40f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(150f, 0f);
        titleRect.offsetMax = new Vector2(-150f, 0f);
        titleText = titleGo.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.font = MainCore.Res.Get<TMPro.TMP_FontAsset>(Asset.SUIT_Medium);
        titleText.fontSize = 24;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(GameObject.transform, false);
        var bodyRect = bodyGo.AddComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(12f, 12f);
        bodyRect.offsetMax = new Vector2(-12f, -64f);

        var (_, content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, bodyGo.transform, 10f, 10f);
        Transform root = content.transform;

        var thumbHead = O5Factory.ControlTextH1(O5KitAdapters.Ctx, O5Factory.Row(O5KitAdapters.Ctx, root));
        thumbHead.text = T("O5CP_THUMBNAIL", "Thumbnail");

        var thumbRow = O5Factory.Row(O5KitAdapters.Ctx, root, 140f);
        var previewGo = new GameObject("Preview");
        previewGo.transform.SetParent(thumbRow, false);
        var previewRect = previewGo.AddComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0f, 0.5f);
        previewRect.anchorMax = new Vector2(0f, 0.5f);
        previewRect.pivot = new Vector2(0f, 0.5f);
        previewRect.anchoredPosition = new Vector2(0f, 0f);
        previewRect.sizeDelta = new Vector2(220f, 124f);
        previewImage = previewGo.AddComponent<Image>();
        previewImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        previewImage.type = Image.Type.Simple;
        previewImage.preserveAspect = true;
        previewImage.color = new Color(1f, 1f, 1f, 0.35f);

        var browseBtn = O5Factory.Button(O5KitAdapters.Ctx, thumbRow,
            () => BrowseThumbnail(), T("BROWSE", "Browse"), "props_thumb_browse", 40f);
        browseBtn.Rect.anchorMin = new Vector2(0f, 1f);
        browseBtn.Rect.anchorMax = new Vector2(0f, 1f);
        browseBtn.Rect.pivot = new Vector2(0f, 1f);
        browseBtn.Rect.anchoredPosition = new Vector2(236f, 0f);
        browseBtn.Rect.sizeDelta = new Vector2(140f, 40f);

        var clearBtn = O5Factory.Button(O5KitAdapters.Ctx, thumbRow,
            () => SetThumbnail(null), T("REMOVE", "Remove"), "props_thumb_clear", 40f);
        clearBtn.Rect.anchorMin = new Vector2(0f, 1f);
        clearBtn.Rect.anchorMax = new Vector2(0f, 1f);
        clearBtn.Rect.pivot = new Vector2(0f, 1f);
        clearBtn.Rect.anchoredPosition = new Vector2(384f, 0f);
        clearBtn.Rect.sizeDelta = new Vector2(140f, 40f);

        var metaHead = O5Factory.ControlTextH1(O5KitAdapters.Ctx, O5Factory.Row(O5KitAdapters.Ctx, root));
        metaHead.text = T("O5CP_META", "Package info");

        authorInput = LabeledInput(root, "O5CP_AUTHOR", "Author");
        descInput = LabeledInput(root, "O5CP_DESCRIPTION", "Description", multiline: true);
        versionInput = LabeledInput(root, "O5CP_VERSION", "Version");
        licenseInput = LabeledInput(root, "O5CP_LICENSE", "License");

        var scriptHead = O5Factory.ControlTextH1(O5KitAdapters.Ctx, O5Factory.Row(O5KitAdapters.Ctx, root));
        scriptHead.text = T("O5CP_SCRIPTS", "Extra JS files");
        var scriptNoteRow = O5Factory.Row(O5KitAdapters.Ctx, root, 30f);
        var scriptNote = O5Factory.ControlText(O5KitAdapters.Ctx, scriptNoteRow, 16f, true);
        scriptNote.text = T("O5CP_SCRIPTS_NOTE",
            "JS the exporter cannot trace (patches, object access) must be listed here.");
        scriptNote.color = new Color(1f, 1f, 1f, 0.6f);

        scriptList = new GameObject("ScriptList").AddComponent<RectTransform>();
        scriptList.transform.SetParent(root, false);
        var scriptLayout = scriptList.gameObject.AddComponent<VerticalLayoutGroup>();
        scriptLayout.spacing = 8f;
        scriptLayout.childControlWidth = true;
        scriptLayout.childControlHeight = true;
        scriptLayout.childForceExpandWidth = true;
        scriptLayout.childForceExpandHeight = false;
        var scriptFitter = scriptList.gameObject.AddComponent<ContentSizeFitter>();
        scriptFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var addRow = O5Factory.Row(O5KitAdapters.Ctx, root, 44f);
        O5Factory.Button(O5KitAdapters.Ctx, addRow, () => BrowseScript(),
            T("O5CP_ADD_SCRIPT", "Add JS"), "props_script_add", 40f);
    }

    private static O5InputField LabeledInput(Transform root, string key, string fallback, bool multiline = false) {
        var labelRow = O5Factory.Row(O5KitAdapters.Ctx, root, 28f);
        var label = O5Factory.ControlText(O5KitAdapters.Ctx, labelRow, 18f, true);
        label.text = MainCore.Tr.Get(key, fallback);
        var inputRow = O5Factory.Row(O5KitAdapters.Ctx, root, multiline ? 80f : 44f);
        return O5Factory.Input(O5KitAdapters.Ctx, inputRow, null, string.Empty,
            null, MainCore.Tr.Get(key, fallback), null, $"props_{key.ToLowerInvariant()}",
            null, multiline);
    }

    public void Open(OvCanvas canvas) {
        if (canvas == null) {
            return;
        }
        currentCanvas = canvas;
        draft = canvas.Props?.Copy() ?? new O5cpProps();
        titleText.text = $"{T("O5CP_PROPS_TITLE", "Canvas Properties")} — {canvas.Config?.Name?.Value}";
        authorInput.Set(draft.Author ?? string.Empty);
        descInput.Set(draft.Description ?? string.Empty);
        versionInput.Set(string.IsNullOrEmpty(draft.Version) ? "1.0.0" : draft.Version);
        licenseInput.Set(draft.License ?? string.Empty);
        RefreshScripts();
        SetThumbnail(string.IsNullOrEmpty(draft.ThumbnailPath)
            ? null
            : UserResourceManager.FromUser(draft.ThumbnailPath));
        GameObject.SetActive(true);
    }

    public void Close(bool silent = false) {
        ClearPreview();
        currentCanvas = null;
        GameObject.SetActive(false);
        if (!silent) {
            onBackAction?.Invoke();
        }
    }

    private void Save() {
        if (currentCanvas == null) {
            Close();
            return;
        }
        draft.Author = authorInput.Value ?? string.Empty;
        draft.Description = descInput.Value ?? string.Empty;
        draft.Version = string.IsNullOrWhiteSpace(versionInput.Value) ? "1.0.0" : versionInput.Value.Trim();
        draft.License = licenseInput.Value ?? string.Empty;
        currentCanvas.Props = draft.Copy();
        OverlayCore.SaveAllCanvases();
        Close();
    }

    private void RefreshScripts() {
        if (scriptList == null) {
            return;
        }
        foreach (Transform child in scriptList.transform) {
            UnityEngine.Object.Destroy(child.gameObject);
        }
        foreach (string tokenized in draft.ExtraScripts.ToArray()) {
            string local = tokenized;
            var row = O5Factory.Row(O5KitAdapters.Ctx, scriptList.transform, 44f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 22f, true);
            label.text = Path.GetFileName(UserResourceManager.FromUser(local));
            label.alignment = TMPro.TextAlignmentOptions.Left;
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            var del = O5Factory.Button(O5KitAdapters.Ctx, row, () => {
                draft.ExtraScripts.Remove(local);
                RefreshScripts();
            }, "X", $"props_script_del_{local.GetHashCode()}", 36f);
            var delLe = del.Rect.gameObject.AddComponent<LayoutElement>();
            delLe.minWidth = 64f;
            delLe.preferredWidth = 64f;
            delLe.flexibleWidth = 0f;
        }
    }

    private void BrowseThumbnail() {
        _ = Utility.NativeImageFilePicker.PickAsync().ContinueWith(task => {
            MainThread.Enqueue(() => {
                if (currentCanvas == null || !GameObject.activeSelf) {
                    return;
                }
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if (string.IsNullOrWhiteSpace(path)) {
                    return;
                }
                SetThumbnail(path);
            });
        });
    }

    private void SetThumbnail(string diskPath) {
        ClearPreview();
        if (!string.IsNullOrEmpty(diskPath) && File.Exists(diskPath)) {
            try {
                byte[] bytes = File.ReadAllBytes(diskPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes)) {
                    tex.filterMode = FilterMode.Bilinear;
                    previewTexture = tex;
                    previewImage.sprite = Sprite.Create(
                        tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    previewImage.color = Color.white;
                    draft.ThumbnailPath = UserResourceManager.ToUser(diskPath);
                    return;
                }
                UnityEngine.Object.Destroy(tex);
            } catch {
            }
        }
        previewImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        previewImage.color = new Color(1f, 1f, 1f, 0.35f);
        if (diskPath == null) {
            draft.ThumbnailPath = string.Empty;
        }
    }

    private void BrowseScript() {
        string dir = MainCore.V8?.ScriptFolderPath;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        _ = Utility.NativeDialogThread.Run(() => {
            try {
                return NativeFileDialog.Extended.NFD.OpenDialog(
                    dir,
                    new Dictionary<string, string> { ["JavaScript"] = "js" });
            } catch (Exception e) {
                MainCore.Log.Err($"[ScriptPicker] File dialog failed: {e.Message}");
                return null;
            }
        }).ContinueWith(task => {
            MainThread.Enqueue(() => {
                if (currentCanvas == null || !GameObject.activeSelf) {
                    return;
                }
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if (string.IsNullOrWhiteSpace(path)) {
                    return;
                }
                string tokenized = UserResourceManager.ToUser(path);
                if (!draft.ExtraScripts.Contains(tokenized)) {
                    draft.ExtraScripts.Add(tokenized);
                    RefreshScripts();
                }
            });
        });
    }

    private void ClearPreview() {
        if (previewTexture) {
            previewImage.sprite = null;
            UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null;
        }
    }

    public void Dispose() {
        ClearPreview();
        if (GameObject) {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
}
