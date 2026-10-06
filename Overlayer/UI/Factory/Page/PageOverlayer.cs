using Overlayer.Tween;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Async;
using Overlayer.Localization;
using Overlayer.Overlay;
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

        if(!subscribedToCanvases) {
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

        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) {
                ToggleUIStateByMod(grid.transform, isEnabled);
                if(isEnabled) {
                    MainThread.Enqueue(() => {
                        if(MainCore.IsModEnabled) {
                            BuildAllTiles(grid.transform);
                        }
                    });
                }
            }
        };

        if(!MainCore.IsModEnabled) {
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
            if(gridRef == null || !MainCore.IsModEnabled) {
                return;
            }
            BuildAllTiles(gridRef);
        });
    }

    private static void ToggleUIStateByMod(Transform transform, bool isEnabled) {
        if(!isEnabled) {
            settingPage?.Close(true);
            FadeCanvasGroup(viewportCanvasGroup, 1f, true, true);
            if(disabledPanel != null) {
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
        if(transform == null) {
            return;
        }

        ClearAllTiles(transform);

        for(int i = 0; i < OverlayCore.Canvases.Count; i++) {
            var c = OverlayCore.Canvases[i];
            if(!tileMap.TryGetValue(c, out var tile)) {
                tile = CreateCanvasTile(transform, c, () => BuildAllTiles(transform));
                tileMap[c] = tile;
            }
        }

        CreateCanvasActionTile(transform, () => {
            OverlayCore.CreateOvCanvas();
            BuildAllTiles(transform);
        }, () => BeginImportCanvas(transform));

        if(contentRectRef != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);
        }
        transform.GetComponent<GridRatioKeeper>()?.RefreshNow();
    }

    private static void ClearAllTiles(Transform transform) {
        if(transform == null) {
            return;
        }

        foreach(Transform child in transform) {
            if(child.name == "DisabledOverlayPanel") {
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
        O5Effects.HoverOutline(O5KitAdapters.Ctx, bg, tileTrigger);
        AddTileHoverScale(bg, tileTrigger);

        var tileControls = new List<GameObject>();

        var bgOvent = bg.AddComponent<OventHandler>();
        bgOvent.OnClick += btn => {
            switch(btn) {
                case InputButton.Left:
                    if(!canvas.Config.Enabled.Value) {
                        break;
                    }
                    if(PointerOnTileControl(bg, tileControls)) {
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
            exportBtn.OnPressExit();
            exportBtn.OnHoverExit();
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
                if(OverlayCore.CloneCanvas(canvas) != null) {
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
        if(hoverOutline != null) {
            UnityEngine.Object.Destroy(hoverOutline.gameObject);
        }
        var toggleBg = enabledToggle.Rect.GetComponent<Image>();
        if(toggleBg != null) {
            toggleBg.color = Color.clear;
        }
        tileControls.Add(exportRect.gameObject);
        tileControls.Add(cloneRect.gameObject);
        tileControls.Add(toggleGo);
        ApplyTileEnabledVisual(bgImg, txt, canvas.Config.Enabled.Value);

        return bg;
    }

    private static void ApplyTileEnabledVisual(Image bgImg, TextMeshProUGUI nameText, bool enabled) {
        var bg = UIColors.ObjectBG;
        bg.a = enabled ? 1f : 0.4f;
        bgImg.color = bg;
        nameText.color = enabled ? Color.white : UIColors.ObjectInactive;
    }

    private static readonly Dictionary<string, Sprite> tileIconCache = [];

    private static Sprite TileIcon(string fileName, UISprite fallback) {
        if(tileIconCache.TryGetValue(fileName, out var cached)) {
            return cached;
        }
        Sprite sprite = null;
        try {
            string path = Path.Combine(MainCore.Paths.RootPath, "Icons", fileName);
            if(File.Exists(path)) {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if(tex.LoadImage(File.ReadAllBytes(path))) {
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
        if(icon != null) {
            return O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, icon, id, 5f, 34f);
        }
        return O5Factory.Button(O5KitAdapters.Ctx, parent, onClick, fallbackText, id, 34f);
    }

    private static bool PointerOnTileControl(GameObject tile, List<GameObject> controls) {
        var es = EventSystem.current;
        if(es == null || tile == null) {
            return false;
        }
        var ped = new PointerEventData(es) { position = UnityEngine.Input.mousePosition };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        foreach(var h in hits) {
            var go = h.gameObject;
            if(go == null || go == tile) {
                return false;
            }
            foreach(var control in controls) {
                if(go == control || go.transform.IsChildOf(control.transform)) {
                    return true;
                }
            }
            if(go.transform.IsChildOf(tile.transform)) {
                continue;
            }
            return false;
        }
        return false;
    }

    private static GameObject CreateCanvasActionTile(Transform parent, Action create, Action import) {
        var go = new GameObject("CanvasActionsTile");
        go.transform.SetParent(parent, false);
        var root = go.AddComponent<RectTransform>();

        var background = go.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        background.color = UIColors.PanelBG;
        background.raycastTarget = true;

        var left = CreateDiagonalHalf(go.transform, false);
        var right = CreateDiagonalHalf(go.transform, true);
        CreateTileActionIcon(left.transform, BuiltinIcon(UISprite.Plus128), 0.25f);
        CreateTileActionIcon(right.transform, TileIcon("Download128.png", UISprite.Download128), 0.75f);

        var trigger = go.AddComponent<EventTrigger>();
        var handler = go.AddComponent<OventHandler>();
        AddActionHalfHoverScale(handler, root, left, right);
        handler.OnClick += button => {
            if(button != InputButton.Left) {
                return;
            }
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, UnityEngine.Input.mousePosition, null, out var local)) {
                return;
            }
            Rect rect = root.rect;
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            float splitX = Mathf.Lerp(0.28f, 0.72f, y);
            float split = rect.xMin + rect.width * splitX;
            if(local.x < split) {
                create?.Invoke();
            } else {
                import?.Invoke();
            }
        };

        ITweenHandle hover = null;
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => {
                hover?.Kill();
                hover = background.TColor(UIColors.PanelBG, 0.12f, O5Ease.OutSine);
            }),
            (EventTriggerType.PointerExit, () => {
                hover?.Kill();
                left.color = right.color = UIColors.ObjectButton;
                hover = background.TColor(UIColors.PanelBG, 0.12f, O5Ease.OutSine);
            })
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
                    if(target) {
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
        DiagonalTileGraphic left, DiagonalTileGraphic right) {
        left.rectTransform.pivot = new Vector2(0.25f, 0.5f);
        right.rectTransform.pivot = new Vector2(0.75f, 0.5f);
        ITweenHandle leftTween = null;
        ITweenHandle rightTween = null;
        int active = -1;

        void SetScale(Transform target, bool isLeft, float value) {
            if(isLeft) {
                leftTween?.Kill();
            } else {
                rightTween?.Kill();
            }
            var tween = O5KitAdapters.Ctx.Tween.TweenFloat(
                () => target ? target.localScale.x : value,
                v => {
                    if(target) {
                        target.localScale = Vector3.one * v;
                    }
                },
                value,
                0.22f,
                ease: O5Ease.OutExpo
            );
            if(isLeft) {
                leftTween = tween;
            } else {
                rightTween = tween;
            }
        }

        handler.OnHoverUpdate = () => {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, UnityEngine.Input.mousePosition, null, out var local)) {
                return;
            }
            Rect rect = root.rect;
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            float splitX = Mathf.Lerp(0.28f, 0.72f, y);
            int next = local.x < rect.xMin + rect.width * splitX ? 0 : 1;
            if(next == active) {
                return;
            }
            active = next;
            left.color = next == 0 ? UIColors.ObjectActiveLightBright : UIColors.ObjectButton;
            right.color = next == 1 ? UIColors.ObjectActiveLightBright : UIColors.ObjectButton;
            SetScale(left.transform, true, next == 0 ? 1.02f : 1f);
            SetScale(right.transform, false, next == 1 ? 1.02f : 1f);
        };

        void ResetHover() {
            active = -1;
            leftTween?.Kill();
            rightTween?.Kill();
            left.transform.localScale = Vector3.one;
            right.transform.localScale = Vector3.one;
            left.color = right.color = UIColors.ObjectButton;
        }
        handler.OnDisabled += ResetHover;
        var trigger = root.GetComponent<EventTrigger>();
        if(trigger != null) {
            UnityUtils.AddEvents(trigger, (EventTriggerType.PointerExit, ResetHover));
        }
    }

    private static DiagonalTileGraphic CreateDiagonalHalf(Transform parent, bool right) {
        var go = new GameObject(right ? "ImportHalf" : "CreateHalf");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(right ? 0.75f : 0.25f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var graphic = go.AddComponent<DiagonalTileGraphic>();
        graphic.RightHalf = right;
        graphic.color = UIColors.ObjectButton;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void CreateTileActionIcon(Transform parent, Sprite sprite, float anchorX) {
        if(sprite == null) {
            return;
        }
        var go = new GameObject("ActionIcon");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(56f, 56f);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void BeginExportCanvas(OvCanvas canvas) {
        string baseName = string.Join("_", (canvas.Config.Name.Value ?? "Canvas").Split(Path.GetInvalidFileNameChars()));
        if(string.IsNullOrWhiteSpace(baseName)) {
            baseName = "Canvas";
        }
        _ = Overlayer.UI.Utility.NativeDialogThread.Run(() => {
            try {
                string dir = OverlayCore.ExportDir;
                if(!Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                return NativeFileDialog.Extended.NFD.SaveDialog(
                    dir,
                    $"{baseName}.json",
                    new Dictionary<string, string> { ["JSON"] = "json" }
                );
            } catch(Exception e) {
                MainCore.Log.Err($"[CanvasExport] File dialog failed: {e.Message}");
                return null;
            }
        }).ContinueWith(task => {
            MainThread.Enqueue(() => {
                if(!MainCore.IsModEnabled) {
                    return;
                }
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if(string.IsNullOrWhiteSpace(path)) {
                    return;
                }
                OverlayCore.ExportCanvas(canvas, path);
            });
        });
    }

    private static void BeginImportCanvas(Transform grid) {
        _ = Overlayer.UI.Utility.NativeDialogThread.Run(() => {
            try {
                string dir = OverlayCore.ExportDir;
                if(!Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                return NativeFileDialog.Extended.NFD.OpenDialog(
                    dir,
                    new Dictionary<string, string> { ["JSON"] = "json" }
                );
            } catch(Exception e) {
                MainCore.Log.Err($"[CanvasImport] File dialog failed: {e.Message}");
                return null;
            }
        }).ContinueWith(task => {
            MainThread.Enqueue(() => {
                if(!MainCore.IsModEnabled) {
                    return;
                }
                string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if(!string.IsNullOrWhiteSpace(path)) {
                    OverlayCore.ImportCanvas(path);
                }
                BuildAllTiles(grid);
            });
        });
    }

    private static ITweenHandle fadeTween;
    private static void FadeCanvasGroup(CanvasGroup cg, float targetAlpha, bool setActive, bool noAnimate = false) {
        if(cg == null) {
            return;
        }

        fadeTween?.Kill();

        if(setActive) {
            cg.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cg.GetComponent<RectTransform>());
        }

        if(noAnimate) {
            cg.alpha = targetAlpha;
            cg.blocksRaycasts = setActive;
            if(!setActive) {
                cg.gameObject.SetActive(false);
            }
        } else {
            cg.blocksRaycasts = targetAlpha > 0;

            fadeTween = cg.TFade(targetAlpha, 0.25f, O5Ease.OutCubic, () => {
                if(!setActive) {
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
            if(gridLayout == null || rectTransform == null) {
                return;
            }

            if(Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                UpdateGridCellSize(false);
            }
        }

        private void LateUpdate() {
            if(Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) {
                return;
            }

            UpdateGridCellSize(false);
        }

        public void RefreshNow() => UpdateGridCellSize(true);

        private void UpdateGridCellSize(bool force) {
            if(contentRect == null || gridLayout == null) {
                return;
            }

            float targetWidth = contentRect.rect.width;
            if(targetWidth <= 0) {
                return;
            }

            int columns = Math.Max(1, gridLayout.constraintCount);
            var padding = gridLayout.padding;
            float totalSpacing = gridLayout.spacing.x * (columns - 1);
            float cellWidth = (targetWidth - padding.left - padding.right - totalSpacing) / columns;

            if(cellWidth <= 0) {
                return;
            }

            // Skip work when the grid already matches: this is what heals
            // tiles that were built while the panel was hidden (zero-width)
            // without needing another rebuild trigger.
            if(!force
                && Mathf.Abs(rectTransform.sizeDelta.x - targetWidth) <= 0.5f
                && Mathf.Abs(gridLayout.cellSize.x - cellWidth) <= 0.5f) {
                return;
            }

            rectTransform.sizeDelta = new Vector2(targetWidth, rectTransform.sizeDelta.y);

            float cellHeight = cellWidth / (16f / 9f);

            if(cellWidth > 0 && cellHeight > 0) {
                gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }
    }
}
