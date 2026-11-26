using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.Tween;
using Overlayer.UI;
using Overlayer.UI.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

internal static partial class HudEditor {
    private const float Border = 1.5f;
    private const float MinPanelWidth = 380f;
    private const float MaxPanelWidth = 1000f;
    // Dragging the panel's left edge resizes it; remembered in the settings.
    private static float PanelWidth => Mathf.Clamp(MainCore.Conf.HudPanelWidth.Value, MinPanelWidth, MaxPanelWidth);
    private const float ToolbarHeight = 48f;
    private const float MenuWidth = 300f;
    private const float Margin = 16f;

    // Overlay canvases render below Overlayer's UI canvas, so this also tints the HUD: keep it light.
    // A light veil over the game while editing (also tints the HUD, so keep it faint).
    private static readonly Color DimColor = new(1f, 1f, 1f, 0.07f);
    private static readonly Color IdleColor = new(1f, 1f, 1f, 0.14f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.75f);
    private static Color SelectedColor => UIColors.ObjectActive;
    private static Color GroupColor => O5Kit.Core.O5Palette.WithAlpha(UIColors.ObjectActive, 0.5f);
    private static readonly Color GuideColor = new(1f, 0.35f, 0.75f, 0.9f);
    private static readonly Color MeasureColor = new(1f, 0.32f, 0.36f, 1f);

    internal static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    internal static string Tr(string key, string fallback, params object[] args) => string.Format(MainCore.Tr.Get(key, fallback), args);

    private static readonly (string Key, string Fallback)[] PlacementNames = [
        ("HUD_PLACE_MAIN", "Main canvas"),
        ("HUD_PLACE_SIBLING", "Next to selected"),
        ("HUD_PLACE_CHILD", "Inside selected"),
    ];

    private static readonly (string Key, string Fallback)[] PlacementTooltips = [
        ("HUD_PLACE_MAIN_TIP", "Root canvas of the selected object"),
        ("HUD_PLACE_SIBLING_TIP", "As a sibling of the selected object"),
        ("HUD_PLACE_CHILD_TIP", "As a child of the selected object"),
    ];

    private static RectTransform root;
    private static GameObject gizmoCanvasObj;
    private static CanvasGroup gizmoGroup;
    private static RectTransform gizmoLayer, pixelHandleLayer, handleLayer;
    private static RectTransform aspectDiagonalLine;
    private static RectTransform panel;
    private static RectTransform toolbar;
    private static RectTransform contextMenu;
    private static Sprite softFrameSprite, thinFrameSprite;

    private sealed class Frame {
        public RectTransform Rect;
        public Image Border;
    }

    private static readonly List<Frame> frames = [];
    private static readonly List<RectTransform> guides = [];
    private static readonly RectTransform[] cornerHandles = new RectTransform[4];
    private static readonly RectTransform[] edgeHandles = new RectTransform[4];
    private static Frame groupFrame;
    private static Frame marqueeFrame;

    private static TextMeshProUGUI titleText;
    private static TextMeshProUGUI infoText;
    private static O5Slider xSlider, ySlider, scaleSlider, rotationSlider;
    private static O5Button placementButton, fitButton, ungroupButton;
    private static GameObject singleRows, multiRows, inspectorHost;
    private static RectTransform inspectorContent;
    private static HudInspector inspector;
    private static object inspectedObject;
    private static bool inspectorDirty;
    private static HudElement.State? panelBefore;
    private static readonly List<O5Kit.Core.O5Object> controls = [];

    private static float UiScale => UICore.Canvas != null && UICore.Canvas.scaleFactor > 0f ? UICore.Canvas.scaleFactor : 1f;

    private static Placement CurrentPlacement {
        get => Enum.TryParse(MainCore.Conf.HudPlacement.Value, out Placement p) && Enum.IsDefined(typeof(Placement), p) ? p : Placement.MainCanvas;
        set {
            MainCore.Conf.HudPlacement.Value = value.ToString();
            MainCore.ConfMgr.RequestSave();
        }
    }

    // ---- building blocks ----

    private static RectTransform NewRect(string name, Transform parent) => UiStyle.NewRect(name, parent);

    private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null) {
        var rect = NewRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform NewHandleSquare(string name, Transform parent, float size) {
        var border = NewImage(name, parent, SelectedColor);
        var rect = border.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        var fill = NewImage("Fill", rect, Color.white);
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = Vector2.one;
        fill.rectTransform.offsetMax = -Vector2.one;
        return rect;
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Track<T>(T control) where T : O5Kit.Core.O5Object {
        controls.Add(control);
        return control;
    }

    private static RectTransform Cell(Transform parent, float width) => UiStyle.Cell(parent, width);

    private static O5Button Button(Transform parent, string id, string text, Action onClick, string tooltip = null,
        bool primary = false, TextAlignmentOptions align = TextAlignmentOptions.Center) {
        var button = Track(UiStyle.Button(parent, "plus_hud_" + id, text, onClick, primary, align));
        UiStyle.Tooltip(button, tooltip);
        return button;
    }

    private static O5Button Icon(Transform parent, string id, UIIcon icon, Action onClick, string tooltip, bool primary = false, bool flipX = false) {
        var button = Track(UiStyle.IconButton(Cell(parent, ToolbarHeight - 12f), "plus_hud_" + id, icon, onClick, 9f, primary));
        if (flipX && button.Icon != null) {
            button.Icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
        UiStyle.Tooltip(button, tooltip);
        return button;
    }

    private static void BuildView() {
        DestroyView();
        var ctx = O5KitAdapters.Ctx;

        root = NewRect("OverlayerPlus.HudEditor", UICore.CanvasObj.transform);
        Stretch(root);
        root.SetAsLastSibling();
        dimImg = root.gameObject.AddComponent<Image>();
        dimImg.color = DimColor;
        dimImg.raycastTarget = true; // swallow clicks meant for the game / other UI

        // Dedicated scale-1 pixel-perfect overlay for selection frames, grid and snap/drag lines
        // so borders and 1px lines are unaffected by MainCore.Conf.UIScale.
        gizmoCanvasObj = new GameObject("OverlayerPlus.HudGizmos");
        gizmoCanvasObj.transform.SetParent(MainCore.Root.transform, false);
        var gizmoCanvas = gizmoCanvasObj.AddComponent<Canvas>();
        gizmoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gizmoCanvas.sortingOrder = 32764;
        CanvasCompat.SetPixelPerfect(gizmoCanvas, true);
        O5Kit.Core.O5ColorSpace.Track(gizmoCanvas);
        gizmoGroup = gizmoCanvasObj.AddComponent<CanvasGroup>();
        gizmoGroup.blocksRaycasts = false;
        gizmoGroup.interactable = false;

        gridLayer = NewRect("Grid", gizmoCanvasObj.transform);
        Stretch(gridLayer);
        gizmoLayer = NewRect("Gizmos", gizmoCanvasObj.transform);
        Stretch(gizmoLayer);
        pixelHandleLayer = NewRect("PixelHandles", gizmoCanvasObj.transform);
        Stretch(pixelHandleLayer);
        handleLayer = NewRect("Handles", root);
        Stretch(handleLayer);
        BuildRulers();

        EnsureFrameSprites();
        marqueeFrame = NewFrame(thin: true);
        marqueeFrame.Border.color = SelectedColor;
        var marqueeFill = NewImage("Fill", marqueeFrame.Rect, O5Kit.Core.O5Palette.WithAlpha(UIColors.ObjectActive, 0.12f));
        Stretch(marqueeFill.rectTransform);
        marqueeFill.rectTransform.offsetMin = Vector2.one;
        marqueeFill.rectTransform.offsetMax = -Vector2.one;
        marqueeFill.rectTransform.SetAsFirstSibling();
        groupFrame = NewFrame();
        groupFrame.Border.color = GroupColor;
        aspectDiagonalLine = NewImage("AspectDiagonal", gizmoLayer, SelectedColor).rectTransform;
        aspectDiagonalLine.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++) {
            cornerHandles[i] = NewHandleSquare("Corner" + i, pixelHandleLayer, HudHandleMath.HandleSize);
        }
        for (int i = 0; i < 4; i++) {
            edgeHandles[i] = NewHandleSquare("Edge" + i, pixelHandleLayer, HudHandleMath.EdgeHandleSize);
        }
        BuildAutoAnchorGizmos();

        BuildToolbar();
        BuildBottomBar();
        BuildPanel(ctx);
        BuildPullTab();
        BuildLayersPanel();
        ApplyPanelLayout();
        RestoreDocks();
        UpdateGridButton();
        UpdateLayersButton();
        UpdateRulersButton();
        UpdateAutoAnchorButton();
        undo.Changed -= UpdateHistoryButtons;
        undo.Changed += UpdateHistoryButtons;
        UpdateHistoryButtons();
    }

    // ---- toolbar: a compact floating bar, top-center of the free area ----

    private static void BuildToolbar() {
        // Centered over the area left of the panel, sized to its buttons.
        var area = toolbarArea = NewRect("ToolbarArea", root);
        area.anchorMin = new Vector2(0f, 1f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = new Vector2(0f, -1f);
        area.offsetMax = new Vector2(-PanelWidth - Margin, 0f);
        toolbar = UiStyle.Rounded("Toolbar", area, UiStyle.Surface).rectTransform;
        toolbarGroup = toolbar.gameObject.AddComponent<CanvasGroup>();
        toolbar.anchorMin = toolbar.anchorMax = new Vector2(0.5f, 1f);
        toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.anchoredPosition = new Vector2(0f, -Margin);
        toolbar.sizeDelta = new Vector2(0f, ToolbarHeight);
        UiStyle.Horizontal(toolbar.gameObject, 4f, new RectOffset(6, 6, 6, 6));
        var fitter = toolbar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        layersButton = Icon(toolbar, "layers", UIIcon.Canvas, ToggleLayers, Tr("HUD_CANVAS_LIST", "Canvas list"));
        UiStyle.Separator(toolbar, true);
        Icon(toolbar, "add_text", UIIcon.Text, () => Create(NewKind.Text, CurrentPlacement, null), Tr("HUD_ADD_TEXT", "Add text"));
        Icon(toolbar, "add_image", UIIcon.Image, () => Create(NewKind.Image, CurrentPlacement, null), Tr("HUD_ADD_IMAGE", "Add image"));
        O5Button presetsButton = null;
        presetsButton = Icon(toolbar, "presets", UIIcon.Star, () => {
            // A second press closes it.
            if (contextMenu != null && contextMenu.name == "PresetMenu") {
                CloseContextMenu();
                return;
            }
            var c = new Vector3[4];
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(presetsButton.Rect, c);
            OpenPresetMenu(new Vector2(c[0].x, c[0].y - 6f), null);
        }, Tr("HUD_PRESETS", "Presets"));
        placementButton = Button(Cell(toolbar, 170f), "placement", string.Empty, CyclePlacement);
        UiStyle.Tooltip(placementButton, CurrentPlacementTooltip);
        gridButton = Icon(toolbar, "grid", UIIcon.Grid, ToggleGrid, Tr("HUD_GRID", "Grid"));
        rulersButton = Icon(toolbar, "rulers", UIIcon.Ruler, ToggleRulers, Tr("HUD_RULERS", "Rulers & guides"));
        autoAnchorButton = Icon(toolbar, "auto_anchor", UIIcon.DotInCircle, ToggleAutoAnchor, Tr("HUD_AUTO_ANCHOR", "Auto anchor"));
        UiStyle.Separator(toolbar, true);
        duplicateButton = Icon(toolbar, "duplicate", UIIcon.Clone, DuplicateSelection, Tr("HUD_DUPLICATE", "Duplicate"));
        deleteButton = Icon(toolbar, "delete", UIIcon.X, null, Tr("HUD_DELETE_HOLD", "Delete (Hold)"));
        HoldToConfirm.Attach(deleteButton, DeleteSelection, canConfirm: () => selection.Any(e => e.IsAlive && !e.Locked));
        UiStyle.Separator(toolbar, true);
        undoButton = Icon(toolbar, "undo", UIIcon.TurnArrow, () => AfterHistory(undo.Undo()), Tr("HUD_UNDO", "Undo"));
        redoButton = Icon(toolbar, "redo", UIIcon.TurnArrow, () => AfterHistory(undo.Redo()), Tr("HUD_REDO", "Redo"), flipX: true);
        UiStyle.Separator(toolbar, true);
        Icon(toolbar, "close", UIIcon.Check, Close, Tr("HUD_DONE", "Done"), primary: true);
        UpdatePlacementLabel();
    }

    private static void UpdateHistoryButtons() {
        undoButton?.SetBlocked(!undo.CanUndo, true);
        redoButton?.SetBlocked(!undo.CanRedo, true);
    }

    private static string CurrentPlacementTooltip() {
        var tip = PlacementTooltips[(int)CurrentPlacement];
        return Tr(tip.Key, tip.Fallback);
    }

    private static void CyclePlacement() {
        CurrentPlacement = (Placement)(((int)CurrentPlacement + 1) % PlacementNames.Length);
        UpdatePlacementLabel();
        O5KitAdapters.Ctx.Tooltip.Show(CurrentPlacementTooltip());
    }

    private static void UpdatePlacementLabel() {
        if (placementButton != null) {
            var p = PlacementNames[(int)CurrentPlacement];
            placementButton.Text = Tr(p.Key, p.Fallback) + "  >";
        }
    }

    // ---- side panel ----

    private static void BuildPanel(O5Kit.Core.O5Context ctx) {
        panel = UiStyle.Rounded("Panel", root, UiStyle.Surface).rectTransform;
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        // Hangs from the top-right corner (or left side if dodging selection); layout comes from ApplyPanelLayout.
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
        panel.anchoredPosition = new Vector2(-Margin, -Margin);

        var content = NewRect("Content", panel);
        Stretch(content);
        content.offsetMin = new Vector2(18f, 18f);
        content.offsetMax = new Vector2(-18f, -16f);
        UiStyle.Vertical(content.gameObject, 8f);

        titleText = O5Factory.ControlTextH1(ctx, O5Factory.Row(ctx, content, 34f));
        infoText = O5Factory.ControlText(ctx, O5Factory.Row(ctx, content, 36f), 14f);
        infoText.color = UiStyle.Muted;
        infoText.enableWordWrapping = true;
        // They span the panel's width, right under the pin button: don't take its clicks.
        titleText.raycastTarget = false;
        infoText.raycastTarget = false;

        // Everything below scrolls together.
        var (_, scrollContent, _) = O5Factory.ScrollView(ctx, content, 10f, expandLayout: true);
        var body = VerticalGroup("Body", scrollContent);

        // One object: the common things first, everything else folded under "All properties".
        singleRows = VerticalGroup("Single", body).gameObject;
        var single = singleRows.transform;
        BuildQuickName(ctx, single);
        var place = DockSection(single, "transform", Tr("HUD_POS_SIZE", "Position and size"), () => selection.Count == 1 || MultiEditActive);
        xSlider = Slider(ctx, place, "x", "X", -1000, 1000, "F1",
            (ref HudElement.State s, double v) => s.Position = new Vector2((float)v, s.Position.y), e => e.CanMove);
        ySlider = Slider(ctx, place, "y", "Y", -600, 600, "F1",
            (ref HudElement.State s, double v) => s.Position = new Vector2(s.Position.x, (float)v), e => e.CanMove);
        BuildQuickSize(ctx, place);
        rotationSlider = Slider(ctx, place, "rotation", Tr("HUD_ROTATION", "Rotation"), -180, 180, "F1",
            (ref HudElement.State s, double v) => s.Rotation = (float)v, e => e.CanRotate);
        scaleSlider = Slider(ctx, place, "scale", Tr("HUD_SCALE", "Scale"), 0, 3, "F2",
            (ref HudElement.State s, double v) => s.Scale = new Vector3((float)v, (float)v, s.Scale.z), e => e.CanScale);
        BuildAnchorPicker(ctx, place);
        var actions = UiStyle.HRow(place, 6f, 36f);
        Button(Cell(actions, 0f), "reset", Tr("HUD_RESET", "Reset"), ResetSelection, Tr("HUD_RESET_TRANSFORM", "Reset transform"));
        fitButton = Button(Cell(actions, 0f), "fit", Tr("HUD_FIT_TEXT", "Fit to text"), FitSelection, Tr("HUD_FIT_TEXT", "Fit to text"));
        ungroupButton = Button(Cell(actions, 0f), "ungroup", Tr("HUD_UNGROUP", "Ungroup"), UngroupSelection, Tr("HUD_UNGROUP", "Ungroup"));
        BuildQuickLayout(ctx, DockSection(single, "layout", Tr("HUD_AUTO_LAYOUT", "Auto layout"),
            () => (selection.Count == 1 && CanAutoLayout(Primary)) || (MultiEditActive && selection.Any(e => e.IsAlive && !e.ReadOnly && CanAutoLayout(e)))));
        BuildQuickLook(ctx, DockSection(single, "look", Tr("HUD_LOOK", "Look"), () => selection.Count == 1 || MultiEditActive));
        BuildAllPropsToggle(single);

        // Several objects: align / distribute.
        multiRows = VerticalGroup("Multi", body).gameObject;
        var multi = DockSection(multiRows.transform, "arrange", Tr("HUD_ALIGN_ARRANGE", "Align and arrange"), () => selection.Count > 1);
        UiStyle.Section(multi, Tr("HUD_ALIGN", "Align"));
        var alignRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(alignRow, 0f), "align_left", Tr("HUD_LEFT", "Left"), () => Align(AlignMode.Left));
        Button(Cell(alignRow, 0f), "align_cx", Tr("HUD_CENTER", "Center"), () => Align(AlignMode.CenterX));
        Button(Cell(alignRow, 0f), "align_right", Tr("HUD_RIGHT", "Right"), () => Align(AlignMode.Right));
        var alignRowY = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(alignRowY, 0f), "align_top", Tr("HUD_TOP", "Top"), () => Align(AlignMode.Top));
        Button(Cell(alignRowY, 0f), "align_cy", Tr("HUD_MIDDLE", "Middle"), () => Align(AlignMode.CenterY));
        Button(Cell(alignRowY, 0f), "align_bottom", Tr("HUD_BOTTOM", "Bottom"), () => Align(AlignMode.Bottom));
        UiStyle.Section(multi, Tr("HUD_DISTRIBUTE", "Distribute"));
        var distRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(distRow, 0f), "dist_x", Tr("HUD_HORIZONTALLY", "Horizontally"), () => Align(AlignMode.DistributeX), Tr("HUD_DIST_X_TIP", "Distribute horizontally"));
        Button(Cell(distRow, 0f), "dist_y", Tr("HUD_VERTICALLY", "Vertically"), () => Align(AlignMode.DistributeY), Tr("HUD_DIST_Y_TIP", "Distribute vertically"));
        UiStyle.Section(multi, Tr("HUD_GROUP", "Group"));
        var arrangeRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(arrangeRow, 0f), "group", Tr("HUD_GROUP", "Group"), GroupSelection, Tr("HUD_GROUP", "Group"));
        Button(Cell(arrangeRow, 0f), "multi_ungroup", Tr("HUD_UNGROUP", "Ungroup"), UngroupSelection, Tr("HUD_UNGROUP", "Ungroup"));
        var flowRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(flowRow, 0f), "group_row", Tr("HUD_GROUP_ROW", "Group as row"), () => GroupAs(Flow.Row), Tr("HUD_GROUP_ROW", "Group as row"));
        Button(Cell(flowRow, 0f), "group_column", Tr("HUD_GROUP_COLUMN", "Group as column"), () => GroupAs(Flow.Column), Tr("HUD_GROUP_COLUMN", "Group as column"));
        BuildMultiPropsToggle(multiRows.transform);

        inspectorContent = VerticalGroup("Inspector", body);
        inspectorHost = inspectorContent.gameObject;
        // Last, so the edge grips and the pin sit above the content.
        BuildResizeGrip();
    }

    private static RectTransform VerticalGroup(string name, Transform parent) {
        var rect = NewRect(name, parent);
        rect.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0f;
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static List<(HudElement Element, HudElement.State Before)> panelBeforeMulti;

    private static O5Slider Slider(O5Kit.Core.O5Context ctx, Transform parent, string id, string label,
        double min, double max, string format, Setter set, Func<HudElement, bool> canEdit = null) {
        var slider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, parent), "plus_hud_" + id, new O5SliderOptions {
            Min = min,
            Max = max,
            Label = label,
            Format = format,
            ClampDrag = false,
            ClampInput = false,
        }));
        slider.OnChanged = value => {
            if (selection.Count == 1) {
                var el = Primary;
                if (el == null) {
                    return;
                }
                panelBefore ??= el.Capture();
                var state = el.Capture();
                set(ref state, value);
                el.Apply(state);
            } else if (MultiEditActive) {
                if (panelBeforeMulti == null) {
                    panelBeforeMulti = [];
                    foreach (var el in selection) {
                        if (el.IsAlive && !el.ReadOnly && (canEdit == null || canEdit(el))) {
                            panelBeforeMulti.Add((el, el.Capture()));
                        }
                    }
                }
                foreach (var (el, _) in panelBeforeMulti) {
                    if (!el.IsAlive) {
                        continue;
                    }
                    var state = el.Capture();
                    set(ref state, value);
                    el.Apply(state);
                }
            }
        };
        slider.OnComplete = _ => CommitPanelEdit();
        return slider;
    }

    private delegate void Setter(ref HudElement.State state, double value);

    private static void CommitPanelEdit() {
        if (panelBeforeMulti != null) {
            var changes = panelBeforeMulti
                .Where(p => p.Element.IsAlive)
                .Select(p => (p.Element, p.Before, p.Element.Capture()))
                .ToList();
            panelBeforeMulti = null;
            Commit(changes);
            return;
        }
        var el = Primary;
        if (panelBefore == null || el == null) {
            panelBefore = null;
            return;
        }
        var before = panelBefore.Value;
        panelBefore = null;
        Commit([(el, before, el.Capture())]);
    }

    private static void ResetSelection() {
        CommitPanelEdit();
        var changes = new List<(HudElement, HudElement.State, HudElement.State)>();
        foreach (var el in selection) {
            if (el.Locked || !el.IsAlive) {
                continue;
            }
            var before = el.Capture();
            var reset = before;
            reset.Position = Vector2.zero;
            reset.Scale = Vector3.one;
            reset.Rotation = 0f;
            el.Apply(reset);
            changes.Add((el, before, el.Capture()));
        }
        Commit(changes);
    }

    private static void FitSelection() {
        var items = selection.Where(e => e.IsAlive && !e.Locked).ToList();
        var sizes = items.Select(e => (El: e, Before: e.Config.SizeDelta.StaticValue)).ToList();
        var changed = sizes.Where(s => s.El.FitToText()).Select(s => (s.El, s.Before, After: s.El.Config.SizeDelta.StaticValue)).ToList();
        if (changed.Count == 0) {
            return;
        }
        void Set(bool after) {
            foreach (var (el, b, a) in changed) {
                if (!el.IsAlive) {
                    continue;
                }
                el.Config.SizeDelta.StaticValue = after ? a : b;
                el.Config.ToUnity(el.Object.GameObject);
            }
        }
        undo.Push(new ActionCommand(() => Set(false), () => Set(true)));
        Save();
        RefreshInspectorSoon();
    }

    private static void RefreshInspectorSoon() => inspectorDirty = true;

    private static void RefreshPanel() {
        if (panel == null) {
            return;
        }
        CommitQuickEdit();
        UpdateBottomBar();
        var el = Primary;
        bool single = selection.Count == 1;
        bool multiEdit = MultiEditActive;
        singleRows.SetActive(single || multiEdit);
        // One object: its name is the editable field instead of the title.
        titleText.transform.parent.gameObject.SetActive(!single);
        nameInput.Rect.parent.gameObject.SetActive(single);
        multiRows.SetActive(selection.Count > 1);

        if (selection.Count > 0 || (selectedCanvas != null && !Overlayer.Overlay.OverlayCore.Canvases.Contains(selectedCanvas))) {
            selectedCanvas = null;
        }
        if (selection.Count == 0 && selectedCanvas != null) {
            titleText.text = selectedCanvas.Config.Name.Value;
            infoText.text = Tr("HUD_CANVAS_SELECTED_DESC", "Canvas. New objects and presets are added here.");
        } else if (selection.Count == 0) {
            titleText.text = Tr("HUD_NOTHING_SELECTED", "Nothing selected");
            infoText.text = Tr("HUD_NOTHING_SELECTED_DESC", "Click a HUD element to edit it, or right-click to add one.");
        } else if (!single) {
            titleText.text = Tr("HUD_N_SELECTED", "{0} selected", selection.Count);
            int locked = selection.Count(e => e.Locked || !e.CanMove);
            infoText.text = Tr("HUD_MULTI_DRAG_HINT", "Drag any of them to move together.")
                + (locked > 0 ? "\n" + Tr("HUD_MULTI_LOCKED_HINT", "{0} can't move (locked or driven by an expression).", locked) : string.Empty);
            RefreshMultiPropsToggle();
            if (multiEdit) {
                RefreshQuickMulti();
            }
        } else {
            titleText.text = el.Name;
            var locks = new List<string>();
            if (el.Locked) {
                locks.Add(el.LockReason);
            } else {
                if (!el.CanMove) {
                    locks.Add(Tr("HUD_PROP_POSITION", "position"));
                }
                if (!el.CanScale) {
                    locks.Add(Tr("HUD_PROP_SCALE", "scale"));
                }
                if (!el.CanRotate) {
                    locks.Add(Tr("HUD_PROP_ROTATION", "rotation"));
                }
            }
            infoText.text = Tr("HUD_CANVAS_INFO", "Canvas: {0}", el.Canvas.Config.Name.Value)
                + (locks.Count == 0 ? string.Empty
                    : el.Locked ? "\n" + Tr("HUD_LOCKED_INFO", "Locked: {0}", locks[0])
                    : "\n" + Tr("HUD_DRIVEN_BY_FX", "Driven by an expression: {0}", string.Join(", ", locks)));
            // O5Object.EnabledWhen is only evaluated once, so block per selection here.
            xSlider.SetBlocked(!el.CanMove, true);
            ySlider.SetBlocked(!el.CanMove, true);
            scaleSlider.SetBlocked(!el.CanScale, true);
            rotationSlider.SetBlocked(!el.CanRotate, true);
            fitButton.Rect.parent.gameObject.SetActive(el.Object.GameObject.GetComponent<TextMeshProUGUI>() != null);
            ungroupButton.Rect.parent.gameObject.SetActive(!el.Locked && el.Object.Children.Count > 0);
            RefreshQuick(el);
        }
        SyncPanelValues();

        object target = single && !el.ReadOnly && allPropsShown ? el.Object
            : selection.Count == 0 ? selectedCanvas : null;
        if (!ReferenceEquals(target, inspectedObject)) {
            RebuildInspector(target);
        }
        if (panelShown != PanelWanted) {
            ApplyPanelLayout();
        }
    }

    private static void RebuildInspector(object target) {
        inspectorDirty = false;
        inspectedObject = target;
        inspectorHost.SetActive(target != null);
        inspector?.Dispose();
        inspector = target switch {
            Overlayer.Overlay.OvObject o => HudInspector.ForObject(inspectorContent, o, OnInspectorEdit, SetUpCards),
            Overlayer.Overlay.OvCanvas c => HudInspector.ForCanvas(inspectorContent, c, OnInspectorEdit, SetUpCards, OnSelectedCanvasRenamed),
            _ => null,
        };
    }

    // The inspector applied an edit itself: follow it on screen, don't rebuild (keeps field focus).
    private static void OnInspectorEdit() {
        collectedFrame = -1000;
        SyncPanelValues();
        if (Primary != null) {
            titleText.text = Primary.Name;
        }
        Save();
    }

    private static void SyncPanelValues() {
        if (panel == null) {
            return;
        }
        if (selection.Count == 1) {
            var s = Primary.Capture();
            xSlider.Set(s.Position.x, false);
            ySlider.Set(s.Position.y, false);
            scaleSlider.Set(s.Scale.x, false);
            rotationSlider.Set(s.Rotation, false);
            SyncQuickValues(Primary);
        } else if (MultiEditActive) {
            var editable = EditableSelection().ToList();
            SetSliderMulti(xSlider, editable.Where(e => e.CanMove).Select(e => (double)e.Capture().Position.x), "F1");
            SetSliderMulti(ySlider, editable.Where(e => e.CanMove).Select(e => (double)e.Capture().Position.y), "F1");
            SetSliderMulti(scaleSlider, editable.Where(e => e.CanScale).Select(e => (double)e.Capture().Scale.x), "F2");
            SetSliderMulti(rotationSlider, editable.Where(e => e.CanRotate).Select(e => (double)e.Capture().Rotation), "F1");
            SyncQuickMultiValues();
        }
    }

    // ---- context menu ----

    private static bool ContextMenuOpen => contextMenu != null;
    private static int contextMenuOpenedFrame = -1;

    private static void OpenContextMenu(Vector2 mouse, HudElement target) {
        // null = separator
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?>();
        if (target == null || target.Locked) {
            // Empty spot: relative placements make no sense here, use the main canvas instead.
            items.Add((Tr("HUD_ADD_TEXT_HERE", "Add text here"), null, () => Create(NewKind.Text, Placement.MainCanvas, mouse), false));
            items.Add((Tr("HUD_ADD_IMAGE_HERE", "Add image here"), null, () => Create(NewKind.Image, Placement.MainCanvas, mouse), false));
            items.Add((Tr("HUD_ADD_PRESET_HERE", "Add preset here…"), null, () => OpenPresetMenu(mouse, mouse), false));
            items.Add((Tr("HUD_PASTE", "Paste"), "Ctrl+V", () => Paste(false), false));
            if (selection.Count > 0) {
                items.Add(null);
                items.Add((Tr("HUD_DESELECT", "Deselect"), "Esc", () => Select(null), false));
            }
        } else {
            bool single = selection.Count == 1;
            // Everything under the pointer, top first: reach objects hidden behind others.
            var under = AllUnder(mouse);
            if (under.Count > 1) {
                foreach (var el in under.Take(6)) {
                    var captured = el;
                    string indent = new(' ', Math.Min(el.Depth, 4) * 2);
                    items.Add(($"{indent}{(selection.Contains(el) ? "● " : "")}{el.Name}", null, () => Select(captured), false));
                }
                items.Add(null);
            }
            // The text under the pointer, also when what's picked is the group it's in (a preset card).
            var text = target.Object.Config.TextConfig != null ? target
                : under.Concat(AllUnder(mouse)).FirstOrDefault(e => e.Object.Config.TextConfig != null);
            if (text != null && CanEditText(text)) {
                string which = ReferenceEquals(text, target) ? string.Empty : $"  ({text.Name})";
                items.Add((Tr("HUD_EDIT_TEXT", "Edit text") + which, ReferenceEquals(text, target) ? Tr("HUD_DOUBLE_CLICK", "Double-click") : null, () => BeginInlineEdit(text), false));
                items.Add(null);
            }
            if (single) {
                items.Add((Tr("HUD_ADD_TEXT_INSIDE", "Add text inside"), null, () => Create(NewKind.Text, Placement.Child, mouse), false));
                items.Add((Tr("HUD_ADD_TEXT_NEXT", "Add text next to it"), null, () => Create(NewKind.Text, Placement.Sibling, mouse), false));
                items.Add(null);
                items.Add((Tr("HUD_BRING_FRONT", "Bring to front"), "Ctrl+Shift+]", () => MoveToEnd(true), false));
                items.Add((Tr("HUD_BRING_FORWARD", "Bring forward"), "Ctrl+]", () => MoveOrder(1), false));
                items.Add((Tr("HUD_SEND_BACKWARD", "Send backward"), "Ctrl+[", () => MoveOrder(-1), false));
                items.Add((Tr("HUD_SEND_BACK", "Send to back"), "Ctrl+Shift+[", () => MoveToEnd(false), false));
                items.Add(null);
            }
            items.Add((Tr("HUD_GROUP", "Group"), "Ctrl+G", GroupSelection, false));
            if (selection.Any(e => !e.Locked && e.Object.Children.Count > 0)) {
                items.Add((Tr("HUD_UNGROUP", "Ungroup"), "Ctrl+Shift+G", UngroupSelection, false));
            }
            items.Add(null);
            items.Add((Tr("HUD_COPY", "Copy"), "Ctrl+C", CopySelection, false));
            items.Add((Tr("HUD_PASTE", "Paste"), "Ctrl+V", () => Paste(false), false));
            items.Add((Tr("HUD_COPY_STYLE", "Copy style"), "Ctrl+Alt+C", CopyStyle, false));
            items.Add((Tr("HUD_PASTE_STYLE", "Paste style"), "Ctrl+Alt+V", PasteStyle, false));
            items.Add((Tr("HUD_DUPLICATE", "Duplicate"), "Ctrl+D", DuplicateSelection, false));
            items.Add(null);
            items.Add((Tr("HUD_LOCK", "Lock"), "Ctrl+Shift+L", ToggleLockSelection, false));
            items.Add((Tr("HUD_HIDE", "Hide"), "Ctrl+Shift+H", HideSelection, false));
            items.Add((Tr("HUD_RESET_TRANSFORM", "Reset transform"), null, ResetSelection, false));
            items.Add(null);
            items.Add((Tr("HUD_DELETE", "Delete"), "Del", DeleteSelection, true));
        }
        ShowContextMenu(mouse, items);
    }

    private static void ShowContextMenu(Vector2 mouse, List<(string Text, string Shortcut, Action Run, bool Danger)?> items) {
        CloseContextMenu();
        var clean = new List<(string Text, string Shortcut, Action Run, bool Danger)?>(items.Count);
        foreach (var entry in items) {
            if (entry == null) {
                if (clean.Count > 0 && clean[clean.Count - 1] != null) {
                    clean.Add(null);
                }
            } else {
                clean.Add(entry);
            }
        }
        while (clean.Count > 0 && clean[clean.Count - 1] == null) {
            clean.RemoveAt(clean.Count - 1);
        }
        if (clean.Count == 0) {
            return;
        }

        var host = UICore.CanvasObj != null ? UICore.CanvasObj.transform : root;
        contextMenu = UiStyle.Rounded("ContextMenu", host, UiStyle.Surface).rectTransform;
        contextMenu.anchorMin = contextMenu.anchorMax = Vector2.zero;
        contextMenu.pivot = new Vector2(0f, 1f);
        UiStyle.Vertical(contextMenu.gameObject, 2f, new RectOffset(6, 6, 6, 6));
        var fitter = contextMenu.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contextMenu.sizeDelta = new Vector2(MenuWidth, 0f);

        for (int i = 0; i < clean.Count; i++) {
            if (clean[i] is not { } item) {
                var gap = NewRect("Gap", contextMenu);
                gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 5f;
                UiStyle.Separator(contextMenu);
                var gap2 = NewRect("Gap", contextMenu);
                gap2.gameObject.AddComponent<LayoutElement>().preferredHeight = 5f;
                continue;
            }
            string label = item.Shortcut == null ? item.Text : $"{item.Text}<pos=60%><alpha=#55>{item.Shortcut}";
            var button = Button(O5Factory.Row(O5KitAdapters.Ctx, contextMenu, 32f), "menu_" + i, label, () => {
                CloseContextMenu();
                item.Run();
            }, align: TextAlignmentOptions.MidlineLeft);
            if (button.Label != null) {
                button.Label.fontSize = 16f;
                button.Label.enableWordWrapping = false;
                if (item.Danger) {
                    button.Label.color = UiStyle.Danger;
                }
            }
            button.NormalColor = Color.clear;
            button.UpdateVisual(true);
        }

        // Keep the whole menu on screen.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contextMenu);
        Vector2 local = mouse / UiScale;
        Vector2 size = contextMenu.rect.size;
        Vector2 screen = new(Screen.width / UiScale, Screen.height / UiScale);
        local.x = Mathf.Clamp(local.x, 4f, Mathf.Max(4f, screen.x - size.x - 4f));
        local.y = Mathf.Clamp(local.y, size.y + 4f, Mathf.Max(size.y + 4f, screen.y - 4f));
        contextMenu.anchoredPosition = local;
        contextMenu.SetAsLastSibling();
        contextMenuOpenedFrame = Time.frameCount;
    }

    private static void CloseContextMenu() {
        if (contextMenu == null) {
            return;
        }
        // Menu buttons are tracked like every other control; drop the ones under the menu.
        var menu = contextMenu;
        controls.RemoveAll(c => {
            if (c.Rect == null || !c.Rect.IsChildOf(menu)) {
                return false;
            }
            c.Dispose();
            return true;
        });
        Object.Destroy(menu.gameObject);
        contextMenu = null;
    }

    // Anything of ours (or a popup opened from the inspector) under the pointer, other than the dim backdrop.
#if ML && IL2CPP
    private static readonly Il2CppSystem.Collections.Generic.List<RaycastResult> raycastHits = new();
#else
    private static readonly List<RaycastResult> raycastHits = [];
#endif

    private static bool PointerOverEditorUi(Vector2 mouse) {
        var events = EventSystem.current;
        if (events == null) {
            return false;
        }
        raycastHits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = mouse }, raycastHits);
        foreach (var hit in raycastHits) {
            if (hit.gameObject == null) {
                continue;
            }
            return hit.gameObject != root.gameObject;
        }
        return false;
    }

    // ---- rendering ----

    private static Vector2 ToLocal(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
        return local;
    }

    private static void Render() {
        if (panelShown != PanelWanted) {
            ApplyPanelLayout();
        }
        RenderGrid();
        TickLayers();
        RenderRulers();
        FadeBottomBarWhileDragging();
        TickDocks();
        if (root == null) {
            return;
        }
        if (inspectorDirty && !TypingInField() && drag == DragMode.None) {
            inspectorDirty = false;
            if (inspector != null) {
                inspector.Rebuild();
            }
        }

        // While moving / scaling / rotating / resizing only what's selected keeps its outline.
        bool dragging = dragChanged && drag is DragMode.Move or DragMode.Scale or DragMode.Rotate or DragMode.Resize;
        int used = 0;
        foreach (var el in elements) {
            if (!el.IsAlive || (dragging && !selection.Contains(el))) {
                continue;
            }
            var frame = GetFrame(used++);
            el.GetScreenCorners(corners);
            PlacePixelQuad(frame.Rect, corners, 1f);
            // One look for every outline (locked ones say so in the panel instead).
            frame.Border.color = selection.Contains(el) ? SelectedColor
                : ReferenceEquals(el, hovered) ? HoverColor : IdleColor;
        }
        for (int i = used; i < frames.Count; i++) {
            frames[i].Rect.gameObject.SetActive(false);
        }

        // Selection box while dragging on empty space.
        if (drag == DragMode.Marquee && dragChanged) {
            var r = MarqueeRect(lastMouse);
            PlacePixelQuad(marqueeFrame.Rect, [new(r.xMin, r.yMin), new(r.xMin, r.yMax), new(r.xMax, r.yMax), new(r.xMax, r.yMin)], 0f);
            marqueeFrame.Rect.SetAsLastSibling();
        } else {
            marqueeFrame.Rect.gameObject.SetActive(false);
        }

        // Multi-selection: one box around everything that moves together.
        if (selection.Count > 1 && UnionBounds(selection, out float minX, out float maxX, out float minY, out float maxY)) {
            var box = new Vector3[] { new(minX - 4f, minY - 4f), new(minX - 4f, maxY + 4f), new(maxX + 4f, maxY + 4f), new(maxX + 4f, minY - 4f) };
            PlacePixelQuad(groupFrame.Rect, box, 1f);
            groupFrame.Rect.SetAsLastSibling();
        } else {
            groupFrame.Rect.gameObject.SetActive(false);
        }

        var sel = selection.Count == 1 ? Primary : null;
        bool showHandles = sel != null && sel.IsAlive;
        float boxAngle = 0f;
        bool axisAligned = true;
        if (showHandles) {
            sel.GetScreenCorners(corners);
            Vector2 boxDir = corners[3] - corners[0];
            boxAngle = boxDir.sqrMagnitude > 1e-4f ? Mathf.Atan2(boxDir.y, boxDir.x) * Mathf.Rad2Deg : 0f;
            axisAligned = Mathf.Abs(corners[1].y - corners[2].y) < 0.01f && Mathf.Abs(corners[1].x - corners[0].x) < 0.01f;
        }
        bool showDiagonal = showHandles && Shift && drag is DragMode.Scale or DragMode.Resize;
        if (showDiagonal) {
            if (drag == DragMode.Scale) {
                PlacePixelLine(aspectDiagonalLine, corners[scaleCorner], corners[(scaleCorner + 2) % 4], 1f);
            } else {
                PlacePixelLine(aspectDiagonalLine, corners[0], corners[2], 1f);
            }
            aspectDiagonalLine.SetAsLastSibling();
        } else {
            aspectDiagonalLine.gameObject.SetActive(false);
        }

        bool showCorner = showHandles && (sel.CanScale || sel.CanResizeX || sel.CanResizeY);
        for (int i = 0; i < 4; i++) {
            cornerHandles[i].gameObject.SetActive(showCorner);
            if (showCorner) {
                Vector2 pos = corners[i];
                cornerHandles[i].anchoredPosition = axisAligned ? new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)) : pos;
                cornerHandles[i].localEulerAngles = axisAligned ? Vector3.zero : new Vector3(0f, 0f, boxAngle);
            }
        }
        for (int i = 0; i < 4; i++) {
            bool showEdge = false;
            if (showHandles) {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                float edgeLen = (b - a).magnitude;
                bool canEdge = (i % 2 == 0 ? sel.CanResizeX : sel.CanResizeY) || sel.CanScale;
                showEdge = canEdge && edgeLen >= HudHandleMath.EdgeHandleMinSpanPx;
                if (showEdge) {
                    Vector2 mid = (a + b) * 0.5f;
                    edgeHandles[i].anchoredPosition = axisAligned ? new Vector2(Mathf.Round(mid.x), Mathf.Round(mid.y)) : mid;
                    edgeHandles[i].localEulerAngles = axisAligned ? Vector3.zero : new Vector3(0f, 0f, boxAngle);
                }
            }
            edgeHandles[i].gameObject.SetActive(showEdge);
        }

        int g = 0;
        foreach (float x in guideX) {
            var line = GetGuide(g++);
            PlacePixelLine(line, new Vector2(x, 0f), new Vector2(x, Screen.height), 1f);
            line.SetAsLastSibling();
        }
        foreach (float y in guideY) {
            var line = GetGuide(g++);
            PlacePixelLine(line, new Vector2(0f, y), new Vector2(Screen.width, y), 1f);
            line.SetAsLastSibling();
        }
        for (int i = g; i < guides.Count; i++) {
            guides[i].gameObject.SetActive(false);
        }
        RenderMeasures();

        // Keep handles above frames and measure badges.
        foreach (var h in edgeHandles) {
            h.SetAsLastSibling();
        }
        foreach (var h in cornerHandles) {
            h.SetAsLastSibling();
        }
        RenderAutoAnchor();
        UpdateInlineTether();
    }

    private static void EnsureFrameSprites() {
        if (softFrameSprite == null) {
            var tex = new Texture2D(7, 7, TextureFormat.RGBA32, false, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[49];
            for (int y = 0; y < 7; y++) {
                int dy = Mathf.Min(y, 6 - y);
                for (int x = 0; x < 7; x++) {
                    int dx = Mathf.Min(x, 6 - x);
                    int d = Mathf.Min(dx, dy);
                    byte a = d == 1 ? (byte)255 : (dx == 0 && dy == 0) ? (byte)32 : d < 3 ? (byte)90 : (byte)0;
                    px[y * 7 + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softFrameSprite = SpriteManager.CreateSliced(tex, 100f, new Vector4(3f, 3f, 3f, 3f));
        }
        if (thinFrameSprite == null) {
            var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[9];
            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 3; x++) {
                    byte a = (x == 1 && y == 1) ? (byte)0 : (byte)255;
                    px[y * 3 + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            thinFrameSprite = SpriteManager.CreateSliced(tex, 100f, new Vector4(1f, 1f, 1f, 1f));
        }
    }

    private static void PlacePixelQuad(RectTransform rect, Vector3[] screenCorners, float expandPx) {
        Vector2 bl = screenCorners[0], tl = screenCorners[1], tr = screenCorners[2], br = screenCorners[3];
        if (Mathf.Abs(tl.y - tr.y) < 0.01f && Mathf.Abs(tl.x - bl.x) < 0.01f) {
            float minX = Mathf.Round(Mathf.Min(bl.x, tr.x)) - expandPx;
            float maxX = Mathf.Round(Mathf.Max(bl.x, tr.x)) + expandPx;
            float minY = Mathf.Round(Mathf.Min(bl.y, tr.y)) - expandPx;
            float maxY = Mathf.Round(Mathf.Max(bl.y, tr.y)) + expandPx;
            rect.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
            rect.localEulerAngles = Vector3.zero;
        } else {
            rect.anchoredPosition = (bl + tl + tr + br) * 0.25f;
            rect.sizeDelta = new Vector2((tr - tl).magnitude + expandPx * 2f, (tl - bl).magnitude + expandPx * 2f);
            Vector2 dir = tr - tl;
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
        rect.gameObject.SetActive(true);
    }

    private static void PlacePixelLine(RectTransform rect, Vector2 a, Vector2 b, float thickness = 1f) {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        if (Mathf.Abs(a.y - b.y) < 0.01f) {
            float y = Mathf.Round(a.y) + 0.5f;
            float x0 = Mathf.Round(Mathf.Min(a.x, b.x));
            float x1 = Mathf.Round(Mathf.Max(a.x, b.x));
            rect.anchoredPosition = new Vector2((x0 + x1) * 0.5f, y);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, x1 - x0), thickness);
            rect.localEulerAngles = Vector3.zero;
        } else if (Mathf.Abs(a.x - b.x) < 0.01f) {
            float x = Mathf.Round(a.x) + 0.5f;
            float y0 = Mathf.Round(Mathf.Min(a.y, b.y));
            float y1 = Mathf.Round(Mathf.Max(a.y, b.y));
            rect.anchoredPosition = new Vector2(x, (y0 + y1) * 0.5f);
            rect.sizeDelta = new Vector2(thickness, Mathf.Max(1f, y1 - y0));
            rect.localEulerAngles = Vector3.zero;
        } else {
            Vector2 dir = b - a;
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.sizeDelta = new Vector2(dir.magnitude, thickness);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
        rect.gameObject.SetActive(true);
    }

    private static Frame NewFrame(bool thin = false) {
        var border = NewImage("Frame", gizmoLayer, IdleColor, thin ? thinFrameSprite : softFrameSprite);
        border.type = Image.Type.Sliced;
        border.fillCenter = false;
        var rect = border.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.gameObject.SetActive(false);
        return new Frame { Rect = rect, Border = border };
    }

    private static Frame GetFrame(int index) {
        while (frames.Count <= index) {
            var frame = NewFrame();
            frame.Border.color = IdleColor;
            frames.Add(frame);
        }
        return frames[index];
    }

    // ---- panel size / pin ----

    private const float MinPanelHeight = 260f;
    private static RectTransform toolbarArea, widthGripRect;
    private static CanvasGroup panelGroup, toolbarGroup;
    private static bool resizingPanel;
    private static float? hoveredPanelGripAngle;
    private static bool panelShown = true;
    private static bool panelOnLeft;
    private static bool panelPreferredLeft;
    private static O5Button pinButton, sideButton, duplicateButton, deleteButton, undoButton, redoButton;

    // Pinned: always shown. Unpinned: only while something is selected.
    private static bool PanelWanted => MainCore.Conf.HudPanelPinned.Value || selection.Count > 0 || pullingPanel;

    private static void BuildResizeGrip() {
        // Side edge (left when panel is right-docked, right when left-docked): width.
        widthGripRect = Grip("WidthGrip", Vector2.zero, new Vector2(0f, 1f), new Vector2(12f, 0f), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.7f),
            new Vector2(3f, 0f), 0f,
            d => MainCore.Conf.HudPanelWidth.Value = Mathf.Clamp(PanelWidth + (panelOnLeft ? d.x : -d.x) / UiScale, MinPanelWidth, MaxPanelWidth),
            () => { });
        // Bottom edge: height (double-click: full height again).
        Grip("HeightGrip", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, 0.5f),
            new Vector2(0f, 3f), 90f,
            d => MainCore.Conf.HudPanelHeight.Value = Mathf.Clamp(PanelHeight - d.y / UiScale, MinPanelHeight, AvailableHeight),
            () => MainCore.Conf.HudPanelHeight.Value = 0f);

        // Side-flip button next to pin, so users can move the panel to either side on demand.
        var sideCell = NewRect("Side", panel);
        sideCell.anchorMin = sideCell.anchorMax = sideCell.pivot = Vector2.one;
        sideCell.anchoredPosition = new Vector2(-76f, -8f);
        sideCell.sizeDelta = new Vector2(28f, 24f);
        sideButton = Track(UiStyle.Button(sideCell, "plus_hud_side", string.Empty, TogglePanelSide, fontSize: 13f));
        UiStyle.Tooltip(sideButton, Tr("HUD_SWITCH_SIDE", "Switch panel side"));
        UpdateSideLabel();

        // Small pin toggle in the corner: pinned = always shown, else only with a selection.
        var pinCell = NewRect("Pin", panel);
        pinCell.anchorMin = pinCell.anchorMax = pinCell.pivot = Vector2.one;
        pinCell.anchoredPosition = new Vector2(-8f, -8f);
        pinCell.sizeDelta = new Vector2(64f, 24f);
        pinButton = Track(UiStyle.Button(pinCell, "plus_hud_pin", string.Empty, TogglePin, fontSize: 13f));
        UiStyle.Tooltip(pinButton, Tr("HUD_PIN_PANEL", "Pin panel"));
        UpdatePinLabel();
        sideCell.SetAsLastSibling();
        pinCell.SetAsLastSibling();
    }

    private static RectTransform Grip(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 lineMin, Vector2 lineMax,
        Vector2 lineSize, float cursorAngle, Action<Vector2> drag, Action doubleClick) {
        var grip = NewImage(name, panel, Color.clear);
        grip.raycastTarget = true;
        var r = grip.rectTransform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        var line = NewImage("Line", r, UiStyle.Line);
        line.rectTransform.anchorMin = lineMin;
        line.rectTransform.anchorMax = lineMax;
        line.rectTransform.sizeDelta = lineSize;

        var trigger = grip.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, _ => {
                line.color = SelectedColor;
                hoveredPanelGripAngle = cursorAngle;
                HudCursor.SetResize(cursorAngle);
            }),
            (EventTriggerType.PointerExit, _ => {
                if (!resizingPanel) {
                    line.color = UiStyle.Line;
                    hoveredPanelGripAngle = null;
                    HudCursor.SetDefault();
                }
            }),
            (EventTriggerType.BeginDrag, _ => {
                resizingPanel = true;
                hoveredPanelGripAngle = cursorAngle;
                HudCursor.SetResize(cursorAngle);
            }),
            (EventTriggerType.Drag, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe) {
                    drag(pe.delta);
                    ApplyPanelLayout();
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                resizingPanel = false;
                line.color = UiStyle.Line;
                hoveredPanelGripAngle = null;
                HudCursor.SetDefault();
                MainCore.ConfMgr.RequestSave();
            }),
            (EventTriggerType.PointerClick, e => {
                if (UnityUtils.AsPointerEventData(e) is { clickCount: 2 }) {
                    doubleClick();
                    ApplyPanelLayout();
                    MainCore.ConfMgr.RequestSave();
                }
            })
        );
        return r;
    }

    private static void TogglePin() {
        MainCore.Conf.HudPanelPinned.Value = !MainCore.Conf.HudPanelPinned.Value;
        MainCore.ConfMgr.RequestSave();
        UpdatePinLabel();
        ApplyPanelLayout();
    }

    private static void UpdatePinLabel() {
        if (pinButton != null) {
            pinButton.Text = MainCore.Conf.HudPanelPinned.Value ? Tr("HUD_PINNED", "Pinned") : Tr("HUD_AUTO", "Auto");
        }
    }

    private static void TogglePanelSide() {
        panelPreferredLeft = !panelOnLeft;
        SetPanelSide(panelPreferredLeft);
    }

    private static void UpdateSideLabel() {
        if (sideButton != null) {
            sideButton.Text = panelOnLeft ? "→" : "←";
        }
    }

    private static void SetPanelSide(bool left) {
        if (panelOnLeft == left) {
            return;
        }
        panelOnLeft = left;
        UpdateSideLabel();
        if (panelGroup != null) {
            panelGroup.alpha = Mathf.Min(panelGroup.alpha, 0.2f);
        }
        ApplyPanelLayout();
    }

    private static float TopInset => RulersShown ? RulerPx + 8f : Margin;
    private static float LeftInset => RulersShown ? RulerPx + 8f : Margin;

    private static void UpdatePanelSide() {
        if (root == null || panel == null || selection.Count == 0 || pullingPanel || resizingPanel) {
            return;
        }
        if (!UnionBounds(selection, out float sMinX, out float sMaxX, out _, out float sMaxY)) {
            return;
        }
        Vector2 size = root.rect.size;
        Vector2 minL = ToLocal(new Vector2(sMinX, 0f)) + size * 0.5f;
        Vector2 maxL = ToLocal(new Vector2(sMaxX, sMaxY)) + size * 0.5f;
        float leftMin = LayersShown ? LeftInset + LayersWidth + Margin : LeftInset;
        bool wantLeft = PickPanelSide(minL.x, maxL.x, maxL.y, size.x, size.y, PanelWidth, PanelHeight, leftMin, TopInset, Margin, panelPreferredLeft);
        SetPanelSide(wantLeft);
    }

    internal static bool PickPanelSide(float selMinX, float selMaxX, float selMaxY, float canvasW, float canvasH,
        float panelW, float panelH, float leftMin, float topInset, float rightMargin, bool preferredLeft) {
        if (selMaxY <= canvasH - topInset - panelH) {
            return preferredLeft;
        }
        float leftMax = leftMin + panelW;
        float rightMax = canvasW - rightMargin, rightMin = rightMax - panelW;
        float leftOverlap = Mathf.Max(0f, Mathf.Min(selMaxX, leftMax) - Mathf.Max(selMinX, leftMin));
        float rightOverlap = Mathf.Max(0f, Mathf.Min(selMaxX, rightMax) - Mathf.Max(selMinX, rightMin));
        return preferredLeft ? leftOverlap <= rightOverlap + 1f : rightOverlap > leftOverlap + 1f;
    }

    private static float AvailableHeight => root != null ? Mathf.Max(MinPanelHeight, root.rect.height - TopInset - Margin) : 800f;

    // 0 = full height.
    private static float PanelHeight {
        get {
            float h = MainCore.Conf.HudPanelHeight.Value;
            return h <= 0f ? AvailableHeight : Mathf.Clamp(h, MinPanelHeight, AvailableHeight);
        }
    }

    private static void ApplyPanelLayout() {
        if (panel == null) {
            return;
        }
        // A tooltip only hides when the pointer leaves its control, which never happens if
        // the control is switched off under the pointer.
        if (panelShown != PanelWanted) {
            O5KitAdapters.Ctx.Tooltip.Hide();
        }
        panelShown = PanelWanted;
        panel.gameObject.SetActive(panelShown);
        float w = PanelWidth;
        panel.sizeDelta = new Vector2(w, PanelHeight);
        float leftMargin = LeftInset;
        float top = TopInset;
        float leftBound = LayersShown ? leftMargin + LayersWidth : (RulersShown ? RulerPx : 0f);
        float panelLeftX = LayersShown ? leftMargin + LayersWidth + Margin : leftMargin;
        float panelW = panelShown ? w + Margin : 0f;
        // The toolbar and bottom bar use the space the side panels leave.
        float left = panelOnLeft && panelShown ? panelLeftX + w : leftBound;
        float right = panelOnLeft ? 0f : panelW;
        float xAnchor = panelOnLeft ? 0f : 1f;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(xAnchor, 1f);
        if (!pullingPanel) {
            panel.anchoredPosition = new Vector2(panelOnLeft ? panelLeftX : -Margin, -top);
        }
        if (widthGripRect != null) {
            float gx = panelOnLeft ? 1f : 0f;
            widthGripRect.anchorMin = new Vector2(gx, 0f);
            widthGripRect.anchorMax = new Vector2(gx, 1f);
        }
        UpdateSideLabel();
        if (layersPanel != null) {
            layersPanel.gameObject.SetActive(LayersShown);
            layersPanel.anchoredPosition = new Vector2(leftMargin, -top);
            layersPanel.sizeDelta = new Vector2(LayersWidth, AvailableHeight);
        }
        if (toolbarArea != null) {
            toolbarArea.offsetMin = new Vector2(left, toolbarArea.offsetMin.y);
            toolbarArea.offsetMax = new Vector2(-right, 0f);
        }
        if (toolbar != null) {
            toolbar.anchoredPosition = new Vector2(0f, -top);
        }
        if (bottomArea != null) {
            bottomArea.offsetMin = new Vector2(left, 0f);
            bottomArea.offsetMax = new Vector2(-right, 1f);
        }
        ApplyRulerLayout();
        UpdatePullTab();
        if (keysOpen) {
            PositionKeysBubble();
        }
    }

    // ---- grid ----

    private static RectTransform gridLayer;
    private static O5Button gridButton;
    private static readonly List<Image> gridLines = [];
    private static float gridBuiltSpacing = -1f;
    private static Vector2 gridBuiltScreen;

    private static void ToggleGrid() {
        MainCore.Conf.HudGrid.Value = !MainCore.Conf.HudGrid.Value;
        MainCore.ConfMgr.RequestSave();
        UpdateGridButton();
    }

    private static void UpdateGridButton() {
        if (gridButton == null) {
            return;
        }
        gridButton.NormalColor = MainCore.Conf.HudGrid.Value ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        gridButton.UpdateVisual(true);
    }

    /// <summary>Grid spacing in screen pixels (the setting is in HUD canvas units).</summary>
    private static float GridSpacing => Mathf.Max(4f, MainCore.Conf.HudGridSize.Value) / UnitsPerPixel(elements.FirstOrDefault());

    // Lines through the screen centre every GridSpacing, a stronger one every 4th.
    private static void RenderGrid() {
        bool on = MainCore.Conf.HudGrid.Value;
        gridLayer.gameObject.SetActive(on);
        if (!on) {
            return;
        }
        float s = GridSpacing;
        var screen = new Vector2(Screen.width, Screen.height);
        if (Mathf.Approximately(s, gridBuiltSpacing) && screen == gridBuiltScreen) {
            return;
        }
        gridBuiltSpacing = s;
        gridBuiltScreen = screen;
        int used = 0;
        Vector2 c = screen * 0.5f;
        void Line(Vector2 a, Vector2 b, int step) {
            while (gridLines.Count <= used) {
                gridLines.Add(NewImage("GridLine", gridLayer, Color.white));
            }
            var img = gridLines[used++];
            img.color = new Color(1f, 1f, 1f, step == 0 ? 0.22f : step % 4 == 0 ? 0.12f : 0.05f);
            PlacePixelLine(img.rectTransform, a, b, 1f);
        }
        int nx = Mathf.CeilToInt(c.x / s);
        for (int i = -nx; i <= nx; i++) {
            float x = c.x + i * s;
            Line(new Vector2(x, 0f), new Vector2(x, screen.y), i);
        }
        int ny = Mathf.CeilToInt(c.y / s);
        for (int i = -ny; i <= ny; i++) {
            float y = c.y + i * s;
            Line(new Vector2(0f, y), new Vector2(screen.x, y), i);
        }
        for (int i = used; i < gridLines.Count; i++) {
            gridLines[i].gameObject.SetActive(false);
        }
    }

    // Distance lines with a value badge in the middle (Alt measuring, equal-spacing gaps).
    private static readonly List<Image> measureLines = [];
    private static readonly List<(Image Back, TextMeshProUGUI Text)> measureLabels = [];

    private static void RenderMeasures() {
        int i = 0;
        foreach (var (a, b, label, spacing) in measures) {
            Color color = spacing ? GuideColor : MeasureColor;
            while (measureLines.Count <= i) {
                measureLines.Add(NewImage("Measure", gizmoLayer, MeasureColor));
            }
            while (measureLabels.Count <= i) {
                var back = UiStyle.Rounded("MeasureLabel", handleLayer, MeasureColor);
                back.raycastTarget = false;
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var text = O5Factory.ControlText(O5KitAdapters.Ctx, back.rectTransform, 13f, true);
                Stretch(text.rectTransform);
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.color = Color.white;
                measureLabels.Add((back, text));
            }
            var line = measureLines[i];
            line.color = color;
            PlacePixelLine(line.rectTransform, a, b, 1f);
            line.rectTransform.SetAsLastSibling();
            var (bg, txt) = measureLabels[i];
            bg.color = color;
            txt.text = label;
            // Pixel-snapped pill: even pixel height, radius exactly half of it,
            // so the rounded ends stay circular instead of squashed corners.
            float sf = UiScale;
            float heightPx = 2f * Mathf.Max(1f, Mathf.Round(9f * sf));
            Vector2 size = new(Mathf.Round((12f + label.Length * 8f) * sf) / sf, heightPx / sf);
            bg.sprite = MainCore.Spr.Shapes.Get(new O5Kit.Core.O5Shape(size.y * 0.5f));
            bg.rectTransform.sizeDelta = size;
            Vector2 corner = ToLocal((a + b) * 0.5f) - size * 0.5f;
            corner = new Vector2(Mathf.Round(corner.x * sf) / sf, Mathf.Round(corner.y * sf) / sf);
            bg.rectTransform.anchoredPosition = corner + size * 0.5f;
            bg.gameObject.SetActive(true);
            bg.rectTransform.SetAsLastSibling();
            i++;
        }
        for (int j = i; j < measureLines.Count; j++) {
            measureLines[j].gameObject.SetActive(false);
        }
        for (int j = i; j < measureLabels.Count; j++) {
            measureLabels[j].Back.gameObject.SetActive(false);
        }
    }

    private static RectTransform GetGuide(int index) {
        while (guides.Count <= index) {
            guides.Add(NewImage("Guide", gizmoLayer, GuideColor).rectTransform);
        }
        return guides[index];
    }

    private static Image dimImg;
    private static bool isClosing;
    private static readonly List<O5Kit.Core.ITweenHandle> viewAnimTweens = [];
    private const float WallAnimDuration = 0.3f;

    private static void KillViewAnims() {
        foreach (var t in viewAnimTweens) {
            t?.Kill();
        }
        viewAnimTweens.Clear();
    }

    private static void AnimateView(bool opening, Action onDone = null) {
        KillViewAnims();
        if (dimImg != null) {
            dimImg.raycastTarget = opening;
            if (opening) {
                dimImg.color = new Color(DimColor.r, DimColor.g, DimColor.b, 0f);
            }
            viewAnimTweens.Add(dimImg.TColor(opening ? DimColor : new Color(DimColor.r, DimColor.g, DimColor.b, 0f), WallAnimDuration, O5Kit.Core.O5Ease.OutExpo));
        }

        void Slide(RectTransform r, Vector2 wallPos, Vector2 restingPos, Action done = null) {
            if (r == null || !r.gameObject.activeSelf) {
                done?.Invoke();
                return;
            }
            if (opening) {
                r.anchoredPosition = wallPos;
                viewAnimTweens.Add(r.TAnchorPos(restingPos, WallAnimDuration, O5Kit.Core.O5Ease.OutExpo, done));
            } else {
                viewAnimTweens.Add(r.TAnchorPos(wallPos, WallAnimDuration, O5Kit.Core.O5Ease.OutExpo, done));
            }
        }

        float top = TopInset;
        float leftMargin = LeftInset;
        float panelLeftX = LayersShown ? leftMargin + LayersWidth + Margin : leftMargin;
        Slide(toolbar, new Vector2(0f, ToolbarHeight + top + 28f), new Vector2(0f, -top));
        Slide(bottomBar, new Vector2(0f, -(ToolbarHeight + Margin + 28f)), new Vector2(0f, Margin));
        Slide(panel,
            panelOnLeft ? new Vector2(-(PanelWidth + leftMargin + 28f), -top) : new Vector2(PanelWidth + Margin + 28f, -top),
            new Vector2(panelOnLeft ? panelLeftX : -Margin, -top));
        Slide(layersPanel, new Vector2(-(LayersWidth + leftMargin + 28f), -top), new Vector2(leftMargin, -top));
        Slide(pullTab, new Vector2(TabWidth + 24f, 0f), new Vector2(10f, 0f));

        if (rulerLayer != null && rulerLayer.gameObject.activeSelf) {
            if (rulerTop != null) {
                Vector2 restTop = rulerTop.anchoredPosition;
                Slide(rulerTop, restTop + new Vector2(0f, RulerPx + 20f), restTop);
            }
            if (rulerLeft != null) {
                Vector2 restLeft = rulerLeft.anchoredPosition;
                Slide(rulerLeft, restLeft + new Vector2(-(RulerPx + 20f), 0f), restLeft);
            }
            if (rulerCorner != null) {
                Slide(rulerCorner, new Vector2(-(RulerPx + 20f), RulerPx + 20f), Vector2.zero);
            }
            var rulerCg = rulerLayer.GetComponent<CanvasGroup>() ?? rulerLayer.gameObject.AddComponent<CanvasGroup>();
            if (opening) {
                rulerCg.alpha = 0f;
            }
            viewAnimTweens.Add(rulerCg.TFade(opening ? 1f : 0f, WallAnimDuration, O5Kit.Core.O5Ease.OutExpo));
        }

        if (gizmoGroup != null) {
            if (opening) {
                gizmoGroup.alpha = 0f;
            }
            viewAnimTweens.Add(gizmoGroup.TFade(opening ? 1f : 0f, WallAnimDuration, O5Kit.Core.O5Ease.OutExpo));
        }

        if (onDone != null) {
            viewAnimTweens.Add(O5KitAdapters.Ctx.Tween.TweenFloat(() => 0f, _ => { }, 1f, WallAnimDuration, onDone));
        }
    }

    private static void DestroyView() {
        HudCursor.Reset();
        resizingPanel = false;
        hoveredPanelGripAngle = null;
        KillViewAnims();
        isClosing = false;
        closingRestorePanel = false;
        dimImg = null;
        bottomBar = null;
        EndInlineEdit();
        CommitQuickEdit();
        O5KitAdapters.Ctx.Tooltip.Hide();
        pullTab = null;
        pullingPanel = false;
        bottomArea = null;
        docks.Clear();
        grabbable.Clear();
        tearing = moving = titleDragging = null;
        pressedHeader = null;
        dropHint = null;
        bottomGroup = null;
        bottomTools.Clear();
        DestroyKeysBubble();
        DestroyRulers();
        DestroyAutoAnchorGizmos();
        CloseContextMenu();
        inspector?.Dispose();
        inspector = null;
        inspectedObject = null;
        inspectorDirty = false;
        frames.Clear();
        guides.Clear();
        measureLines.Clear();
        measureLabels.Clear();
        groupFrame = marqueeFrame = null;
        panelBefore = null;
        foreach (var control in controls) {
            try {
                control.Dispose();
            } catch {
            }
        }
        controls.Clear();
        undo.Changed -= UpdateHistoryButtons;
        xSlider = ySlider = scaleSlider = rotationSlider = null;
        placementButton = fitButton = ungroupButton = pinButton = sideButton = gridButton = duplicateButton = deleteButton = undoButton = redoButton = null;
        gridLines.Clear();
        gridBuiltSpacing = -1f;
        gridLayer = null;
        panelShown = true;
        panelOnLeft = panelPreferredLeft = false;
        panelGroup = toolbarGroup = layersGroup = gizmoGroup = null;
        layersPanel = layersContent = null;
        layersScroller = null;
        layerRows.Clear();
        dropMarker = null;
        renameField = null;
        layersButton = null;
        dragRow = null;
        layersDirty = true;
        layersSignature = null;
        if (gizmoCanvasObj != null) {
            Object.Destroy(gizmoCanvasObj);
            gizmoCanvasObj = null;
        }
        if (root != null) {
            Object.Destroy(root.gameObject);
        }
        root = gizmoLayer = pixelHandleLayer = handleLayer = aspectDiagonalLine = panel = toolbar = inspectorContent = toolbarArea = widthGripRect = null;
        singleRows = multiRows = inspectorHost = textRows = imageRows = anchorRow = allPropsRow = multiPropsRow = null;
        nameInput = textInput = null;
        wSlider = hSlider = opacitySlider = fontSizeSlider = null;
        textColorPicker = imageColorPicker = null;
        textAlign = null;
        allPropsButton = multiPropsButton = null;
        panelBeforeMulti = null;
        layoutRows = layoutDetails = null;
        gapSlider = paddingSlider = null;
    }
}
