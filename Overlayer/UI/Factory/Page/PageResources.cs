using Overlayer.Async;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.IO.User.Impl;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
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
    private static RectTransform spriteEditorPanel;
    private static GameObject spriteEditorBlocker;
    private static TextMeshProUGUI spriteEditorTitle;
    private static RectTransform spriteEditorPreview;
    private static RectTransform spriteEditorGuideOverlay;
    private static RectTransform spriteEditorFieldRow;
    private static RawImage spriteEditorImage;
    private static TextMeshProUGUI spriteEditorHint;
    private static UISlider spriteEditorLeftInput;
    private static UISlider spriteEditorRightInput;
    private static UISlider spriteEditorBottomInput;
    private static UISlider spriteEditorTopInput;
    private static RectTransform spriteEditorLeftGuide;
    private static RectTransform spriteEditorRightGuide;
    private static RectTransform spriteEditorBottomGuide;
    private static RectTransform spriteEditorTopGuide;
    private static Vector4 spriteEditorBorder;
    private static Texture2D spriteEditorTexture;
    private static UIButton browseButton;
    private static UIButton addButton;
    private static TextMeshProUGUI statusLabel;
    private static TextMeshProUGUI titleLabel;
    private static RectTransform listContent;
    private static RectTransform listViewport;
    private static string settingsEditKey;
    private static string spriteEditorKey;
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
        layout.padding = new RectOffset { left = 0, right = 0, top = 0, bottom = 12 };
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        root.gameObject.AddComponent<UIScrollController>().SetContent(listContent, listViewport);
        CreateSpriteEditor(UICore.CanvasObj.transform);

        CreateDisabledPanel(root);
        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) {
                ToggleUIStateByMod(isEnabled);
            }
        };
        ToggleUIStateByMod(MainCore.IsModEnabled);

        BuildList();
        MainThread.Enqueue(() => {
            BuildList();
            Canvas.ForceUpdateCanvases();
            if(listContent) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            }
        });
    }

    private static void BeginBrowse() {
        if(busy) {
            return;
        }

        browseButton.SetBlocked(true);
        browseButton.Label.text = "...";
        SetStatus("OPENING_FILE_PICKER", "Opening file picker...", UIColors.ObjectActive);

        if(currentMode == ResourceMode.Images) {
            _ = NativeImageFilePicker.PickAsync().ContinueWith(task => {
                MainThread.Enqueue(() => {
                    if(!MainCore.IsModEnabled) {
                        return;
                    }

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
                    if(!MainCore.IsModEnabled) {
                        return;
                    }

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
        if(busy) {
            return;
        }

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
            }).ContinueWith(task => MainThread.Enqueue(() => FinishImport(task, key)));
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
            }).ContinueWith(task => MainThread.Enqueue(() => FinishFontImport(task, key)));
        }
    }

    private static void FinishImport(
        Task<(byte[] Bytes, string Path, string Error)> task,
        string key
    ) {
        if(!MainCore.IsModEnabled) {
            return;
        }

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
        if(!MainCore.IsModEnabled) {
            return;
        }

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

    private enum SpriteGuide { Left, Right, Bottom, Top }

    private static RectTransform CreateSpriteEditor(Transform parent) {
        spriteEditorBlocker = new GameObject("SpriteEditorBlocker");
        spriteEditorBlocker.transform.SetParent(parent, false);
        RectTransform blockerRect = spriteEditorBlocker.AddComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        Image blockerImage = spriteEditorBlocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.58f);
        blockerImage.raycastTarget = true;
        GenerateUI.AddButton(spriteEditorBlocker, button => {
            if(button == PointerEventData.InputButton.Left) {
                CloseSpriteEditor();
            }
        });

        GameObject panelObject = new("SpriteEditor");
        panelObject.transform.SetParent(parent, false);
        spriteEditorPanel = panelObject.AddComponent<RectTransform>();
        spriteEditorPanel.anchorMin = new Vector2(0.5f, 0.5f);
        spriteEditorPanel.anchorMax = new Vector2(0.5f, 0.5f);
        spriteEditorPanel.pivot = new Vector2(0.5f, 0.5f);
        spriteEditorPanel.sizeDelta = new Vector2(760f, 580f);

        Image background = panelObject.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        background.color = UIColors.PanelBG;

        GameObject topBarObject = new("TopBar");
        topBarObject.transform.SetParent(panelObject.transform, false);
        topBarObject.AddComponent<DragHandler>();
        Image topBarImage = topBarObject.AddComponent<Image>();
        topBarImage.color = UIColors.TopBar;
        topBarImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleHalf256P1024);
        topBarImage.type = Image.Type.Sliced;
        RectTransform topBar = topBarObject.GetComponent<RectTransform>();
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.offsetMin = new Vector2(0f, -60f);
        topBar.offsetMax = Vector2.zero;
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.anchoredPosition = Vector2.zero;
        topBar.sizeDelta = new Vector2(0f, 60f);

        spriteEditorTitle = CreateText(topBar, T("SPRITE_EDITOR", "Sprite Editor"), 22f, TextAlignmentOptions.Left);
        spriteEditorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        spriteEditorTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        spriteEditorTitle.rectTransform.offsetMin = new Vector2(22f, -56f);
        spriteEditorTitle.rectTransform.offsetMax = new Vector2(-70f, -4f);

        CreateSpriteEditorClose(topBar);

        GameObject workspaceObject = new("Workspace");
        workspaceObject.transform.SetParent(panelObject.transform, false);
        RectTransform workspace = workspaceObject.AddComponent<RectTransform>();
        workspace.anchorMin = Vector2.zero;
        workspace.anchorMax = Vector2.one;
        workspace.offsetMin = new Vector2(22f, 184f);
        workspace.offsetMax = new Vector2(-22f, -66f);
        Image workspaceBackground = workspaceObject.AddComponent<Image>();
        workspaceBackground.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        workspaceBackground.type = Image.Type.Sliced;
        workspaceBackground.color = UIColors.ObjectBG;
        workspaceBackground.raycastTarget = true;

        GameObject previewObject = new("Preview");
        previewObject.transform.SetParent(workspace, false);
        spriteEditorPreview = previewObject.AddComponent<RectTransform>();
        spriteEditorPreview.anchorMin = new Vector2(0.5f, 0.5f);
        spriteEditorPreview.anchorMax = new Vector2(0.5f, 0.5f);
        spriteEditorPreview.pivot = new Vector2(0.5f, 0.5f);
        spriteEditorPreview.sizeDelta = new Vector2(520f, 340f);

        spriteEditorImage = previewObject.AddComponent<RawImage>();
        spriteEditorImage.color = Color.white;
        spriteEditorImage.raycastTarget = false;

        GameObject guideOverlayObject = new("GuideOverlay");
        guideOverlayObject.transform.SetParent(workspace, false);
        spriteEditorGuideOverlay = guideOverlayObject.AddComponent<RectTransform>();
        spriteEditorGuideOverlay.anchorMin = new Vector2(0.5f, 0.5f);
        spriteEditorGuideOverlay.anchorMax = new Vector2(0.5f, 0.5f);
        spriteEditorGuideOverlay.pivot = new Vector2(0.5f, 0.5f);
        spriteEditorGuideOverlay.sizeDelta = spriteEditorPreview.sizeDelta;

        spriteEditorLeftGuide = CreateSpriteGuide(spriteEditorGuideOverlay, SpriteGuide.Left);
        spriteEditorRightGuide = CreateSpriteGuide(spriteEditorGuideOverlay, SpriteGuide.Right);
        spriteEditorBottomGuide = CreateSpriteGuide(spriteEditorGuideOverlay, SpriteGuide.Bottom);
        spriteEditorTopGuide = CreateSpriteGuide(spriteEditorGuideOverlay, SpriteGuide.Top);

        GameObject fieldRowObject = new("BorderFields");
        fieldRowObject.transform.SetParent(panelObject.transform, false);
        spriteEditorFieldRow = fieldRowObject.AddComponent<RectTransform>();
        spriteEditorFieldRow.anchorMin = Vector2.zero;
        spriteEditorFieldRow.anchorMax = new Vector2(1f, 0f);
        spriteEditorFieldRow.offsetMin = new Vector2(22f, 90f);
        spriteEditorFieldRow.offsetMax = new Vector2(-22f, 176f);

        spriteEditorHint = CreateText(
            panelObject.transform,
            T("SPRITE_EDITOR_HINT", "Drag the green guides to set 9-slice borders."),
            15f,
            TextAlignmentOptions.Center
        );
        spriteEditorHint.rectTransform.anchorMin = new Vector2(0f, 0f);
        spriteEditorHint.rectTransform.anchorMax = new Vector2(1f, 0f);
        spriteEditorHint.rectTransform.pivot = new Vector2(0.5f, 0f);
        spriteEditorHint.rectTransform.offsetMin = new Vector2(22f, 60f);
        spriteEditorHint.rectTransform.offsetMax = new Vector2(-22f, 82f);
        spriteEditorHint.color = new Color(1f, 1f, 1f, 0.65f);

        UIButton cancel = GenerateUI.Button(panelObject.transform, CloseSpriteEditor, T("CANCEL", "Cancel"), "sprite_editor_cancel");
        cancel.Rect.anchorMin = new Vector2(1f, 0f);
        cancel.Rect.anchorMax = new Vector2(1f, 0f);
        cancel.Rect.pivot = new Vector2(1f, 0f);
        cancel.Rect.anchoredPosition = new Vector2(-152f, 12f);
        cancel.Rect.sizeDelta = new Vector2(130f, 42f);
        cancel.Label.gameObject.AddComponent<TextLocalization>().Init("CANCEL", "Cancel");

        UIButton applyButton = GenerateUI.Button(panelObject.transform, BeginSpriteEditorApply, T("APPLY_SETTINGS", "Apply Settings"), "sprite_editor_apply");
        applyButton.Rect.anchorMin = new Vector2(1f, 0f);
        applyButton.Rect.anchorMax = new Vector2(1f, 0f);
        applyButton.Rect.pivot = new Vector2(1f, 0f);
        applyButton.Rect.anchoredPosition = new Vector2(-12f, 12f);
        applyButton.Rect.sizeDelta = new Vector2(130f, 42f);
        applyButton.Label.gameObject.AddComponent<TextLocalization>().Init("APPLY_SETTINGS", "Apply Settings");

        topBarObject.transform.SetAsLastSibling();

        GameObject outlineObject = new("Outline");
        outlineObject.transform.SetParent(panelObject.transform, false);
        outlineObject.transform.SetAsLastSibling();
        Image outlineImage = outlineObject.AddComponent<Image>();
        outlineImage.color = Color.white;
        outlineImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256O32P1024);
        outlineImage.type = Image.Type.Sliced;
        outlineImage.raycastTarget = false;
        RectTransform outlineRect = outlineObject.GetComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;

        SetSpriteEditor(null, Vector4.zero);
        spriteEditorPanel.gameObject.SetActive(false);
        spriteEditorBlocker.SetActive(false);
        return spriteEditorPanel;
    }

    private static void CreateSpriteEditorClose(Transform parent) {
        GameObject closeObject = new("Close");
        closeObject.transform.SetParent(parent, false);
        RectTransform closeRect = closeObject.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-16f, 0f);
        closeRect.sizeDelta = new Vector2(38f, 38f);

        Image hoverImage = new GameObject("Hover").AddComponent<Image>();
        hoverImage.transform.SetParent(closeObject.transform, false);
        hoverImage.sprite = MainCore.Spr.Get(UISprite.Circle256);
        hoverImage.color = new Color(UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 0f);
        hoverImage.raycastTarget = true;
        RectTransform hoverRect = hoverImage.rectTransform;
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;

        Image xImage = new GameObject("X").AddComponent<Image>();
        xImage.transform.SetParent(closeObject.transform, false);
        xImage.sprite = MainCore.Spr.Get(UISprite.X128);
        xImage.raycastTarget = false;
        RectTransform xRect = xImage.rectTransform;
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.offsetMin = new Vector2(4f, 4f);
        xRect.offsetMax = new Vector2(-4f, -4f);

        GenerateUI.AddButton(closeObject, button => {
            if(button == PointerEventData.InputButton.Left) {
                CloseSpriteEditor();
            }
        });

        EventTrigger trigger = closeObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvents(
            trigger,
            (EventTriggerType.PointerEnter, _ => hoverImage.color = new Color(
                UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 1f
            )),
            (EventTriggerType.PointerExit, _ => hoverImage.color = new Color(
                UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 0f
            ))
        );
    }

    private static void ShowSpriteEditor(string key) {
        RectTransform canvasRect = UICore.CanvasObj.GetComponent<RectTransform>();
        spriteEditorPanel.sizeDelta = new Vector2(
            Mathf.Min(760f, canvasRect.rect.width - 32f),
            Mathf.Min(580f, canvasRect.rect.height - 32f)
        );
        spriteEditorBlocker.SetActive(true);
        spriteEditorPanel.gameObject.SetActive(true);
        spriteEditorBlocker.transform.SetAsLastSibling();
        spriteEditorPanel.SetAsLastSibling();
        spriteEditorTitle.text = $"{T("SPRITE_EDITOR", "Sprite Editor")} — {key}";
        Canvas.ForceUpdateCanvases();
    }

    private static void HideSpriteEditor() {
        spriteEditorPanel?.gameObject.SetActive(false);
        spriteEditorBlocker?.SetActive(false);
    }

    private static void OpenSpriteEditor(string key) {
        if(busy || !UserResourceManager.T2D.TryGet(key, out var textureValue)) {
            return;
        }

        spriteEditorKey = key;
        Vector4 border = UserResourceManager.Spr.TryGet(key, out var spriteValue)
            ? spriteValue.settings.Border
            : Vector4.zero;
        RebuildSpriteBorderSliders(textureValue.texture.width, textureValue.texture.height);
        ShowSpriteEditor(key);
        SetSpriteEditor(textureValue.texture, border);
    }

    private static void CloseSpriteEditor() {
        HideSpriteEditor();
        spriteEditorKey = null;
        SetSpriteEditor(null, Vector4.zero);
    }

    private static void BeginSpriteEditorApply() {
        string key = spriteEditorKey;
        if(
            busy ||
            string.IsNullOrEmpty(key) ||
            !UserResourceManager.T2D.TryGet(key, out var textureValue) ||
            !UserResourceManager.Spr.TryGet(key, out var spriteValue)
        ) {
            return;
        }

        Vector4 border = NormalizeBorder(
            ReadBorder(),
            textureValue.texture.width,
            textureValue.texture.height
        );
        if(Approximately(spriteValue.settings.Border, border)) {
            CloseSpriteEditor();
            SetStatus("SETTINGS_UNCHANGED", "Settings unchanged.", UIColors.ObjectActive);
            return;
        }

        if(!UserResourceManager.Spr.UpdateBorder(key, border)) {
            SetStatus("SPRITE_REBUILD_FAILED", "Sprite rebuild failed.", UIColors.ObjectActiveMathErr);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        OverlayCore.RequestLayoutRefresh();
        CloseSpriteEditor();
        BuildList();
        SetStatus("SPRITE_SETTINGS_APPLIED", "Sprite settings applied.", UIColors.ObjectActiveMathOk);
    }

    private static void RebuildSpriteBorderSliders(int width, int height) {
        DisposeSpriteBorderSlider(ref spriteEditorLeftInput);
        DisposeSpriteBorderSlider(ref spriteEditorRightInput);
        DisposeSpriteBorderSlider(ref spriteEditorBottomInput);
        DisposeSpriteBorderSlider(ref spriteEditorTopInput);

        spriteEditorLeftInput = CreateBorderSlider(
            spriteEditorFieldRow, T("SPRITE_BORDER_LEFT", "Left"), SpriteGuide.Left, false, true, width
        );
        spriteEditorRightInput = CreateBorderSlider(
            spriteEditorFieldRow, T("SPRITE_BORDER_RIGHT", "Right"), SpriteGuide.Right, true, true, width
        );
        spriteEditorBottomInput = CreateBorderSlider(
            spriteEditorFieldRow, T("SPRITE_BORDER_BOTTOM", "Bottom"), SpriteGuide.Bottom, false, false, height
        );
        spriteEditorTopInput = CreateBorderSlider(
            spriteEditorFieldRow, T("SPRITE_BORDER_TOP", "Top"), SpriteGuide.Top, true, false, height
        );
    }

    private static void DisposeSpriteBorderSlider(ref UISlider input) {
        if(input == null) {
            return;
        }

        RectTransform rect = input.Rect;
        input.Dispose();
        if(rect) {
            UnityEngine.Object.Destroy(rect.gameObject);
        }
        input = null;
    }

    private static UISlider CreateBorderSlider(
        Transform parent,
        string label,
        SpriteGuide guide,
        bool right,
        bool top,
        float max
    ) {
        UISlider input = GenerateUI.Slider(
            parent,
            0f,
            0f,
            max,
            0f,
            "F0",
            true,
            value => FilterBorderSlider(guide, value),
            value => SetBorderFromSlider(guide, value),
            null,
            label,
            $"sprite_border_{guide.ToString().ToLowerInvariant()}"
        );
        input.Rect.anchorMin = new Vector2(right ? 0.5f : 0f, top ? 0.5f : 0f);
        input.Rect.anchorMax = new Vector2(right ? 1f : 0.5f, top ? 1f : 0.5f);
        input.Rect.offsetMin = new Vector2(right ? 5f : 0f, top ? 3f : 0f);
        input.Rect.offsetMax = new Vector2(right ? 0f : -5f, top ? 0f : -3f);
        input.Label.fontSize = 16f;
        (string localizationKey, string fallback) = guide switch {
            SpriteGuide.Left => ("SPRITE_BORDER_LEFT", "Left"),
            SpriteGuide.Right => ("SPRITE_BORDER_RIGHT", "Right"),
            SpriteGuide.Bottom => ("SPRITE_BORDER_BOTTOM", "Bottom"),
            _ => ("SPRITE_BORDER_TOP", "Top")
        };
        input.Label.gameObject.AddComponent<TextLocalization>().Init(localizationKey, fallback);
        input.PreviewLabel.fontSize = 16f;
        input.InputCore.InputField.textComponent.fontSize = 16f;
        return input;
    }

    private static RectTransform CreateSpriteGuide(RectTransform parent, SpriteGuide guide) {
        GameObject guideObject = new(guide.ToString());
        guideObject.transform.SetParent(parent, false);
        RectTransform rect = guideObject.AddComponent<RectTransform>();
        bool vertical = guide is SpriteGuide.Left or SpriteGuide.Right;
        (rect.anchorMin, rect.anchorMax) = guide switch {
            SpriteGuide.Left => (new Vector2(0f, 0f), new Vector2(0f, 1f)),
            SpriteGuide.Right => (new Vector2(1f, 0f), new Vector2(1f, 1f)),
            SpriteGuide.Bottom => (new Vector2(0f, 0f), new Vector2(1f, 0f)),
            _ => (new Vector2(0f, 1f), new Vector2(1f, 1f))
        };
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = vertical ? new Vector2(24f, 0f) : new Vector2(0f, 24f);

        Image hitArea = guideObject.AddComponent<Image>();
        Color idleColor = new(0.15f, 1f, 0.25f, 0.12f);
        Color hoverColor = new(0.15f, 1f, 0.25f, 0.32f);
        hitArea.color = idleColor;
        hitArea.raycastTarget = true;

        CreateGuideLine(guideObject.transform, vertical, 6f, new Color(0f, 0f, 0f, 0.9f));
        CreateGuideLine(guideObject.transform, vertical, 3f, new Color(0.15f, 1f, 0.25f, 1f));

        GameObject handleObject = new("Handle");
        handleObject.transform.SetParent(guideObject.transform, false);
        RectTransform handle = handleObject.AddComponent<RectTransform>();
        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = vertical ? new Vector2(24f, 42f) : new Vector2(42f, 24f);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color(0f, 0f, 0f, 0.9f);
        handleImage.raycastTarget = false;

        GameObject handleFillObject = new("Fill");
        handleFillObject.transform.SetParent(handleObject.transform, false);
        RectTransform handleFill = handleFillObject.AddComponent<RectTransform>();
        handleFill.anchorMin = Vector2.zero;
        handleFill.anchorMax = Vector2.one;
        handleFill.offsetMin = new Vector2(3f, 3f);
        handleFill.offsetMax = new Vector2(-3f, -3f);
        Image handleFillImage = handleFillObject.AddComponent<Image>();
        handleFillImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        handleFillImage.type = Image.Type.Sliced;
        handleFillImage.color = new Color(0.15f, 1f, 0.25f, 1f);
        handleFillImage.raycastTarget = false;

        UnityUtils.AddEvents(
            guideObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => hitArea.color = hoverColor),
            (EventTriggerType.PointerExit, _ => hitArea.color = idleColor),
            (EventTriggerType.PointerDown, data => DragSpriteGuide(guide, data)),
            (EventTriggerType.BeginDrag, data => DragSpriteGuide(guide, data)),
            (EventTriggerType.Drag, data => DragSpriteGuide(guide, data))
        );
        return rect;
    }

    private static void CreateGuideLine(Transform parent, bool vertical, float thickness, Color color) {
        GameObject lineObject = new("Line");
        lineObject.transform.SetParent(parent, false);
        RectTransform line = lineObject.AddComponent<RectTransform>();
        line.anchorMin = vertical ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
        line.anchorMax = vertical ? new Vector2(0.5f, 1f) : new Vector2(1f, 0.5f);
        line.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
        Image lineImage = lineObject.AddComponent<Image>();
        lineImage.color = color;
        lineImage.raycastTarget = false;
    }

    private static Vector4 ReadBorder() => spriteEditorBorder;

    private static float FilterBorderSlider(SpriteGuide guide, float value) {
        value = Mathf.Round(value);
        if(spriteEditorTexture == null) {
            return Mathf.Max(0f, value);
        }

        return guide switch {
            SpriteGuide.Left => Mathf.Clamp(value, 0f, spriteEditorTexture.width - spriteEditorBorder.z),
            SpriteGuide.Right => Mathf.Clamp(value, 0f, spriteEditorTexture.width - spriteEditorBorder.x),
            SpriteGuide.Bottom => Mathf.Clamp(value, 0f, spriteEditorTexture.height - spriteEditorBorder.w),
            _ => Mathf.Clamp(value, 0f, spriteEditorTexture.height - spriteEditorBorder.y)
        };
    }

    private static void SetBorderFromSlider(SpriteGuide guide, float value) {
        if(spriteEditorTexture == null) {
            return;
        }

        value = FilterBorderSlider(guide, value);
        switch(guide) {
            case SpriteGuide.Left:
                spriteEditorBorder.x = value;
                break;
            case SpriteGuide.Right:
                spriteEditorBorder.z = value;
                break;
            case SpriteGuide.Bottom:
                spriteEditorBorder.y = value;
                break;
            case SpriteGuide.Top:
                spriteEditorBorder.w = value;
                break;
        }

        UpdateSpriteGuides();
    }

    private static void UpdateSpriteBorderInputs() {
        UpdateSpriteBorderInput(spriteEditorLeftInput, spriteEditorBorder.x);
        UpdateSpriteBorderInput(spriteEditorRightInput, spriteEditorBorder.z);
        UpdateSpriteBorderInput(spriteEditorBottomInput, spriteEditorBorder.y);
        UpdateSpriteBorderInput(spriteEditorTopInput, spriteEditorBorder.w);
    }

    private static void UpdateSpriteBorderInput(UISlider input, float value) {
        if(input == null) {
            return;
        }

        input.Set(Mathf.Round(value), false);
    }

    private static void SetSpriteEditor(Texture2D texture, Vector4 border) {
        bool hasTexture = texture != null;
        spriteEditorTexture = texture;
        if(hasTexture) {
            border = NormalizeBorder(border, texture.width, texture.height);
            spriteEditorBorder = NormalizeBorder(
                new Vector4(
                    Mathf.Round(border.x),
                    Mathf.Round(border.y),
                    Mathf.Round(border.z),
                    Mathf.Round(border.w)
                ),
                texture.width,
                texture.height
            );
        } else {
            spriteEditorBorder = Vector4.zero;
        }
        if(spriteEditorImage == null) {
            return;
        }

        spriteEditorImage.texture = texture;
        spriteEditorImage.color = hasTexture ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        if(hasTexture) {
            RectTransform workspace = spriteEditorPreview.parent as RectTransform;
            float maxWidth = Mathf.Max(120f, workspace.rect.width - 56f);
            float maxHeight = Mathf.Max(100f, workspace.rect.height - 48f);
            float scale = Mathf.Min(maxWidth / texture.width, maxHeight / texture.height);
            spriteEditorPreview.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
        } else {
            spriteEditorPreview.sizeDelta = new Vector2(520f, 340f);
            spriteEditorHint.text = T("SPRITE_EDITOR_HINT", "Drag the green guides to set 9-slice borders.");
        }

        spriteEditorGuideOverlay.sizeDelta = spriteEditorPreview.sizeDelta;
        spriteEditorGuideOverlay.SetAsLastSibling();
        SetGuideActive(spriteEditorLeftGuide, hasTexture);
        SetGuideActive(spriteEditorRightGuide, hasTexture);
        SetGuideActive(spriteEditorBottomGuide, hasTexture);
        SetGuideActive(spriteEditorTopGuide, hasTexture);
        UpdateSpriteBorderInputs();
        if(hasTexture) {
            Canvas.ForceUpdateCanvases();
            UpdateSpriteGuides();
        }
    }

    private static void SetGuideActive(RectTransform guide, bool active) {
        if(guide) {
            guide.gameObject.SetActive(active);
        }
    }

    private static void UpdateSpriteGuides() {
        if(spriteEditorTexture == null || spriteEditorGuideOverlay == null) {
            return;
        }

        float width = spriteEditorGuideOverlay.rect.width;
        float height = spriteEditorGuideOverlay.rect.height;
        float scaleX = width / spriteEditorTexture.width;
        float scaleY = height / spriteEditorTexture.height;
        float inset = 3f;
        float left = Mathf.Clamp(spriteEditorBorder.x * scaleX, inset, width - inset);
        float right = Mathf.Clamp(spriteEditorBorder.z * scaleX, inset, width - inset);
        float bottom = Mathf.Clamp(spriteEditorBorder.y * scaleY, inset, height - inset);
        float top = Mathf.Clamp(spriteEditorBorder.w * scaleY, inset, height - inset);
        spriteEditorLeftGuide.anchoredPosition = new Vector2(left, 0f);
        spriteEditorRightGuide.anchoredPosition = new Vector2(-right, 0f);
        spriteEditorBottomGuide.anchoredPosition = new Vector2(0f, bottom);
        spriteEditorTopGuide.anchoredPosition = new Vector2(0f, -top);
        UpdateSpriteBorderInputs();
        UpdateSpriteEditorHint();
    }

    private static void UpdateSpriteEditorHint() {
        if(spriteEditorHint == null || spriteEditorTexture == null) {
            return;
        }

        spriteEditorHint.text = T(
            "SPRITE_EDITOR_VALUES",
            "L {0}  R {1}  B {2}  T {3}",
            Mathf.RoundToInt(spriteEditorBorder.x),
            Mathf.RoundToInt(spriteEditorBorder.z),
            Mathf.RoundToInt(spriteEditorBorder.y),
            Mathf.RoundToInt(spriteEditorBorder.w)
        );
    }

    private static PointerEventData GetSpritePointer(BaseEventData data) {
#if ML && IL2CPP
        return data.TryCast<PointerEventData>();
#else
        return data as PointerEventData;
#endif
    }

    private static void DragSpriteGuide(SpriteGuide guide, BaseEventData data) {
        PointerEventData pointer = GetSpritePointer(data);
        if(pointer == null || pointer.button != PointerEventData.InputButton.Left ||
            spriteEditorTexture == null || spriteEditorPreview == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                spriteEditorPreview,
                pointer.position,
                pointer.pressEventCamera,
                out Vector2 local
            )) {
            return;
        }

        float width = spriteEditorPreview.rect.width;
        float height = spriteEditorPreview.rect.height;
        if(width <= 0f || height <= 0f) {
            return;
        }
        float x = Mathf.Round(Mathf.Clamp(local.x + width * 0.5f, 0f, width) / width * spriteEditorTexture.width);
        float y = Mathf.Round(Mathf.Clamp(local.y + height * 0.5f, 0f, height) / height * spriteEditorTexture.height);
        switch(guide) {
            case SpriteGuide.Left:
                spriteEditorBorder.x = Mathf.Clamp(x, 0f, spriteEditorTexture.width - spriteEditorBorder.z);
                break;
            case SpriteGuide.Right:
                spriteEditorBorder.z = Mathf.Clamp(spriteEditorTexture.width - x, 0f, spriteEditorTexture.width - spriteEditorBorder.x);
                break;
            case SpriteGuide.Bottom:
                spriteEditorBorder.y = Mathf.Clamp(y, 0f, spriteEditorTexture.height - spriteEditorBorder.w);
                break;
            case SpriteGuide.Top:
                spriteEditorBorder.w = Mathf.Clamp(spriteEditorTexture.height - y, 0f, spriteEditorTexture.height - spriteEditorBorder.y);
                break;
        }

        UpdateSpriteGuides();
    }

    private static Vector4 NormalizeBorder(Vector4 border, int width, int height) {
        float left = Mathf.Clamp(border.x, 0f, width);
        float right = Mathf.Clamp(border.z, 0f, width - left);
        float bottom = Mathf.Clamp(border.y, 0f, height);
        float top = Mathf.Clamp(border.w, 0f, height - bottom);
        return new Vector4(left, bottom, right, top);
    }

    private static bool Approximately(Vector4 left, Vector4 right) => (left - right).sqrMagnitude < 0.0001f;

    private static void BeginSettingsApply() {
        if(busy) {
            return;
        }

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
        Vector4 border = UserResourceManager.Spr.TryGet(key, out var spriteValue)
            ? spriteValue.settings.Border
            : Vector4.zero;
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
        }).ContinueWith(task => MainThread.Enqueue(() => FinishSettingsApply(task, key, mipChain, linear, border)));
    }

    private static void FinishSettingsApply(
        Task<(byte[] Bytes, string Error)> task,
        string key,
        bool mipChain,
        bool linear,
        Vector4 border
    ) {
        if(!MainCore.IsModEnabled) {
            return;
        }

        var (Bytes, Error) = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : (Bytes: (byte[])null, Error: "Image read task failed.");
        if(Bytes == null) {
            FinishSettingsBusy();
            SetStatus("SETTINGS_FAILED", "Settings failed: {0}", UIColors.ObjectActiveMathErr, Error);
            return;
        }

        UserTexture2D.Result textureResult = UserResourceManager.T2D.ReplaceData(
            key,
            Bytes,
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

        border = NormalizeBorder(border, textureValue.texture.width, textureValue.texture.height);
        if(
            UserResourceManager.Spr.TryGet(key, out _) &&
            !UserResourceManager.Spr.UpdateBorder(key, border)
        ) {
            FinishSettingsBusy();
            SetStatus("SPRITE_REBUILD_FAILED", "Sprite rebuild failed.", UIColors.ObjectActiveMathErr);
            return;
        }

        UserResourceManager.Config.RequestSave(50);
        OverlayCore.RequestLayoutRefresh();
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
        if(busy || !UserResourceManager.T2D.TryGet(key, out var textureValue)) {
            return;
        }

        if(!UserResourceManager.T2D.TryGetPath(key, out string path)) {
            return;
        }

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
        if(busy) {
            return;
        }

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
        if(listContent == null) {
            return;
        }

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
                if(currentMode == ResourceMode.Images) {
                    CreateCard(listContent, key);
                } else {
                    CreateFontCard(listContent, key);
                }
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

        imageSettingsRow?.gameObject.SetActive(currentMode == ResourceMode.Images);

        BuildList();
    }

    private static void ToggleUIStateByMod(bool isEnabled) {
        if(disabledPanel == null) {
            return;
        }

        disabledPanel.SetActive(!isEnabled);
        if(!isEnabled) {
            busy = false;
            CloseSpriteEditor();
            CancelSettingsEdit();
            BuildList();
            Tooltip.Hide();
            return;
        }
        MainThread.Enqueue(() => {
            if(!MainCore.IsModEnabled) {
                return;
            }

            BuildList();
            Canvas.ForceUpdateCanvases();
            if(listContent) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            }
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
        if(!UserResourceManager.Spr.TryGet(key, out var spriteValue)) {
            return;
        }

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
        name.rectTransform.offsetMax = new Vector2(-466f, -12f);
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
        renameInput.Rect.offsetMax = new Vector2(-466f, -12f);
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
        details.rectTransform.offsetMax = new Vector2(-466f, 0f);

        UIButton spriteEditor = GenerateUI.Button(
            card,
            () => OpenSpriteEditor(key),
            T("SPRITE_EDITOR", "Sprite Editor"),
            "sprite_editor_" + key
        );
        PlaceRight(spriteEditor.Rect, 134f, 54f, 324f);
        spriteEditor.Label.fontSize = 14f;
        spriteEditor.Label.gameObject.AddComponent<TextLocalization>().Init("SPRITE_EDITOR", "Sprite Editor");

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
        if(!UserResourceManager.Fnt.TryGet(key, out var fontAsset)) {
            return;
        }

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
                    try {
                        if(File.Exists(filePath)) {
                            File.Delete(filePath);
                        }
                    } catch { }
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
                try {
                    if(File.Exists(path)) {
                        File.Delete(path);
                    }
                } catch { }
            });
        }
    }

    private static string SanitizeKey(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string result = new(value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return result.Trim().Trim('.', ' ');
    }

    private static string T(string key, string defaultValue, params object[] args) => string.Format(MainCore.Tr.Get(key, defaultValue), args);

    private static void SetStatus(string key, string defaultValue, Color color, params object[] args) => SetStatus(T(key, defaultValue, args), color);

    private static void SetStatus(string text, Color color) {
        if(statusLabel == null) {
            return;
        }

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

    private static void ResizeInput(RectTransform rect, float rightWidth) => rect.offsetMax = new Vector2(-rightWidth, 0f);

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
