using Overlayer.Tween;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Async;
using Overlayer.IO.User;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Package;
using Overlayer.Resource;
using O5Kit.Factory;
using O5Kit.Control;
using Overlayer.UI.Overlay;
using O5Kit.Behaviour;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using O5Kit.Core;
using O5Kit.Input;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageOverlayer {
    static readonly Dictionary<OvCanvas, GameObject> tileMap = [];

    private static GameObject rootViewport;
    private static CanvasGroup viewportCanvasGroup;
    private static GameObject disabledPanel;
    private static RectTransform contentRectRef;
    private static Transform gridRef;
    private static bool subscribedToCanvases;

    private static OvCanvasSettingPage settingPage;
    private static OvCanvasPropsPage propsPage;
    private static bool subscribedToPackages;

    private static readonly Dictionary<OvCanvas, (Texture2D tex, Sprite sprite, string path, DateTime mtime)> thumbCache = [];

    public static void Tick() => settingPage?.Tick();

    public static void Create(RectTransform parent) {
        MainCore.Log.Msg("Creating Overlayer Page UI...");
        var (viewportRect, contentRect, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, parent, 0f);
        GameObject viewport = viewportRect.gameObject;
        contentRectRef = contentRect;
        rootViewport = viewport;
        viewportCanvasGroup = viewport.AddComponent<CanvasGroup>();

        GameObject grid = new("Grid");
        grid.transform.SetParent(contentRectRef, false);

        grid.AddComponent<RectTransform>();
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        layout.spacing = new Vector2(18, 18);
        layout.padding = new() {
            left = 18,
            right = 18,
            top = 18,
            bottom = 18
        };

        var keeper = grid.AddComponent<GridRatioKeeper>();
        keeper.Setup(contentRectRef);
        gridRef = grid.transform;

        if (!subscribedToCanvases) {
            subscribedToCanvases = true;
            OverlayCore.OnCanvasesChanged += RefreshTilesExternal;
        }

        var pageScroll = parent.gameObject.AddComponent<UIScrollController>();
        pageScroll.Ctx = O5KitAdapters.Ctx;
        pageScroll.SetContent(contentRectRef, viewportRect);

        CreateDisabledPanel(viewportRect);

        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);

        settingPage = new OvCanvasSettingPage(parent, () => {
            settingPage.Close();
            FadeCanvasGroup(viewportCanvasGroup, 1f, true);
            BuildAllTiles(grid.transform);
        });

        propsPage = new OvCanvasPropsPage(parent, () => {
            propsPage.Close(true);
            FadeCanvasGroup(viewportCanvasGroup, 1f, true);
            BuildAllTiles(grid.transform);
        });

        if (!subscribedToPackages) {
            subscribedToPackages = true;
            PackageStore.OnChanged += RefreshTilesExternal;
        }

        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if (!isDispose) {
                ToggleUIStateByMod(grid.transform, isEnabled);
                if (isEnabled) {
                    MainThread.Enqueue(() => {
                        if (MainCore.IsModEnabled) {
                            BuildAllTiles(grid.transform);
                        }
                    });
                }
            }
        };

        if (!MainCore.IsModEnabled) {
            ToggleUIStateByMod(grid.transform, false);
        } else {
            // The grid starts empty: build it now instead of waiting for the
            // next toggle/import event.
            ToggleUIStateByMod(grid.transform, true);
        }
    }

    private static void RefreshTilesExternal() {
        // Canvas mutations can come from any thread (e.g. module importers
        // running file dialogs); tile construction must run on the main
        // thread, deferred by one frame at most.
        MainThread.Enqueue(() => {
            if (gridRef == null || gridRef.Equals(null) || !MainCore.IsModEnabled) {
                return;
            }
            BuildAllTiles(gridRef);
        });
    }

    private static void ToggleUIStateByMod(Transform transform, bool isEnabled) {
        if (transform == null || transform.Equals(null)) {
            return;
        }
        if (!isEnabled) {
            settingPage?.Close(true);
            propsPage?.Close(true);
            FadeCanvasGroup(viewportCanvasGroup, 1f, true, true);
            if (disabledPanel != null) {
                disabledPanel.SetActive(true);
                disabledPanel.transform.SetAsLastSibling();
            }
            ClearAllTiles(transform);
            return;
        }

        disabledPanel?.SetActive(false);
        BuildAllTiles(transform);
    }

    private static void BuildAllTiles(Transform transform) {
        if (transform == null || transform.Equals(null)) {
            return;
        }

        ClearAllTiles(transform);

        for (int i = 0; i < OverlayCore.Canvases.Count; i++) {
            var c = OverlayCore.Canvases[i];
            if (!tileMap.TryGetValue(c, out var tile)) {
                tile = CreateCanvasTile(transform, c, () => BuildAllTiles(transform));
                tileMap[c] = tile;
            }
        }

        foreach (var pkg in PackageStore.Packages) {
            CreatePackageTile(transform, pkg, () => BuildAllTiles(transform));
        }

        CreateCanvasActionTile(transform, () => {
            OverlayCore.CreateOvCanvas();
            BuildAllTiles(transform);
        }, () => BeginImportCanvas(transform), () => BeginImportPreset(transform));

        if (contentRectRef != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);
        }
        transform.GetComponent<GridRatioKeeper>()?.RefreshNow();
    }

    private static void ClearAllTiles(Transform transform) {
        if (transform == null) {
            return;
        }

        try {
            O5KitAdapters.Ctx?.Tooltip?.Hide();
        } catch {
        }

        foreach (var key in thumbCache.Keys.ToArray()) {
            if (!OverlayCore.Canvases.Contains(key)) {
                DropTileThumb(key);
            }
        }

        foreach (Transform child in transform) {
            if (child.name == "DisabledOverlayPanel") {
                continue;
            }

            UnityEngine.Object.Destroy(child.gameObject);
        }

        tileMap.Clear();
    }

    private static void CreateDisabledPanel(RectTransform parent) {
        disabledPanel = new GameObject("DisabledOverlayPanel");
        disabledPanel.transform.SetParent(parent, false);

        var rect = disabledPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = disabledPanel.AddComponent<Image>();
        img.color = UIColors.PanelBG;
        img.raycastTarget = true;

        GameObject textGo = new("DisabledMessageText");
        textGo.transform.SetParent(disabledPanel.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        var txt = textGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Only available when the Mod is Enabled!";
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 24;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        txt.gameObject.AddComponent<TextLocalization>().Init("ONLY_AVAILABLE_WHEN_ENABLED", "Only available when the Mod is Enabled!");

        disabledPanel.SetActive(false);
    }

    static GameObject CreateCanvasTile(Transform parent, OvCanvas canvas, Action onChanged = null) {
        var bg = new GameObject(canvas.Config.Name);
        bg.transform.SetParent(parent, false);

        bg.AddComponent<RectTransform>();

        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.ObjectBG;
        bgImg.raycastTarget = true;
        ApplyThumbBg(bg, bgImg, GetTileThumb(canvas));

        GameObject textGo = new("CanvasNameText");
        textGo.transform.SetParent(bg.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 20);
        textRect.offsetMax = new Vector2(-20, -20);

        var txt = textGo.AddComponent<TextMeshProUGUI>();
        txt.text = string.IsNullOrEmpty(canvas.Config.Name) ? "(Empty)" : canvas.Config.Name;
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 22;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        var tileTrigger = bg.AddComponent<EventTrigger>();
        var hoverImg = O5Effects.HoverOutline(O5KitAdapters.Ctx, bg, tileTrigger);
        hoverImg.transform.SetAsLastSibling();
        hoverImg.raycastTarget = false;
        AddTileHoverScale(bg, tileTrigger);

        var tileControls = new List<GameObject>();

        var bgOvent = bg.AddComponent<OventHandler>();
        bgOvent.OnClick += btn => {
            switch (btn) {
                case InputButton.Left:
                    if (!canvas.Config.Enabled.Value) {
                        break;
                    }
                    if (PointerOnTileControl(bg, tileControls)) {
                        break;
                    }
                    FadeCanvasGroup(viewportCanvasGroup, 0f, false);
                    settingPage?.Open(canvas);
                    break;
            }
        };

        O5Button exportBtn = TileButton(bg.transform, TileIcon("Upload128.png", UISprite.Upload128), "Export",
            $"tile_export_{canvas.GetHashCode()}", () => { });
        exportBtn.OnClick = () => {
            BeginExportCanvas(canvas);
        };
        var exportRect = exportBtn.Rect;
        exportRect.anchorMin = new Vector2(1f, 0f);
        exportRect.anchorMax = new Vector2(1f, 0f);
        exportRect.pivot = new Vector2(1f, 0f);
        exportRect.anchoredPosition = new Vector2(-10f, 10f);
        exportRect.sizeDelta = new Vector2(110f, 34f);

        var cloneRect = TileButton(bg.transform, BuiltinIcon(UISprite.Clone128), "Clone",
            $"tile_clone_{canvas.GetHashCode()}", () => {
                if (OverlayCore.CloneCanvas(canvas) != null) {
                    onChanged?.Invoke();
                }
            }).Rect;
        cloneRect.anchorMin = new Vector2(0f, 0f);
        cloneRect.anchorMax = new Vector2(0f, 0f);
        cloneRect.pivot = new Vector2(0f, 0f);
        cloneRect.anchoredPosition = new Vector2(10f, 10f);
        cloneRect.sizeDelta = new Vector2(110f, 34f);

        var toggleGo = new GameObject("EnabledToggle");
        toggleGo.transform.SetParent(bg.transform, false);
        var toggleRect = toggleGo.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(1f, 1f);
        toggleRect.anchoredPosition = new Vector2(-10f, -10f);
        toggleRect.sizeDelta = new Vector2(64f, 34f);
        var enabledToggle = O5Factory.Toggle(O5KitAdapters.Ctx,
            toggleGo.transform,
            null,
            canvas.Config.Enabled.Value,
            toggle => {
                canvas.Config.Enabled.Value = toggle;
                OverlayCore.SaveAllCanvases();
                ApplyTileEnabledVisual(bgImg, txt, toggle);
            },
            string.Empty,
            $"tile_enabled_{canvas.GetHashCode()}");
        var hoverOutline = enabledToggle.Rect.transform.Find("Hover");
        if (hoverOutline != null) {
            UnityEngine.Object.Destroy(hoverOutline.gameObject);
        }
        var toggleBg = enabledToggle.Rect.GetComponent<Image>();
        if (toggleBg != null) {
            toggleBg.color = Color.clear;
        }
        tileControls.Add(exportRect.gameObject);
        tileControls.Add(cloneRect.gameObject);
        tileControls.Add(toggleGo);

        OvCanvas armedDeleteFor = null;
        DateTime armedDeleteAt = default;
        var delBtn = TileButton(bg.transform, BuiltinIcon(UISprite.X128), "X",
            $"tile_del_{canvas.GetHashCode()}", () => { });
        delBtn.OnClick = () => {
            if (ShiftSkipConfirm()) {
                OverlayCore.DeleteOvCanvas(canvas);
                onChanged?.Invoke();
                return;
            }
            if (!ReferenceEquals(armedDeleteFor, canvas)
                || (DateTime.Now - armedDeleteAt).TotalSeconds > 5) {
                armedDeleteFor = canvas;
                armedDeleteAt = DateTime.Now;
                delBtn.NormalColor = UIColors.SoftRed;
                if (delBtn.Icon != null) {
                    delBtn.Icon.color = UIColors.SoftRed;
                }
                if (delBtn.Label != null) {
                    delBtn.Label.text = "!";
                }
                delBtn.UpdateVisual();
                return;
            }
            OverlayCore.DeleteOvCanvas(canvas);
            onChanged?.Invoke();
        };
        var delRect = delBtn.Rect;
        delRect.anchorMin = new Vector2(0f, 0f);
        delRect.anchorMax = new Vector2(0f, 0f);
        delRect.pivot = new Vector2(0f, 0f);
        delRect.anchoredPosition = new Vector2(130f, 10f);
        delRect.sizeDelta = new Vector2(34f, 34f);
        tileControls.Add(delRect.gameObject);

        var propsRect = TileButton(bg.transform, null, "...",
            $"tile_props_{canvas.GetHashCode()}", () => {
                FadeCanvasGroup(viewportCanvasGroup, 0f, false);
                propsPage?.Open(canvas);
            }).Rect;
        propsRect.anchorMin = new Vector2(1f, 0f);
        propsRect.anchorMax = new Vector2(1f, 0f);
        propsRect.pivot = new Vector2(1f, 0f);
        propsRect.anchoredPosition = new Vector2(-130f, 10f);
        propsRect.sizeDelta = new Vector2(110f, 34f);
        tileControls.Add(propsRect.gameObject);

        ApplyTileEnabledVisual(bgImg, txt, canvas.Config.Enabled.Value);

        return bg;
    }

    private static Sprite GetTileThumb(OvCanvas canvas) {
        if (canvas?.Props == null || string.IsNullOrWhiteSpace(canvas.Props.ThumbnailPath)) {
            return null;
        }
        string disk;
        try {
            disk = UserResourceManager.FromUser(canvas.Props.ThumbnailPath);
        } catch {
            return null;
        }
        if (string.IsNullOrEmpty(disk) || !File.Exists(disk)) {
            return null;
        }
        DateTime mtime = File.GetLastWriteTimeUtc(disk);
        if (thumbCache.TryGetValue(canvas, out var cached)
            && cached.sprite
            && cached.path == disk
            && cached.mtime == mtime) {
            return cached.sprite;
        }
        DropTileThumb(canvas);
        try {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(disk))) {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            thumbCache[canvas] = (tex, sprite, disk, mtime);
            return sprite;
        } catch {
            return null;
        }
    }

    private static void DropTileThumb(OvCanvas canvas) {
        if (canvas != null && thumbCache.Remove(canvas, out var dropped) && dropped.tex) {
            UnityEngine.Object.Destroy(dropped.tex);
        }
    }

    private static void ApplyThumbBg(GameObject tile, Image bgImg, Sprite thumb) {
        if (thumb == null) {
            return;
        }
        var maskGo = new GameObject("ThumbMask");
        maskGo.transform.SetParent(tile.transform, false);
        var maskRect = maskGo.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = Vector2.zero;
        maskRect.offsetMax = Vector2.zero;
        var maskImg = maskGo.AddComponent<Image>();
        maskImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        maskImg.type = Image.Type.Sliced;
        maskGo.AddComponent<Mask>().showMaskGraphic = false;

        var thumbGo = new GameObject("Thumb");
        thumbGo.transform.SetParent(maskGo.transform, false);
        var thumbRect = thumbGo.AddComponent<RectTransform>();
        thumbRect.anchorMin = thumbRect.anchorMax = thumbRect.pivot = new Vector2(0.5f, 0.5f);
        thumbRect.sizeDelta = Vector2.zero;
        var thumbImg = thumbGo.AddComponent<Image>();
        thumbImg.sprite = thumb;
        thumbImg.raycastTarget = false;
        var fitter = thumbGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        float aspect = 1f;
        try {
            if (thumb.rect.height > 0f) {
                aspect = thumb.rect.width / thumb.rect.height;
            }
        } catch {
        }
        fitter.aspectRatio = aspect;

        var dim = new GameObject("Dim");
        dim.transform.SetParent(maskGo.transform, false);
        var dimRect = dim.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.45f);
        dimImg.raycastTarget = false;
    }

    static GameObject CreatePackageTile(Transform parent, PackageEntry entry, Action onChanged = null) {
        var bg = new GameObject($"PKG_{entry.Id}");
        bg.transform.SetParent(parent, false);

        bg.AddComponent<RectTransform>();

        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.PanelBG;
        bgImg.raycastTarget = true;
        ApplyThumbBg(bg, bgImg, entry.ThumbnailSprite);

        GameObject textGo = new("CanvasNameText");
        textGo.transform.SetParent(bg.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 20);
        textRect.offsetMax = new Vector2(-20, -20);

        var txt = textGo.AddComponent<TextMeshProUGUI>();
        string pkgName = entry.Manifest?.Package?.Name;
        if (string.IsNullOrEmpty(pkgName)) {
            pkgName = "(Empty)";
        }
        txt.text = pkgName;
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 22;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        GameObject badgeGo = new("PkgBadge");
        badgeGo.transform.SetParent(bg.transform, false);
        var badgeRect = badgeGo.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(10f, -10f);
        badgeRect.sizeDelta = new Vector2(64f, 26f);
        var badge = badgeGo.AddComponent<TextMeshProUGUI>();
        badge.text = "PKG";
        badge.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        badge.fontSize = 15;
        badge.alignment = TextAlignmentOptions.Center;
        badge.color = new Color(0.55f, 0.9f, 1f);
        badge.raycastTarget = false;

        string pkgTip = BuildPackageTooltip(entry);
        if (!string.IsNullOrEmpty(pkgTip)) {
            bg.transform.AddToolTip(O5KitAdapters.Ctx, pkgTip);
        }

        var tileControls = new List<GameObject>();

        var copyRect = TileButton(bg.transform, BuiltinIcon(UISprite.Download128), "Copy",
            $"tile_promote_{entry.Id}", () => BeginPromote(entry)).Rect;
        copyRect.anchorMin = new Vector2(0f, 0f);
        copyRect.anchorMax = new Vector2(0f, 0f);
        copyRect.pivot = new Vector2(0f, 0f);
        copyRect.anchoredPosition = new Vector2(10f, 10f);
        copyRect.sizeDelta = new Vector2(110f, 34f);

        bool confirmDelete = false;
        DateTime pkgArmedAt = default;
        var deleteBtn = TileButton(bg.transform, BuiltinIcon(UISprite.X128), "X",
            $"tile_pkgdel_{entry.Id}", () => { });
        deleteBtn.OnClick = () => {
            if (ShiftSkipConfirm()) {
                PackageStore.Remove(entry.Id);
                return;
            }
            if (!confirmDelete || (DateTime.Now - pkgArmedAt).TotalSeconds > 5) {
                confirmDelete = true;
                pkgArmedAt = DateTime.Now;
                if (deleteBtn.Label != null) {
                    deleteBtn.Label.text = "!";
                }
                if (deleteBtn.Icon != null) {
                    deleteBtn.Icon.color = UIColors.SoftRed;
                }
                deleteBtn.NormalColor = UIColors.SoftRed;
                deleteBtn.UpdateVisual();
                return;
            }
            PackageStore.Remove(entry.Id);
        };
        var deleteRect = deleteBtn.Rect;
        deleteRect.anchorMin = new Vector2(0f, 0f);
        deleteRect.anchorMax = new Vector2(0f, 0f);
        deleteRect.pivot = new Vector2(0f, 0f);
        deleteRect.anchoredPosition = new Vector2(130f, 10f);
        deleteRect.sizeDelta = new Vector2(34f, 34f);

        var toggleGo = new GameObject("EnabledToggle");
        toggleGo.transform.SetParent(bg.transform, false);
        var toggleRect = toggleGo.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(1f, 1f);
        toggleRect.anchoredPosition = new Vector2(-10f, -10f);
        toggleRect.sizeDelta = new Vector2(64f, 34f);
        var enabledToggle = O5Factory.Toggle(O5KitAdapters.Ctx,
            toggleGo.transform,
            null,
            entry.Enabled,
            toggle => {
                var toggleWarnings = PackageStore.SetEnabled(entry, toggle);
                ApplyTileEnabledVisual(bgImg, txt, entry.Enabled);
                if (toggleWarnings.Count > 0) {
                    O5cpDialogs.ShowWarnings("Package warnings", toggleWarnings);
                }
            },
            string.Empty,
            $"tile_pkgenabled_{entry.Id}");
        var hoverOutline = enabledToggle.Rect.transform.Find("Hover");
        if (hoverOutline != null) {
            UnityEngine.Object.Destroy(hoverOutline.gameObject);
        }
        var toggleBg = enabledToggle.Rect.GetComponent<Image>();
        if (toggleBg != null) {
            toggleBg.color = Color.clear;
        }
        tileControls.Add(copyRect.gameObject);
        tileControls.Add(deleteRect.gameObject);
        tileControls.Add(toggleGo);
        ApplyTileEnabledVisual(bgImg, txt, entry.Enabled);

        return bg;
    }

    private static string BuildPackageTooltip(PackageEntry entry) {
        var pkg = entry?.Manifest?.Package;
        if (pkg == null) {
            return string.Empty;
        }
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(pkg.Author)) {
            lines.Add($"by {pkg.Author.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(pkg.Version)) {
            lines.Add(pkg.Version.Trim());
        }
        if (!string.IsNullOrWhiteSpace(pkg.License)) {
            lines.Add(pkg.License.Trim());
        }
        if (!string.IsNullOrWhiteSpace(pkg.Description)) {
            if (lines.Count > 0) {
                lines.Add("--");
            }
            lines.Add(pkg.Description.Trim());
        }
        return string.Join("\n", lines);
    }

    private static bool ShiftSkipConfirm()
        => O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

    private static void BeginPromote(PackageEntry entry) {
        if (entry == null) {
            return;
        }
        var plan = O5cpPromote.BuildPlan(entry);
        if (plan.Items.Any(i => i.IsConflict)) {
            O5cpDialogs.ShowPromoteConflicts(entry, plan, () => {
                var canvas = OverlayCore.PromotePackage(entry, plan, out var warnings);
                if (canvas != null && warnings.Count > 0) {
                    O5cpDialogs.ShowWarnings("Copy warnings", warnings);
                }
            });
            return;
        }
        var promoted = OverlayCore.PromotePackage(entry, plan, out var promoteWarnings);
        if (promoted != null && promoteWarnings.Count > 0) {
            O5cpDialogs.ShowWarnings("Copy warnings", promoteWarnings);
        }
    }

    private static void ApplyTileEnabledVisual(Image bgImg, TextMeshProUGUI nameText, bool enabled) {
        var bg = UIColors.ObjectBG;
        bg.a = enabled ? 1f : 0.4f;
        bgImg.color = bg;
        nameText.color = enabled ? Color.white : UIColors.ObjectInactive;
        var dim = bgImg.transform.Find("ThumbMask/Dim");
        if (dim != null) {
            var dimImg = dim.GetComponent<Image>();
            if (dimImg != null) {
                dimImg.color = new Color(0f, 0f, 0f, enabled ? 0.45f : 0.75f);
            }
        }
    }

    private static readonly Dictionary<string, Sprite> tileIconCache = [];

    private static Sprite TileIcon(string fileName, UISprite fallback) {
        if (tileIconCache.TryGetValue(fileName, out var cached)) {
            return cached;
        }
        Sprite sprite = null;
        try {
            string path = Path.Combine(MainCore.Paths.RootPath, "Icons", fileName);
            if (File.Exists(path)) {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(path))) {
                    tex.filterMode = FilterMode.Bilinear;
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                } else {
                    UnityEngine.Object.Destroy(tex);
                }
            }
        } catch {
            sprite = null;
        }
        sprite ??= BuiltinIcon(fallback);
        tileIconCache[fileName] = sprite;
        return sprite;
    }

    private static Sprite BuiltinIcon(UISprite sprite) {
        try {
            return MainCore.Spr.Get(sprite);
        } catch {
            return null;
        }
    }

    private static O5Button TileButton(Transform parent, Sprite icon, string fallbackText, string id, Action onClick) {
        if (icon != null) {
            return O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, icon, id, 5f, 34f);
        }
        return O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, fallbackText, id, 34f);
    }

    private static bool PointerOnTileControl(GameObject tile, List<GameObject> controls) {
        var es = EventSystem.current;
        if (es == null || tile == null) {
            return false;
        }
        var ped = new PointerEventData(es) { position = O5Input.MousePosition };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        foreach (var h in hits) {
            var go = h.gameObject;
            if (go == null || go == tile) {
                return false;
            }
            foreach (var control in controls) {
                if (go == control || go.transform.IsChildOf(control.transform)) {
                    return true;
                }
            }
            if (go.transform.IsChildOf(tile.transform)) {
                continue;
            }
            return false;
        }
        return false;
    }

    private static GameObject CreateCanvasActionTile(Transform parent, Action create, Action import, Action preset) {
        var go = new GameObject("CanvasActionsTile");
        go.transform.SetParent(parent, false);
        var root = go.AddComponent<RectTransform>();

        var background = go.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        background.color = UIColors.PanelBG;
        background.raycastTarget = true;

        var left = CreateDiagonalHalf(go.transform, false);
        var presetZone = CreateDiagonalHalf(go.transform, true, 1);
        var importZone = CreateDiagonalHalf(go.transform, true, 2);
        var createIcon = CreateTileActionIcon(left.transform, BuiltinIcon(UISprite.Plus128), 0.25f);
        var presetIcon = CreateTileActionIcon(presetZone.transform, BuiltinIcon(UISprite.Star128), 0.75f, 0.73f);
        var importIcon = CreateTileActionIcon(importZone.transform, TileIcon("Download128.png", UISprite.Download128), 0.75f, 0.27f);

        var trigger = go.AddComponent<EventTrigger>();
        var handler = go.AddComponent<OventHandler>();
        AddActionHalfHoverScale(handler, root, left, presetZone, importZone,
            createIcon, presetIcon, importIcon);
        handler.OnClick += button => {
            if (button != InputButton.Left) {
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, O5Input.MousePosition, null, out var local)) {
                return;
            }
            Rect rect = root.rect;
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            float splitX = Mathf.Lerp(0.28f, 0.72f, y);
            float split = rect.xMin + rect.width * splitX;
            float halfGap = DiagonalTileGraphic.Gap * 0.5f;
            if (local.x < split) {
                create?.Invoke();
            } else if (local.y > (rect.yMin + rect.yMax) * 0.5f + halfGap) {
                preset?.Invoke();
            } else if (local.y < (rect.yMin + rect.yMax) * 0.5f - halfGap) {
                import?.Invoke();
            }
        };

        ITweenHandle hover = null;
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => {
                hover?.Kill();
                hover = background.TColor(UIColors.PanelBG, 0.12f, O5Ease.OutSine);
            }
        ),
            (EventTriggerType.PointerExit, () => {
                hover?.Kill();
                hover = background.TColor(UIColors.PanelBG, 0.12f, O5Ease.OutSine);
            }
        )
        );
        return go;
    }

    private static void AddTileHoverScale(GameObject go, EventTrigger trigger) {
        ITweenHandle tween = null;
        Transform target = go.transform;
        void ScaleTo(float value) {
            tween?.Kill();
            tween = O5KitAdapters.Ctx.Tween.TweenFloat(
                () => target ? target.localScale.x : value,
                v => {
                    if (target) {
                        target.localScale = Vector3.one * v;
                    }
                },
                value,
                0.22f,
                ease: O5Ease.OutExpo
            );
        }
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => ScaleTo(1.02f)),
            (EventTriggerType.PointerExit, () => ScaleTo(1f))
        );
    }

    private static void AddActionHalfHoverScale(OventHandler handler, RectTransform root,
        DiagonalTileGraphic left, DiagonalTileGraphic presetZone, DiagonalTileGraphic importZone,
        GameObject createIcon, GameObject presetIcon, GameObject importIcon) {
        left.rectTransform.pivot = new Vector2(0.25f, 0.5f);
        presetZone.rectTransform.pivot = new Vector2(0.75f, 0.75f);
        importZone.rectTransform.pivot = new Vector2(0.75f, 0.25f);
        ITweenHandle createTween = null;
        ITweenHandle presetZoneTween = null;
        ITweenHandle importZoneTween = null;
        ITweenHandle createIconTween = null;
        ITweenHandle importTween = null;
        ITweenHandle presetTween = null;
        int active = -1;

        ITweenHandle SetScale(Transform target, ITweenHandle tween, float value) {
            tween?.Kill();
            return O5KitAdapters.Ctx.Tween.TweenFloat(
                () => target ? target.localScale.x : value,
                v => {
                    if (target) {
                        target.localScale = Vector3.one * v;
                    }
                },
                value,
                0.22f,
                ease: O5Ease.OutExpo
            );
        }

        int ZoneIndex(Vector2 local, Rect rect) {
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            float splitX = Mathf.Lerp(0.28f, 0.72f, y);
            if (local.x < rect.xMin + rect.width * splitX) {
                return 0;
            }
            float midY = (rect.yMin + rect.yMax) * 0.5f;
            float halfGap = DiagonalTileGraphic.Gap * 0.5f;
            if (local.y > midY + halfGap) return 1;
            if (local.y < midY - halfGap) return 2;
            return -1;
        }

        handler.OnHoverUpdate = () => {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, O5Input.MousePosition, null, out var local)) {
                return;
            }
            Rect rect = root.rect;
            int next = ZoneIndex(local, rect);
            if (next == active) {
                return;
            }
            active = next;
            left.color = next == 0 ? UIColors.ObjectActiveLightBright : UIColors.ObjectButton;
            presetZone.color = next == 1 ? UIColors.ObjectActiveLightBright : UIColors.ObjectButton;
            importZone.color = next == 2 ? UIColors.ObjectActiveLightBright : UIColors.ObjectButton;
            createTween = SetScale(left.transform, createTween, next == 0 ? 1.03f : 1f);
            presetZoneTween = SetScale(presetZone.transform, presetZoneTween, next == 1 ? 1.03f : 1f);
            importZoneTween = SetScale(importZone.transform, importZoneTween, next == 2 ? 1.03f : 1f);
            createIconTween = SetScale(createIcon.transform, createIconTween, next == 0 ? 1.15f : 1f);
            presetTween = SetScale(presetIcon.transform, presetTween, next == 1 ? 1.15f : 1f);
            importTween = SetScale(importIcon.transform, importTween, next == 2 ? 1.15f : 1f);
        };

        void ResetHover() {
            active = -1;
            createTween?.Kill();
            presetZoneTween?.Kill();
            importZoneTween?.Kill();
            createIconTween?.Kill();
            importTween?.Kill();
            presetTween?.Kill();
            left.transform.localScale = Vector3.one;
            presetZone.transform.localScale = Vector3.one;
            importZone.transform.localScale = Vector3.one;
            createIcon.transform.localScale = Vector3.one;
            importIcon.transform.localScale = Vector3.one;
            presetIcon.transform.localScale = Vector3.one;
            left.color = presetZone.color = importZone.color = UIColors.ObjectButton;
        }
        handler.OnDisabled += ResetHover;
        var trigger = root.GetComponent<EventTrigger>();
        if (trigger != null) {
            UnityUtils.AddEvents(trigger, (EventTriggerType.PointerExit, ResetHover));
        }
    }

    private static DiagonalTileGraphic CreateDiagonalHalf(Transform parent, bool right, int rightSection = 0) {
        string name = !right ? "CreateHalf" : rightSection == 1 ? "PresetHalf" : rightSection == 2 ? "ImportHalf" : "RightHalf";
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(right ? 0.75f : 0.25f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var graphic = go.AddComponent<DiagonalTileGraphic>();
        graphic.RightHalf = right;
        graphic.RightSection = rightSection;
        graphic.color = UIColors.ObjectButton;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static GameObject CreateTileActionIcon(Transform parent, Sprite sprite, float anchorX, float anchorY = 0.5f) {
        var go = new GameObject("ActionIcon");
        go.transform.SetParent(parent, false);
        if (sprite == null) {
            return go;
        }
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(56f, 56f);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return go;
    }

    private static void BeginExportCanvas(OvCanvas canvas) {
        if (canvas == null) {
            return;
        }
        O5cpDialogs.ShowExportOptions(options => {
            if (canvas.GameObject == null) {
                return;
            }
            string baseName = string.Join("_", (canvas.Config.Name.Value ?? "Canvas").Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(baseName)) {
                baseName = "Canvas";
            }
            _ = Overlayer.UI.Utility.NativeDialogThread.Run(() => {
                try {
                    return NativeFileDialog.Extended.NFD.SaveDialog(
                        ExportDialogDir(),
                        $"{baseName}.o5cp",
                        new Dictionary<string, string> { ["Overlayer Package"] = "o5cp" }
                    );
                } catch (Exception e) {
                    MainCore.Log.Err($"[CanvasExport] File dialog failed: {e.Message}");
                    return null;
                }
            }).ContinueWith(task => {
                MainThread.Enqueue(() => {
                    if (!MainCore.IsModEnabled) {
                        return;
                    }
                    if (canvas.GameObject == null) {
                        return;
                    }
                    string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                    if (string.IsNullOrWhiteSpace(path)) {
                        return;
                    }
                    try {
                        var result = O5cpExporter.Export(canvas, path, options);
                        if (result == null) {
                            MainCore.Log.Err("[CanvasExport] Package export failed.");
                            return;
                        }
                        foreach (string warning in result.Warnings) {
                            MainCore.Log.Wrn($"[CanvasExport] {warning}");
                        }
                        MainCore.Log.Msg($"[CanvasExport] Exported package to {path}");
                        if (result.Warnings.Count > 0) {
                            O5cpDialogs.ShowWarnings("Export warnings", result.Warnings);
                        }
                    } catch (Exception e) {
                        MainCore.Log.Err($"[CanvasExport] Package export failed: {e.Message}");
                    }
                });
            });
        });
    }

    private static void BeginImportCanvas(Transform grid) {
        _ = Overlayer.UI.Utility.NativeDialogThread.Run(() => {
            try {
                return NativeFileDialog.Extended.NFD.OpenDialog(
                    ExportDialogDir(),
                    new Dictionary<string, string> {
                        ["Overlayer Package"] = "o5cp",
                        ["JSON"] = "json",
                    }
                );
            } catch (Exception e) {
                MainCore.Log.Err($"[CanvasImport] File dialog failed: {e.Message}");
                return null;
            }
        }).ContinueWith(task => {
            MainThread.Enqueue(() => {
                if (!MainCore.IsModEnabled) {
                    return;
                }
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if (!string.IsNullOrWhiteSpace(path)) {
                    if (path.EndsWith(".o5cp", StringComparison.OrdinalIgnoreCase)) {
                        BeginInstallPackage(path, grid);
                    } else {
                        OverlayCore.ImportCanvas(path);
                    }
                }
                BuildAllTiles(grid);
            });
        });
    }

    private static void BeginImportPreset(Transform grid) {
        if (!MainCore.IsModEnabled) {
            return;
        }
        O5cpDialogs.ShowPresets(preset => {
            if (!MainCore.IsModEnabled) {
                return;
            }
            byte[] data;
            try {
                data = preset.ReadBytes?.Invoke();
            } catch (Exception e) {
                MainCore.Log.Err($"[CanvasImport] Preset read failed: {e.Message}");
                O5cpDialogs.ShowWarnings("Import warnings", [$"preset read failed: {e.Message}"]);
                return;
            }
            PackageEntry entry;
            try {
                entry = PackageStore.InstallBytes(data, out var warnings);
                foreach (string warning in warnings) {
                    MainCore.Log.Wrn($"[CanvasImport] {warning}");
                }
                if (entry != null && warnings.Count > 0) {
                    O5cpDialogs.ShowWarnings("Import warnings", warnings);
                }
            } catch (Exception e) {
                MainCore.Log.Err($"[CanvasImport] Preset install failed: {e.Message}");
            }
            BuildAllTiles(grid);
        });
    }

    private static string ExportDialogDir() {
        try {
            string root = MainCore.Paths.RootPath;
            if (Directory.Exists(root)) {
                return root;
            }
        } catch {
        }
        return null;
    }

    private static void BeginInstallPackage(string path, Transform grid) {
        PackageStore.StagedPackage staged;
        try {
            staged = PackageStore.Stage(path, out var stageWarnings);
            foreach (string warning in stageWarnings) {
                MainCore.Log.Wrn($"[CanvasImport] {warning}");
            }
            if (staged == null) {
                if (stageWarnings.Count > 0) {
                    O5cpDialogs.ShowWarnings("Import warnings", stageWarnings);
                }
                return;
            }
        } catch (Exception e) {
            MainCore.Log.Err($"[CanvasImport] Stage failed: {e.Message}");
            return;
        }
        PackageEntry entry;
        try {
            entry = PackageStore.CommitStaged(staged, out var warnings);
            foreach (string warning in warnings) {
                MainCore.Log.Wrn($"[CanvasImport] {warning}");
            }
            if (entry != null && warnings.Count > 0) {
                O5cpDialogs.ShowWarnings("Import warnings", warnings);
            }
        } catch (Exception e) {
            MainCore.Log.Err($"[CanvasImport] Install failed: {e.Message}");
            staged.Dispose();
        }
        BuildAllTiles(grid);
    }

    private static ITweenHandle fadeTween;
    private static void FadeCanvasGroup(CanvasGroup cg, float targetAlpha, bool setActive, bool noAnimate = false) {
        if (cg == null) {
            return;
        }

        fadeTween?.Kill();

        if (setActive) {
            cg.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cg.GetComponent<RectTransform>());
        }

        if (noAnimate) {
            cg.alpha = targetAlpha;
            cg.blocksRaycasts = setActive;
            if (!setActive) {
                cg.gameObject.SetActive(false);
            }
        } else {
            cg.blocksRaycasts = targetAlpha > 0;

            fadeTween = cg.TFade(targetAlpha, 0.25f, O5Ease.OutCubic, () => {
                if (!setActive) {
                    cg.gameObject.SetActive(false);
                }
            });
        }
    }

#if ML && IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class GridRatioKeeper : MonoBehaviour {

        private RectTransform rectTransform;
        private GridLayoutGroup gridLayout;
        private RectTransform contentRect;

        private int lastScreenWidth;
        private int lastScreenHeight;

        private void Awake() {
            rectTransform = GetComponent<RectTransform>();
            gridLayout = GetComponent<GridLayoutGroup>();
        }

        public void Setup(RectTransform content) {
            contentRect = content;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void Update() {
            if (gridLayout == null || rectTransform == null) {
                return;
            }

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                UpdateGridCellSize(false);
            }
        }

        private void LateUpdate() {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) {
                return;
            }

            UpdateGridCellSize(false);
        }

        public void RefreshNow() => UpdateGridCellSize(true);

        private void UpdateGridCellSize(bool force) {
            if (contentRect == null || gridLayout == null) {
                return;
            }

            float targetWidth = contentRect.rect.width;
            if (targetWidth <= 0) {
                return;
            }

            int columns = Math.Max(1, gridLayout.constraintCount);
            var padding = gridLayout.padding;
            float totalSpacing = gridLayout.spacing.x * (columns - 1);
            float cellWidth = (targetWidth - padding.left - padding.right - totalSpacing) / columns;

            if (cellWidth <= 0) {
                return;
            }

            // Skip work when the grid already matches: this is what heals
            // tiles that were built while the panel was hidden (zero-width)
            // without needing another rebuild trigger.
            if (!force
                && Mathf.Abs(rectTransform.sizeDelta.x - targetWidth) <= 0.5f
                && Mathf.Abs(gridLayout.cellSize.x - cellWidth) <= 0.5f) {
                return;
            }

            rectTransform.sizeDelta = new Vector2(targetWidth, rectTransform.sizeDelta.y);

            float cellHeight = cellWidth / (16f / 9f);

            if (cellWidth > 0 && cellHeight > 0) {
                gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }
    }
}
