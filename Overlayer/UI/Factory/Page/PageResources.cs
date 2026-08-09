using Overlayer.Async;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.IO.User.Impl;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageResources {
    private static UIInput pathInput;
    private static UIInput keyInput;
    private static UIButton modeButton;
    private static UIInput searchInput;
    private static UIToggle mipChainToggle;
    private static UIToggle linearToggle;
    private static RectTransform imageSettingsRow;
    private static UIButton browseButton;
    private static UIButton addButton;
    private static TextMeshProUGUI statusLabel;
    private static TextMeshProUGUI titleLabel;
    private static RectTransform listContent;
    private static RectTransform listViewport;
    private static string settingsEditKey;
    private static GameObject disabledPanel;
    private static bool busy;

    private enum ResourceMode { Images, Fonts }
    private static ResourceMode currentMode = ResourceMode.Images;

    public static void Create(RectTransform parent) {
        RectTransform root = CreateStretch(parent, "ResourcesRoot");

        RectTransform titleRow = CreateContainer(root, "TitleRow");
        titleRow.anchorMin = new Vector2(0f, 1f);
        titleRow.anchorMax = new Vector2(1f, 1f);
        titleRow.offsetMin = new Vector2(24f, -62f);
        titleRow.offsetMax = new Vector2(-24f, -12f);

        titleLabel = CreateText(titleRow, T("IMAGE_RESOURCES", "Image Resources"), 30f, TextAlignmentOptions.Left);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-190f, 0f);
        titleLabel.raycastTarget = true;
        titleLabel.gameObject.AddComponent<TextLocalization>().Init("IMAGE_RESOURCES", "Image Resources");

        modeButton = GenerateUI.Button(titleRow, ToggleMode, T("RESOURCE_MODE_IMAGES", "Images"), "resource_mode");
        PlaceRight(modeButton.Rect, 174f);
        modeButton.Label.gameObject.AddComponent<TextLocalization>().Init("RESOURCE_MODE_IMAGES", "Images");

        RectTransform importCard = CreateContainer(root, "ImportRows");
        importCard.anchorMin = new Vector2(0f, 1f);
        importCard.anchorMax = new Vector2(1f, 1f);
        importCard.offsetMin = new Vector2(12f, -260f);
        importCard.offsetMax = new Vector2(-12f, -78f);

        RectTransform pathRow = CreateRow(importCard, 8f);
        pathInput = GenerateUI.Input(
            pathRow,
            string.Empty,
            string.Empty,
            _ => { },
            T("IMAGE_PATH", "Image path"),
            null,
            "resource_image_path"
        );
        pathInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("IMAGE_PATH", "Image path");
        pathInput.Rect.AddToolTip(
            "IMAGE_PATH_TOOLTIP",
            "Select image file to import."
        );
        ResizeInput(pathInput.Rect, 190f);

        browseButton = GenerateUI.Button(pathRow, BeginBrowse, T("BROWSE", "Browse"), "resource_browse");
        PlaceRight(browseButton.Rect, 174f);
        browseButton.Label.gameObject.AddComponent<TextLocalization>().Init("BROWSE", "Browse");

        RectTransform keyRow = CreateRow(importCard, 66f);
        keyInput = GenerateUI.Input(
            keyRow,
            string.Empty,
            string.Empty,
            _ => { },
            "Resource name",
            null,
            "resource_image_name"
        );
        keyInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("RESOURCE_NAME", "Resource name");
        keyInput.Rect.AddToolTip(
            "RESOURCE_NAME_TOOLTIP",
            "Name used by Image components to reference this resource."
        );
        ResizeInput(keyInput.Rect, 190f);

        addButton = GenerateUI.Button(keyRow, BeginImport, T("ADD_IMAGE", "Add Image"), "resource_add");
        PlaceRight(addButton.Rect, 174f);
        addButton.Label.gameObject.AddComponent<TextLocalization>().Init("ADD_IMAGE", "Add Image");

        RectTransform settingsRow = CreateRow(importCard, 124f);
        imageSettingsRow = settingsRow;
        mipChainToggle = GenerateUI.Toggle(
            settingsRow,
            false,
            false,
            _ => { },
            T("MIP_CHAIN", "Mip Chain"),
            "resource_mip_chain"
        );
        PlaceHalf(mipChainToggle.Rect, false);
        mipChainToggle.Rect.AddToolTip(
            "MIP_CHAIN_TOOLTIP",
            "Create mip levels for smoother minified rendering."
        );
        mipChainToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIP_CHAIN", "Mip Chain");

        linearToggle = GenerateUI.Toggle(
            settingsRow,
            false,
            false,
            _ => { },
            T("LINEAR", "Linear"),
            "resource_linear"
        );
        PlaceHalf(linearToggle.Rect, true);
        linearToggle.Rect.AddToolTip(
            "LINEAR_TOOLTIP",
            "Load texture data as linear color instead of sRGB."
        );
        linearToggle.Label.gameObject.AddComponent<TextLocalization>().Init("LINEAR", "Linear");

        statusLabel = CreateText(root, T("READY", "Ready"), 15f, TextAlignmentOptions.Left);
        statusLabel.color = new Color(1f, 1f, 1f, 0.55f);
        statusLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusLabel.rectTransform.offsetMin = new Vector2(24f, -296f);
        statusLabel.rectTransform.offsetMax = new Vector2(-24f, -268f);

        RectTransform searchRow = CreateRow(root, 0f);
        searchRow.anchorMin = new Vector2(0f, 1f);
        searchRow.anchorMax = new Vector2(1f, 1f);
        searchRow.offsetMin = new Vector2(18f, -352f);
        searchRow.offsetMax = new Vector2(-18f, -302f);
        searchInput = GenerateUI.Input(
            searchRow,
            string.Empty,
            string.Empty,
            _ => BuildList(),
            T("SEARCH_RESOURCE", "Search resources"),
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "resource_search"
        );
        searchInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("SEARCH_RESOURCE", "Search resources");
        searchInput.Rect.AddToolTip(
            "SEARCH_RESOURCE_TOOLTIP",
            "Filter resources by name."
        );
        searchInput.Rect.offsetMax = Vector2.zero;

        GameObject viewportObject = new("ImageViewport");
        viewportObject.transform.SetParent(root, false);
        listViewport = viewportObject.AddComponent<RectTransform>();
        listViewport.anchorMin = Vector2.zero;
        listViewport.anchorMax = Vector2.one;
        listViewport.offsetMin = new Vector2(18f, 18f);
        listViewport.offsetMax = new Vector2(-18f, -366f);
        viewportObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = new("ImageList");
        contentObject.transform.SetParent(listViewport, false);
        listContent = contentObject.AddComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = Vector2.zero;
        listContent.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        root.gameObject.AddComponent<UIScrollController>().SetContent(listContent, listViewport);

        CreateDisabledPanel(root);
        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) ToggleUIStateByMod(isEnabled);
        };
        ToggleUIStateByMod(MainCore.IsModEnabled);

        BuildList();
        MainThread.Enqueue(() => {
            BuildList();
            Canvas.ForceUpdateCanvases();
            if(listContent) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        });
    }

    private static void BeginBrowse() {
        if(busy) return;
        browseButton.SetBlocked(true);
        browseButton.Label.text = "...";
        SetStatus("OPENING_FILE_PICKER", "Opening file picker...", UIColors.ObjectActive);

        if(currentMode == ResourceMode.Images) {
            _ = NativeImageFilePicker.PickAsync().ContinueWith(task => {
                MainThread.Enqueue(() => {
                    if(!MainCore.IsModEnabled) return;
                    browseButton.SetBlocked(false);
                    browseButton.Label.text = T("BROWSE", "Browse");
                    string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                    if(string.IsNullOrWhiteSpace(path)) {
                        SetStatus("NO_IMAGE_SELECTED", "No image selected.", UIColors.ObjectActiveMathWarn);
                        return;
                    }

                    pathInput.Set(path);
                    if(string.IsNullOrWhiteSpace(keyInput.Value)) {
                        keyInput.Set(Path.GetFileNameWithoutExtension(path));
                    }
                    SetStatus("IMAGE_SELECTED_ADD_IMAGE", "Image selected. Choose Add Image.", UIColors.ObjectActive);
                });
            });
        } else {
            _ = NativeFontFilePicker.PickAsync().ContinueWith(task => {
                MainThread.Enqueue(() => {
                    if(!MainCore.IsModEnabled) return;
                    browseButton.SetBlocked(false);
                    browseButton.Label.text = T("BROWSE", "Browse");
                    string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                    if(string.IsNullOrWhiteSpace(path)) {
                        SetStatus("NO_FONT_SELECTED", "No font selected.", UIColors.ObjectActiveMathWarn);
                        return;
                    }

                    pathInput.Set(path);
                    if(string.IsNullOrWhiteSpace(keyInput.Value)) {
                        keyInput.Set(Path.GetFileNameWithoutExtension(path));
                    }
                    SetStatus("FONT_SELECTED_ADD_FONT", "Font selected. Choose Add Font.", UIColors.ObjectActive);
                });
            });
        }
    }

    private static void BeginImport() {
        if(busy) return;
        if(currentMode == ResourceMode.Images) {
            if(!string.IsNullOrEmpty(settingsEditKey)) {
                BeginSettingsApply();
                return;
            }
            string source = UserResourceManager.FromUser(pathInput.Value?.Trim());
            string key = SanitizeKey(keyInput.Value);

            if(string.IsNullOrWhiteSpace(source)) {
                SetStatus("CHOOSE_IMAGE_FIRST", "Choose an image first.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(string.IsNullOrWhiteSpace(key)) {
                SetStatus("ENTER_RESOURCE_NAME", "Enter a resource name.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(UserResourceManager.T2D.Keys.Contains(key) || UserResourceManager.Spr.Keys.Contains(key)) {
                SetStatus("RESOURCE_NAME_ALREADY_EXISTS", "Resource name already exists.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(!UserTexture2D.Ext.Contains(Path.GetExtension(source).ToLowerInvariant())) {
                SetStatus("UNSUPPORTED_IMAGE_FORMAT", "Unsupported image format.", UIColors.ObjectActiveMathErr);
                return;
            }

            busy = true;
            pathInput.SetBlocked(true);
            keyInput.SetBlocked(true);
            browseButton.SetBlocked(true);
            addButton.SetBlocked(true);
            addButton.Label.text = T("LOADING", "Loading...");
            SetStatus("READING_IMAGE", "Reading image...", UIColors.ObjectActive);

            string extension = Path.GetExtension(source).ToLowerInvariant();
            string target = Path.Combine(MainCore.Paths.UserImagePath, key + extension);
            _ = Task.Run(() => {
                try {
                    Directory.CreateDirectory(MainCore.Paths.UserImagePath);
                    byte[] bytes = File.ReadAllBytes(source);
                    File.WriteAllBytes(target, bytes);
                    return (Bytes: bytes, Path: target, Error: string.Empty);
                } catch(Exception e) {
                    return (Bytes: (byte[])null, Path: target, Error: e.Message);
                }
            }).ContinueWith(task => {
                MainThread.Enqueue(() => FinishImport(task, key));
            });
        } else {
            // Fonts
            string source = UserResourceManager.FromUser(pathInput.Value?.Trim());
            string key = SanitizeKey(keyInput.Value);

            if(string.IsNullOrWhiteSpace(source)) {
                SetStatus("CHOOSE_FONT_FIRST", "Choose a font first.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(string.IsNullOrWhiteSpace(key)) {
                SetStatus("ENTER_RESOURCE_NAME", "Enter a resource name.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(UserResourceManager.Fnt.Keys.Contains(key)) {
                SetStatus("RESOURCE_NAME_ALREADY_EXISTS", "Resource name already exists.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(!UserFont.Ext.Contains(Path.GetExtension(source).ToLowerInvariant())) {
                SetStatus("UNSUPPORTED_FONT_FORMAT", "Unsupported font format.", UIColors.ObjectActiveMathErr);
                return;
            }

            busy = true;
            pathInput.SetBlocked(true);
            keyInput.SetBlocked(true);
            browseButton.SetBlocked(true);
            addButton.SetBlocked(true);
            addButton.Label.text = T("LOADING", "Loading...");
            SetStatus("READING_FONT", "Reading font...", UIColors.ObjectActive);

            string extension = Path.GetExtension(source).ToLowerInvariant();
            string target = Path.Combine(MainCore.Paths.UserFontPath, key + extension);
            _ = Task.Run(() => {
                try {
                    Directory.CreateDirectory(MainCore.Paths.UserFontPath);
                    byte[] bytes = File.ReadAllBytes(source);
                    File.WriteAllBytes(target, bytes);
                    return (Path: target, Bytes: bytes, Error: string.Empty);
                } catch(Exception e) {
                    return (Path: string.Empty, Bytes: (byte[])null, Error: e.Message);
                }
            }).ContinueWith(task => {
                MainThread.Enqueue(() => FinishFontImport(task, key));
            });
        }
    }

    private static void FinishImport(
        Task<(byte[] Bytes, string Path, string Error)> task,
        string key
    ) {
        if(!MainCore.IsModEnabled) return;
        var result = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : (Bytes: (byte[])null, Path: string.Empty, Error: "Image read task failed.");
        if(result.Bytes == null) {
            FinishBusy();
            SetStatus("IMPORT_FAILED", "Import failed: {0}", UIColors.ObjectActiveMathErr, result.Error);
            return;
        }

        UserTexture2D.Result textureResult = UserResourceManager.T2D.LoadData(
            key,
            result.Path,
            result.Bytes,
            mipChainToggle?.Value ?? false,
            linearToggle?.Value ?? false
        );
        if(textureResult != UserTexture2D.Result.Success ||
            !UserResourceManager.T2D.TryGet(key, out var textureValue)) {
            FinishBusy();
            SetStatus("IMAGE_LOAD_FAILED", "Image load failed: {0}", UIColors.ObjectActiveMathErr, textureResult);
            return;
        }

        UserSprite.Result spriteResult = UserResourceManager.Spr.Load(
            key,
            key,
            new Rect(0f, 0f, textureValue.texture.width, textureValue.texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            Vector4.zero,
            out _
        );
        if(spriteResult != UserSprite.Result.Success) {
            UserResourceManager.T2D.Remove(key);
            FinishBusy();
            SetStatus("SPRITE_CREATION_FAILED", "Sprite creation failed: {0}", UIColors.ObjectActiveMathErr, spriteResult);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        pathInput.Set(string.Empty);
        keyInput.Set(string.Empty);
        FinishBusy();
        BuildList();
        SetStatus($"Added {key}.", UIColors.ObjectActiveMathOk);
    }

    private static void FinishFontImport(
        Task<(string Path, byte[] Bytes, string Error)> task,
        string key
    ) {
        if(!MainCore.IsModEnabled) return;
        var result = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : (Path: string.Empty, Bytes: (byte[])null, Error: "Font read task failed.");
        if(result.Bytes == null) {
            FinishBusy();
            SetStatus("IMPORT_FAILED", "Import failed: {0}", UIColors.ObjectActiveMathErr, result.Error);
            return;
        }

        var fontResult = UserResourceManager.Fnt.Load(key, result.Path);
        if(fontResult != UserFont.Result.Success || !UserResourceManager.Fnt.TryGet(key, out var fontValue)) {
            FinishBusy();
            SetStatus("FONT_LOAD_FAILED", "Font load failed: {0}", UIColors.ObjectActiveMathErr, fontResult);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        pathInput.Set(string.Empty);
        keyInput.Set(string.Empty);
        FinishBusy();
        BuildList();
        SetStatus($"Added {key}.", UIColors.ObjectActiveMathOk);
    }

    private static void FinishBusy() {
        busy = false;
        pathInput.SetBlocked(false);
        keyInput.SetBlocked(false);
        browseButton.SetBlocked(false);
        addButton.SetBlocked(false);
        addButton.Label.text = currentMode == ResourceMode.Images ? T("ADD_IMAGE", "Add Image") : T("ADD_FONT", "Add Font");
    }

    private static void BeginSettingsApply() {
        string key = settingsEditKey;
        if(
            string.IsNullOrEmpty(key) ||
            !UserResourceManager.T2D.TryGetPath(key, out string path) ||
            !File.Exists(path)
        ) {
            SetStatus("IMAGE_FILE_NOT_FOUND", "Image file not found.", UIColors.ObjectActiveMathErr);
            return;
        }

        bool mipChain = mipChainToggle.Value;
        bool linear = linearToggle.Value;
        if(
            UserResourceManager.T2D.TryGet(key, out var current) &&
            current.settings.MipChain == mipChain &&
            current.settings.Linear == linear
        ) {
            SetStatus("SETTINGS_UNCHANGED", "Settings unchanged.", UIColors.ObjectActive);
            CancelSettingsEdit();
            return;
        }

        busy = true;
        browseButton.SetBlocked(true);
        addButton.SetBlocked(true);
        addButton.Label.text = T("APPLYING", "Applying...");
        SetStatus("READING_IMAGE", "Reading image...", UIColors.ObjectActive);

        _ = Task.Run(() => {
            try {
                return (Bytes: File.ReadAllBytes(path), Error: string.Empty);
            } catch(Exception e) {
                return (Bytes: (byte[])null, Error: e.Message);
            }
        }).ContinueWith(task => {
            MainThread.Enqueue(() => FinishSettingsApply(task, key, mipChain, linear));
        });
    }

    private static void FinishSettingsApply(
        Task<(byte[] Bytes, string Error)> task,
        string key,
        bool mipChain,
        bool linear
    ) {
        if(!MainCore.IsModEnabled) return;
        var result = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : (Bytes: (byte[])null, Error: "Image read task failed.");
        if(result.Bytes == null) {
            FinishSettingsBusy();
            SetStatus("SETTINGS_FAILED", "Settings failed: {0}", UIColors.ObjectActiveMathErr, result.Error);
            return;
        }

        UserTexture2D.Result textureResult = UserResourceManager.T2D.ReplaceData(
            key,
            result.Bytes,
            mipChain,
            linear
        );
        if(
            textureResult != UserTexture2D.Result.Success ||
            !UserResourceManager.T2D.TryGet(key, out var textureValue)
        ) {
            FinishSettingsBusy();
            SetStatus("SETTINGS_FAILED", "Settings failed: {0}", UIColors.ObjectActiveMathErr, textureResult);
            return;
        }

        if(
            UserResourceManager.Spr.TryGet(key, out _) &&
            !UserResourceManager.Spr.RebuildTexture(key, textureValue.texture)
        ) {
            FinishSettingsBusy();
            SetStatus("SPRITE_REBUILD_FAILED", "Sprite rebuild failed.", UIColors.ObjectActiveMathErr);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        busy = false;
        CancelSettingsEdit();
        BuildList();
        SetStatus("TEXTURE_SETTINGS_APPLIED", "Texture settings applied.", UIColors.ObjectActiveMathOk);
    }

    private static void FinishSettingsBusy() {
        busy = false;
        browseButton.SetBlocked(false);
        addButton.SetBlocked(false);
        addButton.Label.text = T("APPLY_SETTINGS", "Apply Settings");
    }

    private static void EnterSettingsEdit(string key) {
        if(busy || !UserResourceManager.T2D.TryGet(key, out var textureValue)) return;
        if(!UserResourceManager.T2D.TryGetPath(key, out string path)) return;

        settingsEditKey = key;
        pathInput.Set(UserResourceManager.ToUser(path));
        keyInput.Set(key);
        pathInput.SetBlocked(true);
        keyInput.SetBlocked(true);
        browseButton.OnClick = CancelSettingsEdit;
        browseButton.Label.text = T("CANCEL", "Cancel");
        addButton.Label.text = T("APPLY_SETTINGS", "Apply Settings");
        mipChainToggle.Set(textureValue.settings.MipChain);
        linearToggle.Set(textureValue.settings.Linear);
        SetStatus("EDITING_TEXTURE_SETTINGS", "Editing {0} texture settings.", UIColors.ObjectActive, key);
    }

    private static void CancelSettingsEdit() {
        if(busy) return;
        settingsEditKey = null;
        pathInput.Set(string.Empty);
        keyInput.Set(string.Empty);
        mipChainToggle.Set(false);
        linearToggle.Set(false);
        pathInput.SetBlocked(false);
        keyInput.SetBlocked(false);
        browseButton.OnClick = BeginBrowse;
        FinishBusy();
        browseButton.Label.text = T("BROWSE", "Browse");
    }

    private static void BuildList() {
        if(listContent == null) return;
        for(int i = listContent.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(listContent.GetChild(i).gameObject);
        }

        string query = searchInput?.Value?.Trim() ?? string.Empty;
        string[] keys;
        if(currentMode == ResourceMode.Images) {
            keys = UserResourceManager.Spr.Keys
                .Where(key => string.IsNullOrEmpty(query) || key.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        } else {
            keys = UserResourceManager.Fnt.Keys
                .Where(key => string.IsNullOrEmpty(query) || key.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if(keys.Length == 0) {
            string text = currentMode == ResourceMode.Images
                ? T("NO_IMAGES_YET", "No images yet. Add one above.")
                : T("NO_FONTS_YET", "No fonts yet. Add one above.");
            TextMeshProUGUI empty = CreateText(listContent, text, 18f, TextAlignmentOptions.Center);
            empty.color = new Color(1f, 1f, 1f, 0.45f);
            LayoutElement element = empty.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 100f;
            element.preferredHeight = 100f;
        } else {
            foreach(string key in keys) {
                if(currentMode == ResourceMode.Images) CreateCard(listContent, key);
                else CreateFontCard(listContent, key);
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
    }

    private static void ToggleMode() {
        currentMode = currentMode == ResourceMode.Images ? ResourceMode.Fonts : ResourceMode.Images;
        titleLabel.text = T(
            currentMode == ResourceMode.Images ? "IMAGE_RESOURCES" : "FONT_RESOURCES",
            currentMode == ResourceMode.Images ? "Image Resources" : "Font Resources"
        );
        titleLabel.GetComponent<TextLocalization>()?.Init(
            currentMode == ResourceMode.Images ? "IMAGE_RESOURCES" : "FONT_RESOURCES",
            currentMode == ResourceMode.Images ? "Image Resources" : "Font Resources"
        );

        if(currentMode == ResourceMode.Images) {
            pathInput.Placeholder.GetComponent<TextLocalization>()?.Init("IMAGE_PATH", "Image path");
            pathInput.Rect.AddToolTip("IMAGE_PATH_TOOLTIP", "Select image file to import.");
            modeButton.Label.GetComponent<TextLocalization>()?.Init("RESOURCE_MODE_IMAGES", "Images");
            modeButton.Label.text = T("RESOURCE_MODE_IMAGES", "Images");
        } else {
            pathInput.Placeholder.GetComponent<TextLocalization>()?.Init("FONT_PATH", "Font path");
            pathInput.Rect.AddToolTip("FONT_PATH_TOOLTIP", "Select font file to import.");
            modeButton.Label.GetComponent<TextLocalization>()?.Init("RESOURCE_MODE_FONTS", "Fonts");
            modeButton.Label.text = T("RESOURCE_MODE_FONTS", "Fonts");
        }

        addButton.Label.GetComponent<TextLocalization>()?.Init(
            currentMode == ResourceMode.Images ? "ADD_IMAGE" : "ADD_FONT",
            currentMode == ResourceMode.Images ? "Add Image" : "Add Font"
        );
        addButton.Label.text = currentMode == ResourceMode.Images ? T("ADD_IMAGE", "Add Image") : T("ADD_FONT", "Add Font");

        if(imageSettingsRow != null) {
            imageSettingsRow.gameObject.SetActive(currentMode == ResourceMode.Images);
        }

        BuildList();
    }

    private static void ToggleUIStateByMod(bool isEnabled) {
        if(disabledPanel == null) return;
        disabledPanel.SetActive(!isEnabled);
        if(!isEnabled) {
            busy = false;
            CancelSettingsEdit();
            BuildList();
            Tooltip.Hide();
            return;
        }
        MainThread.Enqueue(() => {
            if(!MainCore.IsModEnabled) return;
            BuildList();
            Canvas.ForceUpdateCanvases();
            if(listContent) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        });
    }

    private static void CreateDisabledPanel(RectTransform parent) {
        disabledPanel = new GameObject("DisabledResourcesPanel");
        disabledPanel.transform.SetParent(parent, false);

        RectTransform rect = disabledPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = disabledPanel.AddComponent<Image>();
        image.color = UIColors.PanelBG;
        image.raycastTarget = true;

        TextMeshProUGUI message = CreateText(disabledPanel.transform, T("ONLY_AVAILABLE_WHEN_ENABLED", "Only available when the Mod is Enabled!"), 24f, TextAlignmentOptions.Center);
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

    private static void CreateCard(Transform parent, string key) {
        if(!UserResourceManager.Spr.TryGet(key, out var spriteValue)) return;
        GameObject cardObject = new(key);
        cardObject.transform.SetParent(parent, false);
        RectTransform card = cardObject.AddComponent<RectTransform>();
        LayoutElement cardElement = cardObject.AddComponent<LayoutElement>();
        cardElement.minHeight = 112f;
        cardElement.preferredHeight = 112f;
        Image background = cardObject.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        background.color = UIColors.ObjectBG;

        GameObject thumbnailObject = new("Thumbnail");
        thumbnailObject.transform.SetParent(card, false);
        RectTransform thumbnailRect = thumbnailObject.AddComponent<RectTransform>();
        thumbnailRect.anchorMin = new Vector2(0f, 0.5f);
        thumbnailRect.anchorMax = new Vector2(0f, 0.5f);
        thumbnailRect.pivot = new Vector2(0f, 0.5f);
        thumbnailRect.anchoredPosition = new Vector2(12f, 0f);
        thumbnailRect.sizeDelta = new Vector2(88f, 88f);
        Image thumbnail = thumbnailObject.AddComponent<Image>();
        thumbnail.sprite = spriteValue.sprite;
        thumbnail.preserveAspect = true;
        thumbnail.color = Color.white;
        thumbnail.raycastTarget = false;

        TextMeshProUGUI name = CreateText(card, key, 20f, TextAlignmentOptions.Left);
        name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.offsetMin = new Vector2(116f, -4f);
        name.rectTransform.offsetMax = new Vector2(-324f, -12f);
        name.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);

        UIInput renameInput = GenerateUI.Input(
            card,
            key,
            key,
            _ => { },
            T("RESOURCE_NAME", "Resource name"),
            null,
            "rename_" + key
        );
        renameInput.Rect.anchorMin = new Vector2(0f, 0.5f);
        renameInput.Rect.anchorMax = new Vector2(1f, 1f);
        renameInput.Rect.offsetMin = new Vector2(116f, -4f);
        renameInput.Rect.offsetMax = new Vector2(-324f, -12f);
        renameInput.Rect.gameObject.SetActive(false);

        string path = string.Empty;
        UserResourceManager.T2D.TryGetPath(key, out path);
        TextMeshProUGUI details = CreateText(
            card,
            $"{spriteValue.sprite.texture.width} × {spriteValue.sprite.texture.height}\n{UserResourceManager.ToUser(path)}",
            14f,
            TextAlignmentOptions.Left
        );
        details.color = new Color(1f, 1f, 1f, 0.5f);
        details.rectTransform.anchorMin = new Vector2(0f, 0f);
        details.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        details.rectTransform.offsetMin = new Vector2(116f, 14f);
        details.rectTransform.offsetMax = new Vector2(-324f, 0f);

        UIButton settings = GenerateUI.Button(card, () => EnterSettingsEdit(key), T("SETTINGS", "Settings"), "settings_" + key);
        PlaceRight(settings.Rect, 100f, 54f, 216f);
        settings.Label.fontSize = 15f;
        settings.Label.gameObject.AddComponent<TextLocalization>().Init("SETTINGS", "Settings");

        UIButton rename = GenerateUI.Button(card, () => { }, "Rename", "rename_button_" + key);
        PlaceRight(rename.Rect, 100f, 54f, 108f);
        rename.Label.fontSize = 15f;
        rename.Label.gameObject.AddComponent<TextLocalization>().Init("RENAME", "Rename");

        UIButton remove = GenerateUI.Button(card, () => { }, "Remove", "remove_" + key);
        PlaceRight(remove.Rect, 100f, 54f);
        remove.Label.fontSize = 15f;
        remove.Label.gameObject.AddComponent<TextLocalization>().Init("REMOVE", "Remove");
        bool confirm = false;
        bool editing = false;
        rename.OnClick = () => {
            if(!editing) {
                editing = true;
                confirm = false;
                name.gameObject.SetActive(false);
                renameInput.Set(key, false);
                renameInput.Rect.gameObject.SetActive(true);
                rename.Label.text = T("SAVE", "Save");
                rename.NormalColor = UIColors.ObjectActive;
                rename.UpdateVisual();
                remove.Label.text = T("CANCEL", "Cancel");
                remove.NormalColor = UIColors.ObjectButton;
                remove.UpdateVisual();
                renameInput.InputField.Select();
                renameInput.InputField.ActivateInputField();
                return;
            }

            if(Rename(key, renameInput.Value)) {
                SetStatus("RENAMED_RESOURCE", "Renamed {0}.", UIColors.ObjectActiveMathOk, key);
                BuildList();
            }
        };
        remove.OnClick = () => {
            if(editing) {
                editing = false;
                renameInput.Rect.gameObject.SetActive(false);
                name.gameObject.SetActive(true);
                rename.Label.text = T("RENAME", "Rename");
                rename.NormalColor = UIColors.ObjectButton;
                rename.UpdateVisual();
                remove.Label.text = T("REMOVE", "Remove");
                return;
            }

            if(!confirm) {
                confirm = true;
                remove.Label.text = T("CONFIRM", "Confirm");
                remove.NormalColor = UIColors.SoftRed;
                remove.UpdateVisual();
                return;
            }
            Remove(key);
        };

        GenerateUI.AddOutlineHover(cardObject, cardObject.AddComponent<EventTrigger>());
    }

    private static void CreateFontCard(Transform parent, string key) {
        if(!UserResourceManager.Fnt.TryGet(key, out var fontAsset)) return;

        GameObject cardObject = new(key);
        cardObject.transform.SetParent(parent, false);
        RectTransform card = cardObject.AddComponent<RectTransform>();
        LayoutElement cardElement = cardObject.AddComponent<LayoutElement>();
        cardElement.minHeight = 112f;
        cardElement.preferredHeight = 112f;
        Image background = cardObject.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        background.color = UIColors.ObjectBG;

        TextMeshProUGUI sample = CreateText(card, T("SAMPLE_TEXT", "The quick brown fox jumps over the lazy dog"), 20f, TextAlignmentOptions.Left);
        sample.gameObject.AddComponent<TextLocalization>().Init("SAMPLE_TEXT", "The quick brown fox jumps over the lazy dog");
        sample.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sample.rectTransform.anchorMax = new Vector2(1f, 1f);
        sample.rectTransform.offsetMin = new Vector2(16f, -4f);
        sample.rectTransform.offsetMax = new Vector2(-324f, -12f);
        sample.font = fontAsset;

        TextMeshProUGUI name = CreateText(card, key, 20f, TextAlignmentOptions.Left);
        name.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        name.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        name.rectTransform.offsetMin = new Vector2(16f, 8f);
        name.rectTransform.offsetMax = new Vector2(-324f, -8f);
        name.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);

        UIInput renameInput = GenerateUI.Input(
            card,
            key,
            key,
            _ => { },
            T("RESOURCE_NAME", "Resource name"),
            null,
            "rename_" + key
        );
        renameInput.Rect.anchorMin = new Vector2(0f, 0.35f);
        renameInput.Rect.anchorMax = new Vector2(1f, 0.65f);
        renameInput.Rect.offsetMin = new Vector2(16f, 8f);
        renameInput.Rect.offsetMax = new Vector2(-324f, -8f);
        renameInput.Rect.gameObject.SetActive(false);

        string path = string.Empty;
        UserResourceManager.Fnt.TryGetPath(key, out path);
        TextMeshProUGUI details = CreateText(
            card,
            UserResourceManager.ToUser(path),
            14f,
            TextAlignmentOptions.Left
        );
        details.color = new Color(1f, 1f, 1f, 0.5f);
        details.rectTransform.anchorMin = new Vector2(0f, 0f);
        details.rectTransform.anchorMax = new Vector2(1f, 0.35f);
        details.rectTransform.offsetMin = new Vector2(16f, 8f);
        details.rectTransform.offsetMax = new Vector2(-324f, -4f);

        UIButton rename = GenerateUI.Button(card, () => { }, "Rename", "rename_button_" + key);
        PlaceRight(rename.Rect, 100f, 54f, 108f);
        rename.Label.fontSize = 15f;
        rename.Label.gameObject.AddComponent<TextLocalization>().Init("RENAME", "Rename");

        UIButton remove = GenerateUI.Button(card, () => { }, "Remove", "remove_" + key);
        PlaceRight(remove.Rect, 100f, 54f);
        remove.Label.fontSize = 15f;
        remove.Label.gameObject.AddComponent<TextLocalization>().Init("REMOVE", "Remove");
        bool confirm = false;
        bool editing = false;
        rename.OnClick = () => {
            if(!editing) {
                editing = true;
                confirm = false;
                name.gameObject.SetActive(false);
                renameInput.Set(key, false);
                renameInput.Rect.gameObject.SetActive(true);
                rename.Label.text = T("SAVE", "Save");
                rename.NormalColor = UIColors.ObjectActive;
                rename.UpdateVisual();
                remove.Label.text = T("CANCEL", "Cancel");
                remove.NormalColor = UIColors.ObjectButton;
                remove.UpdateVisual();
                renameInput.InputField.Select();
                renameInput.InputField.ActivateInputField();
                return;
            }

            string newKey = SanitizeKey(renameInput.Value);
            if(string.IsNullOrWhiteSpace(newKey)) {
                SetStatus("ENTER_RESOURCE_NAME", "Enter a resource name.", UIColors.ObjectActiveMathErr);
                return;
            }
            if(string.Equals(key, newKey, StringComparison.Ordinal)) {
                SetStatus("NAME_UNCHANGED", "Name unchanged.", UIColors.ObjectActive);
                return;
            }
            if(UserResourceManager.Fnt.Keys.Contains(newKey)) {
                SetStatus("RESOURCE_NAME_ALREADY_EXISTS", "Resource name already exists.", UIColors.ObjectActiveMathErr);
                return;
            }

            if(!UserResourceManager.Fnt.TryRenameKey(key, newKey)) {
                SetStatus("RESOURCE_RENAME_FAILED", "Resource rename failed.", UIColors.ObjectActiveMathErr);
                return;
            }

            UserResourceManager.Config.RequestSave(50);
            SetStatus("RENAMED_RESOURCE_TO", "Renamed {0} to {1}.", UIColors.ObjectActiveMathOk, key, newKey);
            BuildList();
        };
        remove.OnClick = () => {
            if(editing) {
                editing = false;
                renameInput.Rect.gameObject.SetActive(false);
                name.gameObject.SetActive(true);
                rename.Label.text = T("RENAME", "Rename");
                rename.NormalColor = UIColors.ObjectButton;
                rename.UpdateVisual();
                remove.Label.text = T("REMOVE", "Remove");
                return;
            }

            if(!confirm) {
                confirm = true;
                remove.Label.text = T("CONFIRM", "Confirm");
                remove.NormalColor = UIColors.SoftRed;
                remove.UpdateVisual();
                return;
            }

            string filePath = path;
            UserResourceManager.Fnt.Remove(key);
            UserResourceManager.Config.RequestSave(50);
            BuildList();
            SetStatus("RESOURCE_REMOVED", "Removed {0}.", UIColors.ObjectActiveMathOk, key);

            if(!string.IsNullOrEmpty(filePath) && filePath.StartsWith(MainCore.Paths.UserFontPath, StringComparison.OrdinalIgnoreCase)) {
                _ = Task.Run(() => {
                    try { if(File.Exists(filePath)) File.Delete(filePath); } catch { }
                });
            }
        };

        GenerateUI.AddOutlineHover(cardObject, cardObject.AddComponent<EventTrigger>());
    }

    private static bool Rename(string oldKey, string value) {
        string newKey = SanitizeKey(value);
        if(string.IsNullOrWhiteSpace(newKey)) {
            SetStatus("ENTER_RESOURCE_NAME", "Enter a resource name.", UIColors.ObjectActiveMathErr);
            return false;
        }
        if(string.Equals(oldKey, newKey, StringComparison.Ordinal)) {
            SetStatus("NAME_UNCHANGED", "Name unchanged.", UIColors.ObjectActive);
            return false;
        }
        if(
            UserResourceManager.T2D.Keys.Contains(newKey) ||
            UserResourceManager.Spr.Keys.Contains(newKey)
        ) {
            SetStatus("RESOURCE_NAME_ALREADY_EXISTS", "Resource name already exists.", UIColors.ObjectActiveMathErr);
            return false;
        }

        bool hasTexture = UserResourceManager.T2D.Keys.Contains(oldKey);
        bool hasSprite = UserResourceManager.Spr.Keys.Contains(oldKey);
        bool textureRenamed = !hasTexture ||
            UserResourceManager.T2D.TryRenameKey(oldKey, newKey);
        bool spriteRenamed = !hasSprite ||
            UserResourceManager.Spr.TryRenameKey(oldKey, newKey);

        if(!textureRenamed || !spriteRenamed) {
            if(hasTexture && textureRenamed) {
                UserResourceManager.T2D.TryRenameKey(newKey, oldKey);
            }
            SetStatus("RESOURCE_RENAME_FAILED", "Resource rename failed.", UIColors.ObjectActiveMathErr);
            return false;
        }

        if(hasTexture) {
            UserResourceManager.Spr.RenameTextureKey(oldKey, newKey);
        }
        if(!hasTexture && !hasSprite) {
            SetStatus("RESOURCE_NOT_FOUND", "Resource not found.", UIColors.ObjectActiveMathErr);
            return false;
        }

        UserResourceManager.Config.RequestSave(50);
        return true;
    }

    private static void Remove(string key) {
        string path = string.Empty;
        UserResourceManager.T2D.TryGetPath(key, out path);
        UserResourceManager.Spr.Remove(key);
        bool textureUsedElsewhere = UserResourceManager.Spr.TryGetKey(
            value => string.Equals(value.textureKey, key, StringComparison.Ordinal),
            out _
        );
        if(!textureUsedElsewhere) {
            UserResourceManager.T2D.Remove(key);
        }
        UserResourceManager.Config.RequestSave(50);
        BuildList();
        SetStatus("RESOURCE_REMOVED", "Removed {0}.", UIColors.ObjectActiveMathOk, key);

        if(path.StartsWith(MainCore.Paths.UserImagePath, StringComparison.OrdinalIgnoreCase)) {
            _ = Task.Run(() => {
                try { if(File.Exists(path)) File.Delete(path); } catch { }
            });
        }
    }

    private static string SanitizeKey(string value) {
        if(string.IsNullOrWhiteSpace(value)) return string.Empty;
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = new(value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return result.Trim().Trim('.', ' ');
    }

    private static string T(string key, string defaultValue, params object[] args) {
        return string.Format(MainCore.Tr.Get(key, defaultValue), args);
    }

    private static void SetStatus(string key, string defaultValue, Color color, params object[] args) {
        SetStatus(T(key, defaultValue, args), color);
    }

    private static void SetStatus(string text, Color color) {
        if(statusLabel == null) return;
        statusLabel.text = text;
        statusLabel.color = color;
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

    private static RectTransform CreateContainer(Transform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        return rect;
    }

    private static RectTransform CreateRow(Transform parent, float top) {
        GameObject obj = new("Row");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(12f, -top - 50f);
        rect.offsetMax = new Vector2(-12f, -top);
        return rect;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions alignment) {
        TextMeshProUGUI label = GenerateUI.AddText(parent, true);
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
        return label;
    }

    private static void ResizeInput(RectTransform rect, float rightWidth) {
        rect.offsetMax = new Vector2(-rightWidth, 0f);
    }

    private static void PlaceRight(RectTransform rect, float width, float height = 0f, float rightOffset = 0f) {
        rect.anchorMin = new Vector2(1f, height > 0f ? 0.5f : 0f);
        rect.anchorMax = new Vector2(1f, height > 0f ? 0.5f : 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-rightOffset - width, height > 0f ? -height * 0.5f : 0f);
        rect.offsetMax = new Vector2(-rightOffset, 0f);
    }

    private static void PlaceHalf(RectTransform rect, bool right) {
        rect.anchorMin = new Vector2(right ? 0.5f : 0f, 0f);
        rect.anchorMax = new Vector2(right ? 1f : 0.5f, 1f);
        rect.offsetMin = new Vector2(right ? 4f : 0f, 0f);
        rect.offsetMax = new Vector2(right ? 0f : -4f, 0f);
    }
}
