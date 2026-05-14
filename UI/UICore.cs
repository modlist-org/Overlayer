using DG.Tweening;
using Overlayer.UI.UISprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Overlayer.UI;

internal static class UICore {
    private static GameObject canvasObj;
    public static Canvas Canvas { get; private set; }

    public static void Initialize() {
        canvasObj = new GameObject("OverlayerUICanvas");
        canvasObj.transform.SetParent(Core.OverlayerObject.transform, false);
        canvasObj.SetActive(false);

        Canvas = canvasObj.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 32767;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        CreatePanel();
        ResizeHandle.CreateResizeHandles(Panel);
    }

    public static RectTransform Panel;
    public static Image CloseImage;
    public const float MENU_WIDTH = 210f;
    public static RectTransform MenuPanel;
    public static RectTransform Menu;
    private static RectTransform Page;
    private static CanvasGroup menuCanvasGroup;

    private static void CreatePanel() {
        GameObject panel = new("Panel");
        panel.transform.SetParent(canvasObj.transform, false);

        {
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.165f, 0.161f, 0.196f, 1f);
            image.type = Image.Type.Sliced;
            image.sprite = SpriteDatabase.Get(UISliceSprite.Circle256);
        }

        Panel = panel.GetComponent<RectTransform>();
        Panel.anchorMin = new(0.5f, 0.5f);
        Panel.anchorMax = new(0.5f, 0.5f);
        Panel.pivot = new(0.5f, 0.5f);
        Panel.sizeDelta = new(
            Mathf.Min(1280, Screen.width),
            Mathf.Min(720, Screen.height)
        );

        lastPanelPosition = Panel.position;
        lastPanelSize = Panel.sizeDelta;

        panel.AddComponent<RectMask2D>();

        {
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new(2, -2);
        }

        {
            // Menu Panel
            GameObject menuPanel = new("MenuPanel");
            menuPanel.transform.SetParent(panel.transform, false);

            var menuPanelRect = menuPanel.AddComponent<RectTransform>();
            menuPanelRect.anchorMin = Vector2.zero;
            menuPanelRect.anchorMax = new(1, 1);
            menuPanelRect.pivot = new(0.5f, 0.5f);
            menuPanelRect.anchoredPosition = Vector2.zero;
            menuPanelRect.offsetMin = new(1, 1);
            menuPanelRect.offsetMax = new(-1, -1);
            menuPanelRect.sizeDelta = Vector2.zero;

            // Mask
            var maskImage = menuPanel.AddComponent<Image>();
            maskImage.color = Color.white;
            maskImage.type = Image.Type.Sliced;
            maskImage.sprite = SpriteDatabase.Get(UISliceSprite.Circle256);
            maskImage.raycastTarget = false;

            var mask = menuPanel.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Page = MenuFactory.CreatePages(menuPanel);

            // Menu
            GameObject menu = new("Menu");
            menu.transform.SetParent(menuPanel.transform, false);

            Menu = menu.AddComponent<RectTransform>();
            Menu.anchorMin = Vector2.zero;
            Menu.anchorMax = new(0, 1);
            Menu.pivot = new(0, 0.5f);

            Menu.sizeDelta = new(MENU_WIDTH, 0);
            Menu.anchoredPosition = new(-MENU_WIDTH, 0);

            var image = menu.AddComponent<Image>();
            image.color = new Color(0.42f, 0.431f, 0.545f, 1f);

            menuCanvasGroup = Menu.gameObject.AddComponent<CanvasGroup>();

            // Menu Content
            GameObject content = new("Content");
            content.transform.SetParent(Menu, false);

            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new(0, 1);
            contentRect.anchorMax = new(1, 1);
            contentRect.pivot = new(0.5f, 1);

            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new(0, -60);

            // Layout
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            layout.spacing = 0f;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MenuFactory.CreateMenu(content.transform);
        }

        // Top Bar
        GameObject topBar = new("TopBar");
        topBar.transform.SetParent(panel.transform, false);
        topBar.AddComponent<DragHandler>();

        var topImage = topBar.AddComponent<Image>();
        topImage.color = new Color(0.255f, 0.259f, 0.333f, 1f);
        topImage.type = Image.Type.Sliced;
        topImage.sprite = SpriteDatabase.Get(UISliceSprite.CircleHalf256);

        var topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new(0, 1);
        topRect.anchorMax = new(1, 1);
        topRect.offsetMin = new(0, -60);
        topRect.offsetMax = Vector2.zero;
        topRect.pivot = new(0.5f, 1);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new(0, 60);

        {
            // Logo
            GameObject logo = new("Logo");
            logo.transform.SetParent(topBar.transform, false);

            var logoImage = logo.AddComponent<Image>();
            logoImage.sprite = SpriteDatabase.Get(UISprite.OV5LogoOutline256);

            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new(0, 0.5f);
            logoRect.anchorMax = new(0, 0.5f);
            logoRect.pivot = new(0, 0.5f);
            logoRect.anchoredPosition = new(14, 0);
            logoRect.sizeDelta = new(46f, 46f);

            var btn = logo.AddComponent<NonRaycastButton>();
            btn.onClick += ToggleMenu;
        }

        {
            // Root button
            GameObject close = new("Close", typeof(RectTransform));
            close.transform.SetParent(topBar.transform, false);

            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new(1, 0.5f);
            closeRect.anchorMax = new(1, 0.5f);
            closeRect.pivot = new(1, 0.5f);
            closeRect.anchoredPosition = new(-16, 0);
            closeRect.sizeDelta = new(38, 38);

            // Button
            var btn = close.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Close());

            // Background circle (hover layer)
            GameObject bg = new("Bg");
            bg.transform.SetParent(close.transform, false);

            CloseImage = bg.AddComponent<Image>();
            CloseImage.sprite = SpriteDatabase.Get(UISprite.Circle256);
            CloseImage.color = new Color(0.886f, 0.404f, 0.427f, 0f);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // X icon (always visible)
            GameObject xObj = new("X");
            xObj.transform.SetParent(close.transform, false);

            Image xImage = xObj.AddComponent<Image>();
            xImage.sprite = SpriteDatabase.Get(UISprite.X128);

            RectTransform xRect = xObj.GetComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = new(4, 4);
            xRect.offsetMax = new(-4, -4);

            EventTrigger trigger = close.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(_ => CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 1f));

            var exit = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerExit
            };
            exit.callback.AddListener(_ => CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 0f));

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }
    }

    private static float holdStartTime = 0f;
    private static bool holdingToggle = false;

    private static Tweener panelTweener;
    private static Sequence resetSequence;

    private static bool isOpen = false;

    private static Vector2 lastPanelPosition;
    private static Vector2 lastPanelSize;

    private static Vector2 DefaultPanelSize => new(
        Mathf.Min(1280, Screen.width),
        Mathf.Min(720, Screen.height)
    );

    public static void HandleUpdate() {
        bool pressed =
            Input.GetKey(KeyCode.LeftAlt)
            && Input.GetKey(KeyCode.BackQuote);

        // key down
        if(Input.GetKey(KeyCode.LeftAlt)
            && Input.GetKeyDown(KeyCode.BackQuote)) {
            Toggle();

            holdStartTime = Time.unscaledTime;
            holdingToggle = true;
        }

        // hold reset
        if(holdingToggle && pressed) {
            if(Time.unscaledTime - holdStartTime >= 0.4f) {
                ResetScalePosition(!isOpen);
                holdingToggle = false;
            }
        }

        // key up
        if(Input.GetKeyUp(KeyCode.BackQuote)) {
            holdingToggle = false;
        }
    }

    private static Vector2 GetRandomOffscreenPosition() {
        float halfW = Screen.width * 0.5f;
        float halfH = Screen.height * 0.5f;

        int side = Random.Range(0, 4);

        return side switch {
            // Left
            0 => new(
                -halfW - Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),

            // Right
            1 => new(
                halfW + Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),

            // Top
            2 => new(
                Random.Range(-halfW, halfW),
                halfH + Panel.sizeDelta.y
            ),

            // Bottom
            _ => new(
                Random.Range(-halfW, halfW),
                -halfH - Panel.sizeDelta.y
            )
        };
    }

    public static void Open(bool noAnimate = false) {
        if(isOpen) {
            return;
        }

        isOpen = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        panelTweener?.Kill(true);
        resetSequence?.Kill(true);

        if(noAnimate) {
            Panel.anchoredPosition = lastPanelPosition;
            Panel.sizeDelta = lastPanelSize;

            canvasObj.SetActive(true);
            return;
        }

        Vector2 startPos = GetRandomOffscreenPosition();

        Panel.anchoredPosition = startPos;
        Panel.sizeDelta = lastPanelSize;

        canvasObj.SetActive(true);

        panelTweener = Panel
            .DOAnchorPos(lastPanelPosition, 0.1f)
            .SetEase(Ease.OutExpo);
    }

    public static void Close(bool noAnimate = false) {
        if(!isOpen) {
            return;
        }

        isOpen = false;

        lastPanelPosition = Panel.anchoredPosition;
        lastPanelSize = Panel.sizeDelta;

        CloseImage.color = new Color(
            CloseImage.color.r,
            CloseImage.color.g,
            CloseImage.color.b,
            0f
        );

        panelTweener?.Kill(true);
        resetSequence?.Kill(true);

        if(noAnimate) {
            canvasObj.SetActive(false);
            return;
        }

        Vector2 targetPos = GetRandomOffscreenPosition();

        panelTweener = Panel
            .DOAnchorPos(targetPos, 0.1f)
            .SetEase(Ease.OutExpo)
            .OnComplete(() => canvasObj.SetActive(false));
    }

    public static void Toggle(bool noAnimate = false) {
        if(isOpen) {
            Close(noAnimate);
        } else {
            Open(noAnimate);
        }
    }

    public static void ResetScalePosition(bool noAnimate = false) {
        Vector2 targetSize = DefaultPanelSize;

        lastPanelPosition = Vector2.zero;
        lastPanelSize = targetSize;

        panelTweener?.Kill();
        resetSequence?.Kill();

        if(noAnimate) {
            Panel.anchoredPosition = lastPanelPosition;
            Panel.sizeDelta = lastPanelSize;
            return;
        }

        resetSequence = DOTween.Sequence()
            .Join(
                Panel
                    .DOAnchorPos(lastPanelPosition, 0.26f)
                    .SetEase(Ease.OutExpo)
            )
            .Join(
                Panel
                    .DOSizeDelta(lastPanelSize, 0.26f)
                    .SetEase(Ease.OutExpo)
            );
    }

    private static bool isMenuOpen = false;
    private static Sequence menuSequence;

    public static void OpenMenu() {
        menuSequence?.Kill();

        isMenuOpen = true;

        Menu.anchoredPosition = new(-MENU_WIDTH, 0);
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;

        menuSequence = DOTween.Sequence()
            .Join(Menu.DOAnchorPos(Vector2.zero, 0.6f).SetEase(Ease.OutExpo))
            .Join(menuCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutSine))
            .Join(DOTween.To(
                () => Page.offsetMin,
                x => Page.offsetMin = x,
                new(MENU_WIDTH, 0),
                0.6f
            ).SetEase(Ease.OutExpo));

        isMenuOpen = true;
    }

    public static void CloseMenu() {
        menuSequence?.Kill();

        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        menuSequence = DOTween.Sequence()
            .Join(Menu.DOAnchorPos(new(-MENU_WIDTH, 0), 0.4f).SetEase(Ease.OutExpo))
            .Join(menuCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.OutSine))
            .Join(DOTween.To(
                    () => Page.offsetMin,
                    x => Page.offsetMin = x,
                    new(0, 0),
                    0.4f
                ).SetEase(Ease.OutExpo));

        isMenuOpen = false;
    }

    public static void ToggleMenu() {
        if(isMenuOpen) {
            CloseMenu();
        } else {
            OpenMenu();
        }
    }
}