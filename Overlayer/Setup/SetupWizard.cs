using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.ModuleAPI;
using Overlayer.UI;
using Overlayer.UI.Factory.Page;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Setup;

// The first-run wizard: fills the Overlayer window under its top bar (a fresh install opens the
// window for it) and walks through language, UI scale and keys, one page at a time. The last
// page suggests the tutorial. Settings can open it again.
// Motion follows osu!lazer's first-run wizard (WizardOverlay / WizardScreen): pages slide 100 units
// over 500 ms with OutQuint and fade in 100 ms, the content settles from 0.99 scale over 400 ms.
internal static class SetupWizard {
    private enum Step { Welcome, Scale, Keys, Finish }

    private const float TopBarHeight = 60f;
    private const float MaxColumnWidth = 900f;
    private const float ColumnMargin = 32f;
    private const float StartDelay = 4f;
    private const float KeyUnit = 40f;
    private const float KeyGap = 4f;

    private const float SlideOffset = 100f;
    private const float SlideSeconds = 0.5f;
    private const float FadeSeconds = 0.1f;
    private const float OpenSeconds = 0.4f;
    private const float CloseFadeSeconds = 0.5f;
    private const float CloseScaleSeconds = 0.4f;
    private const float RestingScale = 0.99f;

    private static Color Accent => UIColors.ObjectActive;
    private static readonly Color KeyColor = new(1f, 1f, 1f, 0.07f);

    private static readonly int StepCount = Enum.GetValues(typeof(Step)).Length;

    // One step's content. Leaving pages stay around until they've faded out.
    private sealed class Page {
        public RectTransform Rect, Host;
        public CanvasGroup Group;
        public TextMeshProUGUI Title, Say;
        public readonly List<O5Kit.Core.O5Object> Controls = [];
        public float Time, FromX, ToX, FromAlpha, ToAlpha = 1f;
    }

    private static RectTransform root, card, column, stage, keyboard;
    private static CanvasGroup rootGroup;
    private static TextMeshProUGUI caption;
    private static O5Button backButton, skipButton, nextButton;
    private static readonly List<O5Kit.Core.O5Object> frameControls = [];
    private static readonly List<Image> litKeys = [];
    private static Page page;
    private static readonly List<Page> leaving = [];
    private static float openTime;

    // The closing card keeps animating after the wizard itself is done.
    private static RectTransform closingRoot, closingCard;
    private static CanvasGroup closingGroup;
    private static float closingTime, closingAlpha, closingScale;
    private static readonly List<O5Kit.Core.O5Object> closingControls = [];

    private static RectTransform stepHost => page.Host;

    private static Step step;
    private static bool showHudKey;
    private static float pendingSince = -1f;

    public static bool IsOpen => root != null;

    /// <summary>Set by <see cref="SetupIntro"/>: keeps the opened wizard invisible (its open
    /// animation waits) until the intro's title and logo have landed.</summary>
    internal static bool HoldReveal;

    /// <summary>Ends <see cref="HoldReveal"/> with the wizard fully opaque at once: the intro's
    /// backdrop fade then reveals it, so the page under the wizard never shows through.</summary>
    internal static void RevealNow() {
        HoldReveal = false;
        openTime = OpenSeconds;
    }

    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    private static float OutQuint(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 5f);

    // ---- opening and closing ----

    public static void Open() {
        if (IsOpen || UICore.CanvasObj == null || HudEditor.IsOpen) {
            return;
        }
        Tutorial.Tutorial.Stop();
        FinishClosing();
        if (UICore.IsMenuOpen) {
            UICore.CloseMenu();
        }
        showHudKey = false;
        step = Step.Welcome;
        openTime = 0f;
        Build();
        Show(Step.Welcome);
        UISound.PlayOpen();
    }

    /// <summary>For <see cref="SetupIntro"/>: the current page title and where its top-left
    /// corner rests once the page has finished sliding in (world space).</summary>
    internal static bool TryGetTitleRest(out TextMeshProUGUI title, out Vector3 restTopLeft) {
        title = null;
        restTopLeft = Vector3.zero;
        if (page == null || page.Title == null || column == null || card == null) {
            return false;
        }
        title = page.Title;
        RectTransform rt = title.rectTransform;
        Rect r = rt.rect;
        Vector3 topLeft = rt.TransformPoint(new Vector3(r.xMin, r.yMax, 0f));
        // Undo the page's current slide offset and the card's opening scale (it rests at 1),
        // otherwise the spot lands slightly toward the card's center.
        Vector3 slide = page.Rect.parent.TransformVector(new Vector3(page.Rect.anchoredPosition.x, 0f, 0f));
        Vector3 center = card.TransformPoint(card.rect.center);
        float cardScale = Mathf.Max(0.0001f, card.localScale.x);
        restTopLeft = center + (topLeft - slide - center) / cardScale;
        return true;
    }

    /// <summary>Current opening scale of the wizard content (1 once open).</summary>
    internal static float ContentScale => card != null ? card.localScale.x : 1f;

    /// <summary>Settles the wizard's layout right away (the intro measures it on the first frame).</summary>
    internal static void ForceLayout() {
        if (column != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
        }
        if (page != null && page.Host != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(page.Host);
        }
    }

    private static void Close(bool startTutorial) {
        KeyCapture.Cancel();
        MainCore.Conf.SetupPending.Value = false;
        BeginClosing();
        PageSettings.SyncValues();
        if (startTutorial) {
            Tutorial.Tutorial.Continue();
        }
        MainCore.ConfMgr.RequestSave();
    }

    private static void Teardown() {
        DropPage(page);
        page = null;
        foreach (var old in leaving) {
            DropPage(old);
        }
        leaving.Clear();
        foreach (var control in frameControls) {
            control.Dispose();
        }
        frameControls.Clear();
        if (root != null) {
            UnityEngine.Object.Destroy(root.gameObject);
        }
        root = card = column = stage = keyboard = null;
    }

    // Hands the view over to the closing animation; the wizard counts as closed from here on.
    private static void BeginClosing() {
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        closingControls.AddRange(frameControls);
        frameControls.Clear();
        foreach (var old in leaving.Append(page)) {
            closingControls.AddRange(old.Controls);
        }
        leaving.Clear();
        page = null;
        litKeys.Clear();
        closingRoot = root;
        closingCard = card;
        closingGroup = rootGroup;
        closingTime = 0f;
        closingAlpha = rootGroup.alpha;
        closingScale = card.localScale.x;
        root = card = column = stage = keyboard = null;
    }

    private static void TickClosing(float dt) {
        if (closingRoot == null) {
            if (closingControls.Count > 0) {
                FinishClosing();
            }
            return;
        }
        closingTime += dt;
        closingGroup.alpha = Mathf.Lerp(closingAlpha, 0f, OutQuint(closingTime / CloseFadeSeconds));
        float scale = Mathf.Lerp(closingScale, RestingScale, OutQuint(closingTime / CloseScaleSeconds));
        closingCard.localScale = new Vector3(scale, scale, 1f);
        if (closingTime >= CloseFadeSeconds) {
            FinishClosing();
        }
    }

    private static void FinishClosing() {
        foreach (var control in closingControls) {
            control.Dispose();
        }
        closingControls.Clear();
        if (closingRoot != null) {
            UnityEngine.Object.Destroy(closingRoot.gameObject);
        }
        closingRoot = closingCard = null;
    }

    private static void TickPending() {
        if (!MainCore.Conf.SetupPending.Value || !MainCore.IsModEnabled || UICore.CanvasObj == null) {
            return;
        }
        // Lets the game finish starting up first.
        if (pendingSince < 0f) {
            pendingSince = Time.realtimeSinceStartup;
        }
        if (Time.realtimeSinceStartup - pendingSince < StartDelay || PlaybackState.IsPlaying
            || HudEditor.IsOpen || Tutorial.Tutorial.IsRunning || SetupIntro.IsPlaying) {
            return;
        }
        // The intro opens the window, plays, then hands over to Open().
        SetupIntro.Play();
    }

    public static void Tick() {
        TickClosing(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        if (root == null) {
            // Destroyed along with the window layer.
            if (frameControls.Count > 0) {
                Teardown();
            }
            TickPending();
            return;
        }
        if (!root.gameObject.activeInHierarchy) {
            return;
        }
        if (root.GetSiblingIndex() != root.parent.childCount - 1) {
            root.SetAsLastSibling();
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        openTime = HoldReveal ? 0f : openTime + dt;
        float opened = OutQuint(openTime / OpenSeconds);
        rootGroup.alpha = opened;
        float cardScale = Mathf.Lerp(RestingScale, 1f, opened);
        card.localScale = new Vector3(cardScale, cardScale, 1f);
        // The window can be resized at any time.
        column.sizeDelta = new Vector2(Mathf.Min(MaxColumnWidth, root.rect.width - ColumnMargin * 2f), column.sizeDelta.y);
        if (keyboard != null) {
            float fit = Mathf.Min(1f, page.Host.rect.width / (KeyUnit * 15f));
            keyboard.localScale = new Vector3(fit, fit, 1f);
        }
        Advance(page, dt);
        for (int i = leaving.Count - 1; i >= 0; i--) {
            Advance(leaving[i], dt);
            if (leaving[i].Time >= FadeSeconds) {
                DropPage(leaving[i]);
                leaving.RemoveAt(i);
            }
        }
        // The keys to press pulse on the keyboard.
        float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f));
        foreach (var key in litKeys) {
            key.color = new Color(Accent.r, Accent.g, Accent.b, pulse);
        }
    }

    // ---- frame ----

    private static void Build() {
        var ctx = O5KitAdapters.Ctx;
        root = UiStyle.NewRect("OverlayerSetup", UICore.MenuPanel);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = new Vector2(0f, -TopBarHeight);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        // Covers the window's pages and keeps clicks away from them.
        var background = root.gameObject.AddComponent<Image>();
        background.color = UIColors.PanelBG;
        background.raycastTarget = true;
        // Sliding pages stay inside the window.
        root.gameObject.AddComponent<RectMask2D>();

        card = UiStyle.NewRect("Content", root);
        card.anchorMin = Vector2.zero;
        card.anchorMax = Vector2.one;
        card.offsetMin = card.offsetMax = Vector2.zero;
        card.localScale = new Vector3(RestingScale, RestingScale, 1f);

        column = UiStyle.NewRect("Column", card);
        column.anchorMin = new Vector2(0.5f, 0f);
        column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.anchoredPosition = Vector2.zero;
        column.sizeDelta = new Vector2(MaxColumnWidth, -48f);
        var layout = UiStyle.Vertical(column.gameObject, 8f);
        layout.childForceExpandHeight = false;

        caption = FrameText(ctx, column, 22f, 13f, UiStyle.Caption);
        caption.characterSpacing = -3f;

        stage = UiStyle.NewRect("Stage", column);
        stage.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var bar = UiStyle.HRow(column, 8f, 44f);
        backButton = Frame(UiStyle.Button(UiStyle.Cell(bar, 110f), "setup_back", string.Empty, () => Show(step - 1)));
        UiStyle.Cell(bar, 0f);
        skipButton = Frame(UiStyle.Button(UiStyle.Cell(bar, 130f), "setup_skip", string.Empty, () => Show(Step.Finish)));
        nextButton = Frame(UiStyle.Button(UiStyle.Cell(bar, 150f), "setup_next", string.Empty, () => Show(step + 1), primary: true));
    }

    private static T2 Frame<T2>(T2 control) where T2 : O5Kit.Core.O5Object {
        frameControls.Add(control);
        return control;
    }

    private static TextMeshProUGUI FrameText(O5Kit.Core.O5Context ctx, Transform parent, float height, float size, Color color) {
        var text = O5Factory.ControlText(ctx, O5Factory.Row(ctx, parent, height), size);
        // Lines up with the buttons under it.
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.richText = true;
        text.raycastTarget = false;
        text.color = color;
        return text;
    }

    private static TextMeshProUGUI Note(string text, float height, float size = 16f) {
        var note = FrameText(O5KitAdapters.Ctx, stepHost, height, size, UiStyle.Muted);
        note.text = text;
        return note;
    }

    private static T2 Track<T2>(T2 control) where T2 : O5Kit.Core.O5Object {
        page.Controls.Add(control);
        return control;
    }

    // ---- pages ----

    private static Page NewPage() {
        var ctx = O5KitAdapters.Ctx;
        var made = new Page { Rect = UiStyle.NewRect("Page", stage) };
        made.Rect.anchorMin = Vector2.zero;
        made.Rect.anchorMax = Vector2.one;
        made.Rect.offsetMin = made.Rect.offsetMax = Vector2.zero;
        made.Group = made.Rect.gameObject.AddComponent<CanvasGroup>();
        // Scrolls when the window is small or the UI scale is high.
        var (_, content, _) = O5Factory.ScrollView(ctx, made.Rect, 8f);
        made.Host = content;
        made.Title = FrameText(ctx, content, 44f, 30f, Color.white);
        made.Title.font = ctx.Fonts.Medium;
        made.Say = FrameText(ctx, content, 64f, 18f, new Color(1f, 1f, 1f, 0.82f));
        made.Say.lineSpacing = 6f;
        return made;
    }

    private static void Advance(Page target, float dt) {
        target.Time += dt;
        float x = Mathf.Lerp(target.FromX, target.ToX, OutQuint(target.Time / SlideSeconds));
        target.Rect.anchoredPosition = new Vector2(x, 0f);
        target.Group.alpha = Mathf.Lerp(target.FromAlpha, target.ToAlpha, Mathf.Clamp01(target.Time / FadeSeconds));
    }

    private static void Leave(Page target, float toX) {
        target.Time = 0f;
        target.FromX = target.Rect.anchoredPosition.x;
        target.ToX = toX;
        target.FromAlpha = target.Group.alpha;
        target.ToAlpha = 0f;
        target.Group.interactable = false;
        target.Group.blocksRaycasts = false;
        leaving.Add(target);
    }

    private static void DropPage(Page target) {
        if (target == null) {
            return;
        }
        foreach (var control in target.Controls) {
            control.Dispose();
        }
        target.Controls.Clear();
        if (target.Rect != null) {
            target.Rect.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(target.Rect.gameObject);
        }
    }

    // animate: going to another step. Without it the page is rebuilt where it is (a choice was made on it).
    private static void Show(Step next, bool animate = true) {
        KeyCapture.Cancel();
        float direction = next >= step ? 1f : -1f;
        step = next;
        litKeys.Clear();
        keyboard = null;
        if (page != null) {
            if (animate) {
                Leave(page, -direction * SlideOffset);
            } else {
                DropPage(page);
            }
        }
        page = NewPage();
        if (animate) {
            page.FromX = direction * SlideOffset;
            page.FromAlpha = 0f;
            page.Rect.anchoredPosition = new Vector2(page.FromX, 0f);
            page.Group.alpha = 0f;
        } else {
            page.Time = SlideSeconds;
            page.FromAlpha = 1f;
        }
        caption.text = string.Format(T("SETUP_STEP", "{0} / {1}"), (int)step + 1, StepCount);
        backButton.Text = T("SETUP_BACK", "Back");
        skipButton.Text = T("SETUP_SKIP", "Skip");
        nextButton.Text = T("SETUP_NEXT", "Next");
        backButton.Rect.parent.gameObject.SetActive(step != Step.Welcome);
        bool last = step == Step.Finish;
        skipButton.Rect.parent.gameObject.SetActive(!last);
        nextButton.Rect.parent.gameObject.SetActive(!last);
        switch (step) {
            case Step.Welcome: ShowWelcome(); break;
            case Step.Scale: ShowScale(); break;
            case Step.Keys: ShowKeys(); break;
            default: ShowFinish(); break;
        }
    }

    private static void Header(string key, string heading, string line) {
        page.Title.text = T($"SETUP_{key}_TITLE", heading);
        page.Say.text = T($"SETUP_{key}", line);
    }

    private static void ShowWelcome() {
        Header("WELCOME", "Welcome to Overlayer", "Select a language.");
        var ctx = O5KitAdapters.Ctx;
        foreach (string language in MainCore.Tr.GetLanguages().Where(l => l != Translator.FALLBACK_LANGUAGE).OrderBy(l => l, StringComparer.OrdinalIgnoreCase)) {
            string captured = language;
            string native = MainCore.Tr.GetForLanguage("0NATIVELANG", language, language);
            Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 46f), "setup_lang_" + language, native, () => PickLanguage(captured),
                primary: language == MainCore.Tr.Language));
        }
    }

    private static void PickLanguage(string language) {
        MainCore.Tr.Language = language;
        MainCore.Conf.Language = language;
        MainCore.ConfMgr.RequestSave();
        TextLocalization.RefreshAll();
        PageSettings.SyncValues();
        Show(Step.Welcome, false);
    }

    private static void ShowScale() {
        // Named like the setting itself.
        Header("SCALE", T("UI_SCALE", "UI Scale"), "Adjust the UI to a comfortable scale.");
        var ctx = O5KitAdapters.Ctx;
        var slider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, stepHost), "setup_scale", new O5SliderOptions {
            DefaultValue = 1f,
            Min = 0.8f,
            Max = 1.6f,
            Value = MainCore.Conf.UIScale,
            Format = "0.00x",
            Label = T("UI_SCALE", "UI Scale"),
            ClampDrag = true,
            ClampInput = true,
            DragFilter = value => Math.Round(value, 2),
            InputFilter = value => Math.Round(value, 2),
        }));
        slider.OnComplete = value => {
            MainCore.Conf.UIScale = (float)value;
            O5KitAdapters.SyncConfig();
            MainCore.ConfMgr.RequestSave();
            UICore.AnimateUiScale((float)value);
            PageSettings.SyncValues();
        };
    }

    private static void ShowKeys() {
        Header("KEYS", "Essential shortcuts", "Press the highlighted keys together.");
        var ctx = O5KitAdapters.Ctx;
        var tabs = UiStyle.HRow(stepHost, 8f, 44f);
        Track(UiStyle.Button(UiStyle.Cell(tabs, 0f), "setup_key_window", $"{T("SETUP_KEY_WINDOW", "Overlayer window")}:  <b>{UICore.WindowKey.ToDisplayString()}</b>",
            () => ShowKeyOf(false), primary: !showHudKey));
        Track(UiStyle.Button(UiStyle.Cell(tabs, 0f), "setup_key_hud", $"{T("SETUP_KEY_HUD", "HUD editor")}:  <b>{HudEditor.Key.ToDisplayString()}</b>",
            () => ShowKeyOf(true), primary: showHudKey));

        BuildKeyboard(showHudKey ? HudEditor.Key : UICore.WindowKey);

        O5Button change = null;
        string Caption() => T("SETUP_KEY_CHANGE", "Change key");
        change = Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_key_change", Caption(), () => KeyCapture.Begin(
            () => change.Text = T("SETUP_KEY_PRESS", "Press a key...   (Esc: cancel)"),
            bind => {
                if (bind is { IsNone: false } b) {
                    if (showHudKey) {
                        MainCore.Conf.HudEditorKey = b.ToString();
                        HudEditor.RegisterShortcut();
                    } else {
                        MainCore.Conf.WindowKey = b.ToString();
                        UICore.RegisterToggleShortcut();
                    }
                    MainCore.ConfMgr.RequestSave();
                    PageSettings.SyncValues();
                    UI.Factory.Page.PageDocs.RefreshHotkeys();
                    Tutorial.Tutorial.RefreshTexts();
                    Show(Step.Keys, false);
                } else if (root != null && step == Step.Keys) {
                    change.Text = Caption();
                }
            })));
    }

    private static void ShowKeyOf(bool hudKey) {
        showHudKey = hudKey;
        Show(Step.Keys, false);
    }

    // ---- keyboard ----

    private readonly struct KeyCap(string label, float width, KeyCode code) {
        public readonly string Label = label;
        public readonly float Width = width;
        public readonly KeyCode Code = code;
    }

    private static KeyCap Cap(string label, KeyCode code, float width = 1f) => new(label, width, code);

    private static KeyCap Gap(float width) => new(null, width, KeyCode.None);

    private static IEnumerable<KeyCap> Letters(string letters) => letters.Select(c => {
        string name = c.ToString();
        return Cap(name, (KeyCode)Enum.Parse(typeof(KeyCode), char.IsDigit(c) ? "Alpha" + name : name));
    });

    private static IEnumerable<KeyCap> FKeys(int from, int to) => Enumerable.Range(from, to - from + 1)
        .Select(n => Cap("F" + n, (KeyCode)Enum.Parse(typeof(KeyCode), "F" + n)));

    private static readonly KeyCap[][] KeyRows = [
        [Cap("Esc", KeyCode.Escape), Gap(1f), .. FKeys(1, 4), Gap(0.5f), .. FKeys(5, 8), Gap(0.5f), .. FKeys(9, 12)],
        [Cap("`", KeyCode.BackQuote), .. Letters("1234567890"), Cap("-", KeyCode.Minus), Cap("=", KeyCode.Equals), Cap("Back", KeyCode.Backspace, 2f)],
        [Cap("Tab", KeyCode.Tab, 1.5f), .. Letters("QWERTYUIOP"), Cap("[", KeyCode.LeftBracket), Cap("]", KeyCode.RightBracket), Cap("\\", KeyCode.Backslash, 1.5f)],
        [Cap("Caps", KeyCode.CapsLock, 1.75f), .. Letters("ASDFGHJKL"), Cap(";", KeyCode.Semicolon), Cap("'", KeyCode.Quote), Cap("Enter", KeyCode.Return, 2.25f)],
        [Cap("Shift", KeyCode.LeftShift, 2.25f), .. Letters("ZXCVBNM"), Cap(",", KeyCode.Comma), Cap(".", KeyCode.Period), Cap("/", KeyCode.Slash), Cap("Shift", KeyCode.RightShift, 2.75f)],
        [Cap("Ctrl", KeyCode.LeftControl, 1.25f), Cap("Win", KeyCode.LeftWindows, 1.25f), Cap("Alt", KeyCode.LeftAlt, 1.25f), Cap("Space", KeyCode.Space, 6.25f),
            Cap("Alt", KeyCode.RightAlt, 1.25f), Cap("Win", KeyCode.RightWindows, 1.25f), Cap("Menu", KeyCode.Menu, 1.25f), Cap("Ctrl", KeyCode.RightControl, 1.25f)],
    ];

    private static bool IsPartOf(KeyCode code, KeyBind bind) {
        if (code == bind.Key) {
            return true;
        }
        return code switch {
            KeyCode.LeftControl or KeyCode.RightControl => (bind.Modifiers & KeyBind.Mods.Ctrl) != 0,
            KeyCode.LeftShift or KeyCode.RightShift => (bind.Modifiers & KeyBind.Mods.Shift) != 0,
            KeyCode.LeftAlt or KeyCode.RightAlt => (bind.Modifiers & KeyBind.Mods.Alt) != 0,
            _ => false,
        };
    }

    private static void BuildKeyboard(KeyBind bind) {
        var ctx = O5KitAdapters.Ctx;
        var row = O5Factory.Row(ctx, stepHost, KeyUnit * KeyRows.Length);
        var board = keyboard = UiStyle.NewRect("Keyboard", row);
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition = Vector2.zero;
        board.sizeDelta = new Vector2(KeyUnit * 15f, KeyUnit * KeyRows.Length);
        for (int y = 0; y < KeyRows.Length; y++) {
            float x = 0f;
            foreach (var cap in KeyRows[y]) {
                float width = cap.Width * KeyUnit;
                if (cap.Label != null) {
                    AddKey(ctx, board, cap, new Vector2(x, -y * KeyUnit), new Vector2(width - KeyGap, KeyUnit - KeyGap), IsPartOf(cap.Code, bind));
                }
                x += width;
            }
        }
    }

    private static void AddKey(O5Kit.Core.O5Context ctx, RectTransform board, KeyCap cap, Vector2 position, Vector2 size, bool lit) {
        var image = UiStyle.Rounded("Key_" + cap.Label, board, lit ? Accent : KeyColor);
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = O5Factory.ControlText(ctx, rect, cap.Label.Length > 2 ? 11f : 14f, true);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = lit ? Color.white : UiStyle.Muted;
        label.font = lit ? ctx.Fonts.Medium : ctx.Fonts.Regular;
        label.text = $"<noparse>{cap.Label}</noparse>";
        if (lit) {
            litKeys.Add(image);
        }
    }

    // ---- the rest ----

    private static void ShowFinish() {
        Header("FINISH", "Setup complete", "All settings can be changed later.");
        var ctx = O5KitAdapters.Ctx;
        var box = UiStyle.Rounded("Suggestion", O5Factory.Row(ctx, stepHost, 124f), new Color(Accent.r, Accent.g, Accent.b, 0.12f)).rectTransform;
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.offsetMin = box.offsetMax = Vector2.zero;
        var text = O5Factory.ControlText(ctx, box, 17f, true);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(18f, 12f);
        text.rectTransform.offsetMax = new Vector2(-18f, -12f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        text.richText = true;
        text.raycastTarget = false;
        text.lineSpacing = 6f;
        text.text = T("SETUP_FINISH_TUTORIAL",
            "<b>New to Overlayer?</b> The tutorial is recommended. It demonstrates each feature with hands-on steps.\n"
            + "It is also available later from the <b>Tutorial</b> page in the menu");

        var buttons = UiStyle.HRow(stepHost, 8f, 50f);
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_tutorial", T("SETUP_FINISH_YES", "Start tutorial"), () => Close(true), primary: true));
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_done", T("SETUP_FINISH_NO", "Start now"), () => Close(false)));
    }
}
