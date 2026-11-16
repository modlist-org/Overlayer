using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Tutorial;

// Runs a tutorial chapter over the real UI: a bubble explains one thing at a time and a frame
// points at what it's about. Steps that ask you to do something wait until you've done it.
// While Overlayer's window layer shows, the tutorial lives at the end of it (both layers have
// the highest sort order, so only being last puts it on top); otherwise in a canvas of its own.
// Only the bubble takes clicks, so the UI underneath stays usable.
internal static class Tutorial {
    private const float BubbleWidth = 400f;
    private const float AdvanceDelay = 0.45f;

    private static readonly Color Accent = new(0.569f, 0.604f, 1f, 1f);
    private static readonly Color Dim = new(0f, 0f, 0f, 0.28f);

    private static GameObject canvasObj;
    private static RectTransform root, bubble, frame;
    private static readonly Image[] edges = new Image[4];
    private static readonly Image[] shades = new Image[4];
    private static TextMeshProUGUI caption, title, body, waiting;
    private static O5Kit.Control.O5Button backButton, nextButton, skipButton, closeButton;
    private static readonly List<O5Kit.Core.O5Object> controls = [];

    private static TutorialChapter chapter;
    private static int index;
    private static float doneAt = -1f;

    public static bool IsRunning => chapter != null;
    public static event Action Changed;

    public static IReadOnlyList<TutorialChapter> Chapters => TutorialContent.Chapters;

    public static bool IsDone(TutorialChapter c) => MainCore.Conf.TutorialDone.Value?.Contains(c.Id) == true;

    public static void Start(TutorialChapter c, int step = 0) {
        Stop();
        if (c == null || c.Steps.Count == 0) {
            return;
        }
        chapter = c;
        Build();
        Show(step);
        Changed?.Invoke();
    }

    public static void Stop() {
        chapter = null;
        if (root != null) {
            UnityEngine.Object.Destroy(root.gameObject);
        }
        if (canvasObj != null) {
            UnityEngine.Object.Destroy(canvasObj);
        }
        foreach (var control in controls) {
            control.Dispose();
        }
        controls.Clear();
        Lines.Clear();
        canvasObj = null;
        root = bubble = frame = null;
        Changed?.Invoke();
    }

    /// <summary>Starts the first chapter not done yet (or the first one).</summary>
    public static void Continue() => Start(Chapters.FirstOrDefault(c => !IsDone(c)) ?? Chapters[0]);

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
        if (chapter == null || root == null) {
            return;
        }
        PickLayer();
        var step = chapter.Steps[index];
        if (step.Done != null && doneAt < 0f && Safe(step.Done)) {
            doneAt = Time.unscaledTime;
            waiting.text = "<color=#7CFF8A>✓</color> " + Tr("TUTORIAL_NICE", "Nice!");
        }
        if (doneAt >= 0f && Time.unscaledTime - doneAt >= AdvanceDelay) {
            Next();
            return;
        }
        PlaceAround(Target(step));
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

    private static void Show(int i) {
        // Steps that don't apply here (no game module, say) are passed over.
        while (i < chapter.Steps.Count && chapter.Steps[i].When != null && !Safe(chapter.Steps[i].When)) {
            i++;
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
        caption.text = $"{Tr("TUTORIAL_CH_" + chapter.Id, chapter.Title).ToUpperInvariant()}   {i + 1} / {chapter.Steps.Count}";
        title.text = Tr($"TUTORIAL_{step.Key}_TITLE", step.Title);
        body.text = Tr($"TUTORIAL_{step.Key}", step.Text);
        bool waits = step.Done != null;
        waiting.gameObject.SetActive(waits);
        waiting.text = Tr("TUTORIAL_YOUR_TURN", "Your turn: do it and this moves on by itself.");
        nextButton.Text = i == chapter.Steps.Count - 1 ? Tr("TUTORIAL_FINISH", "Finish") : Tr("TUTORIAL_NEXT", "Next");
        nextButton.Rect.parent.gameObject.SetActive(!waits);
        skipButton.Rect.parent.gameObject.SetActive(waits);
        backButton.SetBlocked(i == 0, true);
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

    private static void Finish() {
        var done = chapter;
        var list = MainCore.Conf.TutorialDone.Value ?? [];
        if (!list.Contains(done.Id)) {
            list.Add(done.Id);
            MainCore.Conf.TutorialDone.Value = list;
            MainCore.ConfMgr.RequestSave();
        }
        Stop();
    }

    public static string Tr(string key, string fallback) => MainCore.Tr != null ? MainCore.Tr.Get(key, fallback) : fallback;

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

        caption = Line(ctx, 12f, UiStyle.Caption);
        caption.characterSpacing = 4f;
        title = Line(ctx, 21f, Color.white);
        title.fontStyle = FontStyles.Bold;
        body = Line(ctx, 16f, new Color(1f, 1f, 1f, 0.86f));
        body.lineSpacing = 6f;
        waiting = Line(ctx, 14f, Accent);

        var buttons = UiStyle.HRow(bubble, 6f, 34f);
        backButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 70f), "tutorial_back", Tr("TUTORIAL_BACK", "Back"), Back));
        closeButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 70f), "tutorial_close", Tr("TUTORIAL_CLOSE", "Close"), Stop));
        UiStyle.Tooltip(closeButton, Tr("TUTORIAL_CLOSE_TIP", "Stop the tutorial (you can pick it up again on the Tutorial page)"));
        UiStyle.Cell(buttons, 0f);
        skipButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 96f), "tutorial_skip", Tr("TUTORIAL_SKIP", "Skip step"), Next));
        nextButton = Track(UiStyle.Button(UiStyle.Cell(buttons, 96f), "tutorial_next", Tr("TUTORIAL_NEXT", "Next"), Next, primary: true));
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

    private static TextMeshProUGUI Line(O5Kit.Core.O5Context ctx, float size, Color color) {
        var text = O5Factory.ControlText(ctx, UiStyle.NewRect("Line", bubble), size, true);
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
        Lines.Add((text, fitter));
        return text;
    }

    private static readonly List<(TextMeshProUGUI Text, LayoutElement Element)> Lines = [];

    // Bubble next to the target (right, left, below, above: wherever it fits), frame on it,
    // shade around it. No target: bubble at the bottom middle, no frame or shade.
    private static void PlaceAround(RectTransform target) {
        foreach (var (text, element) in Lines) {
            if (text != null) {
                element.preferredHeight = text.GetPreferredValues(text.text, BubbleWidth - 40f, 0f).y;
            }
        }
        Vector2 size = root.rect.size;
        Vector2 bubbleSize = new(BubbleWidth, LayoutUtility.GetPreferredHeight(bubble));
        bool has = target != null;
        frame.gameObject.SetActive(has);
        foreach (var shade in shades) {
            shade.gameObject.SetActive(has);
        }
        Vector2 at;
        if (!has) {
            at = new Vector2((size.x - bubbleSize.x) * 0.5f, 150f + bubbleSize.y);
        } else {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var cam = target.GetComponentInParent<Canvas>()?.rootCanvas is { renderMode: not RenderMode.ScreenSpaceOverlay } c ? c.worldCamera : null;
            Vector2 min = ToLocal(RectTransformUtility.WorldToScreenPoint(cam, corners[0]));
            Vector2 max = ToLocal(RectTransformUtility.WorldToScreenPoint(cam, corners[2]));
            const float pad = 6f;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;
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
            float midY = (min.y + max.y) * 0.5f + bubbleSize.y * 0.5f;
            if (max.x + gap + bubbleSize.x <= size.x - 8f) {
                at = new Vector2(max.x + gap, midY);
            } else if (min.x - gap - bubbleSize.x >= 8f) {
                at = new Vector2(min.x - gap - bubbleSize.x, midY);
            } else if (min.y - gap - bubbleSize.y >= 8f) {
                at = new Vector2((min.x + max.x - bubbleSize.x) * 0.5f, min.y - gap);
            } else {
                at = new Vector2((min.x + max.x - bubbleSize.x) * 0.5f, max.y + gap + bubbleSize.y);
            }
        }
        at.x = Mathf.Clamp(at.x, 8f, size.x - bubbleSize.x - 8f);
        at.y = Mathf.Clamp(at.y, bubbleSize.y + 8f, size.y - 8f);
        bubble.anchoredPosition = at;
        bubble.SetAsLastSibling();
    }

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
