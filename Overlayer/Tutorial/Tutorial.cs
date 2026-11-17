using O5Kit.Behaviour;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.Resource;
using Overlayer.Tutorial.Olly;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Tutorial;

// Runs a tutorial chapter over the real UI. Olly stands next to a bubble and explains one thing
// at a time while a frame points at what it's about; steps that ask you to do something wait
// until you've done it, and some she shows first herself.
// While Overlayer's window layer shows, the tutorial lives at the end of it (both layers have
// the highest sort order, so only being last puts it on top); otherwise in a canvas of its own
// under Overlayer's root. Only the bubble and Olly take clicks, so the UI underneath stays usable.
internal static class Tutorial {
    private const float BubbleWidth = 420f;
    private const float OllyScale = 0.8f;
    private const float OllyGap = 8f;
    private const float AdvanceDelay = 1.0f;
    private const float LettersPerSecond = 40f;

    private static readonly Color Accent = new(0.569f, 0.604f, 1f, 1f);
    private static readonly Color Dim = new(0f, 0f, 0f, 0.28f);

    private static float OllyWidth => OllyView.Size * OllyScale + OllyGap;
    private static float OllyHeight => OllyView.Size * OllyScale;

    private static GameObject canvasObj;
    private static RectTransform root, bubble, frame, pointer, noteBox, choiceRow;
    private static CanvasGroup noteGroup, rootGroup;
    private static Image pointerRing;
    private static readonly Image[] edges = new Image[4];
    private static readonly Image[] shades = new Image[4];
    private static TextMeshProUGUI caption, title, body, note, waiting;
    private static O5Kit.Control.O5Button backButton, nextButton, skipButton, closeButton, replayButton;
    private static readonly List<O5Kit.Core.O5Object> controls = [];
    private static readonly List<O5Kit.Core.O5Object> choiceControls = [];
    private static OllyView olly;

    private static TutorialChapter chapter;
    private static int index = -1;
    private static float doneAt = -1f;
    private static bool course;
    private static float appear;

    // Typing.
    private static float typed;
    private static int letters;
    private static float pause;
    private static float shownAt;
    private static bool nudged;
    private static bool langHooked;

    // Demo.
    private static TutorialDemo demo;
    private static int beat;
    private static float beatTime;
    internal static Vector2 PointerAt;
    internal static float PointerPress;
    internal static bool PointerShown;

    public static bool IsRunning => chapter != null;
    internal static bool Owns(Transform t) => t != null && ((root != null && t.IsChildOf(root)) || (canvasObj != null && t.IsChildOf(canvasObj.transform)));
    /// <summary>Going through every chapter in one go (the first time, or "Start / continue").</summary>
    public static bool InCourse => course;
    /// <summary>Head pats Olly got in this run.</summary>
    public static int Pats => olly?.Pats ?? 0;
    public static event Action Changed;

    public static IReadOnlyList<TutorialChapter> Chapters => TutorialContent.Chapters;

    public static bool IsDone(TutorialChapter c) => MainCore.Conf.TutorialDone.Value?.Contains(c.Id) == true;

    /// <param name="asCourse">Go on to the next chapter not done yet when this one ends.</param>
    public static void Start(TutorialChapter c, int step = 0, bool asCourse = false) {
        Stop(false);
        if (c == null || c.Steps.Count == 0) {
            return;
        }
        chapter = c;
        course = asCourse;
        Build();
        Show(step);
        UI.UISound.PlayOpen();
        Changed?.Invoke();
    }

    public static void Stop() => Stop(true);

    private static void Stop(bool playSound) {
        if (playSound && chapter != null) {
            UI.UISound.PlayClose();
        }
        O5KitAdapters.Ctx?.Tooltip?.Hide();
        LeaveStep();
        if (langHooked) {
            MainCore.Tr.OnLanguageChanged -= OnLanguageChanged;
            MainCore.Tr.OnLoadEnd -= OnLangLoadEnd;
            langHooked = false;
        }
        chapter = null;
        course = false;
        index = -1;
        if (root != null) {
            UnityEngine.Object.Destroy(root.gameObject);
        }
        if (canvasObj != null) {
            UnityEngine.Object.Destroy(canvasObj);
        }
        ClearChoices();
        foreach (var control in controls) {
            control.Dispose();
        }
        controls.Clear();
        Lines.Clear();
        canvasObj = null;
        root = bubble = frame = pointer = noteBox = choiceRow = null;
        olly = null;
        // Olly cleans up after herself (also after a run that was cut short).
        try {
            HudEditor.DemoRemoveAll();
        } catch (Exception e) {
            MainCore.Log.Wrn($"[Tutorial] Couldn't remove the samples: {e.Message}");
        }
        Changed?.Invoke();
    }

    /// <summary>The whole course, from the first chapter not done yet.</summary>
    public static void Continue() => Start(Chapters.FirstOrDefault(c => !IsDone(c)) ?? Chapters[0], 0, true);

    /// <summary>Marks this chapter done and goes on through the rest as a course.</summary>
    public static void IntoCourse() {
        if (chapter == null) {
            return;
        }
        course = true;
        Finish();
    }

    /// <summary>To the step with this key in the running chapter.</summary>
    public static void Jump(string key) {
        int i = chapter?.Steps.FindIndex(s => s.Key == key) ?? -1;
        if (i >= 0) {
            Show(i);
        }
    }

    // ---- per frame ----

    private static void StretchRoot() {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
    }

    private static void PickLayer() {
        var host = UI.UICore.CanvasObj != null && UI.UICore.CanvasObj.activeInHierarchy ? UI.UICore.CanvasObj.transform : canvasObj.transform;
        if (root.parent != host) {
            root.SetParent(host, false);
            StretchRoot();
        }
        // Things opened later (the HUD editor, dialogs) come after it otherwise.
        if (root.GetSiblingIndex() != host.childCount - 1) {
            root.SetAsLastSibling();
        }
    }

    public static void Tick() {
        if (chapter == null || root == null || index < 0) {
            return;
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        PickLayer();
        var step = chapter.Steps[index];

        appear = Mathf.Min(1f, appear + dt / 0.35f);
        float e = 1f - Mathf.Pow(1f - appear, 3f);
        rootGroup.alpha = e;
        olly.Rect.localScale = Vector3.one * (OllyScale * (0.9f + 0.1f * e));

        TickTyping(dt);
        TickDemo(dt);

        if (step.Done != null && doneAt < 0f && Safe(step.Done)) {
            doneAt = Time.unscaledTime;
            waiting.text = "<color=#7CFF8A>✓</color> " + Tr("TUTORIAL_NICE", "Nice!");
            olly.Face = new Face(olly.Face.Eye, olly.Face.Brow, Mouth.Smile, olly.Face.Shut, olly.Face.Mark, olly.Face.Front);
        }
        // Waiting a long while: Olly checks in.
        if (step.Done != null && doneAt < 0f && !nudged && Time.unscaledTime - shownAt > 20f) {
            nudged = true;
            waiting.text = Styled(Tr("TUTORIAL_NUDGE", "...Still there? Stuck? <b>Skip step</b> works too."));
        }
        if (doneAt >= 0f && Time.unscaledTime - doneAt >= AdvanceDelay) {
            Next();
            return;
        }
        var target = Target(step);
        olly.LookTarget = Gaze(step, target);
        olly.Tick(dt);
        PlaceAround(target);
        TickPointer();
    }

    // Straight ahead while she talks; the highlighted part while she explains it; the
    // player's mouse while it's their turn; straight ahead again once they're done.
    private static Vector2? Gaze(TutorialStep step, RectTransform target) {
        if (doneAt >= 0f) {
            return null;
        }
        if (PointerShown && demo != null) {
            return PointerAt;
        }
        if (step.Done != null || step.Choices is { Count: > 0 }) {
            return Input.mousePosition;
        }
        return target != null ? TutorialDemo.Center(target) : null;
    }

    private static void TickTyping(float dt) {
        bool typing = letters >= 0 && body.maxVisibleCharacters < letters;
        // A click on the bubble shows the rest at once.
        if (typing && Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(bubble, Input.mousePosition, null)) {
            body.maxVisibleCharacters = letters;
            typing = false;
        }
        if (typing) {
            if (pause > 0f) {
                pause -= dt;
            } else {
                int prevVisible = body.maxVisibleCharacters;
                typed += dt * LettersPerSecond;
                int to = Mathf.Min(letters, Mathf.FloorToInt(typed));
                for (int i = prevVisible; i < to; i++) {
                    char c = i < body.textInfo.characterInfo.Length ? body.textInfo.characterInfo[i].character : ' ';
                    // A breath after a sentence or a line.
                    char after = i + 1 < letters ? body.textInfo.characterInfo[i + 1].character : ' ';
                    if (c == '\n' || ((c == '.' || c == '?' || c == '!') && char.IsWhiteSpace(after))) {
                        to = i + 1;
                        pause = c == '\n' ? 0.18f : 0.12f;
                        typed = to;
                        break;
                    }
                }
                body.maxVisibleCharacters = to;
                if (to > prevVisible) {
                    for (int i = prevVisible; i < to && i < body.textInfo.characterInfo.Length; i++) {
                        char ch = body.textInfo.characterInfo[i].character;
                        if (!char.IsWhiteSpace(ch) && !char.IsPunctuation(ch)) {
                            UI.UISound.PlayOllyVoice();
                            break;
                        }
                    }
                }
            }
        }
        noteGroup.alpha = Mathf.MoveTowards(noteGroup.alpha, typing ? 0f : 1f, dt * 4f);
    }

    private static void TickDemo(float dt) {
        if (demo == null || beat >= demo.Beats.Count) {
            return;
        }
        var b = demo.Beats[beat];
        beatTime += dt;
        float t = b.Duration <= 0f ? 1f : Mathf.Clamp01(beatTime / b.Duration);
        try {
            b.Update?.Invoke(t);
        } catch (Exception ex) {
            MainCore.Log.Wrn($"[Tutorial] Demo step failed: {ex.Message}");
        }
        if (t < 1f) {
            return;
        }
        beat++;
        beatTime = 0f;
        StartBeat();
    }

    private static void StartBeat() {
        while (demo != null && beat < demo.Beats.Count) {
            var b = demo.Beats[beat];
            try {
                b.Start?.Invoke();
            } catch (Exception ex) {
                MainCore.Log.Wrn($"[Tutorial] Demo step failed: {ex.Message}");
            }
            if (b.Duration > 0f) {
                return;
            }
            // Instant beats run straight through.
            beat++;
        }
        // Played through.
        if (demo != null) {
            PointerShown = false;
            replayButton.Rect.parent.gameObject.SetActive(true);
            waiting.gameObject.SetActive(chapter.Steps[index].Done != null);
        }
    }

    private static void TickPointer() {
        bool on = PointerShown && demo != null;
        if (pointer.gameObject.activeSelf != on) {
            pointer.gameObject.SetActive(on);
        }
        if (!on) {
            return;
        }
        pointer.anchoredPosition = ToLocal(PointerAt);
        pointer.SetAsLastSibling();
        float p = Mathf.Clamp01(PointerPress);
        pointerRing.rectTransform.localScale = Vector3.one * (0.4f + p * 0.9f);
        pointerRing.color = new Color(Accent.r, Accent.g, Accent.b, p * 0.85f);
    }

    private static bool Safe(Func<bool> check) {
        try {
            return check();
        } catch {
            return false;
        }
    }

    private static RectTransform Target(TutorialStep step) {
        try {
            var t = step.Target?.Invoke();
            return t != null && t.gameObject.activeInHierarchy ? t : null;
        } catch {
            return null;
        }
    }

    // ---- moving through ----

    private static void LeaveStep() {
        if (chapter == null || index < 0 || index >= chapter.Steps.Count) {
            return;
        }
        var step = chapter.Steps[index];
        try {
            step.Exit?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Wrn($"[Tutorial] Step '{step.Key}' cleanup failed: {e.Message}");
        }
        demo = null;
        PointerShown = false;
    }

    private static void Show(int i) {
        LeaveStep();
        // Steps that don't apply here (no game module, say) are passed over; in the course,
        // steps that just get you somewhere are done for you.
        while (i < chapter.Steps.Count) {
            var s = chapter.Steps[i];
            if (s.When != null && !Safe(s.When)) {
                i++;
                continue;
            }
            if (course && s.Auto != null) {
                try {
                    s.Auto();
                } catch (Exception e) {
                    MainCore.Log.Wrn($"[Tutorial] Step '{s.Key}' couldn't do itself: {e.Message}");
                }
                i++;
                continue;
            }
            break;
        }
        if (i >= chapter.Steps.Count) {
            Finish();
            return;
        }
        index = i;
        doneAt = -1f;
        var step = chapter.Steps[i];
        try {
            step.Enter?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Wrn($"[Tutorial] Step '{step.Key}' setup failed: {e.Message}");
        }
        int count = chapter.Steps.Count;
        shownAt = Time.unscaledTime;
        nudged = false;
        typed = 0f;
        pause = 0.15f;

        // Expression is fixed per dialogue line, set only at line start.
        olly.Face = step.Face;
        bool waits = step.Done != null;
        bool asks = step.Choices is { Count: > 0 };
        waiting.gameObject.SetActive(waits || step.Demo != null);
        nextButton.Rect.parent.gameObject.SetActive(!waits && !asks);
        skipButton.Rect.parent.gameObject.SetActive(waits);
        replayButton.Rect.parent.gameObject.SetActive(false);
        backButton.SetBlocked(i == 0, true);
        ApplyTexts(step, i, count, keepTyped: false);
        noteGroup.alpha = 0f;

        demo = step.Demo;
        beat = 0;
        beatTime = 0f;
        PointerPress = 0f;
        PointerShown = false;
        if (demo != null) {
            // The pointer enters from just outside the screen.
            PointerAt = new Vector2(-48f, Screen.height * 0.42f);
            StartBeat();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
        replace = true;
        userPlaced = false;
        dragging = false;
        PlaceAround(Target(step));
    }

    private static void ApplyTexts(TutorialStep step, int i, int count, bool keepTyped) {
        bool wasFinished = keepTyped && (letters <= 0 || body.maxVisibleCharacters >= letters);
        caption.text = $"{Tr("TUTORIAL_CH_" + chapter.Id, chapter.Title).ToUpperInvariant()}   {i + 1} / {count}";
        title.text = Styled(Tr($"TUTORIAL_{step.Key}_TITLE", step.Title));
        body.text = Styled(Tr($"TUTORIAL_{step.Key}", step.Text));
        body.ForceMeshUpdate();
        letters = body.textInfo.characterCount;
        if (!keepTyped) {
            body.maxVisibleCharacters = 0;
        } else if (wasFinished) {
            body.maxVisibleCharacters = letters;
            typed = letters;
        } else {
            body.maxVisibleCharacters = Mathf.Min(body.maxVisibleCharacters, letters);
        }

        string noteText = step.Note != null ? Tr($"TUTORIAL_{step.Key}_NOTE", step.Note) : null;
        noteBox.gameObject.SetActive(!string.IsNullOrEmpty(noteText));
        note.text = Styled(noteText ?? string.Empty);

        if (doneAt >= 0f) {
            waiting.text = "<color=#7CFF8A>✓</color> " + Tr("TUTORIAL_NICE", "Nice!");
        } else if (nudged) {
            waiting.text = Styled(Tr("TUTORIAL_NUDGE", "...Still there? Stuck? <b>Skip step</b> works too."));
        } else {
            waiting.text = step.Demo != null
                ? Tr("TUTORIAL_WATCH", "Watch me first.")
                : Tr("TUTORIAL_YOUR_TURN", "Your turn: do it and this moves on by itself.");
        }

        backButton.Text = Tr("TUTORIAL_BACK", "Back");
        closeButton.Text = Tr("TUTORIAL_CLOSE", "Close");
        replayButton.Text = Tr("TUTORIAL_REPLAY", "Show again");
        skipButton.Text = Tr("TUTORIAL_SKIP", "Skip step");
        nextButton.Text = i == count - 1 && !course ? Tr("TUTORIAL_FINISH", "Finish") : Tr("TUTORIAL_NEXT", "Next");
        BuildChoices(step);
    }

    private static void OnLanguageChanged(string _) => RefreshLanguage();

    private static void OnLangLoadEnd(Overlayer.Localization.TranslationFailState state) {
        if (state == Overlayer.Localization.TranslationFailState.Success) {
            RefreshLanguage();
        }
    }

    private static void RefreshLanguage() {
        if (chapter == null || root == null || index < 0 || index >= chapter.Steps.Count) {
            return;
        }
        var step = chapter.Steps[index];
        ApplyTexts(step, index, chapter.Steps.Count, keepTyped: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
        PlaceAround(Target(step));
    }

    private static void Next() => Show(index + 1);

    private static void Back() {
        int i = index - 1;
        // Back over steps that are skipped here.
        while (i > 0 && chapter.Steps[i].When != null && !Safe(chapter.Steps[i].When)) {
            i--;
        }
        if (i >= 0) {
            Show(i);
        }
    }

    private static void Replay() => Show(index);

    private static void Finish() {
        var done = chapter;
        bool goOn = course;
        var list = MainCore.Conf.TutorialDone.Value ?? [];
        if (Chapters.Contains(done) && !list.Contains(done.Id)) {
            list.Add(done.Id);
            MainCore.Conf.TutorialDone.Value = list;
            MainCore.ConfMgr.RequestSave();
        }
        var next = goOn ? Chapters.SkipWhile(c => c != done).Skip(1).FirstOrDefault(c => !IsDone(c)) : null;
        if (next != null) {
            Start(next, 0, true);
        } else if (!goOn && Chapters.Contains(done) && done != TutorialContent.Hello && done != Chapters[Chapters.Count - 1]) {
            // One chapter on its own: Olly says where the rest is.
            Start(TutorialContent.ChapterEnd);
        } else {
            Stop();
        }
    }

    private static void Pick(TutorialChoice choice) {
        var was = chapter;
        int at = index;
        try {
            choice.Pick?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Wrn($"[Tutorial] Choice '{choice.Key}' failed: {e.Message}");
        }
        // Still here: on to the next step.
        if (chapter != null && chapter == was && index == at) {
            Next();
        }
    }

    public static string Tr(string key, string fallback) => MainCore.Tr != null ? MainCore.Tr.Get(key, fallback) : fallback;

    private static readonly System.Text.RegularExpressions.Regex CodeMark = new("«([^»]*)»");
    private static readonly System.Text.RegularExpressions.Regex FaceMark = new(@"\[\[(\w+)\]\]");
    private static readonly System.Text.RegularExpressions.Regex BoldMark = new(@"<b>(.*?)</b>");

    // «code» shows as code, as typed (tags and formatting not run); <b> highlights without TMP boldSpacing bloat.
    internal static string Styled(string text) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }
        text = FaceMark.Replace(text, string.Empty);
        text = CodeMark.Replace(text, m => $"<color=#AEB3FF><noparse>{m.Groups[1].Value}</noparse></color>");
        return BoldMark.Replace(text, m => $"<color=#FFFFFF>{m.Groups[1].Value}</color>");
    }

    // ---- view ----

    private static void Build() {
        canvasObj = new GameObject("OverlayerTutorial");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        root = UiStyle.NewRect("Tutorial", canvasObj.transform);
        StretchRoot();
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        appear = 0f;
        placed = null;
        snap = true;
        dragging = userPlaced = false;
        PickLayer();

        // Shade around the target (doesn't take clicks), and a frame on it.
        for (int i = 0; i < 4; i++) {
            shades[i] = Plain("Shade" + i, root, Dim);
        }
        frame = UiStyle.NewRect("Frame", root);
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = Vector2.zero;
        for (int i = 0; i < 4; i++) {
            edges[i] = Plain("Edge" + i, frame, Accent);
            var r = edges[i].rectTransform;
            r.anchorMin = i switch { 0 => Vector2.zero, 1 => new Vector2(0f, 1f), 2 => Vector2.zero, _ => new Vector2(1f, 0f) };
            r.anchorMax = i switch { 0 => new Vector2(1f, 0f), 1 => Vector2.one, 2 => new Vector2(0f, 1f), _ => Vector2.one };
            r.sizeDelta = i < 2 ? new Vector2(0f, 3f) : new Vector2(3f, 0f);
        }

        var ctx = O5KitAdapters.Ctx;
        var bubbleImage = UiStyle.Rounded("Bubble", root, new Color(0.08f, 0.08f, 0.11f, 0.97f));
        bubbleImage.raycastTarget = true;
        bubble = bubbleImage.rectTransform;
        bubble.anchorMin = bubble.anchorMax = Vector2.zero;
        bubble.pivot = new Vector2(0f, 1f);
        bubble.sizeDelta = new Vector2(BubbleWidth, 0f);
        var layout = UiStyle.Vertical(bubble.gameObject, 8f, new RectOffset(20, 20, 16, 16));
        layout.childForceExpandHeight = false;
        bubble.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var accent = Plain("Accent", bubble, Accent);
        accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        accent.rectTransform.anchorMin = Vector2.zero;
        accent.rectTransform.anchorMax = new Vector2(0f, 1f);
        accent.rectTransform.pivot = new Vector2(0f, 0.5f);
        accent.rectTransform.sizeDelta = new Vector2(4f, -24f);

        // Faint 2x3 grip dots in the top-right corner to show the bubble itself is draggable.
        var grip = UiStyle.NewRect("Grip", bubble);
        grip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        grip.anchorMin = grip.anchorMax = grip.pivot = Vector2.one;
        grip.anchoredPosition = new Vector2(-14f, -14f);
        grip.sizeDelta = new Vector2(11f, 18f);
        var dotSprite = MainCore.Spr.Get(UISprite.Circle256);
        var dotColor = new Color(1f, 1f, 1f, 0.22f);
        for (int row = 0; row < 3; row++) {
            for (int col = 0; col < 2; col++) {
                var dot = Plain($"Dot{row}_{col}", grip, dotColor);
                dot.sprite = dotSprite;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 1f);
                dot.rectTransform.pivot = new Vector2(0f, 1f);
                dot.rectTransform.anchoredPosition = new Vector2(col * 7f, -row * 7f);
                dot.rectTransform.sizeDelta = new Vector2(3.5f, 3.5f);
            }
        }
        AttachDrag(bubble.gameObject, pe => pe.pointerPressRaycast.gameObject?.GetComponentInParent<Button>() == null);

        // Olly stands at the bubble's lower left, outside it.
        olly = new OllyView(bubble);
        olly.Rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        olly.Rect.anchorMin = olly.Rect.anchorMax = Vector2.zero;
        olly.Rect.pivot = new Vector2(1f, 0f);
        olly.Rect.anchoredPosition = new Vector2(-OllyGap, 0f);
        olly.Rect.localScale = Vector3.one * OllyScale;
        AttachDrag(olly.Rect.gameObject, pe => !olly.IsHead(pe.pressPosition));

        caption = Line(ctx, bubble, 12f, UiStyle.Caption, BubbleWidth - 40f);
        caption.characterSpacing = -3f;
        title = Line(ctx, bubble, 20f, Color.white, BubbleWidth - 40f);
        title.font = ctx.Fonts.Medium;
        title.characterSpacing = -3f;
        body = Line(ctx, bubble, 17f, new Color(1f, 1f, 1f, 0.86f), BubbleWidth - 40f);
        body.lineSpacing = 6f;

        var noteImage = UiStyle.Rounded("Note", bubble, new Color(1f, 1f, 1f, 0.05f));
        noteBox = noteImage.rectTransform;
        noteGroup = noteBox.gameObject.AddComponent<CanvasGroup>();
        var noteLayout = UiStyle.Vertical(noteBox.gameObject, 0f, new RectOffset(12, 12, 10, 10));
        noteLayout.childForceExpandHeight = false;
        note = Line(ctx, noteBox, 15f, new Color(1f, 1f, 1f, 0.74f), BubbleWidth - 40f - 24f);
        note.lineSpacing = 4f;

        waiting = Line(ctx, bubble, 14f, Accent, BubbleWidth - 40f);

        choiceRow = UiStyle.HRow(bubble, 6f, 36f);

        var buttons = UiStyle.HRow(bubble, 6f, 34f);
        backButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 70f), "tutorial_back", Tr("TUTORIAL_BACK", "Back"), Back));
        closeButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 70f), "tutorial_close", Tr("TUTORIAL_CLOSE", "Close"), Stop));
        closeButton.Rect.AddToolTip(ctx, () => Tr("TUTORIAL_CLOSE_TIP", "Stop the tutorial (you can pick it up again on the Tutorial page)"));
        UiStyle.Cell(buttons, 0f);
        replayButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 96f), "tutorial_replay", Tr("TUTORIAL_REPLAY", "Show again"), Replay));
        skipButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 96f), "tutorial_skip", Tr("TUTORIAL_SKIP", "Skip step"), Next));
        nextButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 96f), "tutorial_next", Tr("TUTORIAL_NEXT", "Next"), Next, primary: true));

        if (!langHooked && MainCore.Tr != null) {
            MainCore.Tr.OnLanguageChanged += OnLanguageChanged;
            MainCore.Tr.OnLoadEnd += OnLangLoadEnd;
            langHooked = true;
        }

        // The demo pointer: Olly's cursor with tip at (0, 1) and a subtle click ring.
        pointer = UiStyle.NewRect("Pointer", root);
        pointer.anchorMin = pointer.anchorMax = Vector2.zero;
        pointer.pivot = new Vector2(0f, 1f);
        pointer.sizeDelta = new Vector2(36f, 36f);

        pointerRing = Plain("Ring", pointer, Accent);
        pointerRing.sprite = MainCore.Spr.Get(Asset.CircleOutline256O32);
        pointerRing.rectTransform.anchorMin = pointerRing.rectTransform.anchorMax = new Vector2(0f, 1f);
        pointerRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        pointerRing.rectTransform.anchoredPosition = Vector2.zero;
        pointerRing.rectTransform.sizeDelta = new Vector2(34f, 34f);

        var cursor = Plain("Cursor", pointer, Color.white);
        cursor.sprite = MainCore.Spr.Get(Asset.OllyMouse);
        cursor.rectTransform.anchorMin = Vector2.zero;
        cursor.rectTransform.anchorMax = Vector2.one;
        cursor.rectTransform.offsetMin = cursor.rectTransform.offsetMax = Vector2.zero;
        pointer.gameObject.SetActive(false);
    }

    private static void BuildChoices(TutorialStep step) {
        ClearChoices();
        bool asks = step.Choices is { Count: > 0 };
        choiceRow.gameObject.SetActive(asks);
        if (!asks) {
            return;
        }
        UiStyle.Cell(choiceRow, 0f);
        foreach (var choice in step.Choices) {
            var c = choice;
            string text = Tr("TUTORIAL_CHOICE_" + c.Key, c.Text);
            float width = Mathf.Max(90f, text.Length * 11f + 30f);
            choiceControls.Add(UiStyle.Button(UiStyle.Cell(choiceRow, width), "tutorial_choice_" + c.Key, text, () => Pick(c), primary: c.Primary));
        }
    }

    private static void ClearChoices() {
        foreach (var control in choiceControls) {
            control.Dispose();
        }
        choiceControls.Clear();
        if (choiceRow == null) {
            return;
        }
        for (int i = choiceRow.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(choiceRow.GetChild(i).gameObject);
        }
    }

    private static T Track<T>(T control) where T : O5Kit.Core.O5Object {
        controls.Add(control);
        return control;
    }

    private static Image Plain(string name, Transform parent, Color color) {
        var image = UiStyle.NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AttachDrag(GameObject target, Func<PointerEventData, bool> canStart = null) {
        UnityUtils.AddEvents(target.AddComponent<EventTrigger>(),
            (EventTriggerType.BeginDrag, e => {
                if (e is PointerEventData pe && pe.button == PointerEventData.InputButton.Left && (canStart == null || canStart(pe))) {
                    dragging = true;
                    glide = Vector2.zero;
                }
            }),
            (EventTriggerType.Drag, e => {
                if (dragging && e is PointerEventData pe && bubble != null) {
                    var canvas = root != null ? root.GetComponentInParent<Canvas>()?.rootCanvas : null;
                    float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                    bubble.anchoredPosition += pe.delta / scale;
                    placed = bubble.anchoredPosition;
                    userPlaced = true;
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                if (dragging) {
                    dragging = false;
                    // PlaceAround clamps placed into screen bounds and SmoothDamp eases it back in.
                    placed = bubble != null ? bubble.anchoredPosition : placed;
                }
            }));
    }

    private static TextMeshProUGUI Line(O5Kit.Core.O5Context ctx, Transform parent, float size, Color color, float width) {
        var text = O5Factory.ControlText(ctx, UiStyle.NewRect("Line", parent), size, true);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.color = color;
        text.raycastTarget = false;
        text.richText = true;
        // The line's height follows its text (set in PlaceAround).
        var fitter = text.transform.parent.gameObject.AddComponent<LayoutElement>();
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        fitter.minHeight = size + 4f;
        Lines.Add((text, fitter, width));
        return text;
    }

    private static readonly List<(TextMeshProUGUI Text, LayoutElement Element, float Width)> Lines = [];

    // Bubble (with Olly on its left) next to the target (right, left, below, above: wherever
    // it fits), frame on it, shade around it. No target: at the bottom middle, no frame or shade.
    private static void PlaceAround(RectTransform target) {
        foreach (var (text, element, width) in Lines) {
            if (text != null) {
                element.preferredHeight = string.IsNullOrEmpty(text.text) ? 0f : text.GetPreferredValues(text.text, width, 0f).y;
            }
        }
        Vector2 size = root.rect.size;
        float bubbleHeight = LayoutUtility.GetPreferredHeight(bubble);
        // Olly can stand taller than the bubble.
        float above = Mathf.Max(0f, OllyHeight - bubbleHeight);
        float cardWidth = OllyWidth + BubbleWidth;
        bool has = target != null;
        frame.gameObject.SetActive(has);
        foreach (var shade in shades) {
            shade.gameObject.SetActive(has);
        }
        // "at" is the bubble's top left.
        Vector2 at;
        if (!has) {
            at = new Vector2((size.x - cardWidth) * 0.5f + OllyWidth, 90f + bubbleHeight);
        } else {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var cam = target.GetComponentInParent<Canvas>()?.rootCanvas is { renderMode: not RenderMode.ScreenSpaceOverlay } c ? c.worldCamera : null;
            Vector2 min = ToLocal(RectTransformUtility.WorldToScreenPoint(cam, corners[0]));
            Vector2 max = ToLocal(RectTransformUtility.WorldToScreenPoint(cam, corners[2]));
            const float pad = 6f;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;
            targetMin = min;
            targetMax = max;
            frame.anchoredPosition = min;
            frame.sizeDelta = max - min;
            // Shades: below, above, left, right of the hole.
            Shade(0, new Vector2(0f, 0f), new Vector2(size.x, min.y));
            Shade(1, new Vector2(0f, max.y), new Vector2(size.x, size.y));
            Shade(2, new Vector2(0f, min.y), new Vector2(min.x, max.y));
            Shade(3, new Vector2(max.x, min.y), new Vector2(size.x, max.y));
            float alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            foreach (var edge in edges) {
                edge.color = new Color(Accent.r, Accent.g, Accent.b, alpha);
            }

            const float gap = 18f;
            float midY = (min.y + max.y) * 0.5f + bubbleHeight * 0.5f;
            float cardHeight = bubbleHeight + above;
            if (max.x + gap + cardWidth <= size.x - 8f) {
                at = new Vector2(max.x + gap + OllyWidth, midY);
            } else if (min.x - gap - cardWidth >= 8f) {
                at = new Vector2(min.x - gap - BubbleWidth, midY);
            } else if (min.y - gap - cardHeight >= 8f) {
                at = new Vector2((min.x + max.x - cardWidth) * 0.5f + OllyWidth, min.y - gap - above);
            } else {
                at = new Vector2((min.x + max.x - cardWidth) * 0.5f + OllyWidth, max.y + gap + bubbleHeight);
            }
        }
        at = Clamp(at, size, bubbleHeight, above);

        if (dragging) {
            bubble.SetAsLastSibling();
            return;
        }
        // Stay put while it still works (a target that grows as Olly types into it would
        // otherwise push the bubble along letter by letter); move only when the step changed,
        // the target changed, or where it is now covers the target or leaves the screen.
        bool keep = placed.HasValue && !replace && ReferenceEquals(placedFor, target);
        if (keep) {
            Vector2 now = Clamp(placed.Value, size, bubbleHeight, above);
            keep = userPlaced ? now == placed.Value : (now == placed.Value && (!has || !Covers(now, bubbleHeight, above, targetMin, targetMax)));
        }
        if (!keep) {
            placed = userPlaced ? Clamp(placed ?? at, size, bubbleHeight, above) : at;
            placedFor = target;
            replace = false;
        }
        if (snap) {
            bubble.anchoredPosition = placed.Value;
            glide = Vector2.zero;
            snap = false;
        } else {
            bubble.anchoredPosition = Vector2.SmoothDamp(bubble.anchoredPosition, placed.Value, ref glide, 0.18f, Mathf.Infinity, Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
        }
        bubble.SetAsLastSibling();
    }

    private static Vector2? placed;
    private static RectTransform placedFor;
    private static bool replace, snap = true, dragging, userPlaced;
    private static Vector2 glide, targetMin, targetMax;

    private static Vector2 Clamp(Vector2 at, Vector2 size, float bubbleHeight, float above) => new(
        Mathf.Clamp(at.x, 8f + OllyWidth, size.x - BubbleWidth - 8f),
        Mathf.Clamp(at.y, bubbleHeight + 8f, size.y - 8f - above));

    // Whether the card (Olly and the bubble) with the bubble's top left here overlaps the target.
    private static bool Covers(Vector2 at, float bubbleHeight, float above, Vector2 min, Vector2 max)
        => at.x - OllyWidth < max.x && at.x + BubbleWidth > min.x && at.y - bubbleHeight < max.y && at.y + above > min.y;

    private static void Shade(int i, Vector2 from, Vector2 to) {
        var r = shades[i].rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = from;
        r.sizeDelta = new Vector2(Mathf.Max(0f, to.x - from.x), Mathf.Max(0f, to.y - from.y));
    }

    private static Vector2 ToLocal(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
        // From the canvas's middle to its bottom-left corner.
        return local + root.rect.size * 0.5f;
    }
}
