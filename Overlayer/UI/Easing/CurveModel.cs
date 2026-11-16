using Overlayer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Overlayer.UI.Easing;

// The curve being edited. Points are kept rounded to what the tag text stores (3 decimals),
// so what the graph shows is exactly what the tag will do.
internal sealed class CurveModel {
    public const float MinY = -0.5f;
    public const float MaxY = 1.5f;
    public const float MinGap = 0.01f;

    public readonly List<Vector2> Points = [new(0f, 0f), new(1f, 1f)];
    public CustomEaseKind Kind = CustomEaseKind.Curve;
    public event Action Changed;

    private readonly List<(Vector2[] Points, CustomEaseKind Kind)> history = [];
    private int cursor = -1;

    public CurveModel() => Snapshot();

    public string Text => CustomEase.Format(Kind, Points.Select(p => (p.x, p.y)).ToList());

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
        history.Add((Points.ToArray(), Kind));
        if (history.Count > 200) {
            history.RemoveAt(0);
        }
        cursor = history.Count - 1;
    }

    private bool Same((Vector2[] Points, CustomEaseKind Kind) state)
        => state.Kind == Kind && state.Points.SequenceEqual(Points);

    public bool Undo() => Restore(cursor - 1);
    public bool Redo() => Restore(cursor + 1);

    private bool Restore(int index) {
        if (index < 0 || index >= history.Count) {
            return false;
        }
        cursor = index;
        Points.Clear();
        Points.AddRange(history[index].Points);
        Kind = history[index].Kind;
        NotifyChanged();
        return true;
    }

    // ---- edits ----

    public bool Load(string text) {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return false;
        }
        if (CustomEase.TryParse(text, out var ease)) {
            Points.Clear();
            for (int i = 0; i < ease.Count; i++) {
                Points.Add(new Vector2(ease[i].x, ease[i].y));
            }
            Kind = ease.Kind;
        } else if (Enum.TryParse(text, true, out O5Kit.Core.O5Ease preset)) {
            LoadPreset(preset.ToString());
            return true;
        } else {
            return false;
        }
        NotifyChanged();
        Snapshot();
        return true;
    }

    // A preset becomes points: sample it densely, then drop points while the shape holds.
    public void LoadPreset(string preset) {
        const int samples = 40;
        var dense = new List<Vector2>();
        for (int i = 0; i <= samples; i++) {
            float x = i / (float)samples;
            dense.Add(new Vector2(x, Round(EaseMath.Evaluate(preset, x))));
        }
        Points.Clear();
        Points.AddRange(dense);
        Kind = CustomEaseKind.Curve;
        Simplify(t => EaseMath.Evaluate(preset, t), 0.006f);
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
                Points.RemoveAt(i);
                float error = MaxError(target);
                Points.Insert(i, removed);
                if (error < bestError) {
                    bestError = error;
                    best = i;
                }
            }
            if (best < 0 || bestError > tolerance) {
                break;
            }
            Points.RemoveAt(best);
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
        Points.Insert(index, p);
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
        NotifyChanged();
        Snapshot();
        return true;
    }

    public void SetKind(CustomEaseKind kind) {
        if (Kind == kind) {
            return;
        }
        Kind = kind;
        NotifyChanged();
        Snapshot();
    }
}
