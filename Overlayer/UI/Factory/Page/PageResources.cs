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
    private static UIInput searchInput;
    private static UIToggle mipChainToggle;
    private static UIToggle linearToggle;
    private static UIButton browseButton;
    private static UIButton addButton;
    private static TextMeshProUGUI statusLabel;
    private static RectTransform listContent;
    private static RectTransform listViewport;
    private static string settingsEditKey;
    private static GameObject disabledPanel;
    private static bool busy;

    public static void Create(RectTransform parent) {
        RectTransform root = CreateStretch(parent, "ResourcesRoot");

        TextMeshProUGUI title = CreateText(root, "Image Resources", 30f, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(24f, -62f);
        title.rectTransform.offsetMax = new Vector2(-24f, -12f);
        title.raycastTarget = true;
        title.gameObject.AddComponent<TextLocalization>().Init("IMAGE_RESOURCES", "Image Resources");
        title.transform.AddToolTip(
            "IMAGE_RESOURCES_DESC",
            "Add images once, then use them from Image components."
        );

        RectTransform importCard = CreateContainer(root, "ImportRows");
        importCard.anchorMin = new Vector2(0f, 1f);
        importCard.anchorMax = new Vector2(1f, 1f);
        importCard.offsetMin = new Vector2(18f, -260f);
        importCard.offsetMax = new Vector2(-18f, -78f);

        RectTransform pathRow = CreateRow(importCard, 8f);
        pathInput = GenerateUI.Input(
            pathRow,
            string.Empty,
            string.Empty,
            _ => { },
            "Image path",
            null,
            "resource_image_path"
        );
        pathInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("IMAGE_PATH", "Image path");
        pathInput.Rect.AddToolTip(
            "IMAGE_PATH_TOOLTIP",
            "Select image file to import."
        );
        ResizeInput(pathInput.Rect, 190f);

        browseButton = GenerateUI.Button(pathRow, BeginBrowse, "Browse", "resource_browse");
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

        addButton = GenerateUI.Button(keyRow, BeginImport, "Add Image", "resource_add");
        PlaceRight(addButton.Rect, 174f);
        addButton.Label.gameObject.AddComponent<TextLocalization>().Init("ADD_IMAGE", "Add Image");

        RectTransform settingsRow = CreateRow(importCard, 124f);
        mipChainToggle = GenerateUI.Toggle(
            settingsRow,
            false,
            false,
            _ => { },
            "Mip Chain",
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
            "Linear",
            "resource_linear"
        );
        PlaceHalf(linearToggle.Rect, true);
        linearToggle.Rect.AddToolTip(
            "LINEAR_TOOLTIP",
            "Load texture data as linear color instead of sRGB."
        );
        linearToggle.Label.gameObject.AddComponent<TextLocalization>().Init("LINEAR", "Linear");

        statusLabel = CreateText(root, "Ready", 15f, TextAlignmentOptions.Left);
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
            "Search images",
            MainCore.Spr.Get(UISprite.MagnifyingGlass128),
            "resource_search"
        );
        searchInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("SEARCH_IMAGES", "Search images");
        searchInput.Rect.AddToolTip(
            "SEARCH_IMAGES_TOOLTIP",
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
        SetStatus("Opening file picker...", UIColors.ObjectActive);

        _ = NativeImageFilePicker.PickAsync().ContinueWith(task => {
            MainThread.Enqueue(() => {
                if(!MainCore.IsModEnabled) return;
                browseButton.SetBlocked(false);
                browseButton.Label.text = "Browse";
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if(string.IsNullOrWhiteSpace(path)) {
                    SetStatus("No image selected.", UIColors.ObjectActiveMathWarn);
                    return;
                }

                pathInput.Set(path);
                if(string.IsNullOrWhiteSpace(keyInput.Value)) {
                    keyInput.Set(Path.GetFileNameWithoutExtension(path));
                }
                SetStatus("Image selected. Choose Add Image.", UIColors.ObjectActive);
            });
        });
    }

    private static void BeginImport() {
        if(busy) return;
        if(!string.IsNullOrEmpty(settingsEditKey)) {
            BeginSettingsApply();
            return;
        }
        string source = UserResourceManager.FromUser(pathInput.Value?.Trim());
        string key = SanitizeKey(keyInput.Value);

        if(string.IsNullOrWhiteSpace(source)) {
            SetStatus("Choose an image first.", UIColors.ObjectActiveMathErr);
            return;
        }
        if(string.IsNullOrWhiteSpace(key)) {
            SetStatus("Enter a resource name.", UIColors.ObjectActiveMathErr);
            return;
        }
        if(UserResourceManager.T2D.Keys.Contains(key) || UserResourceManager.Spr.Keys.Contains(key)) {
            SetStatus("Resource name already exists.", UIColors.ObjectActiveMathErr);
            return;
        }
        if(!UserTexture2D.Ext.Contains(Path.GetExtension(source).ToLowerInvariant())) {
            SetStatus("Unsupported image format.", UIColors.ObjectActiveMathErr);
            return;
        }

        busy = true;
        pathInput.SetBlocked(true);
        keyInput.SetBlocked(true);
        browseButton.SetBlocked(true);
        addButton.SetBlocked(true);
        addButton.Label.text = "Loading...";
        SetStatus("Reading image...", UIColors.ObjectActive);

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
    }

    private static void FinishImport(
        Task<(byte[] Bytes, string Path, string Error)> task,
        string key
    ) {
        if(!MainCore.IsModEnabled) return;
        var result = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : (null, string.Empty, "Image read task failed.");
        if(result.Bytes == null) {
            FinishBusy();
            SetStatus($"Import failed: {result.Error}", UIColors.ObjectActiveMathErr);
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
            SetStatus($"Image load failed: {textureResult}", UIColors.ObjectActiveMathErr);
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
            SetStatus($"Sprite creation failed: {spriteResult}", UIColors.ObjectActiveMathErr);
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
        addButton.Label.text = "Add Image";
    }

    private static void BeginSettingsApply() {
        string key = settingsEditKey;
        if(
            string.IsNullOrEmpty(key) ||
            !UserResourceManager.T2D.TryGetPath(key, out string path) ||
            !File.Exists(path)
        ) {
            SetStatus("Image file not found.", UIColors.ObjectActiveMathErr);
            return;
        }

        bool mipChain = mipChainToggle.Value;
        bool linear = linearToggle.Value;
        if(
            UserResourceManager.T2D.TryGet(key, out var current) &&
            current.settings.MipChain == mipChain &&
            current.settings.Linear == linear
        ) {
            SetStatus("Settings unchanged.", UIColors.ObjectActive);
            CancelSettingsEdit();
            return;
        }

        busy = true;
        browseButton.SetBlocked(true);
        addButton.SetBlocked(true);
        addButton.Label.text = "Applying...";
        SetStatus("Reading image...", UIColors.ObjectActive);

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
            : (null, "Image read task failed.");
        if(result.Bytes == null) {
            FinishSettingsBusy();
            SetStatus($"Settings failed: {result.Error}", UIColors.ObjectActiveMathErr);
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
            SetStatus($"Settings failed: {textureResult}", UIColors.ObjectActiveMathErr);
            return;
        }

        if(
            UserResourceManager.Spr.TryGet(key, out _) &&
            !UserResourceManager.Spr.RebuildTexture(key, textureValue.texture)
        ) {
            FinishSettingsBusy();
            SetStatus("Sprite rebuild failed.", UIColors.ObjectActiveMathErr);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        busy = false;
        CancelSettingsEdit();
        BuildList();
        SetStatus("Texture settings applied.", UIColors.ObjectActiveMathOk);
    }

    private static void FinishSettingsBusy() {
        busy = false;
        browseButton.SetBlocked(false);
        addButton.SetBlocked(false);
        addButton.Label.text = "Apply Settings";
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
        browseButton.Label.text = "Cancel";
        addButton.Label.text = "Apply Settings";
        mipChainToggle.Set(textureValue.settings.MipChain);
        linearToggle.Set(textureValue.settings.Linear);
        SetStatus($"Editing {key} texture settings.", UIColors.ObjectActive);
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
        browseButton.Label.text = "Browse";
    }

    private static void BuildList() {
        if(listContent == null) return;
        for(int i = listContent.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(listContent.GetChild(i).gameObject);
        }

        string query = searchInput?.Value?.Trim() ?? string.Empty;
        string[] keys = UserResourceManager.Spr.Keys
            .Where(key => string.IsNullOrEmpty(query) || key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if(keys.Length == 0) {
            TextMeshProUGUI empty = CreateText(listContent, "No images yet. Add one above.", 18f, TextAlignmentOptions.Center);
            empty.color = new Color(1f, 1f, 1f, 0.45f);
            LayoutElement element = empty.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 100f;
            element.preferredHeight = 100f;
        } else {
            foreach(string key in keys) CreateCard(listContent, key);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
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

        TextMeshProUGUI message = CreateText(disabledPanel.transform, "Only available when the Mod is Enabled!", 24f, TextAlignmentOptions.Center);
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
            "Resource name",
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

        UIButton settings = GenerateUI.Button(card, () => EnterSettingsEdit(key), "Settings", "settings_" + key);
        PlaceRight(settings.Rect, 100f, 54f, 216f);
        settings.Label.fontSize = 15f;
        settings.Label.gameObject.AddComponent<TextLocalization>().Init("SETTINGS", "Settings");

        UIButton rename = GenerateUI.Button(card, () => { }, "Rename", "rename_button_" + key);
        PlaceRight(rename.Rect, 100f, 54f, 108f);
        rename.Label.fontSize = 15f;

        UIButton remove = GenerateUI.Button(card, () => { }, "Remove", "remove_" + key);
        PlaceRight(remove.Rect, 100f, 54f);
        remove.Label.fontSize = 15f;
        bool confirm = false;
        bool editing = false;
        rename.OnClick = () => {
            if(!editing) {
                editing = true;
                confirm = false;
                name.gameObject.SetActive(false);
                renameInput.Set(key, false);
                renameInput.Rect.gameObject.SetActive(true);
                rename.Label.text = "Save";
                rename.NormalColor = UIColors.ObjectActive;
                rename.UpdateVisual();
                remove.Label.text = "Cancel";
                remove.NormalColor = UIColors.ObjectButton;
                remove.UpdateVisual();
                renameInput.InputField.Select();
                renameInput.InputField.ActivateInputField();
                return;
            }

            if(Rename(key, renameInput.Value)) {
                SetStatus($"Renamed {key}.", UIColors.ObjectActiveMathOk);
                BuildList();
            }
        };
        remove.OnClick = () => {
            if(editing) {
                editing = false;
                renameInput.Rect.gameObject.SetActive(false);
                name.gameObject.SetActive(true);
                rename.Label.text = "Rename";
                rename.NormalColor = UIColors.ObjectButton;
                rename.UpdateVisual();
                remove.Label.text = "Remove";
                return;
            }

            if(!confirm) {
                confirm = true;
                remove.Label.text = "Confirm";
                remove.NormalColor = UIColors.SoftRed;
                remove.UpdateVisual();
                return;
            }
            Remove(key);
        };

        GenerateUI.AddOutlineHover(cardObject, cardObject.AddComponent<EventTrigger>());
    }

    private static bool Rename(string oldKey, string value) {
        string newKey = SanitizeKey(value);
        if(string.IsNullOrWhiteSpace(newKey)) {
            SetStatus("Enter a resource name.", UIColors.ObjectActiveMathErr);
            return false;
        }
        if(string.Equals(oldKey, newKey, StringComparison.Ordinal)) {
            SetStatus("Name unchanged.", UIColors.ObjectActive);
            return false;
        }
        if(
            UserResourceManager.T2D.Keys.Contains(newKey) ||
            UserResourceManager.Spr.Keys.Contains(newKey)
        ) {
            SetStatus("Resource name already exists.", UIColors.ObjectActiveMathErr);
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
            SetStatus("Resource rename failed.", UIColors.ObjectActiveMathErr);
            return false;
        }

        if(hasTexture) {
            UserResourceManager.Spr.RenameTextureKey(oldKey, newKey);
        }
        if(!hasTexture && !hasSprite) {
            SetStatus("Resource not found.", UIColors.ObjectActiveMathErr);
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
        SetStatus($"Removed {key}.", UIColors.ObjectActiveMathOk);

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
