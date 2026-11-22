using O5Kit.Control;
using Overlayer.UI.Utility;
using UnityEngine;

namespace Overlayer.HudEdit;

// Toolbar icons drawn from strokes (20 x 20, offsets from the middle): the bundled icon set
// has no check mark, undo arrow, grid or ruler.
internal static partial class HudEditor {
    private static Color Stroke => UiStyle.IconStroke;
    private static Color StrokeFaint => UiStyle.IconStrokeFaint;

    private static O5Button DrawnButton(Transform parent, string id, Action<RectTransform> draw, Action onClick, string tooltip, bool primary = false) {
        var button = Button(Cell(parent, ToolbarHeight - 12f), id, string.Empty, onClick, tooltip, primary);
        UiStyle.AddDrawnIcon(button, draw);
        return button;
    }

    private static void Polyline(RectTransform icon, Color color, float thickness, params Vector2[] points) =>
        UiStyle.Polyline(icon, color, thickness, points);

    private static void Outline(RectTransform icon, Color color, float x0, float y0, float x1, float y1) => Polyline(icon, color, 1.5f,
        new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0));

    private static void DrawCheck(RectTransform icon) => Polyline(icon, Color.white, 2.5f, new(-6.5f, 0f), new(-2f, -4.5f), new(6.5f, 5f));

    private static void DrawUndo(RectTransform icon, bool redo) => UiStyle.DrawUndo(icon, redo);

    private static void DrawGrid(RectTransform icon) {
        Outline(icon, Stroke, -7f, -7f, 7f, 7f);
        foreach (float v in new[] { -2.3f, 2.3f }) {
            AlignPicker.Segment(icon, new(v, -7f), new(v, 7f), 1f, StrokeFaint);
            AlignPicker.Segment(icon, new(-7f, v), new(7f, v), 1f, StrokeFaint);
        }
    }

    private static void DrawRuler(RectTransform icon) {
        Outline(icon, Stroke, -8.5f, -4f, 8.5f, 4f);
        for (int i = 0; i < 5; i++) {
            float x = -5.5f + i * 3f;
            AlignPicker.Segment(icon, new(x, 4f), new(x, i % 2 == 0 ? -0.5f : 1.5f), 1.2f, Stroke);
        }
    }

    // Two sheets, one over the other.
    private static void DrawCanvases(RectTransform icon) {
        // The back sheet only where the front one doesn't cover it.
        Polyline(icon, StrokeFaint, 1.5f, new(-3f, -1.5f), new(-7f, -1.5f), new(-7f, 7f), new(3f, 7f), new(3f, 2.5f));
        Outline(icon, Stroke, -3f, -6.5f, 7f, 2.5f);
    }
}
