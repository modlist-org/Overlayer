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
    private static AlignPicker textAlign;
    private static GameObject textRows, imageRows, anchorRow, allPropsRow, multiPropsRow;
    private static readonly Image[] anchorHits = new Image[9];
    private static readonly Image[][] anchorMarks = new Image[9][];
    private static Sprite anchorCornerSprite;
    private static int anchorSelected = -1;
    private static int anchorHovered = -1;
    private static O5Button allPropsButton, multiPropsButton;
    private static GameObject layoutRows, layoutDetails;
    private static readonly O5Button[] flowButtons = new O5Button[3];
    private static readonly O5Button[] alignButtons = new O5Button[3];
    private static O5Slider gapSlider, paddingSlider;
    private static bool allPropsShown, multiPropsShown, textInputEdited;

    private static bool MultiEditActive => selection.Count > 1 && multiPropsShown && selection.Any(e => e.IsAlive && !e.ReadOnly);
    private static IEnumerable<HudElement> EditableSelection() => selection.Where(e => e.IsAlive && !e.ReadOnly);

    private const float AnchorGridSize = 108f;
    private const float AnchorFrameInset = 4f;
    private const float AnchorUnit = (AnchorGridSize - AnchorFrameInset * 2f) / 25f;
    private static readonly Color AnchorIdle = new(1f, 1f, 1f, 0.24f);
    private static readonly Color AnchorHover = new(1f, 1f, 1f, 0.72f);
    private static readonly Color AnchorTileHover = new(1f, 1f, 1f, 0.06f);
    private static readonly (string Key, string Fallback)[] AnchorNames = [
        ("HUD_ANCHOR_TL", "Top Left"), ("HUD_ANCHOR_T", "Top"), ("HUD_ANCHOR_TR", "Top Right"),
        ("HUD_ANCHOR_L", "Left"), ("HUD_ANCHOR_C", "Center"), ("HUD_ANCHOR_R", "Right"),
        ("HUD_ANCHOR_BL", "Bottom Left"), ("HUD_ANCHOR_B", "Bottom"), ("HUD_ANCHOR_BR", "Bottom Right"),
    ];
    // Non-overlapping rects in the 25x25 unit box (top-left origin):
    // corners use EnsureAnchorCornerSprite() (6u L-bracket + inward diagonal arrow tail);
    // edges = 7u bar + 3u inward tick; center = 5u crosshair (+).
    private static readonly (float x, float y, float w, float h)[][] AnchorGlyphs = [
        [],                                                 // 0: Top-Left (corner sprite)
        [(9, 0, 7, 1), (12, 1, 1, 3)],                      // 1: Top
        [],                                                 // 2: Top-Right (corner sprite)
        [(0, 9, 1, 7), (1, 12, 3, 1)],                      // 3: Left
        [(10, 12, 5, 1), (12, 10, 1, 2), (12, 13, 1, 2)],   // 4: Center (+)
        [(24, 9, 1, 7), (21, 12, 3, 1)],                    // 5: Right
        [],                                                 // 6: Bottom-Left (corner sprite)
        [(9, 24, 7, 1), (12, 21, 1, 3)],                    // 7: Bottom
        [],                                                 // 8: Bottom-Right (corner sprite)
    ];

    private static Sprite EnsureAnchorCornerSprite() {
        if (anchorCornerSprite != null) {
            return anchorCornerSprite;
        }
        const int size = 96;
        const float pxPerUnit = size / 6f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[size * size];
        for (int py = 0; py < size; py++) {
            float uy = (size - 1 - py + 0.5f) / pxPerUnit;
            for (int px = 0; px < size; px++) {
                float ux = (px + 0.5f) / pxPerUnit;
                float aOrtho = (uy <= 1f || ux <= 1f) ? 1f : 0f;
                float along = (ux + uy) * 0.70710678f;
                float perp = Mathf.Abs(ux - uy) * 0.70710678f;
                float dDiag = Mathf.Max(perp - 0.5f, along - 5.15f);
                float aDiag = Mathf.Clamp01(0.5f - dDiag / 0.125f);
                byte a = (byte)Mathf.RoundToInt(Mathf.Max(aOrtho, aDiag) * 255f);
                pixels[py * size + px] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        anchorCornerSprite = Overlayer.Resource.SpriteManager.Create(tex);
        return anchorCornerSprite;
    }

    private static void BuildQuickName(O5Kit.Core.O5Context ctx, Transform parent) {
        nameInput = Track(O5Factory.Input(ctx, O5Factory.Row(ctx, parent, 44f), "plus_hud_name", new O5InputOptions {
            Placeholder = Tr("HUD_NAME", "Name"),
            OnEndEdit = value => {
                if (!string.IsNullOrWhiteSpace(value)) {
                    QuickEdit("name", o => o.Config.Name.Value, (o, v) => o.Config.Name.Value = v, value.Trim(), true);
                }
                layersDirty = true;
            },
        }));
    }

    private static void BuildQuickSize(O5Kit.Core.O5Context ctx, Transform parent) {
        wSlider = Slider(ctx, parent, "w", Tr("HUD_WIDTH", "Width"), 0, 1500, "F0",
            (ref HudElement.State s, double v) => s.Size = new Vector2(Mathf.Max(0f, (float)v), s.Size.y), e => e.CanResizeX);
        hSlider = Slider(ctx, parent, "h", Tr("HUD_HEIGHT", "Height"), 0, 1000, "F0",
            (ref HudElement.State s, double v) => s.Size = new Vector2(s.Size.x, Mathf.Max(0f, (float)v)), e => e.CanResizeY);
    }

    // 3x3 picker: which corner / edge / middle of the parent it sticks to when the screen
    // size changes. The object stays where it is on screen.
    private static void BuildAnchorPicker(O5Kit.Core.O5Context ctx, Transform parent) {
        var row = UiStyle.HRow(parent, 10f, AnchorGridSize);
        row.name = "PinTo";
        anchorRow = row.gameObject;
        var label = O5Factory.ControlText(ctx, Cell(row, 0f), 15f);
        label.text = $"{Tr("HUD_PIN_TO", "Pin to")}\n<size=12><alpha=#88>{Tr("HUD_PIN_TO_DESC", "keeps its spot when the\nscreen size changes")}</size>";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        var grid = Cell(row, AnchorGridSize);
        var frame = NewImage("Frame", grid, new Color(1f, 1f, 1f, 0.05f));
        var frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = new Vector2(AnchorFrameInset, AnchorFrameInset);
        frameRect.offsetMax = new Vector2(-AnchorFrameInset, -AnchorFrameInset);

        anchorSelected = -1;
        anchorHovered = -1;

        // 9-way partitioned hitbox covering the full picker area.
        for (int i = 0; i < 9; i++) {
            int col = i % 3, line = i / 3;
            int idx = i;
            var hit = NewImage("Pin" + i, frameRect, Color.clear);
            hit.raycastTarget = true;
            var r = hit.rectTransform;
            r.anchorMin = new Vector2(col / 3f, (2 - line) / 3f);
            r.anchorMax = new Vector2((col + 1) / 3f, (3 - line) / 3f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            var anchor = new Vector2(col * 0.5f, 1f - line * 0.5f);
            UnityUtils.AddEvents(hit.gameObject.AddComponent<EventTrigger>(),
                (EventTriggerType.PointerEnter, _ => {
                    anchorHovered = idx;
                    RefreshAnchorVisuals();
                }),
                (EventTriggerType.PointerExit, _ => {
                    if (anchorHovered == idx) {
                        anchorHovered = -1;
                        RefreshAnchorVisuals();
                    }
                }),
                (EventTriggerType.PointerClick, _ => PinTo(anchor)));
            var anchorName = AnchorNames[i];
            r.AddToolTip(ctx, Tr(anchorName.Key, anchorName.Fallback));
            anchorHits[i] = hit;
        }

        // Draw corner arrows, edge brackets, and center crosshair above the tiles.
        var cornerSprite = EnsureAnchorCornerSprite();
        for (int i = 0; i < 9; i++) {
            int col = i % 3, line = i / 3;
            if (col != 1 && line != 1) {
                var mark = NewImage("Mark", frameRect, AnchorIdle, cornerSprite);
                var mr = mark.rectTransform;
                mr.anchorMin = mr.anchorMax = new Vector2(0f, 1f);
                mr.pivot = new Vector2(0.5f, 0.5f);
                mr.sizeDelta = new Vector2(6f * AnchorUnit, 6f * AnchorUnit);
                mr.anchoredPosition = new Vector2((col == 0 ? 3f : 22f) * AnchorUnit, -(line == 0 ? 3f : 22f) * AnchorUnit);
                float angle = i switch {
                    2 => -90f,
                    6 => 90f,
                    8 => 180f,
                    _ => 0f,
                };
                mr.localEulerAngles = new Vector3(0f, 0f, angle);
                anchorMarks[i] = [mark];
                continue;
            }
            var glyphs = AnchorGlyphs[i];
            var marks = new Image[glyphs.Length];
            for (int j = 0; j < glyphs.Length; j++) {
                var (gx, gy, gw, gh) = glyphs[j];
                var mark = NewImage("Mark", frameRect, AnchorIdle);
                var mr = mark.rectTransform;
                mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0f, 1f);
                mr.anchoredPosition = new Vector2(gx * AnchorUnit, -gy * AnchorUnit);
                mr.sizeDelta = new Vector2(gw * AnchorUnit, gh * AnchorUnit);
                marks[j] = mark;
            }
            anchorMarks[i] = marks;
        }
    }

    private static void RefreshAnchorVisuals() {
        for (int i = 0; i < 9; i++) {
            bool on = i == anchorSelected;
            bool hover = i == anchorHovered;
            if (anchorHits[i] != null) {
                anchorHits[i].color = hover ? AnchorTileHover : Color.clear;
            }
            if (anchorMarks[i] is { } marks) {
                Color color = on ? SelectedColor : hover ? AnchorHover : AnchorIdle;
                for (int j = 0; j < marks.Length; j++) {
                    if (marks[j] != null) {
                        marks[j].color = color;
                    }
                }
            }
        }
    }

    private static void BuildQuickLook(O5Kit.Core.O5Context ctx, Transform parent) {
        opacitySlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, parent), "plus_hud_opacity", new O5SliderOptions {
            Min = 0,
            Max = 100,
            Label = Tr("HUD_OPACITY", "Opacity"),
            Format = "F0",
        }));
        opacitySlider.OnChanged = v => QuickEdit("opacity", Alpha, SetAlpha, Mathf.Clamp01((float)v / 100f), false,
            e => e.Object.Config.CanvasGroupConfig?.Alpha.UseFx != true);
        opacitySlider.OnComplete = v => QuickEdit("opacity", Alpha, SetAlpha, Mathf.Clamp01((float)v / 100f), true,
            e => e.Object.Config.CanvasGroupConfig?.Alpha.UseFx != true);

        textRows = VerticalGroup("Text", parent).gameObject;
        textInput = Track(O5Factory.Input(ctx, O5Factory.Row(ctx, textRows.transform, 96f), "plus_hud_text", new O5InputOptions {
            Placeholder = Tr("HUD_TEXT_PLACEHOLDER", "Text  (tags like {Combo} work here)"),
            Multiline = true,
            OnChanged = v => {
                textInputEdited = true;
                QuickEdit("text", TextOf, SetText, v, false, e => e.Object.Config.TextConfig != null && e.Object.Config.TextEngineConfig?.Text.UseFx != true);
            },
            OnEndEdit = v => {
                if (selection.Count == 1 || textInputEdited) {
                    textInputEdited = false;
                    QuickEdit("text", TextOf, SetText, v, true, e => e.Object.Config.TextConfig != null && e.Object.Config.TextEngineConfig?.Text.UseFx != true);
                }
            },
        }));
        fontSizeSlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, textRows.transform), "plus_hud_fontsize", new O5SliderOptions {
            Min = 4,
            Max = 200,
            Label = Tr("HUD_FONT_SIZE", "Font size"),
            Format = "F0",
            ClampDrag = false,
            ClampInput = false,
        }));
        fontSizeSlider.OnChanged = v => QuickEdit("fontsize", o => o.Config.TextConfig.FontSize.StaticValue,
            (o, s) => o.Config.TextConfig.FontSize.StaticValue = s, Mathf.Max(1f, (float)v), false,
            e => e.Object.Config.TextConfig != null && !e.Object.Config.TextConfig.FontSize.UseFx);
        fontSizeSlider.OnComplete = v => QuickEdit("fontsize", o => o.Config.TextConfig.FontSize.StaticValue,
            (o, s) => o.Config.TextConfig.FontSize.StaticValue = s, Mathf.Max(1f, (float)v), true,
            e => e.Object.Config.TextConfig != null && !e.Object.Config.TextConfig.FontSize.UseFx);
        textAlign = new AlignPicker(textRows.transform, Tr("HUD_ALIGNMENT", "Alignment"),
            GetQuickAlignment,
            value => QuickEdit("align", o => o.Config.TextConfig.Alignment.StaticValue, (o, a) => o.Config.TextConfig.Alignment.StaticValue = a, value, true,
                e => e.Object.Config.TextConfig != null && !e.Object.Config.TextConfig.Alignment.UseFx),
            (value, mask) => QuickEditMask("align", o => (int)o.Config.TextConfig.Alignment.StaticValue,
                (o, a) => o.Config.TextConfig.Alignment.StaticValue = (TextAlignmentOptions)a, value, mask,
                e => e.Object.Config.TextConfig != null && !e.Object.Config.TextConfig.Alignment.UseFx));
        textColorPicker = ColorRow(ctx, textRows.transform, "textcolor", Tr("HUD_TEXT_COLOR", "Text color"), (c, done) => QuickEdit("textcolor",
            o => o.Config.TextConfig.Color.StaticValue, (o, g) => o.Config.TextConfig.Color.StaticValue = g, new GradientColor(c, true), done,
            e => e.Object.Config.TextConfig != null && !e.Object.Config.TextConfig.Color.UseFx));

        imageRows = VerticalGroup("Image", parent).gameObject;
        imageColorPicker = ColorRow(ctx, imageRows.transform, "imagecolor", Tr("HUD_COLOR", "Color"), (c, done) => QuickEdit("imagecolor",
            o => o.Config.ImageConfig.Color.StaticValue, (o, v) => o.Config.ImageConfig.Color.StaticValue = v, c, done,
            e => e.Object.Config.ImageConfig != null && !e.Object.Config.ImageConfig.Color.UseFx));
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

    private static void BuildQuickLayout(O5Kit.Core.O5Context ctx, Transform parent) {
        layoutRows = VerticalGroup("AutoLayout", parent).gameObject;
        var rows = layoutRows.transform;
        var flowRow = UiStyle.HRow(rows, 4f, 34f);
        flowButtons[0] = Button(Cell(flowRow, 0f), "flow_off", Tr("HUD_OFF", "Off"), () => SetFlow(Flow.None), Tr("HUD_OFF", "Off"));
        flowButtons[1] = Button(Cell(flowRow, 0f), "flow_row", Tr("HUD_ROW", "Row"), () => SetFlow(Flow.Row), Tr("HUD_ROW", "Row"));
        flowButtons[2] = Button(Cell(flowRow, 0f), "flow_column", Tr("HUD_COLUMN", "Column"), () => SetFlow(Flow.Column), Tr("HUD_COLUMN", "Column"));
        layoutDetails = VerticalGroup("Details", rows).gameObject;
        var details = layoutDetails.transform;
        gapSlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, details), "plus_hud_gap", new O5SliderOptions {
            Min = 0,
            Max = 100,
            Label = Tr("HUD_GAP", "Gap"),
            Format = "F0",
            ClampDrag = false,
            ClampInput = false,
        }));
        gapSlider.OnChanged = v => QuickEdit("gap", o => GapOf(o).StaticValue, (o, g) => GapOf(o).StaticValue = g, Mathf.Max(0f, (float)v), false);
        gapSlider.OnComplete = v => QuickEdit("gap", o => GapOf(o).StaticValue, (o, g) => GapOf(o).StaticValue = g, Mathf.Max(0f, (float)v), true);
        paddingSlider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, details), "plus_hud_padding", new O5SliderOptions {
            Min = 0,
            Max = 100,
            Label = Tr("HUD_PADDING", "Padding"),
            Format = "F0",
            ClampDrag = false,
            ClampInput = false,
        }));
        paddingSlider.OnChanged = v => QuickEdit("padding", PaddingOf, SetPadding, Mathf.Max(0f, (float)v), false);
        paddingSlider.OnComplete = v => QuickEdit("padding", PaddingOf, SetPadding, Mathf.Max(0f, (float)v), true);
        var alignRow = UiStyle.HRow(details, 4f, 34f);
        for (int i = 0; i < 3; i++) {
            int where = i;
            alignButtons[i] = Button(Cell(alignRow, 0f), "flow_align" + i, string.Empty, () => {
                if (Primary is { } el) {
                    var anchor = CrossAlign(FlowOf(el.Object), where);
                    QuickEdit("flowalign", o => AlignOf(o).StaticValue, (o, a) => AlignOf(o).StaticValue = a, anchor, true);
                    SyncPanelValues();
                }
            });
        }
    }

    // One padding for all four sides (the full inspector has them separately).
    private static float PaddingOf(OvObject o) => PadHOf(o).StaticValue.x;

    private static void SetPadding(OvObject o, float p) {
        PadHOf(o).StaticValue = new Vector2(p, p);
        PadVOf(o).StaticValue = new Vector2(p, p);
    }

    private static void HighlightButton(O5Button button, bool on) {
        // Same as the toolbar toggles: the chosen one is the accent button, the rest are ghosts.
        button.NormalColor = on ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        button.UpdateVisual(true);
    }

    private static void BuildAllPropsToggle(Transform parent) {
        var row = UiStyle.HRow(parent, 6f, 36f);
        allPropsRow = row.gameObject;
        allPropsButton = Button(Cell(row, 0f), "allprops", string.Empty, () => {
            allPropsShown = !allPropsShown;
            RefreshPanel();
        }, Tr("HUD_ALL_PROPS_TIP", "All properties"), align: TextAlignmentOptions.MidlineLeft);
    }

    private static void BuildMultiPropsToggle(Transform parent) {
        var row = UiStyle.HRow(parent, 6f, 36f);
        multiPropsRow = row.gameObject;
        multiPropsButton = Button(Cell(row, 0f), "multiprops", string.Empty, () => {
            multiPropsShown = !multiPropsShown;
            RefreshPanel();
        }, Tr("HUD_MULTI_PROPS_TIP", "Edit properties of all selected objects"), align: TextAlignmentOptions.MidlineLeft);
    }

    private static void RefreshMultiPropsToggle() {
        if (multiPropsRow == null || multiPropsButton == null) {
            return;
        }
        bool anyEditable = selection.Any(e => e.IsAlive && !e.ReadOnly);
        multiPropsRow.SetActive(anyEditable);
        multiPropsButton.Text = multiPropsShown
            ? Tr("HUD_HIDE_MULTI_PROPS", "Hide properties")
            : Tr("HUD_MULTI_PROPS", "Edit properties…");
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
    private static readonly List<(OvObject Obj, object Before, System.Func<object> Get, System.Action<object> Set)> liveEdits = [];

    private static List<HudElement> QuickTargets(System.Func<HudElement, bool> filter) {
        if (selection.Count == 1) {
            var el = Primary;
            if (el == null || !el.IsAlive || el.ReadOnly || (filter != null && !filter(el))) {
                return [];
            }
            return [el];
        }
        if (!MultiEditActive) {
            return [];
        }
        return EditableSelection().Where(e => filter == null || filter(e)).ToList();
    }

    private static void QuickEdit<T>(string key, System.Func<OvObject, T> get, System.Action<OvObject, T> set, T value, bool done,
        System.Func<HudElement, bool> filter = null) {
        if (syncingQuick) {
            return;
        }
        var targets = QuickTargets(filter);
        if (targets.Count == 0) {
            return;
        }
        bool sameTargets = liveKey == key
            && liveEdits.Count == targets.Count
            && targets.Select((t, i) => ReferenceEquals(liveEdits[i].Obj, t.Object)).All(x => x);
        if (!sameTargets) {
            CommitQuickEdit();
            liveKey = key;
            foreach (var el in targets) {
                var obj = el.Object;
                liveEdits.Add((obj, get(obj), () => get(obj), v => {
                    set(obj, (T)v);
                    obj.ApplyConfig();
                }));
            }
        }
        foreach (var el in targets) {
            var obj = el.Object;
            set(obj, value);
            obj.ApplyConfig();
            if (obj.Parent != null && FlowOf(obj.Parent) != Flow.None) {
                RebuildLayoutTree(obj.Parent);
            }
        }
        collectedFrame = -1000;
        if (done) {
            CommitQuickEdit();
        }
    }

    private static void QuickEditMask(string key, System.Func<OvObject, int> get, System.Action<OvObject, int> set, int value, int mask,
        System.Func<HudElement, bool> filter = null) {
        if (syncingQuick) {
            return;
        }
        var targets = QuickTargets(filter);
        if (targets.Count == 0) {
            return;
        }
        CommitQuickEdit();
        var changes = new List<(OvObject Obj, int Before, int After)>();
        foreach (var el in targets) {
            var obj = el.Object;
            int before = get(obj);
            int after = (before & ~mask) | value;
            if (before == after) {
                continue;
            }
            set(obj, after);
            obj.ApplyConfig();
            changes.Add((obj, before, after));
        }
        if (changes.Count == 0) {
            return;
        }
        void Apply(bool redo) {
            foreach (var (obj, b, a) in changes) {
                set(obj, redo ? a : b);
                obj.ApplyConfig();
            }
            Save();
            layersDirty = true;
            RefreshInspectorSoon();
            SyncPanelValues();
        }
        undo.Push(new ActionCommand(() => Apply(false), () => Apply(true)));
        Save();
        layersDirty = true;
        RefreshInspectorSoon();
        SyncPanelValues();
    }

    private static void CommitQuickEdit() {
        if (liveEdits.Count == 0) {
            return;
        }
        var snapshot = liveEdits
            .Select(e => (e.Obj, e.Before, After: e.Get(), e.Set))
            .Where(e => !Equals(e.Before, e.After))
            .ToList();
        liveEdits.Clear();
        liveKey = null;
        if (snapshot.Count == 0) {
            return;
        }
        void Apply(bool redo) {
            foreach (var (obj, before, after, set) in snapshot) {
                set(redo ? after : before);
                if (obj.Parent != null && FlowOf(obj.Parent) != Flow.None) {
                    RebuildLayoutTree(obj.Parent);
                }
            }
            Save();
            layersDirty = true;
            RefreshInspectorSoon();
            SyncPanelValues();
        }
        undo.Push(new ActionCommand(() => Apply(false), () => Apply(true)));
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
        var targets = selection.Count == 1
            ? (Primary is { IsAlive: true } p && CanPin(p) ? new List<HudElement> { p } : [])
            : (MultiEditActive ? EditableSelection().Where(CanPin).ToList() : []);
        if (targets.Count == 0) {
            return;
        }
        CommitQuickEdit();
        var changes = new List<(OvObject Obj, Pin Before, Pin After)>();
        foreach (var el in targets) {
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
            changes.Add((obj, before, PinOf(cfg)));
        }

        void Apply(bool redo) {
            foreach (var (obj, b, a) in changes) {
                var c = obj.Config.RectTransformConfig;
                if (c == null) {
                    continue;
                }
                Pin p = redo ? a : b;
                c.AnchorMin.StaticValue = p.AnchorMin;
                c.AnchorMax.StaticValue = p.AnchorMax;
                c.Pivot.StaticValue = p.Pivot;
                c.AnchoredPosition.StaticValue = p.Position;
                c.SizeDelta.StaticValue = p.Size;
                c.ToUnity(obj.GameObject);
            }
            Save();
            RefreshInspectorSoon();
            SyncPanelValues();
        }
        undo.Push(new ActionCommand(() => Apply(false), () => Apply(true)));
        Save();
        RefreshInspectorSoon();
        SyncPanelValues();
    }

    // ---- refresh ----

    private static bool syncingQuick;

    private static void RefreshQuick(HudElement el) {
        var cfg = el.Object.Config;
        bool editable = !el.ReadOnly;
        nameInput.SetBlocked(!editable, true);
        wSlider.SetBlocked(!el.CanResizeX, true);
        hSlider.SetBlocked(!el.CanResizeY, true);
        bool canPin = CanPin(el);
        anchorRow.SetActive(canPin);
        if (!canPin) {
            anchorHovered = -1;
        }
        opacitySlider.SetBlocked(!editable || cfg.CanvasGroupConfig?.Alpha.UseFx == true, true);

        bool isText = cfg.TextConfig != null;
        textRows.SetActive(isText);
        if (isText) {
            textInput.SetBlocked(!editable || cfg.TextEngineConfig?.Text.UseFx == true, true);
            fontSizeSlider.SetBlocked(!editable || cfg.TextConfig.FontSize.UseFx, true);
            textColorPicker.SetBlocked(!editable || cfg.TextConfig.Color.UseFx, true);
            // Set by an expression: only the full inspector can show that.
            textAlign.Rect.gameObject.SetActive(editable && !cfg.TextConfig.Alignment.UseFx);
        }
        imageRows.SetActive(cfg.ImageConfig != null);
        if (cfg.ImageConfig != null) {
            imageColorPicker.SetBlocked(!editable || cfg.ImageConfig.Color.UseFx, true);
        }
        allPropsRow.SetActive(editable);
        bool layoutable = CanAutoLayout(el);
        layoutRows.SetActive(layoutable);
        if (layoutable) {
            var flow = FlowOf(el.Object);
            for (int i = 0; i < 3; i++) {
                HighlightButton(flowButtons[i], (int)flow == i);
            }
            layoutDetails.SetActive(flow != Flow.None);
            if (flow != Flow.None) {
                string[] names = flow == Flow.Row
                    ? [Tr("HUD_TOP", "Top"), Tr("HUD_MIDDLE", "Middle"), Tr("HUD_BOTTOM", "Bottom")]
                    : [Tr("HUD_LEFT", "Left"), Tr("HUD_CENTER", "Center"), Tr("HUD_RIGHT", "Right")];
                for (int i = 0; i < 3; i++) {
                    alignButtons[i].Text = names[i];
                }
                gapSlider.SetBlocked(GapOf(el.Object).UseFx, true);
                paddingSlider.SetBlocked(PadHOf(el.Object).UseFx || PadVOf(el.Object).UseFx, true);
            }
        }
        allPropsButton.Text = allPropsShown ? Tr("HUD_HIDE_ALL_PROPS", "Hide all properties") : Tr("HUD_ALL_PROPS", "All properties…");
    }

    private static void RefreshQuickMulti() {
        var editable = EditableSelection().ToList();
        xSlider.SetBlocked(!editable.Any(e => e.CanMove), true);
        ySlider.SetBlocked(!editable.Any(e => e.CanMove), true);
        scaleSlider.SetBlocked(!editable.Any(e => e.CanScale), true);
        rotationSlider.SetBlocked(!editable.Any(e => e.CanRotate), true);
        wSlider.SetBlocked(!editable.Any(e => e.CanResizeX), true);
        hSlider.SetBlocked(!editable.Any(e => e.CanResizeY), true);
        fitButton.Rect.parent.gameObject.SetActive(editable.Any(e => e.Object.GameObject.GetComponent<TextMeshProUGUI>() != null));
        ungroupButton.Rect.parent.gameObject.SetActive(editable.Any(e => e.Object.Children.Count > 0));

        bool canPin = editable.Any(CanPin);
        anchorRow.SetActive(canPin);
        if (!canPin) {
            anchorHovered = -1;
        }
        opacitySlider.SetBlocked(!editable.Any(e => e.Object.Config.CanvasGroupConfig?.Alpha.UseFx != true), true);

        var texts = editable.Where(e => e.Object.Config.TextConfig != null).ToList();
        textRows.SetActive(texts.Count > 0);
        if (texts.Count > 0) {
            textInput.SetBlocked(!texts.Any(e => e.Object.Config.TextEngineConfig?.Text.UseFx != true), true);
            fontSizeSlider.SetBlocked(!texts.Any(e => !e.Object.Config.TextConfig.FontSize.UseFx), true);
            textColorPicker.SetBlocked(!texts.Any(e => !e.Object.Config.TextConfig.Color.UseFx), true);
            textAlign.Rect.gameObject.SetActive(texts.Any(e => !e.Object.Config.TextConfig.Alignment.UseFx));
        }
        var images = editable.Where(e => e.Object.Config.ImageConfig != null).ToList();
        imageRows.SetActive(images.Count > 0);
        if (images.Count > 0) {
            imageColorPicker.SetBlocked(!images.Any(e => !e.Object.Config.ImageConfig.Color.UseFx), true);
        }
        allPropsRow.SetActive(false);
        layoutRows.SetActive(false);
    }

    private static TextAlignmentOptions GetQuickAlignment() {
        if (selection.Count == 1) {
            return Primary?.Object.Config.TextConfig?.Alignment.StaticValue ?? TextAlignmentOptions.Center;
        }
        var aligns = EditableSelection()
            .Select(e => e.Object.Config.TextConfig)
            .Where(t => t != null && !t.Alignment.UseFx)
            .Select(t => (int)t.Alignment.StaticValue)
            .ToList();
        if (aligns.Count == 0) {
            return 0;
        }
        int h = aligns[0] & 0xFF;
        int v = aligns[0] & 0xFF00;
        foreach (int a in aligns) {
            if ((a & 0xFF) != h) h = 0;
            if ((a & 0xFF00) != v) v = 0;
        }
        return (TextAlignmentOptions)(h | v);
    }

    private static void SetSliderMulti(O5Slider slider, IEnumerable<double> values, string format) {
        if (slider == null) {
            return;
        }
        using var e = values.GetEnumerator();
        if (!e.MoveNext()) {
            slider.Set(slider.Min, false);
            slider.InputCore.InputField.SetTextWithoutNotify("—");
            return;
        }
        double first = e.Current;
        while (e.MoveNext()) {
            if (Math.Abs(e.Current - first) > 1e-3) {
                slider.Set(slider.Min, false);
                slider.InputCore.InputField.SetTextWithoutNotify("—");
                return;
            }
        }
        slider.Set(first, false);
        slider.InputCore.InputField.SetTextWithoutNotify(first.ToString(format));
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
            if (liveKey != "opacity") {
                opacitySlider.Set(Alpha(el.Object) * 100f, false);
            }
            if (cfg.TextConfig != null) {
                if (!TypingIn(textInput)) {
                    if (textInput.Placeholder is TMP_Text ph) {
                        ph.text = Tr("HUD_TEXT_PLACEHOLDER", "Text  (tags like {Combo} work here)");
                    }
                    textInput.Set(TextOf(el.Object), false);
                    textInputEdited = false;
                }
                fontSizeSlider.Set(cfg.TextConfig.FontSize.StaticValue, false);
                textAlign.Refresh();
                if (liveKey != "textcolor") {
                    textColorPicker.Set(cfg.TextConfig.Color.StaticValue.TL, false);
                }
            }
            if (cfg.ImageConfig != null && liveKey != "imagecolor") {
                imageColorPicker.Set(cfg.ImageConfig.Color.StaticValue, false);
            }
            if (FlowOf(el.Object) != Flow.None) {
                gapSlider.Set(GapOf(el.Object).StaticValue, false);
                paddingSlider.Set(PaddingOf(el.Object), false);
                int aligned = CrossAlignIndex(AlignOf(el.Object).StaticValue);
                for (int i = 0; i < 3; i++) {
                    HighlightButton(alignButtons[i], i == aligned);
                }
            }
            // Highlight the pin that matches (none for a stretched box).
            Vector2 min = cfg.RectTransformConfig.AnchorMin.StaticValue, max = cfg.RectTransformConfig.AnchorMax.StaticValue;
            anchorSelected = -1;
            if (min == max) {
                for (int i = 0; i < 9; i++) {
                    var a = new Vector2(i % 3 * 0.5f, 1f - i / 3 * 0.5f);
                    if ((min - a).sqrMagnitude < 1e-4f) {
                        anchorSelected = i;
                        break;
                    }
                }
            }
            RefreshAnchorVisuals();
        } finally {
            syncingQuick = false;
        }
    }

    private static void SyncQuickMultiValues() {
        syncingQuick = true;
        try {
            var editable = EditableSelection().ToList();
            SetSliderMulti(wSlider, editable.Where(e => e.CanResizeX).Select(e => (double)e.Capture().Size.x), "F0");
            SetSliderMulti(hSlider, editable.Where(e => e.CanResizeY).Select(e => (double)e.Capture().Size.y), "F0");
            if (liveKey != "opacity") {
                SetSliderMulti(opacitySlider,
                    editable.Where(e => e.Object.Config.CanvasGroupConfig?.Alpha.UseFx != true).Select(e => (double)(Alpha(e.Object) * 100f)),
                    "F0");
            }
            var texts = editable.Where(e => e.Object.Config.TextConfig != null).ToList();
            if (texts.Count > 0) {
                var staticTexts = texts.Where(e => e.Object.Config.TextEngineConfig?.Text.UseFx != true).Select(e => TextOf(e.Object)).ToList();
                if (!TypingIn(textInput) && staticTexts.Count > 0) {
                    bool allSameText = staticTexts.All(t => t == staticTexts[0]);
                    if (textInput.Placeholder is TMP_Text ph) {
                        ph.text = allSameText
                            ? Tr("HUD_TEXT_PLACEHOLDER", "Text  (tags like {Combo} work here)")
                            : Tr("HUD_MIXED", "Mixed");
                    }
                    textInput.Set(allSameText ? staticTexts[0] : string.Empty, false);
                    textInputEdited = false;
                }
                SetSliderMulti(fontSizeSlider,
                    texts.Where(e => !e.Object.Config.TextConfig.FontSize.UseFx).Select(e => (double)e.Object.Config.TextConfig.FontSize.StaticValue),
                    "F0");
                textAlign.Refresh();
                if (liveKey != "textcolor") {
                    var cols = texts.Where(e => !e.Object.Config.TextConfig.Color.UseFx).Select(e => e.Object.Config.TextConfig.Color.StaticValue.TL).ToList();
                    if (cols.Count > 0) {
                        bool sameColor = cols.All(c => c == cols[0]);
                        textColorPicker.Set(sameColor ? cols[0] : new Color(0.5f, 0.5f, 0.5f, 1f), false);
                    }
                }
            }
            var images = editable.Where(e => e.Object.Config.ImageConfig != null).ToList();
            if (images.Count > 0 && liveKey != "imagecolor") {
                var cols = images.Where(e => !e.Object.Config.ImageConfig.Color.UseFx).Select(e => e.Object.Config.ImageConfig.Color.StaticValue).ToList();
                if (cols.Count > 0) {
                    bool sameColor = cols.All(c => c == cols[0]);
                    imageColorPicker.Set(sameColor ? cols[0] : new Color(0.5f, 0.5f, 0.5f, 1f), false);
                }
            }
            var pinnable = editable.Where(CanPin).ToList();
            anchorSelected = -1;
            if (pinnable.Count > 0) {
                int? common = null;
                foreach (var el in pinnable) {
                    var rt = el.Config;
                    Vector2 min = rt.AnchorMin.StaticValue, max = rt.AnchorMax.StaticValue;
                    int match = -1;
                    if (min == max) {
                        for (int i = 0; i < 9; i++) {
                            var a = new Vector2(i % 3 * 0.5f, 1f - i / 3 * 0.5f);
                            if ((min - a).sqrMagnitude < 1e-4f) {
                                match = i;
                                break;
                            }
                        }
                    }
                    if (common == null) {
                        common = match;
                    } else if (common != match) {
                        common = -1;
                        break;
                    }
                }
                anchorSelected = common ?? -1;
            }
            RefreshAnchorVisuals();
        } finally {
            syncingQuick = false;
        }
    }

    private static bool TypingIn(O5InputField field) => field?.InputField != null && field.InputField.isFocused;
}
