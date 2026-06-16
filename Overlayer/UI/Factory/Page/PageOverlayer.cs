using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Overlay;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Overlayer.Tween;
using GTweens.Easings;
using GTweens.Tweens;

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

    private static OvCanvasSettingPage settingPage;

    public static void Create(RectTransform parent) {
        MainCore.Log.Msg("Creating Overlayer Page UI...");
        GameObject viewport = new("Viewport");
        viewport.transform.SetParent(parent, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        rootViewport = viewport;
        viewportCanvasGroup = viewport.AddComponent<CanvasGroup>();
        viewport.AddComponent<RectMask2D>();

        GameObject content = new("Content");
        content.transform.SetParent(viewportRect, false);
        contentRectRef = content.AddComponent<RectTransform>();
        contentRectRef.anchorMin = new Vector2(0f, 1f);
        contentRectRef.anchorMax = new Vector2(1f, 1f);
        contentRectRef.pivot = new Vector2(0.5f, 1f);
        contentRectRef.offsetMin = Vector2.zero;
        contentRectRef.offsetMax = Vector2.zero;

        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

        parent.gameObject.AddComponent<UIScrollController>().SetContent(contentRectRef, viewportRect);

        CreateDisabledPanel(viewportRect);

        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);

        settingPage = new OvCanvasSettingPage(parent, () => {
            settingPage.Close();
            FadeCanvasGroup(viewportCanvasGroup, 1f, true);
        });

        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) {
                ToggleUIStateByMod(grid.transform, isEnabled);
            }
        };

        if(!MainCore.IsModEnabled) {
            ToggleUIStateByMod(grid.transform, false);
        }
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
                tile = CreateCanvasTile(transform, c);
                tileMap[c] = tile;
            }
        }

        GameObject addTile = CreateAddTile(transform, () => {
            var canvas = OverlayCore.CreateOvCanvas();
            BuildAllTiles(transform);
        });
        addTile.name = "AddTile";

        if(contentRectRef != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);
        }
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

    static GameObject CreateCanvasTile(Transform parent, OvCanvas canvas) {
        var bg = new GameObject(canvas.Config.Name);
        bg.transform.SetParent(parent, false);

        bg.AddComponent<RectTransform>();

        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.ObjectBG;
        bgImg.raycastTarget = false;

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

        GenerateUI.AddOutlineHover(bg, bg.AddComponent<EventTrigger>());

        GenerateUI.AddButton(bg, btn => {
            switch(btn) {
                case InputButton.Left:
                    FadeCanvasGroup(viewportCanvasGroup, 0f, false);
                    settingPage?.Open(canvas);
                    break;
            }
        });

        return bg;
    }

    static GameObject CreateAddTile(Transform parent, Action onClick) {
        var go = new GameObject("AddTile");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<EmptyGraphic>().raycastTarget = true;

        GameObject bgGo = new("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.ObjectButton;
        bgImg.raycastTarget = false;

        GameObject textGo = new("PlusText");
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var txt = textGo.AddComponent<TextMeshProUGUI>();
        txt.text = "+";
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 60;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        var trigger = go.AddComponent<EventTrigger>();

        GenerateUI.AddOutlineHover(go, trigger);

        GenerateUI.AddButton(go, btn => {
            switch(btn) {
                case InputButton.Left:
                    onClick?.Invoke();
                    break;
            }
        });

        GTween bgTween = null;
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => {
                bgTween?.Kill();
                bgTween = bgImg.GTColor(UIColors.ObjectActiveLightBright, 0.12f)
                    .SetEasing(Easing.OutSine);
                MainCore.TC.Play(bgTween);
            }
        ),
            (EventTriggerType.PointerExit, () => {
                bgTween?.Kill();
                bgTween = bgImg.GTColor(UIColors.ObjectButton, 0.12f)
                    .SetEasing(Easing.OutSine);
                MainCore.TC.Play(bgTween);
            }
        )
        );

        return go;
    }

    private static GTween fadeTween;
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

            fadeTween = cg.GTFade(targetAlpha, 0.25f)
                .SetEasing(Easing.OutCubic)
                .OnComplete(() => {
                    if(!setActive) {
                        cg.gameObject.SetActive(false);
                    }
                });

            MainCore.TC.Play(fadeTween);
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
                UpdateGridCellSize();
            }
        }

        private void LateUpdate() {
            if(Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) {
                return;
            }

            if(contentRect != null && rectTransform.rect.width != contentRect.rect.width) {
                UpdateGridCellSize();
            }
        }

        private void UpdateGridCellSize() {
            if(contentRect == null || gridLayout == null) {
                return;
            }

            float targetWidth = contentRect.rect.width;
            if(targetWidth <= 0) {
                return;
            }

            rectTransform.sizeDelta = new Vector2(targetWidth, rectTransform.sizeDelta.y);
            var padding = gridLayout.padding;
            float totalSpacing = gridLayout.spacing.x * (gridLayout.constraintCount - 1);
            float usableWidth = targetWidth - padding.left - padding.right - totalSpacing;

            float cellWidth = usableWidth / gridLayout.constraintCount;

            float cellHeight = cellWidth / (16f / 9f);

            if(cellWidth > 0 && cellHeight > 0) {
                gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }
    }
}