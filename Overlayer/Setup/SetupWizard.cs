using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Package;
using Overlayer.Tutorial.Olly;
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

// The first-run wizard: opens over the Overlayer window the first time it's opened and walks
// through language, UI scale, keys, a starting HUD and importing, one page at a time. The last
// page suggests the tutorial. Settings can open it again.
internal static class SetupWizard {
    private enum Step { Welcome, Scale, Keys, Hud, Import, Finish }

    private const float CardWidth = 1040f;
    private const float CardHeight = 640f;
    private const int MaxLayouts = 5;

    private static readonly int StepCount = Enum.GetValues(typeof(Step)).Length;

    private static RectTransform root, card, stepHost;
    private static CanvasGroup rootGroup, stepGroup;
    private static TextMeshProUGUI caption, title, say;
    private static O5Button backButton, skipButton, nextButton;
    private static OllyView olly;
    private static readonly List<O5Kit.Core.O5Object> frameControls = [];
    private static readonly List<O5Kit.Core.O5Object> stepControls = [];

    private static Step step;
    private static PackagePreset pickedPreset;
    private static string importStatus;

    public static bool IsOpen => root != null;

    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    // ---- opening and closing ----

    public static void Open() {
        if (IsOpen || UICore.CanvasObj == null || HudEditor.IsOpen) {
            return;
        }
        Tutorial.Tutorial.Stop();
        pickedPreset = null;
        importStatus = null;
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
            // Olly already said hello here, so the course starts at the first real chapter.
            var done = MainCore.Conf.TutorialDone.Value ?? [];
            if (!done.Contains(Tutorial.TutorialContent.Hello.Id)) {
                done.Add(Tutorial.TutorialContent.Hello.Id);
                MainCore.Conf.TutorialDone.Value = done;
            }
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
        root = card = stepHost = null;
        olly = null;
    }

    public static void Tick() {
        if (root == null) {
            // Destroyed along with the window layer.
            if (frameControls.Count > 0) {
                Teardown();
            }
            if (MainCore.Conf.SetupPending.Value && MainCore.IsModEnabled && UICore.CanvasObj != null && UICore.CanvasObj.activeInHierarchy
                && !HudEditor.IsOpen && !Tutorial.Tutorial.IsRunning) {
                Open();
            }
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
        olly.Tick(dt);
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
        card = cardImage.rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(CardWidth, CardHeight);

        olly = new OllyView(card);
        olly.Rect.anchorMin = olly.Rect.anchorMax = olly.Rect.pivot = Vector2.zero;
        olly.Rect.anchoredPosition = new Vector2(28f, 28f);

        var column = UiStyle.NewRect("Column", card);
        column.anchorMin = Vector2.zero;
        column.anchorMax = Vector2.one;
        column.offsetMin = new Vector2(292f, 28f);
        column.offsetMax = new Vector2(-36f, -28f);
        var layout = UiStyle.Vertical(column.gameObject, 8f);
        layout.childForceExpandHeight = false;

        caption = FrameText(ctx, column, 22f, 13f, UiStyle.Caption);
        caption.characterSpacing = 4f;
        title = FrameText(ctx, column, 44f, 30f, Color.white);
        title.fontStyle = FontStyles.Bold;
        say = FrameText(ctx, column, 80f, 18f, new Color(1f, 1f, 1f, 0.82f));
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

    private static void Header(string key, string heading, string line, Face face) {
        title.text = T($"SETUP_{key}_TITLE", heading);
        say.text = T($"SETUP_{key}", line);
        olly.Face = face;
    }

    private static void ShowWelcome() {
        Header("WELCOME", "Welcome to Overlayer",
            "Hi, I'm Olly. Let's get a few things set up first. It only takes a minute.\nWhich language do you prefer?", Face.Smile);
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
        Header("SCALE", "UI size", "Too small? Too big? Drag this until it feels right. This card and the window follow along.", Face.Curious);
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
        Header("KEYS", "Two keys to remember", "These two open everything. You can change the HUD editor's key here.", Face.Proud);
        var ctx = O5KitAdapters.Ctx;
        string windowKey = Application.platform == RuntimePlatform.LinuxPlayer ? "Ctrl + `" : "Alt + `";
        var row = UiStyle.Rounded("WindowKey", O5Factory.Row(ctx, stepHost, 50f), UiStyle.Ghost).rectTransform;
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.one;
        row.offsetMin = row.offsetMax = Vector2.zero;
        var label = O5Factory.ControlText(ctx, row, 18f, true);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = $"{T("SETUP_KEY_WINDOW", "Overlayer window")}:  <b>{windowKey}</b>";

        O5Button keyButton = null;
        string Caption() => $"{T("SETUP_KEY_HUD", "HUD editor")}:  <b>{HudEditor.Key}</b>";
        keyButton = Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 50f), "setup_hud_key", Caption(), () => KeyCapture.Begin(
            () => keyButton.Text = T("SETUP_KEY_PRESS", "Press a key...   (Esc: cancel)"),
            bind => {
                if (bind is { IsNone: false } b) {
                    MainCore.Conf.HudEditorKey = b.ToString();
                    MainCore.ConfMgr.RequestSave();
                    HudEditor.RegisterShortcut();
                    PageSettings.SyncValues();
                }
                if (step == Step.Keys && root != null) {
                    keyButton.Text = Caption();
                }
            })));
        Note(T("SETUP_KEY_NOTE", "Click the HUD editor button, then press a key. Hold Ctrl, Shift or Alt for a combination."), 50f);
    }

    private static void ShowHud() {
        Header("HUD", "Pick a starting HUD", "Start from a ready-made layout, or from an empty screen. Everything stays editable.", Face.Happy);
        var ctx = O5KitAdapters.Ctx;
        Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_hud_empty", T("SETUP_HUD_EMPTY", "Empty screen"),
            () => PickPreset(null), primary: pickedPreset == null));
        foreach (var preset in PresetStore.Presets.Where(p => p.Category == HudPreset.LayoutCategory).Take(MaxLayouts)) {
            var captured = preset;
            Track(UiStyle.Button(O5Factory.Row(ctx, stepHost, 44f), "setup_hud_" + preset.Id, preset.Name,
                () => PickPreset(captured), primary: ReferenceEquals(pickedPreset, preset) || pickedPreset?.Id == preset.Id));
        }
    }

    private static void PickPreset(PackagePreset preset) {
        pickedPreset = preset;
        Show(Step.Hud, false);
    }

    private static void ShowImport() {
        Header("IMPORT", "Got a HUD already?", "If someone shared a package with you, bring it in here. No file? Just move on.", Face.Curious);
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
                : string.Format(T("SETUP_IMPORT_FAILED", "Couldn't import {0}. The log has the details."), Path.GetFileName(path));
            if (IsOpen && step == Step.Import) {
                Show(Step.Import, false);
            }
        }));
    }

    private static void ShowFinish() {
        Header("FINISH", "All set!", "That's everything. You can change all of this later in Settings.", Face.Happy);
        var ctx = O5KitAdapters.Ctx;
        var box = UiStyle.Rounded("Suggestion", O5Factory.Row(ctx, stepHost, 124f), new Color(0.569f, 0.604f, 1f, 0.12f)).rectTransform;
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.offsetMin = box.offsetMax = Vector2.zero;
        var text = O5Factory.ControlText(ctx, box, 17f, true);
        text.rectTransform.offsetMin = new Vector2(18f, 12f);
        text.rectTransform.offsetMax = new Vector2(-18f, -12f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        text.richText = true;
        text.raycastTarget = false;
        text.lineSpacing = 6f;
        text.text = T("SETUP_FINISH_TUTORIAL",
            "<b>New to Overlayer?</b> The tutorial is worth a try: I'll show you each thing first, then you try it yourself.\n"
            + "No rush, though. It's always on the <b>Tutorial</b> page in the menu.");

        var buttons = UiStyle.HRow(stepHost, 8f, 50f);
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_tutorial", T("SETUP_FINISH_YES", "Try the tutorial"), () => Close(true), primary: true));
        Track(UiStyle.Button(UiStyle.Cell(buttons, 0f), "setup_finish_done", T("SETUP_FINISH_NO", "Start using Overlayer"), () => Close(false)));
    }
}
