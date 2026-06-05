using DG.Tweening;
using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Overlay;
using Overlayer.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;

namespace Overlayer.UI.Factory.Page;

internal static class PageOverlayer {
    static readonly Dictionary<OvCanvas, GameObject> tileMap = [];

    private static GameObject rootViewport;
    private static CanvasGroup viewportCanvasGroup;
    private static GameObject disabledPanel;
    private static Transform gridTransform;
    private static RectTransform contentRectRef;

    private static OvCanvasSettingPage settingPage;

    public static void Create(RectTransform parent) {
        GameObject viewport = new("Viewport", typeof(RectTransform), typeof(EmptyGraphic), typeof(RectMask2D), typeof(CanvasGroup));
        viewport.transform.SetParent(parent, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<EmptyGraphic>().raycastTarget = true;
        rootViewport = viewport;
        viewportCanvasGroup = viewport.GetComponent<CanvasGroup>();

        GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewportRect, false);
        contentRectRef = content.GetComponent<RectTransform>();
        contentRectRef.anchorMin = new Vector2(0f, 1f);
        contentRectRef.anchorMax = new Vector2(1f, 1f);
        contentRectRef.pivot = new Vector2(0.5f, 1f);
        contentRectRef.offsetMin = Vector2.zero;
        contentRectRef.offsetMax = Vector2.zero;

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject grid = new("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(contentRectRef, false);
        gridTransform = grid.transform;

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.spacing = new Vector2(18, 18);
        layout.padding = new RectOffset(18, 18, 18, 18);

        var keeper = grid.AddComponent<GridRatioKeeper>();
        keeper.Setup(contentRectRef);

        parent.gameObject.AddComponent<UIScrollController>().SetContent(contentRectRef, viewportRect);

        CreateDisabledPanel(viewportRect);

        settingPage = new OvCanvasSettingPage(parent, () => {
            settingPage.Close();
            FadeCanvasGroup(viewportCanvasGroup, 1f, true);
        });

        MainCore.OnModEnabledChanged += (isEnabled, isDispose) => {
            if(!isDispose) {
                ToggleUIStateByMod(isEnabled);
            }
        };

        if(!MainCore.IsModEnabled) {
            ToggleUIStateByMod(false);
        }
    }

    private static void ToggleUIStateByMod(bool isEnabled) {
        if(!isEnabled) {
            settingPage?.Close(true);
            FadeCanvasGroup(viewportCanvasGroup, 1f, true, true);
            if(disabledPanel != null) {
                disabledPanel.SetActive(true);
                disabledPanel.transform.SetAsLastSibling();
            }
            ClearAllTiles();
            return;
        }

        disabledPanel?.SetActive(false);
        BuildAllTiles();
    }

    private static void BuildAllTiles() {
        if(gridTransform == null) {
            return;
        }

        ClearAllTiles();

        for(int i = 0; i < OverlayCore.Canvases.Count; i++) {
            var c = OverlayCore.Canvases[i];
            if(!tileMap.TryGetValue(c, out var tile)) {
                tile = CreateCanvasTile(gridTransform, c);
                tileMap[c] = tile;
            }
        }

        GameObject addTile = CreateAddTile(gridTransform, () => {
            var canvas = OverlayCore.CreateOvCanvas();
            BuildAllTiles();
        });
        addTile.name = "AddTile";

        if(contentRectRef != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectRef);
        }
    }

    private static void ClearAllTiles() {
        if(gridTransform == null) {
            return;
        }

        foreach(Transform child in gridTransform) {
            if(child.name == "DisabledOverlayPanel") {
                continue;
            }

            UnityEngine.Object.Destroy(child.gameObject);
        }

        tileMap.Clear();
    }

    private static void CreateDisabledPanel(RectTransform parent) {
        disabledPanel = new GameObject("DisabledOverlayPanel", typeof(RectTransform), typeof(Image));
        disabledPanel.transform.SetParent(parent, false);

        var rect = disabledPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = disabledPanel.GetComponent<Image>();
        img.color = UIColors.PanelBG;
        img.raycastTarget = true;

        GameObject textGo = new("DisabledMessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(disabledPanel.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        var txt = textGo.GetComponent<TextMeshProUGUI>();
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
        var bg = new GameObject(canvas.Config.Name, typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);

        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.ObjectBG;
        bgImg.raycastTarget = false;

        GameObject textGo = new("CanvasNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bg.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 20);
        textRect.offsetMax = new Vector2(-20, -20);

        var txt = textGo.GetComponent<TextMeshProUGUI>();
        txt.text = string.IsNullOrEmpty(canvas.Config.Name) ? "(Empty)" : canvas.Config.Name;
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 22;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        GenerateUI.AddButton(bg, btn => {
            switch(btn) {
                case InputButton.Left:
                    FadeCanvasGroup(viewportCanvasGroup, 0f, false);
                    settingPage?.Open(canvas);
                    break;
            }
        }, outline: true);

        return bg;
    }

    static GameObject CreateAddTile(Transform parent, Action onClick) {
        var go = new GameObject("AddTile", typeof(RectTransform), typeof(EmptyGraphic));
        go.transform.SetParent(parent, false);
        go.GetComponent<EmptyGraphic>().raycastTarget = true;

        GameObject bgGo = new("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UIColors.ObjectButton;
        bgImg.raycastTarget = false;

        GameObject textGo = new("PlusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var txt = textGo.GetComponent<TextMeshProUGUI>();
        txt.text = "+";
        txt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        txt.fontSize = 60;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;

        GenerateUI.AddButton(go, btn => {
            switch(btn) {
                case InputButton.Left:
                    onClick?.Invoke();
                    break;
            }
        }, false);

        EventTrigger trigger = go.GetComponent<EventTrigger>();

        UnityUtils.AddEvent(EventTriggerType.PointerEnter, e => {
            bgImg.DOKill();
            bgImg.DOColor(UIColors.ObjectActiveLightBright, 0.12f)
                 .SetEase(Ease.OutSine)
                 .SetUpdate(true);
        }, trigger);

        UnityUtils.AddEvent(EventTriggerType.PointerExit, e => {
            bgImg.DOKill();
            bgImg.DOColor(UIColors.ObjectButton, 0.12f)
                 .SetEase(Ease.OutSine)
                 .SetUpdate(true);
        }, trigger);

        return go;
    }
    private static void FadeCanvasGroup(CanvasGroup cg, float targetAlpha, bool setActive, bool noAnimate = false) {
        if(cg == null) {
            return;
        }

        if(setActive) {
            cg.gameObject.SetActive(true);
        }

        cg.DOKill();

        if(noAnimate) {
            cg.alpha = targetAlpha;
            cg.blocksRaycasts = setActive;
            if(!setActive) {
                cg.gameObject.SetActive(false);
            }
        } else {
            bool shouldBlock = targetAlpha > 0;

            cg.blocksRaycasts = shouldBlock;
            cg.DOFade(targetAlpha, 0.25f).SetUpdate(true)
              .SetEase(Ease.OutCubic)
              .OnComplete(() => {
                  cg.blocksRaycasts = setActive;
                  if(!setActive) {
                      cg.gameObject.SetActive(false);
                  }
              });
        }
    }

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