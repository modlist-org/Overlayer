using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.ModuleAPI;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.Tag.Core;
using Overlayer.TextEngine.Core;
using Overlayer.UI;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// A small window that shows one text the way it looks in the game, on a mock game screen,
// with made-up values from the game module's HudSimulator: a while in a menu, then a play
// with judgments coming in, over and over. Only the preview text sees the made-up values.
// The window can be moved by its title and resized from its corner.
internal static class HudPreview {
    private const float MinWidth = 420f;
    private const float MinHeight = 320f;
    private const float MenuSeconds = 4f;
    private const float PlaySeconds = 26f;

    private enum Mode { Auto, Playing, NotPlaying }

    private static RectTransform window, viewport, scene, textRect;
    private static TextMeshProUGUI previewText, statusText, titleText;
    private static readonly O5Button[] modeButtons = new O5Button[3];
    private static readonly List<O5Kit.Core.O5Object> controls = [];
    private static TextEngineCore engine;
    private static HudSimulator simulator;
    private static LocalTagSet simTags;
    private static LocalTagSet ownTags, mergedTags;
    private static Mode mode;
    private static float phaseStart;
    private static bool phasePlaying;

    // What to show: an object's text (read live, so edits show up) or a fixed sample.
    private static OvObject source;
    private static Func<string> sampleText;

    public static bool IsOpen => window != null;

    private static float Width => Mathf.Max(MinWidth, MainCore.Conf.HudPreviewWidth.Value);
    private static float Height => Mathf.Max(MinHeight, MainCore.Conf.HudPreviewHeight.Value);

    /// <summary>Shows <paramref name="obj"/>'s text where it sits on the screen.</summary>
    public static void Open(OvObject obj) {
        if (obj?.Config?.TextConfig == null || obj.RectTransform == null) {
            return;
        }
        Build(obj.Config.Name.Value);
        source = obj;
        sampleText = null;
        StyleFrom(obj);
    }

    /// <summary>Shows a sample text (big, in the middle); <paramref name="text"/> is read every frame.</summary>
    public static void OpenSample(string title, Func<string> text) {
        Build(title);
        source = null;
        sampleText = text;
        previewText.fontSize = 96f;
        previewText.alignment = TextAlignmentOptions.Center;
        previewText.enableWordWrapping = false;
        previewText.color = Color.white;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.62f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(1400f, 200f);
        textRect.localEulerAngles = Vector3.zero;
    }

    public static void Close() {
        if (window != null) {
            UnityEngine.Object.Destroy(window.gameObject);
        }
        foreach (var control in controls) {
            control.Dispose();
        }
        controls.Clear();
        window = viewport = scene = textRect = null;
        previewText = statusText = titleText = null;
        engine?.Dispose();
        engine = null;
        source = null;
        sampleText = null;
        simulator = null;
        ownTags = mergedTags = null;
    }

    // ---- window ----

    private static void Build(string title) {
        Close();
        var ctx = O5KitAdapters.Ctx;
        window = UiStyle.Rounded("OverlayerHudPreview", UICore.CanvasObj.transform, UiStyle.Surface).rectTransform;
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0f, 1f);
        window.sizeDelta = new Vector2(Width, Height);
        window.anchoredPosition = new Vector2(-Width * 0.5f, Height * 0.5f);
        window.SetAsLastSibling();
        // Keep the window from scrolling or clicking what's behind it.
        window.GetComponent<Image>().raycastTarget = true;

        // Title bar: drag to move.
        var bar = UiStyle.NewRect("Title", window);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, 44f);
        var barHit = bar.gameObject.AddComponent<Image>();
        barHit.color = Color.clear;
        barHit.raycastTarget = true;
        UnityUtils.AddEvents(bar.gameObject.AddComponent<EventTrigger>(), (EventTriggerType.Drag, e => {
            if (e is PointerEventData pe && window != null) {
                window.anchoredPosition += pe.delta / Scale;
            }
        }));
        titleText = O5Factory.ControlText(ctx, bar, 15f, true);
        titleText.rectTransform.anchorMin = Vector2.zero;
        titleText.rectTransform.anchorMax = Vector2.one;
        titleText.rectTransform.offsetMin = new Vector2(16f, 0f);
        titleText.rectTransform.offsetMax = new Vector2(-330f, 0f);
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.enableWordWrapping = false;
        titleText.raycastTarget = false;
        titleText.text = $"<b>Preview</b>  <alpha=#88>{title}";

        var modes = UiStyle.NewRect("Modes", bar);
        modes.anchorMin = new Vector2(1f, 0f);
        modes.anchorMax = Vector2.one;
        modes.pivot = new Vector2(1f, 0.5f);
        modes.offsetMin = new Vector2(-322f, 7f);
        modes.offsetMax = new Vector2(-48f, -7f);
        UiStyle.Horizontal(modes.gameObject, 4f);
        string[] names = ["Both", "Playing", "Menu"];
        string[] tips = ["A few seconds in a menu, then a play, again and again", "Only while playing", "Only in a menu (after a play)"];
        for (int i = 0; i < 3; i++) {
            var m = (Mode)i;
            modeButtons[i] = Track(UiStyle.Button(UiStyle.Cell(modes, 0f), "hud_preview_mode" + i, names[i], () => SetMode(m)));
            UiStyle.Tooltip(modeButtons[i], tips[i]);
        }
        var closeHost = UiStyle.NewRect("Close", bar);
        closeHost.anchorMin = closeHost.anchorMax = closeHost.pivot = new Vector2(1f, 0.5f);
        closeHost.anchoredPosition = new Vector2(-8f, 0f);
        closeHost.sizeDelta = new Vector2(32f, 32f);
        Track(UiStyle.IconButton(closeHost, "hud_preview_close", MainCore.Spr.Get(UISprite.X128), Close, 9f));

        // The mock game screen, kept at the screen's shape.
        viewport = UiStyle.NewRect("Screen", window);
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 1f);
        viewport.pivot = new Vector2(0.5f, 1f);
        var back = viewport.gameObject.AddComponent<Image>();
        back.color = new Color(0.035f, 0.035f, 0.05f, 1f);
        back.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        scene = UiStyle.NewRect("Scene", viewport);
        scene.anchorMin = Vector2.zero;
        scene.anchorMax = Vector2.one;
        scene.offsetMin = scene.offsetMax = Vector2.zero;

        textRect = UiStyle.NewRect("Text", viewport);
        previewText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        previewText.raycastTarget = false;
        if (TextFontProvider.Current is { } font) {
            previewText.font = font;
        }

        statusText = O5Factory.ControlText(ctx, window, 13f, true);
        statusText.rectTransform.anchorMin = Vector2.zero;
        statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
        statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
        statusText.rectTransform.offsetMin = new Vector2(16f, 8f);
        statusText.rectTransform.offsetMax = new Vector2(-34f, 30f);
        statusText.alignment = TextAlignmentOptions.MidlineLeft;
        statusText.color = UiStyle.Caption;
        statusText.raycastTarget = false;

        BuildResizeGrip();

        engine = new TextEngineCore();
        simulator = HudSimulators.Current;
        simTags = BuildSimTags(simulator);
        simulator?.BuildScene(scene);
        SetMode(Mode.Auto);
        LayoutViewport();
    }

    private static T Track<T>(T control) where T : O5Kit.Core.O5Object {
        controls.Add(control);
        return control;
    }

    private static float Scale => UICore.Canvas != null && UICore.Canvas.scaleFactor > 0f ? UICore.Canvas.scaleFactor : 1f;

    private static void BuildResizeGrip() {
        var grip = UiStyle.NewRect("Resize", window);
        grip.anchorMin = grip.anchorMax = grip.pivot = new Vector2(1f, 0f);
        grip.sizeDelta = new Vector2(26f, 26f);
        var hit = grip.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        // Two short diagonal lines, like a window corner.
        for (int i = 0; i < 2; i++) {
            var line = UiStyle.NewRect("Line", grip);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(i == 0 ? 16f : 8f, 1.5f);
            line.anchoredPosition = new Vector2(2f + i * 4f, -2f - i * 4f);
            line.localEulerAngles = new Vector3(0f, 0f, 45f);
            var img = line.gameObject.AddComponent<Image>();
            img.color = UiStyle.Muted;
            img.raycastTarget = false;
        }
        UnityUtils.AddEvents(grip.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe && window != null) {
                    var size = window.sizeDelta + new Vector2(pe.delta.x, -pe.delta.y) / Scale;
                    window.sizeDelta = new Vector2(Mathf.Max(MinWidth, size.x), Mathf.Max(MinHeight, size.y));
                    LayoutViewport();
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                MainCore.Conf.HudPreviewWidth.Value = window.sizeDelta.x;
                MainCore.Conf.HudPreviewHeight.Value = window.sizeDelta.y;
                MainCore.ConfMgr.RequestSave();
            }));
    }

    // Biggest screen-shaped box between the title bar and the status line.
    private static void LayoutViewport() {
        if (window == null) {
            return;
        }
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        Vector2 room = window.sizeDelta - new Vector2(24f, 44f + 38f);
        Vector2 size = room.x / aspect <= room.y ? new Vector2(room.x, room.x / aspect) : new Vector2(room.y * aspect, room.y);
        viewport.sizeDelta = size;
        viewport.anchoredPosition = new Vector2(0f, -44f - Mathf.Max(0f, room.y - size.y) * 0.5f);
        if (source != null) {
            StyleFrom(source);
        }
    }

    private static void SetMode(Mode m) {
        mode = m;
        phasePlaying = m != Mode.NotPlaying;
        if (m == Mode.Auto) {
            phasePlaying = false;
        }
        phaseStart = Time.unscaledTime;
        for (int i = 0; i < 3; i++) {
            if (modeButtons[i] == null) {
                continue;
            }
            modeButtons[i].NormalColor = i == (int)m ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
            modeButtons[i].UpdateVisual(true);
        }
    }

    // ---- the text ----

    // Same look and the same spot on the (small) screen as the real one.
    private static void StyleFrom(OvObject obj) {
        var cfg = obj.Config;
        var go = textRect.gameObject;
        cfg.TextConfig.ToUnity(go);
        if (TextFontProvider.Current is { } font) {
            previewText.font = font;
        }
        var fitter = go.GetComponent<ContentSizeFitter>();
        if (cfg.ContentSizeFitterConfig != null) {
            fitter ??= go.AddComponent<ContentSizeFitter>();
            cfg.ContentSizeFitterConfig.ToUnity(go);
        } else if (fitter != null) {
            UnityEngine.Object.Destroy(fitter);
        }
        var real = obj.RectTransform;
        float k = viewport.rect.width / Mathf.Max(1f, Screen.width);
        textRect.anchorMin = textRect.anchorMax = Vector2.zero;
        textRect.pivot = real.pivot;
        textRect.sizeDelta = real.rect.size;
        textRect.localScale = real.lossyScale * k;
        textRect.localEulerAngles = new Vector3(0f, 0f, real.eulerAngles.z);
        Vector2 at = real.position;
        textRect.anchoredPosition = new Vector2(at.x / Mathf.Max(1f, Screen.width) * viewport.rect.width,
            at.y / Mathf.Max(1f, Screen.height) * viewport.rect.height);
        // Faded the same as on screen (its own and its parents' opacity).
        float alpha = 1f;
        foreach (var group in real.GetComponentsInParent<CanvasGroup>()) {
            alpha *= group.alpha;
        }
        var own = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        own.alpha = alpha;
    }

    // Stand-ins for the simulator's tags (only tags that exist and can be faked).
    private static LocalTagSet BuildSimTags(HudSimulator sim) {
        if (sim == null) {
            return null;
        }
        var tags = new Dictionary<string, TagCore>(StringComparer.Ordinal);
        foreach (string name in sim.Tags) {
            if (!TagManager.TryGet(name, out var real) || real.IsJS || (real.TagType & TagType.Advanced) != 0) {
                continue;
            }
            string captured = name;
            tags[name] = TagCore.StandIn(real, args => sim.Get(captured, args));
        }
        return tags.Count == 0 ? null : new LocalTagSet(tags);
    }

    // The object's own local tags (from its Fx code) plus the made-up ones.
    private static LocalTagSet TagsFor(LocalTagSet own) {
        if (own == null) {
            return simTags;
        }
        if (simTags == null) {
            return own;
        }
        if (!ReferenceEquals(own, ownTags)) {
            ownTags = own;
            var merged = new Dictionary<string, TagCore>(simTags.Tags, StringComparer.Ordinal);
            foreach (var pair in own.Tags) {
                merged[pair.Key] = pair.Value;
            }
            mergedTags = new LocalTagSet(merged);
        }
        return mergedTags;
    }

    // ---- per frame ----

    public static void Tick() {
        if (window == null) {
            return;
        }
        if (UICore.CanvasObj == null || !UICore.CanvasObj.activeSelf) {
            Close();
            return;
        }
        if (source != null && (source.GameObject == null || source.Config?.TextConfig == null)) {
            Close();
            return;
        }

        // Phases: Both = menu, then play, again and again.
        float t = Time.unscaledTime - phaseStart;
        if (mode == Mode.Auto && t >= (phasePlaying ? PlaySeconds : MenuSeconds)) {
            phasePlaying = !phasePlaying;
            phaseStart = Time.unscaledTime;
            t = 0f;
        } else if (mode == Mode.Playing && t >= PlaySeconds * 2f) {
            phaseStart = Time.unscaledTime;
            t = 0f;
        }
        simulator?.Step(t, phasePlaying);
        simulator?.UpdateScene(scene);

        string template;
        LocalTagSet own = null;
        if (source != null) {
            var text = source.Config.TextEngineConfig?.Text;
            template = text?.Value ?? string.Empty;
            own = text?.LocalTags;
            // Follow edits of size and style.
            if (Time.frameCount % 15 == 0) {
                StyleFrom(source);
            }
        } else {
            template = sampleText?.Invoke() ?? string.Empty;
        }
        engine.LocalTags = TagsFor(own);
        engine.Text = template;
        previewText.text = engine.Get();

        string phase = phasePlaying ? "<color=#7CFF8A>Playing</color>" : "<color=#FFD36E>Not playing</color>";
        string extra = simulator?.Status;
        statusText.text = simulator == null
            ? "No game values to make up here: tags show what they show right now."
            : extra == null ? phase : $"{phase}   {extra}";
    }
}
