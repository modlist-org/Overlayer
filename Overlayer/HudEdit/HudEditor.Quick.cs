using O5Kit.Control;
using O5Kit.Factory;
using O5Kit.Behaviour;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
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

// The easy part of the side panel: name, size, where it's pinned, opacity, text and colors.
// Everything else is under "All properties" (Overlayer's full inspector), folded by default.
internal static partial class HudEditor {
    private static O5InputField nameInput, textInput;
    private static O5Slider wSlider, hSlider, opacitySlider, fontSizeSlider;
    private static O5ColorPicker textColorPicker, imageColorPicker;
    private static GameObject textRows, imageRows, anchorRow, allPropsRow;
    private static readonly Image[] anchorCells = new Image[9];
    private static O5Button allPropsButton;
    private static bool allPropsShown;

    private static readonly Color AnchorIdle = new(1f, 1f, 1f, 0.12f);

    private static void BuildQuickName(O5Kit.Core.O5Context ctx, Transform parent) {
        nameInput = Track(O5Factory.Input(ctx, O5Factory.Row(ctx, parent, 44f), "plus_hud_name", new O5InputOptions {
            Placeholder = "Name",
            OnEndEdit = value => {
                if (!string.IsNullOrWhiteSpace(value)) {
                    QuickEdit("name", o => o.Config.Name.Value, (o, v) => o.Config.Name.Value = v, value.Trim(), true);
                }
                layersDirty = true;
            },
        }));
    }

    private static void BuildQuickSize(O5Kit.Core.O5Context ctx, Transform parent) {
        wSlider = Slider(ctx, parent, "w", "Width", 0, 1500, "F0",
            (ref HudElement.State s, double v) => s.Size = new Vector2(Mathf.Max(0f, (float)v), s.Size.y));
        hSlider = Slider(ctx, parent, "h", "Height", 0, 1000, "F0",
            (ref HudElement.State s, double v) => s.Size = new Vector2(s.Size.x, Mathf.Max(0f, (float)v)));
    }

    // 3x3 picker: which corner / edge / middle of the parent it sticks to when the screen
    // size changes. The object stays where it is on screen.
    private static void BuildAnchorPicker(O5Kit.Core.O5Context ctx, Transform parent) {
        var row = UiStyle.HRow(parent, 10f, 84f);
        anchorRow = row.gameObject;
        var label = O5Factory.ControlText(ctx, Cell(row, 0f), 15f);
        label.text = "Pin to\n<size=12><alpha=#88>keeps its spot when the\nscreen size changes</size>";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        var grid = Cell(row, 84f);
        for (int i = 0; i < 9; i++) {
            int col = i % 3, line = i / 3;
            var cell = NewImage("Pin" + i, grid, AnchorIdle, MainCore.Spr.Get(Overlayer.Resource.UISprite.Circle256));
            cell.raycastTarget = true;
            var r = cell.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(col * 0.5f, 1f - line * 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            // Inset so the outer dots sit inside the 84px square.
            r.anchoredPosition = new Vector2((1 - col) * 12f, (line - 1) * 12f);
            r.sizeDelta = new Vector2(14f, 14f);
            var anchor = new Vector2(col * 0.5f, 1f - line * 0.5f);
            UnityUtils.AddEvents(cell.gameObject.AddComponent<EventTrigger>(), (EventTriggerType.PointerClick, _ => PinTo(anchor)));
            anchorCells[i] = cell;
        }
        var frame = NewImage("Frame", grid, new Color(1f, 1f, 1f, 0.05f));
        frame.rectTransform.anchorMin = Vector2.zero;
        frame.rectTransform.anchorMax = Vector2.one;
        frame.rectTransform.offsetMin = new Vector2(4f, 4f);
        frame.rectTransform.offsetMax = new Vector2(-4f, -4f);
        frame.rectTransform.SetAsFirstSibling();
    }

    private static void BuildQuickLook(O5Kit.Core.O5Context ctx, Transform parent) {
        opacitySlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, parent), "plus_hud_opacity", new O5SliderOptions {
            Min = 0,
            Max = 100,
            Label = "Opacity",
            Format = "F0",
        }));
        opacitySlider.OnChanged = v => QuickEdit("opacity", Alpha, SetAlpha, Mathf.Clamp01((float)v / 100f), false);
        opacitySlider.OnComplete = v => QuickEdit("opacity", Alpha, SetAlpha, Mathf.Clamp01((float)v / 100f), true);

        textRows = VerticalGroup("Text", parent).gameObject;
        textInput = Track(O5Factory.Input(ctx, O5Factory.Row(ctx, textRows.transform, 96f), "plus_hud_text", new O5InputOptions {
            Placeholder = "Text  (tags like {Combo} work here)",
            Multiline = true,
            OnChanged = v => QuickEdit("text", TextOf, SetText, v, false),
            OnEndEdit = v => QuickEdit("text", TextOf, SetText, v, true),
        }));
        fontSizeSlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, textRows.transform), "plus_hud_fontsize", new O5SliderOptions {
            Min = 4,
            Max = 200,
            Label = "Font size",
            Format = "F0",
            ClampDrag = false,
            ClampInput = false,
        }));
        fontSizeSlider.OnChanged = v => QuickEdit("fontsize", o => o.Config.TextConfig.FontSize.StaticValue,
            (o, s) => o.Config.TextConfig.FontSize.StaticValue = s, Mathf.Max(1f, (float)v), false);
        fontSizeSlider.OnComplete = v => QuickEdit("fontsize", o => o.Config.TextConfig.FontSize.StaticValue,
            (o, s) => o.Config.TextConfig.FontSize.StaticValue = s, Mathf.Max(1f, (float)v), true);
        textColorPicker = ColorRow(ctx, textRows.transform, "textcolor", "Text color", (c, done) => QuickEdit("textcolor",
            o => o.Config.TextConfig.Color.StaticValue, (o, g) => o.Config.TextConfig.Color.StaticValue = g, new GradientColor(c, true), done));

        imageRows = VerticalGroup("Image", parent).gameObject;
        imageColorPicker = ColorRow(ctx, imageRows.transform, "imagecolor", "Color", (c, done) => QuickEdit("imagecolor",
            o => o.Config.ImageConfig.Color.StaticValue, (o, v) => o.Config.ImageConfig.Color.StaticValue = v, c, done));
    }

    private static O5ColorPicker ColorRow(O5Kit.Core.O5Context ctx, Transform parent, string id, string label, System.Action<Color, bool> edit) {
        var picker = Track(O5Factory.ColorPicker(ctx, O5Factory.Row(ctx, parent, 50f), "plus_hud_" + id, new O5ColorPickerOptions {
            CanvasRect = UICore.CanvasObj.GetComponent<RectTransform>(),
            CanvasCamera = UICore.Canvas ? UICore.Canvas.worldCamera : null,
            Label = label,
        }));
        picker.OnChanged = c => edit(c, false);
        picker.OnComplete = c => edit(c, true);
        return picker;
    }

    private static void BuildAllPropsToggle(Transform parent) {
        var row = UiStyle.HRow(parent, 6f, 36f);
        allPropsRow = row.gameObject;
        allPropsButton = Button(Cell(row, 0f), "allprops", string.Empty, () => {
            allPropsShown = !allPropsShown;
            RefreshPanel();
        }, "Every setting Overlayer has for this object", align: TextAlignmentOptions.MidlineLeft);
    }

    private static float Alpha(OvObject o) => o.Config.CanvasGroupConfig?.Alpha.StaticValue ?? 1f;

    private static void SetAlpha(OvObject o, float alpha) {
        if (o.Config.CanvasGroupConfig == null) {
            o.Config.CanvasGroupConfig = new CanvasGroupSettings();
            o.ApplyComponent();
        }
        o.Config.CanvasGroupConfig.Alpha.StaticValue = alpha;
    }

    private static string TextOf(OvObject o) => o.Config.TextEngineConfig?.Text.StaticValue ?? string.Empty;

    private static void SetText(OvObject o, string text) {
        o.Config.TextEngineConfig ??= new OvTextSettings();
        o.Config.TextEngineConfig.Text.StaticValue = text ?? string.Empty;
    }

    // ---- one undo step per field edit (a whole slider drag, a whole typing session) ----

    private static string liveKey;
    private static OvObject liveObject;
    private static object liveBefore;
    private static System.Func<object> liveGet;
    private static System.Action<object> liveSet;

    private static void QuickEdit<T>(string key, System.Func<OvObject, T> get, System.Action<OvObject, T> set, T value, bool done) {

        if (syncingQuick) {
            return;
        }
        var el = Primary;
        if (el == null || !el.IsAlive || el.Locked || selection.Count != 1) {
            return;
        }
        var obj = el.Object;
        if (liveKey != key || !ReferenceEquals(liveObject, obj)) {
            CommitQuickEdit();
            liveKey = key;
            liveObject = obj;
            liveBefore = get(obj);
            liveGet = () => get(obj);
            liveSet = v => {
                set(obj, (T)v);
                obj.ApplyConfig();
            };
        }
        set(obj, value);
        obj.ApplyConfig();
        collectedFrame = -1000;
        if (done) {
            CommitQuickEdit();
        }
    }

    private static void CommitQuickEdit() {
        if (liveObject == null) {
            return;
        }
        object before = liveBefore, after = liveGet();
        var set = liveSet;
        liveObject = null;
        liveKey = null;
        if (Equals(before, after)) {
            return;
        }
        void Apply(object v) {
            set(v);
            Save();
            layersDirty = true;
            RefreshInspectorSoon();
            SyncPanelValues();
        }
        undo.Push(new ActionCommand(() => Apply(before), () => Apply(after)));
        Save();
        layersDirty = true;
        RefreshInspectorSoon();
    }

    // ---- pin to ----

    private struct Pin {
        public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
    }

    private static Pin PinOf(RectTransformSettings c) => new() {
        AnchorMin = c.AnchorMin.StaticValue,
        AnchorMax = c.AnchorMax.StaticValue,
        Pivot = c.Pivot.StaticValue,
        Position = c.AnchoredPosition.StaticValue,
        Size = c.SizeDelta.StaticValue,
    };

    private static bool CanPin(HudElement el) => el.CanMove && !el.Config.AnchorMin.UseFx && !el.Config.AnchorMax.UseFx
        && !el.Config.Pivot.UseFx && !el.Config.SizeDelta.UseFx;

    private static void PinTo(Vector2 anchor) {
        var el = Primary;
        if (el == null || !el.IsAlive || selection.Count != 1 || !CanPin(el)) {
            return;
        }
        CommitQuickEdit();
        var obj = el.Object;
        var cfg = el.Config;
        var before = PinOf(cfg);
        // Keep the real size (a stretched box gets its current size) and the spot on screen.
        Vector2 size = el.Rect.rect.size;
        el.Rect.GetWorldCorners(corners);
        Vector2 cornerBefore = corners[0];
        cfg.AnchorMin.StaticValue = cfg.AnchorMax.StaticValue = cfg.Pivot.StaticValue = anchor;
        cfg.SizeDelta.StaticValue = size;
        cfg.ToUnity(obj.GameObject);
        el.Rect.GetWorldCorners(corners);
        cfg.AnchoredPosition.StaticValue += el.ScreenToParentDelta(cornerBefore - (Vector2)corners[0]);
        cfg.ToUnity(obj.GameObject);
        var after = PinOf(cfg);

        void Set(Pin p) {
            var c = obj.Config.RectTransformConfig;
            if (c == null) {
                return;
            }
            c.AnchorMin.StaticValue = p.AnchorMin;
            c.AnchorMax.StaticValue = p.AnchorMax;
            c.Pivot.StaticValue = p.Pivot;
            c.AnchoredPosition.StaticValue = p.Position;
            c.SizeDelta.StaticValue = p.Size;
            c.ToUnity(obj.GameObject);
            Save();
            RefreshInspectorSoon();
            SyncPanelValues();
        }
        undo.Push(new ActionCommand(() => Set(before), () => Set(after)));
        Save();
        RefreshInspectorSoon();
        SyncPanelValues();
    }

    // ---- refresh ----

    private static bool syncingQuick;

    private static void RefreshQuick(HudElement el) {
        var cfg = el.Object.Config;
        bool editable = !el.Locked;
        nameInput.SetBlocked(!editable, true);
        wSlider.SetBlocked(!el.CanResizeX, true);
        hSlider.SetBlocked(!el.CanResizeY, true);
        anchorRow.SetActive(CanPin(el));
        opacitySlider.SetBlocked(!editable || cfg.CanvasGroupConfig?.Alpha.UseFx == true, true);

        bool isText = cfg.TextConfig != null;
        textRows.SetActive(isText);
        if (isText) {
            textInput.SetBlocked(!editable || cfg.TextEngineConfig?.Text.UseFx == true, true);
            fontSizeSlider.SetBlocked(!editable || cfg.TextConfig.FontSize.UseFx, true);
            textColorPicker.SetBlocked(!editable || cfg.TextConfig.Color.UseFx, true);
        }
        imageRows.SetActive(cfg.ImageConfig != null);
        if (cfg.ImageConfig != null) {
            imageColorPicker.SetBlocked(!editable || cfg.ImageConfig.Color.UseFx, true);
        }
        allPropsRow.SetActive(editable);
        allPropsButton.Text = allPropsShown ? "Hide all properties" : "All properties…";
    }

    // Values only (no visibility changes); runs while dragging too.
    private static void SyncQuickValues(HudElement el) {
        var cfg = el.Object.Config;
        syncingQuick = true;
        try {
            var s = el.Capture();
            wSlider.Set(s.Size.x, false);
            hSlider.Set(s.Size.y, false);
            if (!TypingIn(nameInput)) {
                nameInput.Set(cfg.Name.Value, false);
            }
            if (!ReferenceEquals(liveObject, el.Object) || liveKey != "opacity") {
                opacitySlider.Set(Alpha(el.Object) * 100f, false);
            }
            if (cfg.TextConfig != null) {
                if (!TypingIn(textInput)) {
                    textInput.Set(TextOf(el.Object), false);
                }
                fontSizeSlider.Set(cfg.TextConfig.FontSize.StaticValue, false);
                if (liveKey != "textcolor") {
                    textColorPicker.Set(cfg.TextConfig.Color.StaticValue.TL, false);
                }
            }
            if (cfg.ImageConfig != null && liveKey != "imagecolor") {
                imageColorPicker.Set(cfg.ImageConfig.Color.StaticValue, false);
            }
            // Highlight the pin that matches (none for a stretched box).
            Vector2 min = cfg.RectTransformConfig.AnchorMin.StaticValue, max = cfg.RectTransformConfig.AnchorMax.StaticValue;
            for (int i = 0; i < 9; i++) {
                var a = new Vector2(i % 3 * 0.5f, 1f - i / 3 * 0.5f);
                bool on = min == max && (min - a).sqrMagnitude < 1e-4f;
                anchorCells[i].color = on ? SelectedColor : AnchorIdle;
            }
        } finally {
            syncingQuick = false;
        }
    }

    private static bool TypingIn(O5InputField field) => field?.InputField != null && field.InputField.isFocused;
}
