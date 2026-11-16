using UnityEngine;

namespace Overlayer.HudEdit;

// Distance helpers (screen space, by visible boxes):
// - Alt while hovering: distances from the selection to the object under the pointer
//   (to each edge when one box holds the other, to the screen edges over empty space).
// - While dragging: equal-spacing snap in the row / column, with the equal gaps labelled.
internal static partial class HudEditor {
    private readonly struct Box(float minX, float maxX, float minY, float maxY) {
        public readonly float MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY;
        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
        public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        public Box Shift(float dx, float dy) => new(MinX + dx, MaxX + dx, MinY + dy, MaxY + dy);
        // Rows become columns: lets one routine handle both axes.
        public Box Transposed => new(MinY, MaxY, MinX, MaxX);

        public bool Contains(Box o, float eps = 0.5f)
            => MinX <= o.MinX + eps && MinY <= o.MinY + eps && MaxX >= o.MaxX - eps && MaxY >= o.MaxY - eps;
    }

    // Labelled lines drawn by the view this frame.
    private static readonly List<(Vector2 A, Vector2 B, string Label, bool Spacing)> measures = [];

    private static Box BoxOf(HudElement el) {
        var c = new Vector3[4];
        el.GetScreenCorners(c);
        Bounds(c, out float a, out float b, out float d, out float e);
        return new Box(a, b, d, e);
    }

    // Labels show HUD canvas units (what the inspector shows), not raw screen pixels.
    private static float UnitsPerPixel(HudElement el) {
        var canvas = el?.Rect != null ? el.Rect.GetComponentInParent<Canvas>() : null;
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        return scale > 0f ? 1f / scale : 1f;
    }

    private static void AddMeasure(Vector2 a, Vector2 b, float unitsPerPixel, bool spacing = false) {
        float d = (b - a).magnitude;
        if (d < 0.5f || float.IsNaN(d)) {
            return;
        }
        measures.Add((a, b, Mathf.RoundToInt(d * unitsPerPixel).ToString(), spacing));
    }

    // ---- Alt measuring ----

    private static void UpdateMeasure(bool overUi) {
        measures.Clear();
        if (!Alt || overUi || drag != DragMode.None || selection.Count == 0) {
            return;
        }
        if (!UnionBounds(selection, out float minX, out float maxX, out float minY, out float maxY)) {
            return;
        }
        var a = new Box(minX, maxX, minY, maxY);
        float units = UnitsPerPixel(Primary);
        var b = hovered != null && !selection.Contains(hovered)
            ? BoxOf(hovered)
            : new Box(0f, Screen.width, 0f, Screen.height);
        Measure(a, b, units);
    }

    private static void Measure(Box a, Box b, float units) {
        if (b.Contains(a)) {
            Inside(a, b, units);
            return;
        }
        if (a.Contains(b)) {
            Inside(b, a, units);
            return;
        }
        float? ox = Overlap(a.MinX, a.MaxX, b.MinX, b.MaxX);
        float? oy = Overlap(a.MinY, a.MaxY, b.MinY, b.MaxY);
        bool overlapping = a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinY < b.MaxY && b.MinY < a.MaxY;
        if (overlapping && ox.HasValue && oy.HasValue) {
            // Between matching edges.
            AddMeasure(new(a.MinX, oy.Value), new(b.MinX, oy.Value), units);
            AddMeasure(new(a.MaxX, oy.Value), new(b.MaxX, oy.Value), units);
            AddMeasure(new(ox.Value, a.MinY), new(ox.Value, b.MinY), units);
            AddMeasure(new(ox.Value, a.MaxY), new(ox.Value, b.MaxY), units);
            return;
        }
        // The gaps, along the shared row / column (else from the selection's middle).
        float y = oy ?? a.Center.y;
        if (b.MinX >= a.MaxX) {
            AddMeasure(new(a.MaxX, y), new(b.MinX, y), units);
        } else if (b.MaxX <= a.MinX) {
            AddMeasure(new(b.MaxX, y), new(a.MinX, y), units);
        }
        float x = ox ?? a.Center.x;
        if (b.MinY >= a.MaxY) {
            AddMeasure(new(x, a.MaxY), new(x, b.MinY), units);
        } else if (b.MaxY <= a.MinY) {
            AddMeasure(new(x, b.MaxY), new(x, a.MinY), units);
        }
    }

    // From the inner box to each edge of the outer one, along the inner box's centre lines.
    private static void Inside(Box inner, Box outer, float units) {
        var c = inner.Center;
        AddMeasure(new(outer.MinX, c.y), new(inner.MinX, c.y), units);
        AddMeasure(new(inner.MaxX, c.y), new(outer.MaxX, c.y), units);
        AddMeasure(new(c.x, outer.MinY), new(c.x, inner.MinY), units);
        AddMeasure(new(c.x, inner.MaxY), new(c.x, outer.MaxY), units);
    }

    private static float? Overlap(float a0, float a1, float b0, float b1) {
        float lo = Mathf.Max(a0, b0), hi = Mathf.Min(a1, b1);
        return lo <= hi ? (lo + hi) * 0.5f : null;
    }

    // ---- equal spacing ----

    // Ways the box can sit with equal gaps in its row (horizontal) or column: centred between its
    // nearest neighbours, as far after the previous one as that one is from the one before, or as
    // far before the next one as that one is from the one after. Each: the move along the row and
    // the two gaps (from, to, across) that are then equal.
    private static List<(float Delta, (float From, float To, float Across)[] Gaps)> Spacings(Box r, List<Box> boxes, bool horizontal) {
        Box T(Box b) => horizontal ? b : b.Transposed;
        r = T(r);
        var row = boxes.Select(T).Where(b => b.MinY < r.MaxY && b.MaxY > r.MinY).ToList();
        var before = row.Where(b => b.Center.x < r.Center.x).OrderByDescending(b => b.MaxX).ToList();
        var after = row.Where(b => b.Center.x >= r.Center.x).OrderBy(b => b.MinX).ToList();
        float Mid(Box a, Box b) => (Mathf.Max(a.MinY, b.MinY) + Mathf.Min(a.MaxY, b.MaxY)) * 0.5f;
        float w = r.Width;
        var outs = new List<(float, (float, float, float)[])>();
        if (before.Count > 0 && after.Count > 0 && after[0].MinX - before[0].MaxX > w) {
            var (l, n) = (before[0], after[0]);
            float x0 = (l.MaxX + n.MinX - w) * 0.5f;
            outs.Add((x0 - r.MinX, [(l.MaxX, x0, Mid(l, r)), (x0 + w, n.MinX, Mid(r, n))]));
        }
        if (before.Count > 1 && before[0].MinX > before[1].MaxX) {
            var (l, l2) = (before[0], before[1]);
            float x0 = l.MaxX + (l.MinX - l2.MaxX);
            outs.Add((x0 - r.MinX, [(l2.MaxX, l.MinX, Mid(l2, l)), (l.MaxX, x0, Mid(l, r))]));
        }
        if (after.Count > 1 && after[1].MinX > after[0].MaxX) {
            var (n, n2) = (after[0], after[1]);
            float x0 = n.MinX - (n2.MinX - n.MaxX) - w;
            outs.Add((x0 - r.MinX, [(x0 + w, n.MinX, Mid(r, n)), (n.MaxX, n2.MinX, Mid(n, n2))]));
        }
        return outs;
    }

    /// <summary>Correction along the axis that spaces the box equally, if one is within reach.</summary>
    private static float? EqualSpacing(Box r, List<Box> boxes, bool horizontal, float threshold) {
        float? best = null;
        foreach (var (delta, _) in Spacings(r, boxes, horizontal)) {
            if (Mathf.Abs(delta) <= threshold && (best == null || Mathf.Abs(delta) < Mathf.Abs(best.Value))) {
                best = delta;
            }
        }
        return best;
    }

    // Labels on the gaps that are equal right now.
    private static void AddSpacingGuides(Box r, List<Box> boxes, float units) {
        foreach (bool horizontal in new[] { true, false }) {
            foreach (var (delta, gaps) in Spacings(r, boxes, horizontal)) {
                if (Mathf.Abs(delta) > 0.5f) {
                    continue;
                }
                foreach (var (from, to, across) in gaps) {
                    var a = horizontal ? new Vector2(from, across) : new Vector2(across, from);
                    var b = horizontal ? new Vector2(to, across) : new Vector2(across, to);
                    AddMeasure(a, b, units, spacing: true);
                }
            }
        }
    }
}
