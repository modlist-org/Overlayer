using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.ModuleAPI;
using Overlayer.Overlay;
using Overlayer.Package;
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

// The first-run wizard: a fresh install opens the Overlayer window with this on top, and it walks
// through language, UI scale, keys, a starting HUD and importing, one page at a time. The last
// page suggests the tutorial. Settings can open it again.
internal static class SetupWizard {
    private enum Step { Welcome, Scale, Keys, Hud, Import, Finish }

    private const float CardWidth = 860f;
    private const float CardHeight = 640f;
    private const float StartDelay = 4f;
    private const int MaxLayouts = 5;
    private const float KeyUnit = 40f;
    private const float KeyGap = 4f;

    private static readonly Color Accent = new(0.569f, 0.604f, 1f, 1f);
    private static readonly Color KeyColor = new(1f, 1f, 1f, 0.07f);

    private static readonly int StepCount = Enum.GetValues(typeof(Step)).Length;

    private static RectTransform root, stepHost;
    private static CanvasGroup rootGroup, stepGroup;
    private static TextMeshProUGUI caption, title, say;
    private static O5Button backButton, skipButton, nextButton;
    private static readonly List<O5Kit.Core.O5Object> frameControls = [];
    private static readonly List<O5Kit.Core.O5Object> stepControls = [];
    private static readonly List<Image> litKeys = [];

    private static Step step;
    private static PackagePreset pickedPreset;
    private static string importStatus;
    private static bool showHudKey;
    private static float pendingSince = -1f;

    public static bool IsOpen => root != null;

    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    private static KeyBind WindowKey => new(KeyCode.BackQuote,
        Application.platform == RuntimePlatform.LinuxPlayer ? KeyBind.Mods.Ctrl : KeyBind.Mods.Alt);

    private static string WindowKeyText => Application.platform == RuntimePlatform.LinuxPlayer ? "Ctrl + `" : "Alt + `";

    // ---- opening and closing ----

    public static void Open() {
        if (IsOpen || UICore.CanvasObj == null || HudEditor.IsOpen) {
            return;
        }
        Tutorial.Tutorial.Stop();
        pickedPreset = null;
        importStatus = null;
        showHudKey = false;
        Build();
        Show(Step.Welcome);
        UISound.PlayOpen();
    }

    private static void Close(bool startTutorial) {
        KeyCapture.Cancel();
        MainCore.Conf.SetupPending.Value = false;
        var preset = pickedPreset;
        pickedPreset = null;
        Teardown();
        if (preset != null) {
            HudEditor.AddPresetToMainCanvas(preset);
        }
        PageSettings.SyncValues();
        if (startTutorial) {
            Tutorial.Tutorial.Continue();
        }
        MainCore.ConfMgr.RequestSave();
    }

    private static void Teardown() {
        ClearStep();
        foreach (var control in frameControls) {
            control.Dispose();
        }
        frameControls.Clear();
        if (root != null) {
            UnityEngine.Object.Destroy(root.gameObject);
        }
        root = stepHost = null;
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
            || HudEditor.IsOpen || Tutorial.Tutorial.IsRunning) {
            return;
        }
        UICore.Open();
        Open();
    }

    public static void Tick() {
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
        rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, 1f, dt * 5f);
        stepGroup.alpha = Mathf.MoveTowards(stepGroup.alpha, 1f, dt * 6f);
        // The keys to press pulse on the keyboard.
        float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f));
        foreach (var key in litKeys) {
            key.color = new Color(Accent.r, Accent.g, Accent.b, pulse);
        }
    }

    // ---- frame ----

    private static void Build() {
        var ctx = O5KitAdapters.Ctx;
        root = UiStyle.NewRect("OverlayerSetup", UICore.CanvasObj.transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        // Keeps clicks away from the window underneath.
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);
        dim.raycastTarget = true;

        var cardImage = UiStyle.Rounded("Card", root, new Color(0.08f, 0.08f, 0.11f, 0.98f));
        cardImage.raycastTarget = true;
        var card = cardImage.rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(CardWidth, CardHeight);

        var column = UiStyle.NewRect("Column", card);
        column.anchorMin = Vector2.zero;
        column.anchorMax = Vector2.one;
        column.offsetMin = new Vector2(36f, 28f);
        column.offsetMax = new Vector2(-36f, -28f);
        var layout = UiStyle.Vertical(column.gameObject, 8f);
        layout.childForceExpandHeight = false;

        caption = FrameText(ctx, column, 22f, 13f, UiStyle.Caption);
        caption.characterSpacing = 4f;
        title = FrameText(ctx, column, 44f, 30f, Color.white);
        title.fontStyle = FontStyles.Bold;
        say = FrameText(ctx, column, 64f, 18f, new Color(1f, 1f, 1f, 0.82f));
        say.lineSpacing = 6f;

        stepHost = UiStyle.NewRect("Step", column);
        stepHost.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var stepLayout = UiStyle.Vertical(stepHost.gameObject, 8f);
        stepLayout.childForceExpandHeight = false;
        stepGroup = stepHost.gameObject.AddComponent<CanvasGroup>();

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
        stepControls.Add(control);
        return control;
    }

    private static void ClearStep() {
        foreach (var control in stepControls) {
            control.Dispose();
        }
        stepControls.Clear();
        litKeys.Clear();
        if (stepHost == null) {
            return;
        }
        for (int i = stepHost.childCount - 1; i >= 0; i--) {
            var child = stepHost.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    // ---- pages ----

    private static void Show(Step next, bool fade = true) {
        KeyCapture.Cancel();
        step = next;
        ClearStep();
        if (fade) {
            stepGroup.alpha = 0f;
        }
        caption.text = string.Format(T("SETUP_STEP", "SETUP   {0} / {1}"), (int)step + 1, StepCount);
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
            case Step.Hud: ShowHud(); break;
            case Step.Import: ShowImport(); break;
            default: ShowFinish(); break;
        }
    }

    private static void Header(string key, string heading, string line) {
        title.text = T($"SETUP_{key}_TITLE", heading);
        say.text = T($"SETUP_{key}", line);
    }

    private static void ShowWelcome() {
        Header("WELCOME", "Welcome to Overlayer", "A few quick settings before you start. First, choose a language.");
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
        Header("SCALE", "UI size", "Drag the slider to a comfortable size. This screen and the Overlayer window change with it.");
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
        Header("KEYS", "Keys to remember", "Press the highlighted keys together.");
        var ctx = O5KitAdapters.Ctx;
        var tabs = UiStyle.HRow(stepHost, 8f, 44f);
        Track(UiStyle.Button(UiStyle.Cell(tabs, 0f), "setup_key_window", $"{T("SETUP_KEY_WINDOW", "Overlayer window")}:  <b>{WindowKeyText}</b>",
            () => ShowKeyOf(false), primary: !showHudKey));
        Track(UiStyle.Button(UiStyle.Cell(tabs, 0f), "setup_key_hud", $"{T("SETUP_KEY_HUD", "HUD editor")}:  <b>{HudEditor.Key}</b>",
            () => ShowKeyOf(true), primary: showHudKey));

        BuildKeyboard(showHudKey ? HudEditor.Key : WindowKey);

        if (!showHudKey) {
            Note(T("SETUP_KEY_WINDOW_NOTE", "Opens and closes the Overlayer window. If this screen closes when you try it, press the keys again to come back."), 44f);
            return;
        }
        O5Button change = null;
        string Caption() => T("SETUP_KEY_CHANGE", "Change key");
        change = Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_hud_key", Caption(), () => KeyCapture.Begin(
            () => change.Text = T("SETUP_KEY_PRESS", "Press a key...   (Esc: cancel)"),
            bind => {
                if (bind is { IsNone: false } b) {
                    MainCore.Conf.HudEditorKey = b.ToString();
                    MainCore.ConfMgr.RequestSave();
                    HudEditor.RegisterShortcut();
                    PageSettings.SyncValues();
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
        var board = UiStyle.NewRect("Keyboard", row);
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
        label.fontStyle = lit ? FontStyles.Bold : FontStyles.Normal;
        label.text = $"<noparse>{cap.Label}</noparse>";
        if (lit) {
            litKeys.Add(image);
        }
    }

    // ---- the rest ----

    private static void ShowHud() {
        Header("HUD", "Choose a starting HUD", "Start from a ready-made layout or an empty screen. You can edit everything later.");
        var ctx = O5KitAdapters.Ctx;
        Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_hud_empty", T("SETUP_HUD_EMPTY", "Empty screen"),
            () => PickPreset(null), primary: pickedPreset == null));
        foreach (var preset in PresetStore.Presets.Where(p => p.Category == HudPreset.LayoutCategory).Take(MaxLayouts)) {
            var captured = preset;
            Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_hud_" + preset.Id, preset.Name,
                () => PickPreset(captured), primary: pickedPreset?.Id == preset.Id));
        }
    }

    private static void PickPreset(PackagePreset preset) {
        pickedPreset = preset;
        Show(Step.Hud, false);
    }

    private static void ShowImport() {
        Header("IMPORT", "Import a HUD", "If you have a shared package, import it here. Otherwise, continue to the next step.");
        var ctx = O5KitAdapters.Ctx;
        Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 50f), "setup_import", T("SETUP_IMPORT_PICK", "Choose a file (.o5cp, .json)"), BeginImport));
        if (!string.IsNullOrEmpty(importStatus)) {
            Note(importStatus, 50f);
        }
    }

    private static void BeginImport() {
        _ = NativeDialogThread.Run(() => {
            try {
                return NativeFileDialog.Extended.NFD.OpenDialog(MainCore.Paths.RootPath, new Dictionary<string, string> {
                    ["Overlayer Package"] = "o5cp",
                    ["JSON"] = "json",
                });
            } catch (Exception e) {
                MainCore.Log.Err($"[Setup] File dialog failed: {e.Message}");
                return null;
            }
        }).ContinueWith(task => MainThread.Enqueue(() => {
            string path = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            if (string.IsNullOrWhiteSpace(path) || !MainCore.IsModEnabled) {
                return;
            }
            bool imported = OverlayCore.ImportCanvas(path);
            importStatus = imported
                ? string.Format(T("SETUP_IMPORT_DONE", "Imported {0}."), Path.GetFileName(path))
                : string.Format(T("SETUP_IMPORT_FAILED", "Couldn't import {0}. See the log for details."), Path.GetFileName(path));
            if (IsOpen && step == Step.Import) {
                Show(Step.Import, false);
            }
        }));
    }

    private static void ShowFinish() {
        Header("FINISH", "All set", "Setup is complete. You can change these settings at any time in Settings.");
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
            "<b>New to Overlayer?</b> We recommend trying the tutorial. It shows each feature and lets you try it yourself.\n"
            + "You can also start it later from the <b>Tutorial</b> page in the menu.");

        var buttons = UiStyle.HRow(stepHost, 8f, 50f);
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_tutorial", T("SETUP_FINISH_YES", "Try the tutorial"), () => Close(true), primary: true));
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_done", T("SETUP_FINISH_NO", "Start using Overlayer"), () => Close(false)));
    }
}
