using Overlayer.HudEdit;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.Tutorial;

// Olly doing something herself before asking you to: a list of beats played one after another,
// with a pointer that glides to buttons and "clicks" them.
internal sealed class TutorialDemo {
    internal sealed class Beat(float duration, Action start, Action<float> update) {
        public readonly float Duration = duration;
        public readonly Action Start = start;
        public readonly Action<float> Update = update;
    }

    internal readonly List<Beat> Beats = [];

    private TutorialDemo Add(float duration, Action start, Action<float> update) {
        Beats.Add(new Beat(duration, start, update));
        return this;
    }

    public TutorialDemo Wait(float seconds) => Add(seconds, null, null);

    public TutorialDemo Do(Action action) => Add(0f, action, null);

    public static readonly HumanMouse Mouse = new() { Speed = 1.4f };
    public static readonly HumanKeyboard Keyboard = new();

    /// <summary>The pointer glides to a screen point like a human hand (stays put on null).</summary>
    public TutorialDemo PointAt(Func<Vector2?> screen, float seconds = 0.7f) {
        HumanMouse.Trajectory path = null;
        return Add(seconds, () => {
            Tutorial.PointerShown = true;
            var from = Tutorial.PointerAt;
            var to = Safe(screen) ?? from;
            path = Mouse.Move(from, to);
        }, t => {
            if (path == null) {
                return;
            }
            float moveTime = Mathf.Min(path.Duration, seconds);
            float mt = moveTime > 0f ? Mathf.Clamp01((t * seconds) / moveTime) : 1f;
            Tutorial.PointerAt = path.Sample(mt);
        });
    }

    public TutorialDemo Point(Func<RectTransform> target, float seconds = 0.7f) => PointAt(() => Center(target()), seconds);

    public TutorialDemo Click(Action action = null) => Add(0.3f, () => {
        Tutorial.PointerPress = 1f;
        action?.Invoke();
    }, t => Tutorial.PointerPress = 1f - t);

    public TutorialDemo DoubleClick(Action action = null) => Click().Click(action);

    /// <summary>Moves the cursor off-screen before hiding it (no-op if already hidden, e.g. after slider drag).</summary>
    public TutorialDemo HidePointer() {
        HumanMouse.Trajectory path = null;
        return Add(0.26f, () => {
            if (!Tutorial.PointerShown) {
                path = null;
                return;
            }
            var from = Tutorial.PointerAt;
            path = Mouse.Move(from, OffscreenExit(from), speed: 1.6f);
        }, t => {
            if (path == null) {
                return;
            }
            Tutorial.PointerAt = path.Sample(t);
            if (t >= 1f) {
                Tutorial.PointerShown = false;
            }
        });
    }

    private static Vector2 OffscreenExit(Vector2 p) {
        float w = Screen.width, h = Screen.height;
        float dLeft = p.x, dRight = w - p.x, dBottom = p.y, dTop = h - p.y;
        float min = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBottom, dTop));
        if (min == dRight) {
            return new Vector2(w + 24f, Mathf.Clamp(p.y - 60f, -48f, h + 24f));
        }
        if (min == dBottom) {
            return new Vector2(Mathf.Clamp(p.x + 60f, -48f, w + 24f), -56f);
        }
        if (min == dLeft) {
            return new Vector2(-56f, Mathf.Clamp(p.y - 60f, -48f, h + 24f));
        }
        return new Vector2(Mathf.Clamp(p.x + 60f, -48f, w + 24f), h + 48f);
    }

    /// <summary>Holds an infinite slider at <paramref name="at"/>: hides cursor while dragging and restores it in place when done.</summary>
    public TutorialDemo Drag(Func<Vector2?> at, float seconds, Action<float> during) {
        Vector2 anchor = default;
        return PointAt(at, 0.6f).Add(seconds, () => {
            anchor = Tutorial.PointerAt;
            Tutorial.PointerShown = false;
            Tutorial.PointerPress = 0f;
        }, t => {
            float s = Mathf.Pow(Mathf.Clamp01(t), 0.9f);
            float e = s * s * (3f - 2f * s);
            Tutorial.PointerAt = anchor;
            during?.Invoke(e);
            if (t >= 1f) {
                Tutorial.PointerShown = true;
            }
        });
    }

    /// <summary>Types into a text with human-like cadence; speed is controlled by <see cref="Keyboard"/>.Wpm or <paramref name="wpm"/>.</summary>
    public TutorialDemo Type(Func<OvObject> target, string text, float? wpm = null) {
        var times = Keyboard.Schedule(text, wpm);
        float total = times.Length > 0 ? times[times.Length - 1] : 0f;
        int shown = -1;
        return Add(total + 0.35f, () => shown = -1, t => {
            float elapsed = t * (total + 0.35f);
            int n = 0;
            while (n < times.Length && elapsed >= times[n]) {
                n++;
            }
            if (n == shown) {
                return;
            }
            shown = n;
            string caret = n < text.Length ? "|" : string.Empty;
            HudEditor.DemoSetText(target(), text.Substring(0, n) + caret);
        });
    }

    /// <summary>Sets a text at once.</summary>
    public TutorialDemo Set(Func<OvObject> target, string text) => Do(() => HudEditor.DemoSetText(target(), text));

    private static Vector2? Safe(Func<Vector2?> f) {
        try {
            return f();
        } catch {
            return null;
        }
    }

    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>The closest point on a UI piece's screen rect to <paramref name="screenFrom"/>.</summary>
    public static Vector2? ClosestPoint(RectTransform rt, Vector2 screenFrom) {
        if (rt == null || !rt.gameObject.activeInHierarchy) {
            return null;
        }
        var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        rt.GetWorldCorners(Corners);
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < 4; i++) {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        return new Vector2(Mathf.Clamp(screenFrom.x, minX, maxX), Mathf.Clamp(screenFrom.y, minY, maxY));
    }

    /// <summary>The middle of a UI piece in screen pixels.</summary>
    public static Vector2? Center(RectTransform rt) => Inside(rt, 0.5f, 0.5f);

    /// <summary>A point in a UI piece (0..1 across, 0..1 up) in screen pixels.</summary>
    public static Vector2? Inside(RectTransform rt, float x, float y) {
        if (rt == null || !rt.gameObject.activeInHierarchy) {
            return null;
        }
        var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var r = rt.rect;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(new Vector2(r.xMin + r.width * x, r.yMin + r.height * y)));
    }
}
