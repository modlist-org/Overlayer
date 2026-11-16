using O5Kit.Behaviour;
using O5Kit.Factory;
using Overlayer.Compat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Utility;

/// <summary>
/// Text alignment as two rows of icon buttons (like Unity's TextMeshPro inspector):
/// horizontal on top, vertical below. Icons are drawn from plain bars, no sprites needed.
/// </summary>
internal sealed class AlignPicker {
    private const float ButtonSize = 30f;
    private const float Gap = 3f;

    internal enum Mark { Left, Center, Right, Justified, Flush, Geometry, Top, Middle, Bottom, Baseline, Midline, Capline, DistributeX, DistributeY }

    // TMP keeps the horizontal part in the low byte and the vertical part in the next one.
    private static readonly int[] HorizontalValues = [1, 2, 4, 8, 16, 32];
    private static readonly int[] VerticalValues = [256, 512, 1024, 2048, 4096, 8192];
    private static readonly string[] HorizontalNames = ["Left", "Center", "Right", "Justified", "Flush", "Geometry center"];
    private static readonly string[] VerticalNames = ["Top", "Middle", "Bottom", "Baseline", "Midline", "Capline"];

    private static readonly Color Bar = new(1f, 1f, 1f, 0.85f);
    private static readonly Color Faint = new(1f, 1f, 1f, 0.3f);

    private readonly Func<TextAlignmentOptions> get;
    private readonly Action<TextAlignmentOptions> set;
    private readonly Image[] horizontal = new Image[6];
    private readonly Image[] vertical = new Image[6];

    public RectTransform Rect { get; }

    /// <summary>A row with <paramref name="label"/> on the left and the buttons on the right.</summary>
    public AlignPicker(Transform parent, string label, Func<TextAlignmentOptions> get, Action<TextAlignmentOptions> set) {
        this.get = get;
        this.set = set;
        var ctx = O5KitAdapters.Ctx;
        Rect = O5Factory.Row(ctx, parent, ButtonSize * 2f + Gap + 8f);

        var text = O5Factory.ControlText(ctx, Rect, 16f, true);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(14f, 0f);
        text.rectTransform.offsetMax = new Vector2(-(ButtonSize * 6f + Gap * 5f + 8f), 0f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.text = label;

        var grid = UiStyle.NewRect("Buttons", Rect);
        grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(1f, 0.5f);
        grid.anchoredPosition = new Vector2(-4f, 0f);
        grid.sizeDelta = new Vector2(ButtonSize * 6f + Gap * 5f, ButtonSize * 2f + Gap);
        for (int i = 0; i < 6; i++) {
            int index = i;
            horizontal[i] = Button(grid, i, 0, (Mark)i, HorizontalNames[i], () => Pick(HorizontalValues[index], 0xFF));
            vertical[i] = Button(grid, i, 1, (Mark)(i + 6), VerticalNames[i], () => Pick(VerticalValues[index], 0xFF00));
        }
        Refresh();
    }

    // Swap one half (horizontal or vertical), keep the other.
    private void Pick(int value, int mask) {
        int current = (int)get();
        set((TextAlignmentOptions)((current & ~mask) | value));
        Refresh();
    }

    /// <summary>Highlights the current alignment (call after it changed elsewhere).</summary>
    public void Refresh() {
        int current = (int)get();
        for (int i = 0; i < 6; i++) {
            horizontal[i].color = (current & 0xFF) == HorizontalValues[i] ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
            vertical[i].color = (current & 0xFF00) == VerticalValues[i] ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        }
    }

    private static Image Button(RectTransform grid, int column, int row, Mark mark, string tooltip, Action onClick) {
        var back = UiStyle.NewRect(mark.ToString(), grid).gameObject.AddComponent<Image>();
        back.raycastTarget = true;
        var r = back.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        r.anchoredPosition = new Vector2(column * (ButtonSize + Gap), -row * (ButtonSize + Gap));
        var icon = UiStyle.NewRect("Icon", r);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(18f, 16f);
        DrawIcon(icon, mark);
        var hover = UiStyle.NewRect("Hover", r).gameObject.AddComponent<Image>();
        hover.rectTransform.anchorMin = Vector2.zero;
        hover.rectTransform.anchorMax = Vector2.one;
        hover.rectTransform.offsetMin = hover.rectTransform.offsetMax = Vector2.zero;
        hover.color = Color.clear;
        hover.raycastTarget = false;
        UnityUtils.AddEvents(back.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => hover.color = new Color(1f, 1f, 1f, 0.08f)),
            (EventTriggerType.PointerExit, _ => hover.color = Color.clear),
            (EventTriggerType.PointerClick, _ => onClick()));
        r.AddToolTip(O5KitAdapters.Ctx, tooltip);
        return back;
    }

    // ---- icons: 18 x 16 ----

    /// <summary>Draws one of the marks into <paramref name="icon"/> (18 x 16).</summary>
    internal static void DrawIcon(RectTransform icon, Mark mark) {
        switch (mark) {
            case Mark.DistributeX:
            case Mark.DistributeY:
                // Three bars spread evenly, across or down.
                for (int i = 0; i < 3; i++) {
                    float spot = i * 0.5f;
                    var bar = mark == Mark.DistributeX
                        ? Box(icon, Bar, new Vector2(spot, 0f), new Vector2(spot, 1f), new Vector2(3f, 0f))
                        : Box(icon, Bar, new Vector2(0f, spot), new Vector2(1f, spot), new Vector2(0f, 3f));
                    bar.anchoredPosition = mark == Mark.DistributeX ? new Vector2((1 - i) * 1.5f, 0f) : new Vector2(0f, (1 - i) * 1.5f);
                }
                break;
            case Mark.Left:
                Lines(icon, [1f, 0.6f, 0.85f, 0.5f], 0f);
                break;
            case Mark.Center:
                Lines(icon, [1f, 0.6f, 0.85f, 0.5f], 0.5f);
                break;
            case Mark.Right:
                Lines(icon, [1f, 0.6f, 0.85f, 0.5f], 1f);
                break;
            case Mark.Justified:
                Lines(icon, [1f, 1f, 1f, 0.5f], 0f);
                break;
            case Mark.Flush:
                Lines(icon, [1f, 1f, 1f, 1f], 0f);
                break;
            case Mark.Geometry:
                Lines(icon, [0.55f, 0.9f, 0.45f, 0.75f], 0.5f);
                Box(icon, Faint, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f));
                break;
            case Mark.Top:
            case Mark.Middle:
            case Mark.Bottom:
                // A frame edge and three short lines sitting at the top / middle / bottom.
                float y = mark == Mark.Top ? 1f : mark == Mark.Middle ? 0.5f : 0f;
                Box(icon, Faint, new Vector2(0f, y), new Vector2(1f, y), new Vector2(0f, 1f));
                for (int i = 0; i < 3; i++) {
                    float lineY = mark switch {
                        Mark.Top => -3f - i * 3.5f,
                        Mark.Bottom => 3f + (2 - i) * 3.5f,
                        _ => (1 - i) * 3.5f,
                    };
                    var line = Box(icon, Bar, new Vector2(0.15f, y), new Vector2(0.85f, y), new Vector2(0f, 2f));
                    line.anchoredPosition = new Vector2(0f, lineY);
                }
                break;
            default:
                // An "A" with a line through its baseline / middle / cap height.
                var a = O5Factory.ControlText(O5KitAdapters.Ctx, icon, 17f, true);
                a.rectTransform.anchorMin = Vector2.zero;
                a.rectTransform.anchorMax = Vector2.one;
                a.rectTransform.offsetMin = a.rectTransform.offsetMax = Vector2.zero;
                a.alignment = TextAlignmentOptions.Center;
                a.color = Bar;
                a.raycastTarget = false;
                a.text = "A";
                float at = mark == Mark.Baseline ? 0.12f : mark == Mark.Midline ? 0.5f : 0.88f;
                Box(icon, new Color(0.569f, 0.604f, 1f, 1f), new Vector2(-0.1f, at), new Vector2(1.1f, at), new Vector2(0f, 1.5f));
                break;
        }
    }

    // Four text lines of the given widths (0..1), lined up at x (0 left, 0.5 middle, 1 right).
    private static void Lines(RectTransform icon, float[] widths, float x) {
        for (int i = 0; i < widths.Length; i++) {
            var line = Box(icon, Bar, new Vector2(x, 1f), new Vector2(x, 1f), new Vector2(18f * widths[i], 2f));
            line.pivot = new Vector2(x, 0.5f);
            line.anchoredPosition = new Vector2(0f, -1f - i * 4.5f);
        }
    }

    private static RectTransform Box(RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size) {
        var image = UiStyle.NewRect("Bar", parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var r = image.rectTransform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        return r;
    }
}
