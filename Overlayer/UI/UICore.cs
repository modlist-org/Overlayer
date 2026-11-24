using Overlayer.Tween;
using Overlayer.Async;
using Overlayer.Compat;
using O5Kit.Core;
using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.UI.Factory;
using Overlayer.UI.Factory.Page;
using O5Kit.Control;
using O5Kit.Behaviour;
using O5Kit.Factory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using O5Kit.Input;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI;

public enum OriginalMenuState {
    Overlayer,
    Settings,
    Docs,
    Credits,
    Resources,
    JS,
    Easing,
    Tutorial,
}

public static class UICore {
    public static GameObject CanvasObj;
    public static Canvas Canvas;
    public static CanvasScaler CanvasScaler;

    public static readonly Dictionary<int, RectTransform> Pages = [];
    public static int CurrentMenuState = (int)OriginalMenuState.Overlayer;
    public static readonly Vector2 ReferenceResolution = new(1920, 1080);

    private static Action<TranslationFailState> _onPageSettings;
    private static Action<TranslationFailState> _onRefresh;
    private static Action _onPaletteChanged;
    private static Image panelBgImage;
    private static Image menuBgImage;
    private static Image topBarImage;
    private static Image outlineImage;

    public static void Initialize() {
        CanvasObj = new GameObject("OverlayerUICanvas");
        CanvasObj.transform.SetParent(MainCore.Root.transform, false);
        CanvasObj.SetActive(false);

        Canvas = CanvasObj.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 32765;

        CanvasScaler = CanvasObj.AddComponent<CanvasScaler>();
        CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        //canvasScaler.referenceResolution = new(1920, 1080);
        PanelScale = MainCore.Conf.UIScale;
        CanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        CanvasScaler.matchWidthOrHeight = 0.5f;

        CanvasObj.AddComponent<GraphicRaycaster>();
        MainCore.Spr.Shapes.Track(CanvasObj.transform);

        CreatePanel();
        PanelTransition.ApplyOpacity();
        ResizeHandle.CreateResizeHandles(O5KitAdapters.Ctx, Panel, CanvasObj.GetComponent<RectTransform>());
        for (int i = 0; i < Panel.childCount; i++) {
            Transform child = Panel.GetChild(i);
            if (child.GetComponent<ResizeHandle>() == null) {
                continue;
            }

            var resizeDragTrigger = child.gameObject.AddComponent<EventTrigger>();
            UnityUtils.AddEvents(resizeDragTrigger,
                (EventTriggerType.Drag, _ => MarkPanelUnmaximizedByUser())
            );
        }
        O5KitAdapters.Ctx.Tooltip.Initialize(CanvasObj.transform);
        var tooltipTr = CanvasObj.transform.Find("Tooltip");
        if (tooltipTr != null) {
            var tooltipCanvas = tooltipTr.gameObject.AddComponent<Canvas>();
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = 32767;
        }
        RegisterToggleShortcut();
        HudEdit.HudEditor.RegisterShortcut();

        _onPageSettings = state => {
            if (state == TranslationFailState.Success) {
                PageSettings.OnTranslatorLoadEnd();
            }
        };

        _onRefresh = state => {
            if (state == TranslationFailState.Success) {
                TextLocalization.RefreshAll();
            }
        };

        _onPaletteChanged = RefreshTheme;
        MainCore.Tr.OnLoadEnd += _onPageSettings;
        MainCore.Tr.OnLoadEnd += _onRefresh;
        UIColors.OnPaletteChanged += _onPaletteChanged;

        TextLocalization.RefreshAll();

        if (MainCore.Conf.IsFirstRun) {
            // A fresh install starts with the setup wizard.
            MainCore.Conf.IsFirstRun = false;
            MainCore.Conf.TutorialOffered.Value = true;
            MainCore.Conf.SetupPending.Value = true;
            MainCore.ConfMgr.Save();
        }

        if (MainCore.Conf.ShowOnStartup) {
            Open(true);
        }
    }

    private static ITweenHandle uiScaleSequence;

    /// <summary>Eases the window to a UI scale and its default size for that scale.</summary>
    public static void AnimateUiScale(float value) {
        uiScaleSequence?.Kill();

        float scaleStart = PanelScale;

        Vector2 targetSize = DefaultPanelSize;
        LastPanelSize = targetSize;

        uiScaleSequence = O5Seq.New()
            .Append(done =>
                O5KitAdapters.Ctx.Tween.TweenFloat(
                    () => scaleStart,
                    x => PanelScale = x,
                    value,
                    0.4f,
                    done,
                    O5Ease.OutExpo
                )
            )
            .Join(done =>
                Panel.TSizeDelta(targetSize, 0.4f, O5Ease.OutExpo, done)
            ).Play();
    }

    public static RectTransform Panel;
    public static Image CloseImage;
    public const float MENU_WIDTH = 210f;
    public const float MENU_COLLAPSED_WIDTH = 66f;
    public static RectTransform MenuPanel;
    public static RectTransform Menu;
    public static RectTransform MenuContent;
    private static RectTransform Page;
    private static CanvasGroup menuCanvasGroup;
    private static UIScrollController menuScroller;

    public static float PanelScale {
        get;
        set {
            field = value <= 0.1f ? 1f : value;
            CanvasScaler.referenceResolution =
                new Vector2(ReferenceResolution.x, ReferenceResolution.y) / field;
        }
    } = 1f;

    public static float PanelRatio {
        get => CanvasScaler.matchWidthOrHeight;
        set => CanvasScaler.matchWidthOrHeight = value;
    }

    private static void CreatePanel() {
        GameObject panel = new("Panel");
        panel.transform.SetParent(CanvasObj.transform, false);

        {
            var image = panel.AddComponent<Image>();
            image.color = UIColors.PanelBG;
            image.type = Image.Type.Sliced;
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle128P512);
            panelBgImage = image;
        }

        Panel = panel.GetComponent<RectTransform>();
        Panel.anchorMin = new(0.5f, 0.5f);
        Panel.anchorMax = new(0.5f, 0.5f);
        Panel.pivot = new(0.5f, 0.5f);
        Panel.sizeDelta = LastPanelSize = DefaultPanelSize;
        // NOTE: must be zero, NOT Panel.position. position is a world-space value
        // (screen center in canvas units at init time); assigning it to
        // anchoredPosition pushes the panel a full screen off-center and makes it
        // invisible. Whether position is (0,0,0) or (960,540) here depends on game
        // timing, which is why the panel vanished only in some titles.
        LastPanelPosition = Vector2.zero;

        panel.AddComponent<RectMask2D>();

        {
            // Menu Panel
            GameObject menuPanel = new("MenuPanel");
            menuPanel.transform.SetParent(panel.transform, false);

            var menuPanelRect = MenuPanel = menuPanel.AddComponent<RectTransform>();
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
            maskImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle128P512);
            maskImage.raycastTarget = false;

            var mask = menuPanel.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Page = PageFactory.CreatePages(menuPanel);

            // Menu
            GameObject menu = new("Menu");
            menu.transform.SetParent(menuPanel.transform, false);

            Menu = menu.AddComponent<RectTransform>();
            Menu.anchorMin = Vector2.zero;
            Menu.anchorMax = new(0, 1);
            Menu.pivot = new(0, 0.5f);

            float initialMenuWidth = MainCore.Conf.TabLabelMode.Value == IO.TabLabelMode.Show ? MENU_WIDTH : MENU_COLLAPSED_WIDTH;
            Menu.sizeDelta = new(initialMenuWidth, 0);
            Menu.anchoredPosition = new(-initialMenuWidth, 0);

            var image = menu.AddComponent<Image>();
            image.color = UIColors.MenuBG;
            menuBgImage = image;

            menuCanvasGroup = Menu.gameObject.AddComponent<CanvasGroup>();

            // Menu Scroll (scrollbar-less wheel + right-drag scroll region when tabs overflow)
            (_, MenuContent, menuScroller) = O5Factory.ScrollView(
                O5KitAdapters.Ctx,
                Menu,
                spacing: 6f,
                viewportOffsetMin: new Vector2(0f, 60f),
                viewportOffsetMax: new Vector2(0f, -60f),
                contentPadding: new RectOffset(10, 10, 10, 10)
            );
            // Menu items size themselves via RectTransform.sizeDelta.y (48px) without LayoutElement.
            // childScaleHeight lets Tutorial show/hide shrink cleanly in layout.
            var menuLayout = MenuContent.GetComponent<VerticalLayoutGroup>();
            menuLayout.childControlHeight = false;
            menuLayout.childScaleHeight = true;
            menuScroller.enabled = false;

            MenuFactory.CreateMenu(MenuContent);

            GameObject power = new("Power");
            power.transform.SetParent(Menu, false);
            var powerRect = power.AddComponent<RectTransform>();
            powerRect.anchorMin = new Vector2(0f, 0f);
            powerRect.anchorMax = new Vector2(1f, 0f);
            powerRect.offsetMin = Vector2.zero;
            powerRect.offsetMax = Vector2.zero;
            powerRect.sizeDelta = new Vector2(0f, 60f);
            powerRect.pivot = new Vector2(0.5f, 0f);
            var powerBg = power.AddComponent<Image>();
            powerBg.color = MainCore.Conf.Active
                ? new(0, 0, 0, 0.1f)
                : UIColors.SoftRed;
            var btn = power.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            ITweenHandle powerSeq = null;
            btn.onClick.AddListener(
#if ML && IL2CPP
            new Action(() => {
#else
            () => {
#endif
                bool enable = MainCore.Conf.Active.Value = !MainCore.Conf.Active.Value;
                MainCore.SetModEnabled(enable);

                Color target = enable
                    ? new Color(0f, 0f, 0f, 0.1f)
                    : UIColors.SoftRed;

                powerSeq?.Kill();
                powerSeq = powerBg.TColor(target, 0.32f, O5Ease.OutExpo);
#if ML && IL2CPP
            }));
#else
            });
#endif
            var powerIcon = MainCore.Icons.Create(powerRect, "PowerIcon", UIIcon.Power, new(1f, 1f, 1f, 0.6f), 26f);
            RectTransform powerIconRect = powerIcon.rectTransform;
            powerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            powerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            powerIconRect.pivot = new Vector2(0.5f, 0.5f);
            powerIconRect.sizeDelta = new Vector2(26f, 26f);

            GameObject version = new("Version");
            version.transform.SetParent(Menu, false);
            var versionRect = version.AddComponent<RectTransform>();
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = new(1f, 0f);
            versionRect.offsetMin = new(2f, 0f);
            versionRect.offsetMax = Vector2.zero;
            versionRect.pivot = Vector2.zero;
            var versionText = version.AddComponent<TextMeshProUGUI>();
            versionText.text = $"v{MainCore.Version}";
            versionText.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
            versionText.fontSize = 12f;
            versionText.color = new Color(1f, 1f, 1f, 0.4f);
            versionText.characterSpacing = -3f;
            versionText.alignment = TextAlignmentOptions.BottomLeft;
        }

        // Top Bar
        GameObject topBar = new("TopBar");
        topBar.transform.SetParent(panel.transform, false);
        topBar.AddComponent<DragHandler>();
        var topBarClickTrigger = topBar.AddComponent<EventTrigger>();
        UnityUtils.AddEvents(topBarClickTrigger,
            (EventTriggerType.PointerClick, HandleTopBarPointerClick),
            (EventTriggerType.Drag, _ => MarkPanelUnmaximizedByUser())
        );

        var topImage = topBar.AddComponent<Image>();
        topImage.color = UIColors.TopBar;
        topImage.type = Image.Type.Sliced;
        topImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleHalf128P512);
        topBarImage = topImage;

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
            logoImage.sprite = MainCore.Spr.Get(UISprite.OV5LogoOutline128);

            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new(0, 0.5f);
            logoRect.anchorMax = new(0, 0.5f);
            logoRect.pivot = new(0, 0.5f);
            logoRect.anchoredPosition = new((MENU_COLLAPSED_WIDTH - 46f) * 0.5f, 0);
            logoRect.sizeDelta = new(46f, 46f);

            var btn = logo.AddComponent<NonRaycastButton>();
#if ML && IL2CPP
            btn.onClick += new Action(ToggleMenu);
#else
            btn.onClick += ToggleMenu;
#endif
        }

        {
            // Root button
            GameObject close = new("Close");
            close.transform.SetParent(topBar.transform, false);

            var closeRect = close.AddComponent<RectTransform>();
            closeRect.anchorMin = new(1, 0.5f);
            closeRect.anchorMax = new(1, 0.5f);
            closeRect.pivot = new(1, 0.5f);
            closeRect.anchoredPosition = new(-16, 0);
            closeRect.sizeDelta = new(38, 38);

            // Button
            var btn = close.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(
#if ML && IL2CPP
                new Action(() => Close())
#else
                () => Close()
#endif
            );

            // Background circle (hover layer)
            GameObject bg = new("Bg");
            bg.transform.SetParent(close.transform, false);

            CloseImage = bg.AddComponent<Image>();
            CloseImage.sprite = MainCore.Spr.Get(UISprite.Circle128);
            CloseImage.color = new Color(UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 0f);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // X icon (always visible)
            var xIcon = MainCore.Icons.Create(close.transform, "X", UIIcon.X, Color.white);
            RectTransform xRect = xIcon.rectTransform;
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = new(4, 4);
            xRect.offsetMax = new(-4, -4);

            EventTrigger trigger = close.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(
#if ML && IL2CPP
                new Action<BaseEventData>((_) =>
#else
                (_) =>
#endif
                CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 1f)
#if ML && IL2CPP
            ));
#else
            );
#endif

            var exit = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerExit
            };
            exit.callback.AddListener(
#if ML && IL2CPP
                new Action<BaseEventData>((_) =>
#else
                (_) =>
#endif
                CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 0f)
#if ML && IL2CPP
            ));
#else
            );
#endif

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        {
            // Outline
            GameObject outlineObj = new("Outline");
            outlineObj.transform.SetParent(Panel, false);
            outlineObj.transform.SetSiblingIndex(outlineObj.transform.parent.childCount - 1);

            var outline = outlineObj.AddComponent<Image>();
            outline.color = UIColors.WindowOutline;
            outline.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline128O16P512);
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = false;
            outlineImage = outline;

            var outlineRect = outlineObj.GetComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;
        }
    }

    private const string ToggleShortcutId = "overlayer.toggle_panel";
    private const float ToggleHoldSeconds = 0.4f;

    internal static Utility.KeyBind WindowKey => Utility.KeyBind.Parse(MainCore.Conf.WindowKey.Value);

    internal static void RegisterToggleShortcut() {
        WindowKey.Register(ToggleShortcutId, () => Toggle(), ToggleHoldSeconds, () => ResetScalePosition(!isOpen));
    }

    internal static void UnregisterToggleShortcut() => Utility.KeyBind.Unregister(ToggleShortcutId);

    private static ITweenHandle panelTweener;
    private static ITweenHandle resetSequence;

    private static bool isOpen = false;
    public static bool IsOpen => isOpen;
    private static bool isPanelHiddenForHudEditor;
    private static bool isPanelMaximized;
    private static Vector2 preMaximizePanelPosition;
    private static Vector2 preMaximizePanelSize;
    private static float lastTopBarClickTime = float.NegativeInfinity;

    public static Vector2 LastPanelPosition;
    public static Vector2 LastPanelSize;

    private static void HandleTopBarPointerClick(BaseEventData eventData) {
        PointerEventData pointer = UnityUtils.AsPointerEventData(eventData);
        if (pointer == null || pointer.button != PointerEventData.InputButton.Left) {
            return;
        }

        float now = Time.unscaledTime;
        if (now - lastTopBarClickTime <= 0.2f) {
            lastTopBarClickTime = float.NegativeInfinity;
            TogglePanelMaximize();
        } else {
            lastTopBarClickTime = now;
        }
    }

    private static void TogglePanelMaximize() {
        if (Panel == null || CanvasObj == null) {
            return;
        }

        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();
        Vector2 targetPosition;
        Vector2 targetSize;
        if (!isPanelMaximized) {
            preMaximizePanelPosition = Panel.anchoredPosition;
            preMaximizePanelSize = Panel.sizeDelta;
            Canvas.ForceUpdateCanvases();
            var canvasRect = CanvasObj.GetComponent<RectTransform>();
            targetPosition = Vector2.zero;
            targetSize = canvasRect.rect.size;
            isPanelMaximized = true;
        } else {
            targetPosition = preMaximizePanelPosition;
            targetSize = preMaximizePanelSize;
            isPanelMaximized = false;
        }

        resetSequence = O5Seq.New()
            .Append(done => Panel.TAnchorPos(targetPosition, 0.26f, O5Ease.OutExpo, done))
            .Join(done => Panel.TSizeDelta(targetSize, 0.26f, O5Ease.OutExpo, done))
            .Play();
    }

    private static void MarkPanelUnmaximizedByUser() {
        if (!isPanelMaximized) {
            return;
        }

        isPanelMaximized = false;
        resetSequence?.Kill();
    }

    public static Vector2 DefaultPanelSize => new(
        Math.Min(1280f / MainCore.Conf.UIScale, Screen.width / MainCore.Conf.UIScale),
        Math.Min(720f / MainCore.Conf.UIScale, Screen.height / MainCore.Conf.UIScale)
    );

    public static void HandleUpdate() {
        if (CanvasObj == null) {
            return;
        }

        O5ShortcutManager.HandleUpdate();

        // Controls only live under the UI canvas; skip ticking them while it is hidden.
        // One trailing tick after hiding lets input fields release their input block.
        bool active = CanvasObj.activeSelf;
        if (active) {
            UpdateMenuHover();
        }
        if (active || wasCanvasActive) {
            O5Object.TickAll();
        }
        wasCanvasActive = active;
        O5KitAdapters.Ctx.Tooltip.Tick();
    }

    private static bool wasCanvasActive;

    /// <summary>
    /// Keeps the panel at least partially visible: clamps the center offset so a
    /// bad/stale position (e.g. dragged offscreen, or a world-space value saved by
    /// mistake) can never open fully outside the screen. Center anchors: (0,0) = centered.
    /// </summary>
    private static Vector2 ClampPanelPosition(Vector2 pos) {
        try {
            float scale = 1f;
            try {
                if (Canvas != null && Canvas.scaleFactor > 0f) {
                    scale = Canvas.scaleFactor;
                }
            } catch {
                scale = 1f;
            }
            Vector2 size = Panel != null ? Panel.sizeDelta : DefaultPanelSize;
            if (size.x <= 0f || size.y <= 0f) {
                size = DefaultPanelSize;
            }
            const float margin = 80f;
            float limitX = Screen.width * 0.5f / scale + size.x * 0.5f - margin;
            float limitY = Screen.height * 0.5f / scale + size.y * 0.5f - margin;
            pos.x = Mathf.Clamp(pos.x, -Mathf.Max(limitX, 0f), Mathf.Max(limitX, 0f));
            pos.y = Mathf.Clamp(pos.y, -Mathf.Max(limitY, 0f), Mathf.Max(limitY, 0f));
        } catch {
        }
        return pos;
    }

    private static Vector2 GetRandomOffscreenPosition() {
        float scale = Canvas != null && Canvas.scaleFactor > 0f ? Canvas.scaleFactor : 1f;
        float halfW = Screen.width * 0.5f / scale;
        float halfH = Screen.height * 0.5f / scale;

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

    private static EventSystem uiEventSystem;
    private static EventSystem gameEventSystem;

    private static void EnsureEventSystem() {
        if (uiEventSystem != null && !uiEventSystem.Equals(null)) {
            return;
        }
        uiEventSystem = null;
        var esObj = new GameObject("OverlayerEventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<StandaloneInputModule>();
        if (MainCore.Root != null) {
            esObj.transform.SetParent(MainCore.Root.transform, false);
        }
        uiEventSystem = esObj.GetComponent<EventSystem>();
    }

    /// <summary>
    /// Gives our panel a standard Unity input module while open. This title
    /// drives its UI with Rewired's input module, which does not deliver
    /// keyboard/character events to TMP input fields on a foreign canvas
    /// (UniverseLib solves the same problem with its own EventSystem).
    /// </summary>
    private static void AcquireEventSystem() {
        EnsureEventSystem();
        try {
            var cur = EventSystem.current;
            if (cur != null && !cur.Equals(null) && cur != uiEventSystem) {
                gameEventSystem = cur;
            } else if (gameEventSystem != null && gameEventSystem.Equals(null)) {
                gameEventSystem = null;
            }
            if (gameEventSystem == null) {
                foreach (var es in UnityEngine.Object.FindObjectsOfType<EventSystem>()) {
                    if (es != null && !es.Equals(null) && es != uiEventSystem
                        && es.gameObject.name != "OverlayerEventSystem") {
                        gameEventSystem = es;
                        break;
                    }
                }
            }
        } catch {
        }
        try {
            if (gameEventSystem != null && !gameEventSystem.Equals(null)) {
                gameEventSystem.enabled = false;
            }
        } catch {
        }
        try {
            if (uiEventSystem != null && !uiEventSystem.Equals(null)) {
                uiEventSystem.enabled = true;
                EventSystem.current = uiEventSystem;
            }
        } catch {
        }
    }

    private static void ReleaseEventSystem() {
        try {
            if (gameEventSystem != null && !gameEventSystem.Equals(null)) {
                gameEventSystem.enabled = true;
                EventSystem.current = gameEventSystem;
            }
        } catch {
        }
        try {
            if (uiEventSystem != null && !uiEventSystem.Equals(null)) {
                uiEventSystem.enabled = false;
            }
        } catch {
        }
    }

    // Only record the panel's resting position when it is actually visible and not mid-slide off/onscreen.
    private static void SavePanelBoundsIfStable() {
        if (Panel == null || !Panel.gameObject.activeSelf || isPanelHiddenForHudEditor
            || HudEdit.HudEditor.IsOpen || (panelTweener != null && panelTweener.IsAlive) || PanelTransition.IsPlaying) {
            return;
        }
        resetSequence?.Kill(true);
        LastPanelPosition = ClampPanelPosition(Panel.anchoredPosition);
        LastPanelSize = Panel.sizeDelta;
    }

    public static void Open(bool noAnimate = false) {
        if (isOpen) {
            return;
        }

        AcquireEventSystem();

        // Self-heal: never open fully offscreen (e.g. a stale/bad saved position).
        LastPanelPosition = ClampPanelPosition(LastPanelPosition);

        isOpen = true;
        isPanelHiddenForHudEditor = false;
        if (!noAnimate) {
            UISound.PlayOpen();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();

        if (noAnimate) {
            if (Panel != null && !Panel.gameObject.activeSelf) {
                Panel.gameObject.SetActive(true);
            }
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;

            CanvasObj.SetActive(true);
            return;
        }

        if (Panel != null && !Panel.gameObject.activeSelf) {
            Panel.gameObject.SetActive(true);
        }

        Panel.sizeDelta = LastPanelSize;
        IO.PanelFadeMode fadeMode = MainCore.Conf.PanelFadeMode.Value;
        if (fadeMode != IO.PanelFadeMode.FlyIn) {
            Panel.anchoredPosition = LastPanelPosition;
            CanvasObj.SetActive(true);
            PanelTransition.Play(fadeMode, true, LastPanelPosition, null);
            return;
        }

        Panel.anchoredPosition = GetRandomOffscreenPosition();

        CanvasObj.SetActive(true);

        panelTweener = Panel.TAnchorPos(LastPanelPosition, 0.1f, O5Ease.OutExpo);
    }

    public static void Close(bool noAnimate = false) {
        if (!isOpen) {
            return;
        }

        bool wasHiddenForHud = isPanelHiddenForHudEditor;
        SavePanelBoundsIfStable();
        isOpen = false;
        isPanelHiddenForHudEditor = false;
        if (!noAnimate) {
            UISound.PlayClose();
        }

        ReleaseEventSystem();

        CloseImage.color = new Color(
            CloseImage.color.r,
            CloseImage.color.g,
            CloseImage.color.b,
            0f
        );

        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();

        if (wasHiddenForHud && Panel != null) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
        }

        if (noAnimate) {
            if (Panel != null) {
                Panel.gameObject.SetActive(true);
            }
            CanvasObj.SetActive(false);
            return;
        }

        if (Panel != null && !Panel.gameObject.activeSelf) {
            Panel.gameObject.SetActive(true);
        }
        IO.PanelFadeMode fadeMode = MainCore.Conf.PanelFadeMode.Value;
        if (fadeMode != IO.PanelFadeMode.FlyIn) {
            PanelTransition.Play(fadeMode, false, LastPanelPosition, () => {
                if (!isOpen && CanvasObj != null) {
                    CanvasObj.SetActive(false);
                }
            });
            return;
        }

        Vector2 targetPos = GetRandomOffscreenPosition();

        panelTweener = Panel.TAnchorPos(targetPos, 0.1f, O5Ease.OutExpo, () => {
            if (!isOpen && CanvasObj != null) {
                CanvasObj.SetActive(false);
            }
        });
    }

    public static void HidePanelForHudEditor(bool immediate = false) {
        if (Panel == null) {
            return;
        }
        if (!immediate) {
            SavePanelBoundsIfStable();
        }
        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();
        isPanelHiddenForHudEditor = true;
        if (immediate) {
            Panel.gameObject.SetActive(false);
            return;
        }
        IO.PanelFadeMode fadeMode = MainCore.Conf.PanelFadeMode.Value;
        if (fadeMode != IO.PanelFadeMode.FlyIn && Panel.gameObject.activeInHierarchy) {
            PanelTransition.Play(fadeMode, false, LastPanelPosition, () => {
                if (Panel != null && isPanelHiddenForHudEditor) {
                    Panel.gameObject.SetActive(false);
                }
            });
            return;
        }
        Vector2 offscreen = GetRandomOffscreenPosition();
        panelTweener = Panel.TAnchorPos(offscreen, 0.3f, O5Ease.OutExpo, () => {
            if (Panel != null && isPanelHiddenForHudEditor) {
                Panel.gameObject.SetActive(false);
            }
        });
    }

    public static void ShowPanelFromHudEditor(bool immediate = false) {
        if (Panel == null) {
            return;
        }
        isPanelHiddenForHudEditor = false;
        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();
        LastPanelPosition = ClampPanelPosition(LastPanelPosition);
        Panel.gameObject.SetActive(true);
        if (immediate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            return;
        }
        if (!Panel.gameObject.activeInHierarchy) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            return;
        }
        Panel.sizeDelta = LastPanelSize;
        IO.PanelFadeMode fadeMode = MainCore.Conf.PanelFadeMode.Value;
        if (fadeMode != IO.PanelFadeMode.FlyIn) {
            PanelTransition.Play(fadeMode, true, LastPanelPosition, null);
            return;
        }
        if (Vector2.Distance(Panel.anchoredPosition, LastPanelPosition) < 4f) {
            Panel.anchoredPosition = GetRandomOffscreenPosition();
        }
        panelTweener = Panel.TAnchorPos(LastPanelPosition, 0.3f, O5Ease.OutExpo);
    }

    private static int lastToggleFrame = -1;

    public static void Toggle(bool noAnimate = false) {
        // Guard against multiple registered key variants (e.g. BackQuote + Tilde) firing in the same frame.
        if (lastToggleFrame == Time.frameCount) {
            return;
        }
        lastToggleFrame = Time.frameCount;
        if (HudEdit.HudEditor.HandlePanelToggle()) {
            return;
        }
        if (isOpen) {
            Close(noAnimate);
        } else {
            Open(noAnimate);
        }
    }

    public static void ResetScalePosition(bool noAnimate = false) {
        isPanelMaximized = false;
        Vector2 targetSize = DefaultPanelSize;

        LastPanelPosition = Vector2.zero;
        LastPanelSize = targetSize;

        panelTweener?.Kill();
        PanelTransition.Kill();
        resetSequence?.Kill();

        if (noAnimate || isPanelHiddenForHudEditor || (Panel != null && !Panel.gameObject.activeSelf)) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            return;
        }

        resetSequence = O5Seq.New()
            .Append(done => Panel.TAnchorPos(LastPanelPosition, 0.26f, O5Ease.OutExpo, done))
            .Join(done => Panel.TSizeDelta(LastPanelSize, 0.26f, O5Ease.OutExpo, done))
            .Play();
    }

    private static bool isMenuOpen = false;
    private static bool isMenuExpanded = false;
    private static ITweenHandle menuSequence;
    private static ITweenHandle menuHoverSequence;

    public static bool IsMenuOpen => isMenuOpen;

    private static IO.TabLabelMode CurrentTabLabelMode => MainCore.Conf.TabLabelMode.Value;
    private static bool IsPinnedExpanded => CurrentTabLabelMode == IO.TabLabelMode.Show;
    private static float RestingMenuWidth => IsPinnedExpanded ? MENU_WIDTH : MENU_COLLAPSED_WIDTH;

    private static void UpdateMenuHover() {
        if (!isMenuOpen || Menu == null || CurrentTabLabelMode != IO.TabLabelMode.Hover) {
            return;
        }

        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(Menu, O5Input.MousePosition, null);
        if (hovered != isMenuExpanded) {
            SetMenuExpanded(hovered);
        }
    }

    public static void ApplyTabMode(IO.TabLabelMode mode, bool animate = true) {
        if (Menu == null) {
            return;
        }
        menuHoverSequence?.Kill();
        menuSequence?.Kill();

        bool show = mode == IO.TabLabelMode.Show;
        float targetWidth = show ? MENU_WIDTH : MENU_COLLAPSED_WIDTH;
        isMenuExpanded = show;
        MenuFactory.SetLabelsVisible(show, !animate);

        if (!isMenuOpen) {
            Menu.sizeDelta = new(targetWidth, 0f);
            Menu.anchoredPosition = new(-targetWidth, 0f);
            return;
        }

        if (!animate) {
            Menu.sizeDelta = new(targetWidth, 0f);
            if (Page != null) {
                Page.offsetMin = new(targetWidth, 0f);
            }
            return;
        }

        menuHoverSequence = O5Seq.New()
            .Join(done => Menu.TSizeDelta(new Vector2(targetWidth, 0f), 0.28f, O5Ease.OutExpo, done))
            .Join(done => Page.TOffsetMin(new Vector2(targetWidth, 0f), 0.28f, O5Ease.OutExpo, done))
            .Play();
    }

    private static void SetMenuExpanded(bool expanded) {
        isMenuExpanded = expanded;
        menuHoverSequence?.Kill();

        float targetWidth = expanded ? MENU_WIDTH : MENU_COLLAPSED_WIDTH;
        MenuFactory.SetLabelsVisible(expanded);

        menuHoverSequence = O5Seq.New()
            .Join(done => Menu.TSizeDelta(new Vector2(targetWidth, 0f), 0.28f, O5Ease.OutExpo, done))
            .Play();
    }

    public static void OpenMenu() {
        menuSequence?.Kill();
        menuHoverSequence?.Kill();

        isMenuOpen = true;
        bool show = IsPinnedExpanded;
        float width = show ? MENU_WIDTH : MENU_COLLAPSED_WIDTH;
        isMenuExpanded = show;
        UISound.PlayMenuOpen();

        if (menuCanvasGroup.alpha <= 0.01f) {
            Menu.sizeDelta = new(width, 0);
            Menu.anchoredPosition = new(-width, 0);
        }
        MenuFactory.SetLabelsVisible(show, true);
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
        if (menuScroller != null) {
            menuScroller.enabled = true;
        }

        menuSequence = O5Seq.New()
            .Join(done => Menu.TSizeDelta(new Vector2(width, 0), 0.4f, O5Ease.OutExpo, done))
            .Join(done => Menu.TAnchorPos(Vector2.zero, 0.6f, O5Ease.OutExpo, done))
            .Join(done => menuCanvasGroup.TFade(1f, 0.4f, O5Ease.OutSine, done))
            .Join(done => Page.TOffsetMin(new Vector2(width, 0), 0.6f, O5Ease.OutExpo, done))
            .Play();

        isMenuOpen = true;
    }

    public static void CloseMenu() {
        menuSequence?.Kill();
        menuHoverSequence?.Kill();
        isMenuExpanded = false;
        if (!IsPinnedExpanded) {
            MenuFactory.SetLabelsVisible(false, false);
        }

        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;
        if (menuScroller != null) {
            menuScroller.enabled = false;
        }
        if (isMenuOpen) {
            UISound.PlayMenuClose();
        }

        float hideX = -MENU_WIDTH;
        menuSequence = O5Seq.New()
            .Join(done => Menu.TAnchorPos(new Vector2(hideX, 0), 0.4f, O5Ease.OutExpo, done))
            .Join(done => menuCanvasGroup.TFade(0f, 0.3f, O5Ease.OutSine, done))
            .Join(done => Page.TOffsetMin(new Vector2(0, 0), 0.4f, O5Ease.OutExpo, done))
            .Play();

        isMenuOpen = false;
    }

    public static void ToggleMenu() {
        // The setup wizard takes over the window's content.
        if (Setup.SetupWizard.IsOpen) {
            return;
        }
        if (isMenuOpen) {
            CloseMenu();
        } else {
            OpenMenu();
        }
    }

    public static void Dispose() {
        UnregisterToggleShortcut();
        HudEdit.HudEditor.Close();
        HudEdit.HudEditor.UnregisterShortcut();
        PageDocs.Dispose();
        Easing.CurveEditor.Dispose();
        MainCore.Tr.OnLoadEnd -= _onPageSettings;
        MainCore.Tr.OnLoadEnd -= _onRefresh;
        if (_onPaletteChanged != null) {
            UIColors.OnPaletteChanged -= _onPaletteChanged;
            _onPaletteChanged = null;
        }
        panelBgImage = null;
        menuBgImage = null;
        topBarImage = null;
        outlineImage = null;
        O5KitAdapters.Ctx.Tooltip.Dispose();
        try {
            ReleaseEventSystem();
        } catch {
        }
        uiEventSystem = null;
        gameEventSystem = null;
        menuScroller = null;
        UnityEngine.Object.Destroy(CanvasObj);
        CanvasObj = null;
    }

    internal static void RefreshTheme() {
        if (panelBgImage) {
            panelBgImage.color = UIColors.PanelBG;
        }
        if (menuBgImage) {
            menuBgImage.color = UIColors.MenuBG;
        }
        if (topBarImage) {
            topBarImage.color = UIColors.TopBar;
        }
        if (outlineImage) {
            outlineImage.color = UIColors.WindowOutline;
        }
    }

    /// <summary>
    /// Rebuilds the whole settings UI after our root was destroyed out from
    /// under us (scene wipe). Disposes stale state first so event
    /// subscriptions and shortcut registrations don't duplicate.
    /// </summary>
    public static void Reinitialize() {
        bool wasOpen;
        try {
            wasOpen = isOpen;
        } catch {
            wasOpen = true;
        }
        try {
            panelTweener?.Kill();
            PanelTransition.Kill();
        } catch {
        }
        try {
            resetSequence?.Kill();
        } catch {
        }
        try {
            menuSequence?.Kill();
        } catch {
        }
        panelTweener = null;
        resetSequence = null;
        menuSequence = null;

        wasCanvasActive = false;
        isOpen = false;
        isPanelHiddenForHudEditor = false;
        isMenuOpen = false;
        uiEventSystem = null;
        gameEventSystem = null;
        try {
            Dispose();
        } catch (Exception ex) {
            try {
                MainCore.Log.Wrn($"[UICore] Dispose during re-init: {ex.Message}");
            } catch {
            }
        }
        Initialize();
        if (wasOpen && !isOpen) {
            Open(true);
        }
    }
}
