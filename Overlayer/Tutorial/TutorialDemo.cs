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

    /// <summary>The pointer glides to a screen point (stays put on null).</summary>
    public TutorialDemo PointAt(Func<Vector2?> screen, float seconds = 0.7f) {
        Vector2 from = default, to = default;
        return Add(seconds, () => {
            Tutorial.PointerShown = true;
            from = Tutorial.PointerAt;
            to = Safe(screen) ?? from;
        }, t => Tutorial.PointerAt = Vector2.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)));
    }

    public TutorialDemo Point(Func<RectTransform> target, float seconds = 0.7f) => PointAt(() => Center(target()), seconds);

    public TutorialDemo Click(Action action = null) => Add(0.3f, () => {
        Tutorial.PointerPress = 1f;
        action?.Invoke();
    }, t => Tutorial.PointerPress = 1f - t);

    public TutorialDemo DoubleClick(Action action = null) => Click().Click(action);

    public TutorialDemo HidePointer() => Do(() => Tutorial.PointerShown = false);

    /// <summary>Holds and drags from one point to another; <paramref name="during"/> gets 0..1 on the way.</summary>
    public TutorialDemo Drag(Func<Vector2?> from, Func<Vector2?> to, float seconds, Action<float> during) {
        Vector2 a = default, b = default;
        return PointAt(from, 0.6f).Add(seconds, () => {
            a = Tutorial.PointerAt;
            b = Safe(to) ?? a;
            Tutorial.PointerPress = 0.6f;
        }, t => {
            float e = t * t * (3f - 2f * t);
            Tutorial.PointerAt = Vector2.Lerp(a, b, e);
            Tutorial.PointerPress = t < 1f ? 0.6f : 0f;
            during?.Invoke(e);
        });
    }

    /// <summary>Types into a text letter by letter, with a caret.</summary>
    public TutorialDemo Type(Func<OvObject> target, string text, float perLetter = 0.09f) {
        int shown = -1;
        return Add(text.Length * perLetter + 0.4f, () => shown = -1, t => {
            int n = Mathf.Min(text.Length, Mathf.FloorToInt(t * (text.Length + 4)));
            if (n == shown) {
                return;
            }
            shown = n;
            string caret = n < text.Length ? "<color=#FFFFFF88>|</color>" : string.Empty;
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
