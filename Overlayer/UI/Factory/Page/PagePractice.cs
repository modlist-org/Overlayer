using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using O5Kit.Input;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI.Utility;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

// The tutorial's practice room: a page with no menu entry (only the tutorial opens it). Each
// control reports how it was used so a step can wait for exactly that (drag, wheel, typed math,
// hold-to-delete, Shift-delete, scrolling).
internal static class PagePractice {
    private const int RowCount = 4;
    private const int FillerCount = 14;

    public static O5Slider Slider { get; private set; }
    public static O5Dropdown<string> Dropdown { get; private set; }
    public static UIScrollController Scroller { get; private set; }
    public static RectTransform DeleteArea { get; private set; }
    public static RectTransform ScrollArea { get; private set; }

    // What happened since the last Reset.
    public static bool Dragged, Wheeled, TypedMath, DropdownWheeled, HeldDelete, ShiftDelete, MiddleReset;
    public static float Scrolled;

    private static RectTransform content;
    private static readonly List<GameObject> rows = [];
    private static readonly List<O5Kit.Core.O5Object> rowControls = [];
    private static string typing = string.Empty;
    private static float lastScrollY;
    private const double SliderDefault = 50;
    private static bool sliderOffDefault;
    private static int lastMiddleFrame = -100;

    public static void Create(RectTransform page) {
        var ctx = O5KitAdapters.Ctx;
        var (_, contentRect, scroller) = O5Factory.ScrollView(ctx, page, 12f, 18f);
        content = contentRect;
        Scroller = scroller;

        var title = O5Factory.ControlTextH1(ctx, O5Factory.Row(ctx, content));
        title.text = MainCore.Tr.Get("PRACTICE_TITLE", "Practice room");
        var hint = O5Factory.ControlText(ctx, O5Factory.Row(ctx, content), 16f);
        hint.text = MainCore.Tr.Get("PRACTICE_DESC", "Nothing here is saved. Try anything.");
        hint.color = UiStyle.Muted;

        Slider = O5Factory.Slider(ctx, O5Factory.Row(ctx, content), "practice_slider", new O5SliderOptions {
            DefaultValue = SliderDefault,
            Value = SliderDefault,
            Min = 0,
            Max = 100,
            Format = "0",
            Label = MainCore.Tr.Get("PRACTICE_SLIDER", "Practice slider"),
            ClampDrag = true,
            OnChanged = _ => {
                if (O5Input.GetMouseButton(0)) {
                    Dragged = true;
                }
            },
            OnComplete = _ => {
                if (O5Input.GetMouseButton(0)) {
                    Dragged = true;
                } else if (Mathf.Abs(O5Input.MouseScrollDelta.y) > 0.0001f) {
                    Wheeled = true;
                } else if (IsMath(typing)) {
                    TypedMath = true;
                }
                typing = string.Empty;
            },
        });

        string[] fruits = ["Apple", "Banana", "Cherry", "Grape", "Lemon", "Melon"];
        Dropdown = O5Factory.DropDown(ctx, O5Factory.Row(ctx, content), "practice_dropdown", new O5DropdownOptions<string> {
            Values = [.. fruits],
            Value = fruits[0],
            DefaultValue = fruits[0],
            Display = f => $"{MainCore.Tr.Get("PRACTICE_DROPDOWN", "Fruit")}: {MainCore.Tr.Get("PRACTICE_FRUIT_" + f.ToUpperInvariant(), f)}",
            OnChanged = _ => {
                if (Mathf.Abs(O5Input.MouseScrollDelta.y) > 0.0001f) {
                    DropdownWheeled = true;
                }
            },
        });

        DeleteArea = UiStyle.NewRect("DeleteRows", content);
        var deleteLayout = UiStyle.Vertical(DeleteArea.gameObject, 6f);
        deleteLayout.childForceExpandHeight = false;
        RefillRows();

        ScrollArea = UiStyle.NewRect("Filler", content);
        var fillerLayout = UiStyle.Vertical(ScrollArea.gameObject, 6f);
        fillerLayout.childForceExpandHeight = false;
        for (int i = 1; i <= FillerCount; i++) {
            var text = O5Factory.ControlText(ctx, O5Factory.Row(ctx, ScrollArea, 40f), 16f);
            text.text = string.Format(MainCore.Tr.Get("PRACTICE_ITEM", "Item {0}"), i);
            text.color = UiStyle.Muted;
        }
    }

    /// <summary>Puts every control back to its starting state (called each time the page is entered).</summary>
    public static void ResetAll() {
        Slider?.Set(SliderDefault, false);
        Dropdown?.Set(Dropdown.Values.Count > 0 ? Dropdown.Values[0] : null, false);
        RefillRows();
        Scroller?.ScrollTo(0f, true);
        Reset();
    }

    /// <summary>Clears what was recorded (a step starts fresh).</summary>
    public static void Reset() {
        Dragged = Wheeled = TypedMath = DropdownWheeled = HeldDelete = ShiftDelete = MiddleReset = false;
        sliderOffDefault = Slider != null && !AtDefault();
        Scrolled = 0f;
        typing = string.Empty;
        lastScrollY = Scroller != null && Scroller.content != null ? Scroller.content.anchoredPosition.y : 0f;
    }

    /// <summary>Puts the deletable rows back.</summary>
    public static void RefillRows() {
        foreach (var control in rowControls) {
            control.Dispose();
        }
        rowControls.Clear();
        foreach (var row in rows) {
            if (row != null) {
                UnityEngine.Object.Destroy(row);
            }
        }
        rows.Clear();
        if (DeleteArea == null) {
            return;
        }
        var ctx = O5KitAdapters.Ctx;
        for (int i = 1; i <= RowCount; i++) {
            var row = UiStyle.HRow(DeleteArea, 8f, 44f);
            rows.Add(row.gameObject);
            var label = O5Factory.ControlText(ctx, UiStyle.Cell(row, 0f), 17f);
            label.text = string.Format(MainCore.Tr.Get("PRACTICE_ROW", "Deletable row {0}"), i);
            var del = UiStyle.IconButton(UiStyle.Cell(row, 44f), $"practice_del_{i}", UIIcon.X, () => { });
            rowControls.Add(del);
            var captured = row.gameObject;
            HoldToConfirm.Attach(del, () => {
                // Shift skips the hold; a held delete takes the full fill.
                if (O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift)) {
                    ShiftDelete = true;
                } else {
                    HeldDelete = true;
                }
                UISound.PlayConfirm();
                rows.Remove(captured);
                UnityEngine.Object.Destroy(captured);
            });
        }
    }

    public static int RowsLeft => rows.Count(r => r != null);

    private static bool AtDefault() => Math.Abs(Slider.Value - SliderDefault) < 0.0001;

    public static void Tick() {
        // Middle-click reset: the slider snaps back to its default right around a middle-button release.
        if (Slider != null) {
            if (O5Input.GetMouseButton(2) || O5Input.GetMouseButtonUp(2)) {
                lastMiddleFrame = Time.frameCount;
            }
            bool atDefault = AtDefault();
            if (atDefault && sliderOffDefault && Time.frameCount - lastMiddleFrame <= 2) {
                MiddleReset = true;
            }
            sliderOffDefault = !atDefault;
        }
        if (Slider != null && Slider.InputCore.InputField != null && Slider.InputCore.InputField.isFocused) {
            typing = Slider.InputCore.InputField.text ?? string.Empty;
        }
        if (Scroller != null && Scroller.content != null) {
            float y = Scroller.content.anchoredPosition.y;
            Scrolled += Mathf.Abs(y - lastScrollY);
            lastScrollY = y;
        }
    }

    // An expression, not just a number ("50*2", "(10+5)/3" ...).
    private static bool IsMath(string text) {
        string t = (text ?? string.Empty).Trim();
        if (t.Length == 0 || double.TryParse(t, out _)) {
            return false;
        }
        return t.IndexOfAny(['+', '-', '*', '/', '^', '%', '(', ')']) > 0 || t.StartsWith("(");
    }
}
