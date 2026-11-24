using Overlayer.Utility;
using UnityEngine;

namespace Overlayer.UI.Easing;

// The curve being edited. Points are kept rounded to what the tag text stores (3 decimals),
// so what the graph shows is exactly what the tag will do. Tangents (x = in, y = out slope)
// run parallel to Points and are written only for the anim (AnimationCurve style) kind.
internal sealed class CurveModel {
    public const float MinY = -0.5f;
    public const float MaxY = 1.5f;
    public const float MinGap = 0.01f;
    public const float MaxSlope = 50f;

    public readonly List<Vector2> Points = [new(0f, 0f), new(1f, 1f)];
    public readonly List<Vector2> Tangents = [new(1f, 1f), new(1f, 1f)];
    public CustomEaseKind Kind = CustomEaseKind.Curve;
    public event Action Changed;
    public event Action HistoryChanged;

    private readonly List<(Vector2[] Points, Vector2[] Tangents, CustomEaseKind Kind)> history = [];
    private int cursor = -1;

    public bool CanUndo => cursor > 0;
    public bool CanRedo => cursor >= 0 && cursor < history.Count - 1;

    public CurveModel() => Snapshot();

    public bool IsAnim => Kind == CustomEaseKind.Anim;

    public string Text => CustomEase.Format(Kind, Points.Select(p => (p.x, p.y)).ToList(),
        IsAnim ? Tangents.Select(t => (t.x, t.y)).ToList() : null);

    public float Evaluate(float t) => EaseMath.Evaluate(Text, t);

    public static float Round(float v) => Mathf.Round(v * 1000f) / 1000f;

    public void NotifyChanged() => Changed?.Invoke();

    // ---- history: call Snapshot() after each finished edit ----

    public void Snapshot() {
        if (cursor >= 0 && Same(history[cursor])) {
            return;
        }
        if (history.Count > cursor + 1) {
            history.RemoveRange(cursor + 1, history.Count - cursor - 1);
        }
        history.Add((Points.ToArray(), Tangents.ToArray(), Kind));
        if (history.Count > 200) {
            history.RemoveAt(0);
        }
        cursor = history.Count - 1;
        HistoryChanged?.Invoke();
    }

    private bool Same((Vector2[] Points, Vector2[] Tangents, CustomEaseKind Kind) state)
        => state.Kind == Kind && state.Points.SequenceEqual(Points) && state.Tangents.SequenceEqual(Tangents);

    public bool Undo() => Restore(cursor - 1);
    public bool Redo() => Restore(cursor + 1);

    private bool Restore(int index) {
        if (index < 0 || index >= history.Count) {
            return false;
        }
        cursor = index;
        Points.Clear();
        Points.AddRange(history[index].Points);
        Tangents.Clear();
        Tangents.AddRange(history[index].Tangents);
        Kind = history[index].Kind;
        NotifyChanged();
        HistoryChanged?.Invoke();
        return true;
    }

    // ---- edits ----

    public bool Load(string text) {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return false;
        }
        if (CustomEase.TryParse(text, out var ease)) {
            ApplyParsed(ease);
        } else if (Enum.TryParse(text, true, out O5Kit.Core.O5Ease preset) && Enum.IsDefined(typeof(O5Kit.Core.O5Ease), preset)) {
            LoadPreset(preset.ToString());
            return true;
        } else if (EaseLibrary.TryGet(text, out string saved)) {
            if (CustomEase.TryParse(saved, out var savedEase)) {
                ApplyParsed(savedEase);
            } else if (!string.IsNullOrWhiteSpace(saved) && Enum.TryParse(saved.Trim(), true, out O5Kit.Core.O5Ease savedPreset)
                && Enum.IsDefined(typeof(O5Kit.Core.O5Ease), savedPreset)) {
                LoadPreset(savedPreset.ToString());
                return true;
            } else {
                return false;
            }
        } else {
            return false;
        }
        NotifyChanged();
        Snapshot();
        return true;
    }

    private void ApplyParsed(CustomEase ease) {
        Points.Clear();
        for (int i = 0; i < ease.Count; i++) {
            Points.Add(new Vector2(ease[i].x, ease[i].y));
        }
        Kind = ease.Kind;
        Tangents.Clear();
        Tangents.AddRange(DeriveTangents(ease));
    }

    // A preset becomes points: sample it densely, then drop points while the shape holds.
    // In anim mode the keys also take the preset's slopes, so fewer keys are needed.
    public void LoadPreset(string preset) {
        const int samples = 40;
        float F(float t) => EaseMath.Evaluate(preset, t);
        Points.Clear();
        Tangents.Clear();
        for (int i = 0; i <= samples; i++) {
            float x = i / (float)samples;
            Points.Add(new Vector2(x, Round(F(x))));
            Tangents.Add(NumericSlopes(F, x));
        }
        if (!IsAnim) {
            Kind = CustomEaseKind.Curve;
        }
        Simplify(F, 0.006f);
        NotifyChanged();
        Snapshot();
    }

    /// <summary>"Tidy points": fewer points, same shape (within a small tolerance).</summary>
    public void Tidy() {
        string before = Text;
        Simplify(t => EaseMath.Evaluate(before, t), 0.01f);
        NotifyChanged();
        Snapshot();
    }

    // Greedy: repeatedly drop the interior point whose removal changes the curve least.
    private void Simplify(Func<float, float> target, float tolerance) {
        while (Points.Count > 2) {
            int best = -1;
            float bestError = float.MaxValue;
            for (int i = 1; i < Points.Count - 1; i++) {
                var removed = Points[i];
                var removedTangent = Tangents[i];
                Points.RemoveAt(i);
                Tangents.RemoveAt(i);
                float error = MaxError(target);
                Points.Insert(i, removed);
                Tangents.Insert(i, removedTangent);
                if (error < bestError) {
                    bestError = error;
                    best = i;
                }
            }
            if (best < 0 || bestError > tolerance) {
                break;
            }
            Points.RemoveAt(best);
            Tangents.RemoveAt(best);
        }
    }

    private float MaxError(Func<float, float> target) {
        string text = Text;
        float max = 0f;
        for (int i = 0; i <= 100; i++) {
            float t = i / 100f;
            max = Mathf.Max(max, Mathf.Abs(EaseMath.Evaluate(text, t) - target(t)));
        }
        return max;
    }

    public void Reset() {
        Points.Clear();
        Points.Add(new Vector2(0f, 0f));
        Points.Add(new Vector2(1f, 1f));
        Tangents.Clear();
        Tangents.Add(new Vector2(1f, 1f));
        Tangents.Add(new Vector2(1f, 1f));
        NotifyChanged();
        Snapshot();
    }

    /// <summary>Inserts a point keeping x order; returns its index or -1 when too close to a neighbour.</summary>
    public int Insert(Vector2 p) {
        p = new Vector2(Round(Mathf.Clamp01(p.x)), Round(Mathf.Clamp(p.y, MinY, MaxY)));
        int index = Points.FindIndex(q => q.x > p.x);
        if (index <= 0) {
            return -1;
        }
        if (p.x - Points[index - 1].x < MinGap || Points[index].x - p.x < MinGap) {
            return -1;
        }
        // A new key follows the current curve's direction at that spot.
        string before = Text;
        var slope = NumericSlopes(t => EaseMath.Evaluate(before, t), p.x);
        float s = RoundSlope((slope.x + slope.y) * 0.5f);
        Points.Insert(index, p);
        Tangents.Insert(index, new Vector2(s, s));
        NotifyChanged();
        return index;
    }

    public bool IsEndpoint(int index) => index == 0 || index == Points.Count - 1;

    /// <summary>Moves a point; x stays between its neighbours, endpoints keep their x (and y unless allowed).</summary>
    public void Move(int index, Vector2 p, bool endpointHeights) {
        if (index < 0 || index >= Points.Count) {
            return;
        }
        var old = Points[index];
        float x = old.x;
        float y = old.y;
        if (!IsEndpoint(index)) {
            x = Mathf.Clamp(p.x, Points[index - 1].x + MinGap, Points[index + 1].x - MinGap);
        }
        if (!IsEndpoint(index) || endpointHeights) {
            y = Mathf.Clamp(p.y, MinY, MaxY);
        }
        Points[index] = new Vector2(Round(x), Round(y));
        NotifyChanged();
    }

    public bool Remove(int index) {
        if (index <= 0 || index >= Points.Count - 1) {
            return false;
        }
        Points.RemoveAt(index);
        Tangents.RemoveAt(index);
        NotifyChanged();
        Snapshot();
        return true;
    }

    /// <summary>Switching to anim keeps the shape: keys take the exact slopes of the current curve.</summary>
    public void SetKind(CustomEaseKind kind) {
        if (Kind == kind) {
            return;
        }
        if (kind == CustomEaseKind.Anim && CustomEase.TryParse(Text, out var current)) {
            Tangents.Clear();
            Tangents.AddRange(DeriveTangents(current));
        }
        Kind = kind;
        NotifyChanged();
        Snapshot();
    }

    // ---- tangents (anim) ----

    public bool IsBroken(int index) => index >= 0 && index < Tangents.Count && Tangents[index].x != Tangents[index].y;

    /// <summary>Sets one side's slope; the other follows unless the key is broken or <paramref name="split"/>.</summary>
    public void SetSlope(int index, bool outSide, float slope, bool split) {
        if (index < 0 || index >= Tangents.Count) {
            return;
        }
        slope = RoundSlope(slope);
        var t = Tangents[index];
        bool linked = !split && !IsBroken(index);
        if (outSide || linked) {
            t.y = slope;
        }
        if (!outSide || linked) {
            t.x = slope;
        }
        Tangents[index] = t;
        NotifyChanged();
    }

    /// <summary>Makes in and out the same slope again (their average).</summary>
    public void Join(int index) {
        if (index < 0 || index >= Tangents.Count) {
            return;
        }
        var t = Tangents[index];
        float s = RoundSlope((t.x + t.y) * 0.5f);
        Tangents[index] = new Vector2(s, s);
        NotifyChanged();
        Snapshot();
    }

    public void Flatten(int index) {
        if (index < 0 || index >= Tangents.Count) {
            return;
        }
        Tangents[index] = Vector2.zero;
        NotifyChanged();
        Snapshot();
    }

    /// <summary>Every key back to the smooth (Catmull-Rom) slope.</summary>
    public void AutoTangents() {
        string smooth = CustomEase.Format(CustomEaseKind.Curve, Points.Select(p => (p.x, p.y)).ToList());
        if (!CustomEase.TryParse(smooth, out var ease)) {
            return;
        }
        Tangents.Clear();
        Tangents.AddRange(DeriveTangents(ease));
        NotifyChanged();
        Snapshot();
    }

    private static float RoundSlope(float s) => Round(Mathf.Clamp(s, -MaxSlope, MaxSlope));

    // Slopes that reproduce a parsed curve exactly (lines: the segment slopes on each side).
    private static List<Vector2> DeriveTangents(CustomEase ease) {
        int n = ease.Count;
        var list = new List<Vector2>(n);
        for (int i = 0; i < n; i++) {
            if (ease.Kind != CustomEaseKind.Lines) {
                var (inT, outT) = ease.TangentAt(i);
                list.Add(new Vector2(RoundSlope(inT), RoundSlope(outT)));
                continue;
            }
            float left = i > 0 ? Secant(ease, i - 1) : Secant(ease, 0);
            float right = i < n - 1 ? Secant(ease, i) : Secant(ease, n - 2);
            list.Add(new Vector2(RoundSlope(left), RoundSlope(right)));
        }
        return list;
    }

    private static float Secant(CustomEase ease, int i) => (ease[i + 1].y - ease[i].y) / (ease[i + 1].x - ease[i].x);

    // One-sided slopes of any function at x (x = from the left, y = to the right).
    private static Vector2 NumericSlopes(Func<float, float> f, float x) {
        const float h = 0.001f;
        float y = f(x);
        float left = x - h >= 0f ? (y - f(x - h)) / h : (f(x + h) - y) / h;
        float right = x + h <= 1f ? (f(x + h) - y) / h : left;
        return new Vector2(RoundSlope(left), RoundSlope(right));
    }
}
