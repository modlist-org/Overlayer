using O5Kit.Behaviour;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
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
/// horizontal on top, vertical below. Alignment icons use bundled sprites (flipped for Right and Bottom).
/// </summary>
internal sealed class AlignPicker {
    private const float ButtonSize = 30f;
    private const float Gap = 3f;

    internal enum Mark { Left, Center, Right, Justified, Flush, Geometry, Top, Middle, Bottom, Baseline, Midline, Capline, DistributeX, DistributeY }

    // TMP keeps the horizontal part in the low byte and the vertical part in the next one.
    private static readonly int[] HorizontalValues = [1, 2, 4, 8, 16, 32];
    private static readonly int[] VerticalValues = [256, 512, 1024, 2048, 4096, 8192];
    private static readonly (string Key, string Fallback)[] HorizontalNames = [
        ("ALIGN_LEFT", "Left"),
        ("ALIGN_CENTER", "Center"),
        ("ALIGN_RIGHT", "Right"),
        ("ALIGN_JUSTIFIED", "Justified"),
        ("ALIGN_FLUSH", "Flush"),
        ("ALIGN_GEOMETRY", "Geometry center"),
    ];
    private static readonly (string Key, string Fallback)[] VerticalNames = [
        ("ALIGN_TOP", "Top"),
        ("ALIGN_MIDDLE", "Middle"),
        ("ALIGN_BOTTOM", "Bottom"),
        ("ALIGN_BASELINE", "Baseline"),
        ("ALIGN_MIDLINE", "Midline"),
        ("ALIGN_CAPLINE", "Capline"),
    ];

    private static readonly Color Bar = new(1f, 1f, 1f, 0.85f);

    private readonly Func<TextAlignmentOptions> get;
    private readonly Action<TextAlignmentOptions> set;
    private readonly Action<int, int> pickMask;
    private readonly Image[] horizontal = new Image[6];
    private readonly Image[] vertical = new Image[6];

    public RectTransform Rect { get; }

    /// <summary>A row with <paramref name="label"/> on the left and the buttons on the right.</summary>
    public AlignPicker(Transform parent, string label, Func<TextAlignmentOptions> get, Action<TextAlignmentOptions> set,
        Action<int, int> pickMask = null) {
        this.get = get;
        this.set = set;
        this.pickMask = pickMask;
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
            var h = HorizontalNames[i];
            var v = VerticalNames[i];
            horizontal[i] = Button(grid, i, 0, (Mark)i, () => MainCore.Tr.Get(h.Key, h.Fallback), () => Pick(HorizontalValues[index], 0xFF));
            vertical[i] = Button(grid, i, 1, (Mark)(i + 6), () => MainCore.Tr.Get(v.Key, v.Fallback), () => Pick(VerticalValues[index], 0xFF00));
        }
        Refresh();
    }

    // Swap one half (horizontal or vertical), keep the other.
    private void Pick(int value, int mask) {
        if (pickMask != null) {
            pickMask(value, mask);
            Refresh();
            return;
        }
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

    private static Image Button(RectTransform grid, int column, int row, Mark mark, Func<string> tooltip, Action onClick) {
        var back = UiStyle.NewRect(mark.ToString(), grid).gameObject.AddComponent<Image>();
        back.raycastTarget = true;
        var r = back.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        r.anchoredPosition = new Vector2(column * (ButtonSize + Gap), -row * (ButtonSize + Gap));
        var icon = UiStyle.NewRect("Icon", r);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(18f, 18f);
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

    /// <summary>Draws one of the marks into <paramref name="icon"/>, centered (18 x 18).</summary>
    internal static void DrawIcon(RectTransform icon, Mark mark) {
        switch (mark) {
            case Mark.Left:
                SpriteIcon(icon, UISprite.AliLeft128);
                break;
            case Mark.Center:
                SpriteIcon(icon, UISprite.AliCenter128);
                break;
            case Mark.Right:
                SpriteIcon(icon, UISprite.AliLeft128, flipX: true);
                break;
            case Mark.Justified:
                SpriteIcon(icon, UISprite.AliJustified128);
                break;
            case Mark.Flush:
                SpriteIcon(icon, UISprite.AliFlush128);
                break;
            case Mark.Geometry:
                SpriteIcon(icon, UISprite.AliGeometry128);
                break;
            case Mark.Top:
                SpriteIcon(icon, UISprite.AliTop128);
                break;
            case Mark.Middle:
                SpriteIcon(icon, UISprite.AliMiddle128);
                break;
            case Mark.Bottom:
                SpriteIcon(icon, UISprite.AliTop128, flipY: true);
                break;
            case Mark.Baseline:
                SpriteIcon(icon, UISprite.AliBaseline128);
                break;
            case Mark.Midline:
                SpriteIcon(icon, UISprite.AliMidline128);
                break;
            case Mark.Capline:
                SpriteIcon(icon, UISprite.AliCapline128);
                break;
            case Mark.DistributeX:
            case Mark.DistributeY:
                // Three blocks spread evenly, across or down.
                for (int i = -1; i <= 1; i++) {
                    var block = Box(icon, Bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        mark == Mark.DistributeX ? new Vector2(3f, 14f) : new Vector2(16f, 3f));
                    block.anchoredPosition = mark == Mark.DistributeX ? new Vector2(i * 7f, 0f) : new Vector2(0f, i * 6f);
                }
                break;
        }
    }

    private static void SpriteIcon(RectTransform icon, UISprite sprite, bool flipX = false, bool flipY = false) {
        var image = icon.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(sprite);
        image.preserveAspect = true;
        image.color = Bar;
        image.raycastTarget = false;
        if (flipX || flipY) {
            icon.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);
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
