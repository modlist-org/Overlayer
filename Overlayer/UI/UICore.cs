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

    public static void Initialize() {
        CanvasObj = new GameObject("OverlayerUICanvas");
        CanvasObj.transform.SetParent(MainCore.Root.transform, false);
        CanvasObj.SetActive(false);

        Canvas = CanvasObj.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 32767;

        CanvasScaler = CanvasObj.AddComponent<CanvasScaler>();
        CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        //canvasScaler.referenceResolution = new(1920, 1080);
        PanelScale = MainCore.Conf.UIScale;
        CanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        CanvasScaler.matchWidthOrHeight = 0.5f;

        CanvasObj.AddComponent<GraphicRaycaster>();

        CreatePanel();
        ResizeHandle.CreateResizeHandles(O5KitAdapters.Ctx, Panel, CanvasObj.GetComponent<RectTransform>());
        for(int i = 0; i < Panel.childCount; i++) {
            Transform child = Panel.GetChild(i);
            if(child.GetComponent<ResizeHandle>() == null) {
                continue;
            }

            var resizeDragTrigger = child.gameObject.AddComponent<EventTrigger>();
            UnityUtils.AddEvents(resizeDragTrigger,
                (EventTriggerType.Drag, _ => MarkPanelUnmaximizedByUser())
            );
        }
        O5KitAdapters.Ctx.Tooltip.Initialize(CanvasObj.transform);
        RegisterToggleShortcut();

        _onPageSettings = state => {
            if(state == TranslationFailState.Success) {
                PageSettings.OnTranslatorLoadEnd();
            }
        };

        _onRefresh = state => {
            if(state == TranslationFailState.Success) {
                TextLocalization.RefreshAll();
            }
        };

        MainCore.Tr.OnLoadEnd += _onPageSettings;
        MainCore.Tr.OnLoadEnd += _onRefresh;

        TextLocalization.RefreshAll();

        if(MainCore.Conf.IsFirstRun) {
            MakeFirstRunHelper();
        }

        if(MainCore.Conf.ShowOnStartup) {
            Open(true);
        }
    }

    private static bool firstRunHelperActivated = false;
    private static GameObject firstRunCanvasObj;
    private static Image firstRunHelperImage;
    private static TextMeshProUGUI firstRunHelperText;
    private static ITweenHandle firstRunHelperImageSequence;
    private static ITweenHandle secondRunHelperTextSequence;

    private static void MakeFirstRunHelper() {
        Task.Run(async () => {
            await Task.Delay(4000);
            MainThread.Enqueue(() => {
                firstRunHelperActivated = true;

                firstRunCanvasObj = new GameObject("FirstRunHelperCanvas");
                firstRunCanvasObj.transform.SetParent(MainCore.Root.transform, false);

                firstRunCanvasObj.AddComponent<RectTransform>();

                var canvas = firstRunCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767;

                var scaler = firstRunCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                var frh = new GameObject("FirstRunHelper");
                var frhRect = frh.AddComponent<RectTransform>();
                frh.transform.SetParent(firstRunCanvasObj.transform, false);

                frhRect.anchorMin = new Vector2(0f, 0f);
                frhRect.anchorMax = new Vector2(1f, 0f);
                frhRect.pivot = new Vector2(0.5f, 0f);
                frhRect.offsetMin = new Vector2(0f, 0f);
                frhRect.offsetMax = new Vector2(0f, 4f);

                firstRunHelperImage = frh.AddComponent<Image>();
                firstRunHelperImage.raycastTarget = false;
                firstRunHelperImage.color = new Color(1f, 1f, 1f, 0f);

                var frhTextObj = new GameObject("Text");
                var frhTextRect = frhTextObj.AddComponent<RectTransform>();
                frhTextObj.transform.SetParent(frh.transform, false);

                var tmp = frhTextObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 22f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Bottom;
                tmp.text = "";
                tmp.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);

                frhTextRect.anchorMin = new Vector2(0.5f, 0.5f);
                frhTextRect.anchorMax = new Vector2(0.5f, 0.5f);
                frhTextRect.anchoredPosition = new Vector2(0f, 6f);
                frhTextRect.sizeDelta = new Vector2(1000f, 50f);
                frhTextRect.pivot = new Vector2(0.5f, 0f);

                firstRunHelperText = tmp;
                firstRunHelperImageSequence = O5Seq.New()
                    .Append(done => firstRunHelperImage.TAlpha(1.6f, 0.1f, O5Ease.OutSine, done))
                    .Append(done => firstRunHelperImage.TAlpha(0.04f, 1f, O5Ease.OutSine, done))
                    .SetLoops()
                    .Play();

                string fullText = Application.platform == RuntimePlatform.LinuxPlayer
                    ? "Press Ctrl + ` (BackQuote, left of 1 key)"
                    : "Press Alt + ` (BackQuote, left of 1 key)";
                secondRunHelperTextSequence = O5KitAdapters.Ctx.Tween.TweenFloat(
                    () => 0f,
                    x => firstRunHelperText.text = fullText[..(int)x],
                    fullText.Length,
                    1.4f,
                    null,
                    O5Ease.OutSine
                );
            });
        });
    }

    private static void EndFirstRunHelper() {
        MainCore.Conf.IsFirstRun = false;
        MainCore.ConfMgr.Save();

        firstRunHelperImageSequence?.Kill();
        secondRunHelperTextSequence?.Kill();

        firstRunHelperText.text = "";
        const string endText = "Great Job!";

        O5Seq.New()
            .Append(done => firstRunHelperImage.TAlpha(1.0f, 0.2f, O5Ease.OutSine, done))
            .Join(done => O5KitAdapters.Ctx.Tween.TweenFloat(
                () => 0f,
                x => firstRunHelperText.text = endText[..(int)x],
                endText.Length,
                0.8f,
                done,
                O5Ease.Linear
            ))
            .AppendTime(3.0f)
            .Append(done => firstRunHelperImage.TAlpha(0f, 2.0f, O5Ease.Linear, done))
            .Join(done => firstRunHelperText.TAlpha(0f, 2.0f, O5Ease.Linear, done))

            .AppendCallback(() => {
                if(!firstRunCanvasObj) {
                    UnityEngine.Object.Destroy(firstRunCanvasObj);
                }
            })
            .Play();
    }

    public static RectTransform Panel;
    public static Image CloseImage;
    public const float MENU_WIDTH = 210f;
    public static RectTransform MenuPanel;
    public static RectTransform Menu;
    public static RectTransform MenuContent;
    private static RectTransform Page;
    private static CanvasGroup menuCanvasGroup;

    public static float PanelScale {
        get;
        set {
            field = value;
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
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        }

        Panel = panel.GetComponent<RectTransform>();
        Panel.anchorMin = new(0.5f, 0.5f);
        Panel.anchorMax = new(0.5f, 0.5f);
        Panel.pivot = new(0.5f, 0.5f);
        Panel.sizeDelta = LastPanelSize = DefaultPanelSize;
        LastPanelPosition = Panel.position;

        panel.AddComponent<RectMask2D>();

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
            maskImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
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

            Menu.sizeDelta = new(MENU_WIDTH, 0);
            Menu.anchoredPosition = new(-MENU_WIDTH, 0);

            var image = menu.AddComponent<Image>();
            image.color = UIColors.MenuBG;

            menuCanvasGroup = Menu.gameObject.AddComponent<CanvasGroup>();

            // Menu Content
            GameObject content = new("Content");
            content.transform.SetParent(Menu, false);

            MenuContent = content.AddComponent<RectTransform>();
            MenuContent.anchorMin = new(0, 1);
            MenuContent.anchorMax = new(1, 1);
            MenuContent.pivot = new(0.5f, 1);

            MenuContent.offsetMin = Vector2.zero;
            MenuContent.offsetMax = new(0, -60);

            // Layout
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;
            layout.padding = new() {
                left = 0,
                right = 0,
                top = 0,
                bottom = 0
            };

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
            new Action(
#endif
            () => {
                bool enable = MainCore.Conf.Active.Value = !MainCore.Conf.Active.Value;
                MainCore.SetModEnabled(enable);

                Color target = enable
                    ? new Color(0f, 0f, 0f, 0.1f)
                    : UIColors.SoftRed;

                powerSeq?.Kill();
                powerSeq = powerBg.TColor(target, 0.32f, O5Ease.OutExpo);
            })
#if ML && IL2CPP
            );
#else
            ;
#endif
            GameObject powerIcon = new("PowerIcon");
            powerIcon.transform.SetParent(powerRect, false);
            RectTransform powerIconRect = powerIcon.AddComponent<RectTransform>();
            powerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            powerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            powerIconRect.pivot = new Vector2(0.5f, 0.5f);
            powerIconRect.sizeDelta = new Vector2(26f, 26f);
            Image powerIconImage = powerIcon.AddComponent<Image>();
            powerIconImage.sprite = MainCore.Spr.Get(UISprite.Power128);
            powerIconImage.color = new(1f, 1f, 1f, 0.6f);

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
        topImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleHalf256P1024);

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
            logoImage.sprite = MainCore.Spr.Get(UISprite.OV5LogoOutline256);

            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new(0, 0.5f);
            logoRect.anchorMax = new(0, 0.5f);
            logoRect.pivot = new(0, 0.5f);
            logoRect.anchoredPosition = new(14, 0);
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
#if ML && IL2CPP
            btn.onClick.AddListener(new System.Action(() => Close()));
#else
            btn.onClick.AddListener(() => Close());
#endif

            // Background circle (hover layer)
            GameObject bg = new("Bg");
            bg.transform.SetParent(close.transform, false);

            CloseImage = bg.AddComponent<Image>();
            CloseImage.sprite = MainCore.Spr.Get(UISprite.Circle256);
            CloseImage.color = new Color(UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 0f);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // X icon (always visible)
            GameObject xObj = new("X");
            xObj.transform.SetParent(close.transform, false);

            Image xImage = xObj.AddComponent<Image>();
            xImage.sprite = MainCore.Spr.Get(UISprite.X128);

            RectTransform xRect = xObj.GetComponent<RectTransform>();
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
                DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((_) =>
#else
                (_) =>
#endif
                CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 1f)
#if ML && IL2CPP
                ))
#endif
            );

            var exit = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerExit
            };
            exit.callback.AddListener(
#if ML && IL2CPP
                DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((_) =>
#else
                (_) =>
#endif
                CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 0f)
#if ML && IL2CPP
                ))
#endif
            );

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        {
            // Outline
            GameObject outlineObj = new("Outline");
            outlineObj.transform.SetParent(Panel, false);
            outlineObj.transform.SetAsLastSibling();

            var outline = outlineObj.AddComponent<Image>();
            outline.color = Color.white;
            outline.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256O32P1024);
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = false;

            var outlineRect = outlineObj.GetComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;
        }
    }

    private const string ToggleShortcutId = "overlayer.toggle_panel";
    private const float ToggleHoldSeconds = 0.4f;

    private static void RegisterToggleShortcut() {
        O5ShortcutManager.Unregister(ToggleShortcutId);

        KeyCode modifier = Application.platform == RuntimePlatform.LinuxPlayer
            ? KeyCode.LeftControl
            : KeyCode.LeftAlt;

        O5ShortcutManager.Register(
            ToggleShortcutId,
            new O5KeyCombo(O5KitAdapters.Ctx.Config.ToggleKey, modifier),
            ToggleHoldSeconds,
            onPressed: _ => Toggle(),
            onHeld: _ => ResetScalePosition(!isOpen)
        );
    }

    private static ITweenHandle panelTweener;
    private static ITweenHandle resetSequence;

    private static bool isOpen = false;
    private static bool isPanelMaximized;
    private static Vector2 preMaximizePanelPosition;
    private static Vector2 preMaximizePanelSize;
    private static float lastTopBarClickTime = float.NegativeInfinity;

    public static Vector2 LastPanelPosition;
    public static Vector2 LastPanelSize;

    private static void HandleTopBarPointerClick(BaseEventData eventData) {
        PointerEventData pointer =
#if ML && IL2CPP
            eventData.TryCast<PointerEventData>();
#else
            eventData as PointerEventData;
#endif
        if(pointer == null || pointer.button != PointerEventData.InputButton.Left) {
            return;
        }

        float now = Time.unscaledTime;
        if(now - lastTopBarClickTime <= 0.2f) {
            lastTopBarClickTime = float.NegativeInfinity;
            TogglePanelMaximize();
        } else {
            lastTopBarClickTime = now;
        }
    }

    private static void TogglePanelMaximize() {
        if(Panel == null || CanvasObj == null) {
            return;
        }

        panelTweener?.Kill();
        resetSequence?.Kill();
        Vector2 targetPosition;
        Vector2 targetSize;
        if(!isPanelMaximized) {
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
        if(!isPanelMaximized) {
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
        if(CanvasObj == null) {
            return;
        }

        O5ShortcutManager.HandleUpdate();

        // Controls only live under the UI canvas; skip ticking them while it is hidden.
        // One trailing tick after hiding lets input fields release their input block.
        bool active = CanvasObj.activeSelf;
        if(active || wasCanvasActive) {
            O5Object.TickAll();
        }
        wasCanvasActive = active;
        O5KitAdapters.Ctx.Tooltip.Tick();
    }

    private static bool wasCanvasActive;

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

        if(panelTweener != null) {
            panelTweener.Kill(true);
        }

        if(resetSequence != null) {
            resetSequence.Kill(true);
        }

        if(noAnimate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;

            CanvasObj.SetActive(true);
            return;
        }

        Vector2 startPos = GetRandomOffscreenPosition();

        Panel.anchoredPosition = startPos;
        Panel.sizeDelta = LastPanelSize;

        CanvasObj.SetActive(true);

        panelTweener = Panel.TAnchorPos(LastPanelPosition, 0.1f, O5Ease.OutExpo);

        if(firstRunHelperActivated) {
            firstRunHelperActivated = false;
            EndFirstRunHelper();
        }
    }

    public static void Close(bool noAnimate = false) {
        if(!isOpen) {
            return;
        }

        isOpen = false;

        LastPanelPosition = Panel.anchoredPosition;
        LastPanelSize = Panel.sizeDelta;

        CloseImage.color = new Color(
            CloseImage.color.r,
            CloseImage.color.g,
            CloseImage.color.b,
            0f
        );

        if(panelTweener != null) {
            panelTweener.Kill(true);
        }

        if(resetSequence != null) {
            resetSequence.Kill(true);
        }

        if(noAnimate) {
            CanvasObj.SetActive(false);
            return;
        }

        Vector2 targetPos = GetRandomOffscreenPosition();

        panelTweener = Panel.TAnchorPos(targetPos, 0.1f, O5Ease.OutExpo, () => CanvasObj.SetActive(false));
    }

    public static void Toggle(bool noAnimate = false) {
        if(isOpen) {
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
        resetSequence?.Kill();

        if(noAnimate) {
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
    private static ITweenHandle menuSequence;

    public static void OpenMenu() {
        menuSequence?.Kill();

        isMenuOpen = true;

        Menu.anchoredPosition = new(-MENU_WIDTH, 0);
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;

        menuSequence = O5Seq.New()
            .Join(done => Menu.TAnchorPos(Vector2.zero, 0.6f, O5Ease.OutExpo, done))
            .Join(done => menuCanvasGroup.TFade(1f, 0.4f, O5Ease.OutSine, done))
            .Join(done => Page.TOffsetMin(new Vector2(MENU_WIDTH, 0), 0.6f, O5Ease.OutExpo, done))
            .Play();

        isMenuOpen = true;
    }

    public static void CloseMenu() {
        menuSequence?.Kill();

        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        menuSequence = O5Seq.New()
            .Join(done => Menu.TAnchorPos(new Vector2(-MENU_WIDTH, 0), 0.4f, O5Ease.OutExpo, done))
            .Join(done => menuCanvasGroup.TFade(0f, 0.3f, O5Ease.OutSine, done))
            .Join(done => Page.TOffsetMin(new Vector2(0, 0), 0.4f, O5Ease.OutExpo, done))
            .Play();

        isMenuOpen = false;
    }

    public static void ToggleMenu() {
        if(isMenuOpen) {
            CloseMenu();
        } else {
            OpenMenu();
        }
    }

    public static void Dispose() {
        O5ShortcutManager.Unregister(ToggleShortcutId);
        MainCore.Tr.OnLoadEnd -= _onPageSettings;
        MainCore.Tr.OnLoadEnd -= _onRefresh;
        O5KitAdapters.Ctx.Tooltip.Dispose();
        UnityEngine.Object.Destroy(CanvasObj);
        CanvasObj = null;
    }
}
