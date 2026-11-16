using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
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
    private const float RotateHandleOffset = 28f;
    private const float HandleSize = 10f;
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
    private static readonly Color SelectedColor = new(0.569f, 0.604f, 1f, 1f);
    private static readonly Color GroupColor = new(0.569f, 0.604f, 1f, 0.5f);
    private static readonly Color LockedColor = new(1f, 0.6f, 0.35f, 0.9f);
    private static readonly Color GuideColor = new(1f, 0.35f, 0.75f, 0.9f);
    private static readonly Color MeasureColor = new(1f, 0.32f, 0.36f, 1f);

    private static readonly string[] PlacementNames = ["Main canvas", "New canvas", "Next to selected", "Inside selected", "Wrap selected"];

    private static RectTransform root;
    private static RectTransform gizmoLayer;
    private static RectTransform panel;
    private static RectTransform toolbar;
    private static RectTransform contextMenu;

    private sealed class Frame {
        public RectTransform Rect;
        public Image[] Edges;
    }

    private static readonly List<Frame> frames = [];
    private static readonly List<RectTransform> guides = [];
    private static readonly RectTransform[] cornerHandles = new RectTransform[4];
    private static RectTransform rotateHandle;
    private static RectTransform rotateStem;
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
        get => Enum.TryParse(MainCore.Conf.HudPlacement.Value, out Placement p) ? p : Placement.MainCanvas;
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

    // Dark halo so gizmos stay visible over white/bright HUD elements.
    private static Image WithHalo(Image image) {
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;
        return image;
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

    private static O5Button Icon(Transform parent, string id, UISprite icon, Action onClick, string tooltip) {
        var button = Track(UiStyle.IconButton(Cell(parent, ToolbarHeight - 12f), "plus_hud_" + id, MainCore.Spr.Get(icon), onClick, 9f));
        UiStyle.Tooltip(button, tooltip);
        return button;
    }

    private static void BuildView() {
        DestroyView();
        var ctx = O5KitAdapters.Ctx;

        root = NewRect("OverlayerPlus.HudEditor", UICore.CanvasObj.transform);
        Stretch(root);
        root.SetAsLastSibling();
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = DimColor;
        dim.raycastTarget = true; // swallow clicks meant for the game / other UI

        if (MainCore.Conf.HudShowHints.Value) {
            var hint = O5Factory.ControlText(ctx, root, 14f, true);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.offsetMin = new Vector2(Margin + 4f, 10f);
            hint.rectTransform.offsetMax = new Vector2(-PanelWidth - Margin * 2f, 34f);
            hintRect = hint.rectTransform;
            hint.alignment = TextAlignmentOptions.BottomLeft;
            hint.raycastTarget = false;
            hint.color = UiStyle.Caption;
            hint.text = "Double-click  into group      Ctrl+click  deep select      Drag empty  box select      Right-click  more      Alt  measure / no snap      Esc  close";
        }

        gridLayer = NewRect("Grid", root);
        Stretch(gridLayer);
        gizmoLayer = NewRect("Gizmos", root);
        Stretch(gizmoLayer);

        marqueeFrame = NewFrame();
        foreach (var edge in marqueeFrame.Edges) {
            edge.color = SelectedColor;
        }
        var marqueeFill = NewImage("Fill", marqueeFrame.Rect, new Color(0.569f, 0.604f, 1f, 0.12f));
        Stretch(marqueeFill.rectTransform);
        groupFrame = NewFrame();
        foreach (var edge in groupFrame.Edges) {
            edge.color = GroupColor;
        }
        for (int i = 0; i < 4; i++) {
            var handle = WithHalo(NewImage("Corner" + i, gizmoLayer, Color.white)).rectTransform;
            handle.sizeDelta = new Vector2(HandleSize, HandleSize);
            cornerHandles[i] = handle;
        }
        rotateStem = WithHalo(NewImage("RotateStem", gizmoLayer, SelectedColor)).rectTransform;
        rotateHandle = WithHalo(NewImage("Rotate", gizmoLayer, Color.white, MainCore.Spr.Get(UISprite.Circle256))).rectTransform;
        rotateHandle.sizeDelta = new Vector2(HandleSize + 4f, HandleSize + 4f);

        BuildToolbar();
        BuildPanel(ctx);
        BuildLayersPanel();
        ApplyPanelLayout();
        UpdateGridButton();
        UpdateLayersButton();
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
        toolbar.anchorMin = toolbar.anchorMax = new Vector2(0.5f, 1f);
        toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.anchoredPosition = new Vector2(0f, -Margin);
        toolbar.sizeDelta = new Vector2(0f, ToolbarHeight);
        UiStyle.Horizontal(toolbar.gameObject, 4f, new RectOffset(6, 6, 6, 6));
        var fitter = toolbar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        layersButton = Button(Cell(toolbar, 70f), "layers", "Layers", ToggleLayers, "Show or hide the layers list");
        UiStyle.Separator(toolbar, true);
        Icon(toolbar, "add_text", UISprite.Text128, () => Create(NewKind.Text, CurrentPlacement, null), "Add text  (or right-click the screen)");
        Icon(toolbar, "add_image", UISprite.Image128, () => Create(NewKind.Image, CurrentPlacement, null), "Add image");
        O5Button presetsButton = null;
        presetsButton = Button(Cell(toolbar, 90f), "presets", "Presets", () => {
            var c = new Vector3[4];
            presetsButton.Rect.GetWorldCorners(c);
            OpenPresetMenu(new Vector2(c[0].x, c[0].y - 6f), null);
        }, "Ready-made HUD pieces and layouts");
        placementButton = Button(Cell(toolbar, 170f), "placement", string.Empty, CyclePlacement, "Where new objects go. Click to change.");
        gridButton = Button(Cell(toolbar, 60f), "grid", "Grid", ToggleGrid, "Show the grid and snap to it");
        UiStyle.Separator(toolbar, true);
        Icon(toolbar, "duplicate", UISprite.Clone128, DuplicateSelection, "Duplicate  Ctrl+D");
        Icon(toolbar, "delete", UISprite.X128, DeleteSelection, "Delete  Del");
        UiStyle.Separator(toolbar, true);
        Button(Cell(toolbar, 64f), "undo", "Undo", () => AfterHistory(undo.Undo()), "Ctrl+Z");
        Button(Cell(toolbar, 64f), "redo", "Redo", () => AfterHistory(undo.Redo()), "Ctrl+Y");
        UiStyle.Separator(toolbar, true);
        Button(Cell(toolbar, 76f), "close", "Done", Close, "Esc", primary: true);
        UpdatePlacementLabel();
    }

    private static void CyclePlacement() {
        CurrentPlacement = (Placement)(((int)CurrentPlacement + 1) % PlacementNames.Length);
        UpdatePlacementLabel();
    }

    private static void UpdatePlacementLabel() {
        if (placementButton != null) {
            placementButton.Text = PlacementNames[(int)CurrentPlacement] + "  ▾";
        }
    }

    // ---- side panel ----

    private static void BuildPanel(O5Kit.Core.O5Context ctx) {
        panel = UiStyle.Rounded("Panel", root, UiStyle.Surface).rectTransform;
        // Hangs from the top-right corner; width and height come from ApplyPanelLayout.
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
        panel.anchoredPosition = new Vector2(-Margin, -Margin);
        BuildResizeGrip();

        var content = NewRect("Content", panel);
        Stretch(content);
        content.offsetMin = new Vector2(18f, 18f);
        content.offsetMax = new Vector2(-18f, -16f);
        UiStyle.Vertical(content.gameObject, 8f);

        titleText = O5Factory.ControlTextH1(ctx, O5Factory.Row(ctx, content, 34f));
        infoText = O5Factory.ControlText(ctx, O5Factory.Row(ctx, content, 36f), 14f);
        infoText.color = UiStyle.Muted;
        infoText.enableWordWrapping = true;

        // One object: quick transform values, then Overlayer's full inspector.
        singleRows = VerticalGroup("Single", content).gameObject;
        var single = singleRows.transform;
        UiStyle.Section(single, "Transform");
        xSlider = Slider(ctx, single, "x", "X", -1000, 1000, "F1",
            (ref HudElement.State s, double v) => s.Position = new Vector2((float)v, s.Position.y));
        ySlider = Slider(ctx, single, "y", "Y", -600, 600, "F1",
            (ref HudElement.State s, double v) => s.Position = new Vector2(s.Position.x, (float)v));
        scaleSlider = Slider(ctx, single, "scale", "Scale", 0, 3, "F2",
            (ref HudElement.State s, double v) => s.Scale = new Vector3((float)v, (float)v, s.Scale.z));
        rotationSlider = Slider(ctx, single, "rotation", "Rotation", -180, 180, "F1",
            (ref HudElement.State s, double v) => s.Rotation = (float)v);
        var actions = UiStyle.HRow(single, 6f, 36f);
        Button(Cell(actions, 0f), "reset", "Reset", ResetSelection, "Position 0, scale 1, rotation 0");
        fitButton = Button(Cell(actions, 0f), "fit", "Fit to text", FitSelection, "Resize the box to the visible text");
        ungroupButton = Button(Cell(actions, 0f), "ungroup", "Ungroup", UngroupSelection, "Ctrl+Shift+G");

        // Several objects: align / distribute.
        multiRows = VerticalGroup("Multi", content).gameObject;
        var multi = multiRows.transform;
        UiStyle.Section(multi, "Align");
        var alignRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(alignRow, 0f), "align_left", "Left", () => Align(AlignMode.Left));
        Button(Cell(alignRow, 0f), "align_cx", "Center", () => Align(AlignMode.CenterX));
        Button(Cell(alignRow, 0f), "align_right", "Right", () => Align(AlignMode.Right));
        var alignRowY = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(alignRowY, 0f), "align_top", "Top", () => Align(AlignMode.Top));
        Button(Cell(alignRowY, 0f), "align_cy", "Middle", () => Align(AlignMode.CenterY));
        Button(Cell(alignRowY, 0f), "align_bottom", "Bottom", () => Align(AlignMode.Bottom));
        UiStyle.Section(multi, "Distribute");
        var distRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(distRow, 0f), "dist_x", "Horizontally", () => Align(AlignMode.DistributeX), "Equal gaps, needs 3 or more");
        Button(Cell(distRow, 0f), "dist_y", "Vertically", () => Align(AlignMode.DistributeY), "Equal gaps, needs 3 or more");
        UiStyle.Section(multi, "Arrange");
        var arrangeRow = UiStyle.HRow(multi, 4f, 34f);
        Button(Cell(arrangeRow, 0f), "group", "Group", GroupSelection, "Ctrl+G");
        Button(Cell(arrangeRow, 0f), "multi_ungroup", "Ungroup", UngroupSelection, "Ctrl+Shift+G (selected groups)");

        var properties = VerticalGroup("PropertiesHeader", content);
        UiStyle.Section(properties, "Properties");
        var (viewport, scrollContent, _) = O5Factory.ScrollView(ctx, content, 10f, expandLayout: true);
        inspectorHost = viewport.parent.gameObject;
        inspectorPropertiesHeader = properties.gameObject;
        inspectorContent = VerticalGroup("Inspector", scrollContent);
    }

    private static GameObject inspectorPropertiesHeader;

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

    private static O5Slider Slider(O5Kit.Core.O5Context ctx, Transform parent, string id, string label,
        double min, double max, string format, Setter set) {
        var slider = Track(O5Factory.Slider(ctx, O5Factory.Row(ctx, parent), "plus_hud_" + id, new O5SliderOptions {
            Min = min,
            Max = max,
            Label = label,
            Format = format,
            ClampDrag = false,
            ClampInput = false,
        }));
        slider.OnChanged = value => {
            var el = Primary;
            if (el == null || selection.Count != 1) {
                return;
            }
            panelBefore ??= el.Capture();
            var state = el.Capture();
            set(ref state, value);
            el.Apply(state);
        };
        slider.OnComplete = _ => CommitPanelEdit();
        return slider;
    }

    private delegate void Setter(ref HudElement.State state, double value);

    private static void CommitPanelEdit() {
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
            el.Apply(new HudElement.State { Position = Vector2.zero, Scale = Vector3.one, Rotation = 0f });
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
        var el = Primary;
        bool single = selection.Count == 1;
        singleRows.SetActive(single);
        multiRows.SetActive(selection.Count > 1);

        if (selection.Count == 0) {
            titleText.text = "Nothing selected";
            infoText.text = "Click a HUD element to edit it, or right-click to add one.";
        } else if (!single) {
            titleText.text = $"{selection.Count} selected";
            int locked = selection.Count(e => e.Locked || !e.CanMove);
            infoText.text = "Drag any of them to move together." + (locked > 0 ? $"\n{locked} can't move (locked or driven by an expression)." : string.Empty);
        } else {
            titleText.text = el.Name;
            var locks = new List<string>();
            if (el.Locked) {
                locks.Add(el.LockReason);
            } else {
                if (!el.CanMove) {
                    locks.Add("position");
                }
                if (!el.CanScale) {
                    locks.Add("scale");
                }
                if (!el.CanRotate) {
                    locks.Add("rotation");
                }
            }
            infoText.text = $"Canvas: {el.Canvas.Config.Name.Value}"
                + (locks.Count == 0 ? string.Empty
                    : el.Locked ? $"\nLocked: {locks[0]}" : $"\nDriven by an expression: {string.Join(", ", locks)}");
            // O5Object.EnabledWhen is only evaluated once, so block per selection here.
            xSlider.SetBlocked(!el.CanMove, true);
            ySlider.SetBlocked(!el.CanMove, true);
            scaleSlider.SetBlocked(!el.CanScale, true);
            rotationSlider.SetBlocked(!el.CanRotate, true);
            fitButton.Rect.parent.gameObject.SetActive(el.Object.GameObject.GetComponent<TextMeshProUGUI>() != null);
            ungroupButton.Rect.parent.gameObject.SetActive(!el.Locked && el.Object.Children.Count > 0);
        }
        SyncPanelValues();

        object target = single && !el.Locked ? el.Object : null;
        if (!ReferenceEquals(target, inspectedObject)) {
            RebuildInspector(target);
        }
    }

    private static void RebuildInspector(object target) {
        inspectorDirty = false;
        inspectedObject = target;
        inspectorHost.SetActive(target != null);
        inspectorPropertiesHeader.SetActive(target != null);
        inspector?.Dispose();
        inspector = target == null ? null : HudInspector.ForObject(inspectorContent, (Overlayer.Overlay.OvObject)target, OnInspectorEdit);
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
        if (panel == null || selection.Count != 1) {
            return;
        }
        var s = Primary.Capture();
        xSlider.Set(s.Position.x, false);
        ySlider.Set(s.Position.y, false);
        scaleSlider.Set(s.Scale.x, false);
        rotationSlider.Set(s.Rotation, false);
    }

    // ---- context menu ----

    private static bool ContextMenuOpen => contextMenu != null;

    private static void OpenContextMenu(Vector2 mouse, HudElement target) {
        CloseContextMenu();
        contextMenu = UiStyle.Rounded("ContextMenu", root, UiStyle.Surface).rectTransform;
        contextMenu.anchorMin = contextMenu.anchorMax = Vector2.zero;
        contextMenu.pivot = new Vector2(0f, 1f);
        UiStyle.Vertical(contextMenu.gameObject, 2f, new RectOffset(6, 6, 6, 6));
        var fitter = contextMenu.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contextMenu.sizeDelta = new Vector2(MenuWidth, 0f);

        // null = separator
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?>();
        if (target == null || target.Locked) {
            // Empty spot: relative placements make no sense here, use the main canvas instead.
            Placement where = CurrentPlacement is Placement.NewCanvas ? Placement.NewCanvas : Placement.MainCanvas;
            items.Add(("Add text here", null, () => Create(NewKind.Text, where, mouse), false));
            items.Add(("Add image here", null, () => Create(NewKind.Image, where, mouse), false));
            items.Add(("Add preset here…", null, () => OpenPresetMenu(mouse, mouse), false));
            items.Add(("Paste", "Ctrl+V", () => Paste(false), false));
            if (selection.Count > 0) {
                items.Add(null);
                items.Add(("Deselect", "Esc", () => Select(null), false));
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
            if (single) {
                items.Add(("Add text inside", null, () => Create(NewKind.Text, Placement.Child, mouse), false));
                items.Add(("Add text next to it", null, () => Create(NewKind.Text, Placement.Sibling, mouse), false));
                items.Add(null);
                items.Add(("Bring to front", "Ctrl+Shift+]", () => MoveToEnd(true), false));
                items.Add(("Bring forward", "Ctrl+]", () => MoveOrder(1), false));
                items.Add(("Send backward", "Ctrl+[", () => MoveOrder(-1), false));
                items.Add(("Send to back", "Ctrl+Shift+[", () => MoveToEnd(false), false));
                items.Add(null);
            }
            items.Add(("Group", "Ctrl+G", GroupSelection, false));
            if (selection.Any(e => !e.Locked && e.Object.Children.Count > 0)) {
                items.Add(("Ungroup", "Ctrl+Shift+G", UngroupSelection, false));
            }
            items.Add(null);
            items.Add(("Copy", "Ctrl+C", CopySelection, false));
            items.Add(("Paste", "Ctrl+V", () => Paste(false), false));
            items.Add(("Copy style", "Ctrl+Alt+C", CopyStyle, false));
            items.Add(("Paste style", "Ctrl+Alt+V", PasteStyle, false));
            items.Add(("Duplicate", "Ctrl+D", DuplicateSelection, false));
            items.Add(null);
            items.Add(("Lock", "Ctrl+Shift+L", ToggleLockSelection, false));
            items.Add(("Hide", "Ctrl+Shift+H", HideSelection, false));
            items.Add(("Reset transform", null, ResetSelection, false));
            items.Add(null);
            items.Add(("Delete", "Del", DeleteSelection, true));
        }
        for (int i = 0; i < items.Count; i++) {
            if (items[i] is not { } item) {
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
        local.x = Mathf.Min(local.x, screen.x - size.x - 4f);
        local.y = Mathf.Max(local.y, size.y + 4f);
        contextMenu.anchoredPosition = local;
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
    private static readonly List<RaycastResult> raycastHits = [];

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
        if (root == null) {
            return;
        }
        if (inspectorDirty && !TypingInField() && drag == DragMode.None) {
            inspectorDirty = false;
            if (inspector != null) {
                inspector.Rebuild();
            }
        }

        int used = 0;
        foreach (var el in elements) {
            if (!el.IsAlive) {
                continue;
            }
            var frame = GetFrame(used++);
            el.GetScreenCorners(corners);
            PlaceQuad(frame.Rect, corners);
            bool isHovered = ReferenceEquals(el, hovered);
            Color color = selection.Contains(el) ? SelectedColor
                : el.Locked ? LockedColor * new Color(1f, 1f, 1f, isHovered ? 1f : 0.5f)
                : isHovered ? HoverColor : IdleColor;
            foreach (var edge in frame.Edges) {
                edge.color = color;
            }
        }
        for (int i = used; i < frames.Count; i++) {
            frames[i].Rect.gameObject.SetActive(false);
        }

        // Selection box while dragging on empty space.
        if (drag == DragMode.Marquee && dragChanged) {
            var r = MarqueeRect(lastMouse);
            PlaceQuad(marqueeFrame.Rect, [new(r.xMin, r.yMin), new(r.xMin, r.yMax), new(r.xMax, r.yMax), new(r.xMax, r.yMin)]);
            marqueeFrame.Rect.SetAsLastSibling();
        } else {
            marqueeFrame.Rect.gameObject.SetActive(false);
        }

        // Multi-selection: one box around everything that moves together.
        if (selection.Count > 1 && UnionBounds(selection, out float minX, out float maxX, out float minY, out float maxY)) {
            var box = new Vector3[] { new(minX - 4f, minY - 4f), new(minX - 4f, maxY + 4f), new(maxX + 4f, maxY + 4f), new(maxX + 4f, minY - 4f) };
            PlaceQuad(groupFrame.Rect, box);
            groupFrame.Rect.SetAsLastSibling();
        } else {
            groupFrame.Rect.gameObject.SetActive(false);
        }

        var sel = selection.Count == 1 ? Primary : null;
        bool showHandles = sel != null && sel.IsAlive;
        if (showHandles) {
            sel.GetScreenCorners(corners);
        }
        for (int i = 0; i < 4; i++) {
            bool show = showHandles && sel.CanScale;
            cornerHandles[i].gameObject.SetActive(show);
            if (show) {
                cornerHandles[i].anchoredPosition = ToLocal(corners[i]);
            }
        }
        bool showRotate = showHandles && sel.CanRotate;
        rotateHandle.gameObject.SetActive(showRotate);
        rotateStem.gameObject.SetActive(showRotate);
        if (showRotate) {
            Vector2 top = ToLocal((corners[1] + corners[2]) * 0.5f);
            Vector2 knob = ToLocal(RotateHandleScreen(corners));
            rotateHandle.anchoredPosition = knob;
            PlaceLine(rotateStem, top, knob, Border);
        }

        int g = 0;
        foreach (float x in guideX) {
            PlaceLine(GetGuide(g++), ToLocal(new Vector2(x, 0f)), ToLocal(new Vector2(x, Screen.height)), 1f);
        }
        foreach (float y in guideY) {
            PlaceLine(GetGuide(g++), ToLocal(new Vector2(0f, y)), ToLocal(new Vector2(Screen.width, y)), 1f);
        }
        for (int i = g; i < guides.Count; i++) {
            guides[i].gameObject.SetActive(false);
        }
        RenderMeasures();

        // Keep handles above frames.
        foreach (var h in cornerHandles) {
            h.SetAsLastSibling();
        }
        rotateStem.SetAsLastSibling();
        rotateHandle.SetAsLastSibling();
    }

    private static void PlaceQuad(RectTransform rect, Vector3[] screenCorners) {
        Vector2 bl = ToLocal(screenCorners[0]), tl = ToLocal(screenCorners[1]), tr = ToLocal(screenCorners[2]);
        Vector2 br = ToLocal(screenCorners[3]);
        rect.anchoredPosition = (bl + tl + tr + br) * 0.25f;
        rect.sizeDelta = new Vector2((tr - tl).magnitude, (tl - bl).magnitude);
        Vector2 dir = tr - tl;
        rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        rect.gameObject.SetActive(true);
    }

    private static void PlaceLine(RectTransform rect, Vector2 a, Vector2 b, float thickness) {
        Vector2 dir = b - a;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (a + b) * 0.5f;
        rect.sizeDelta = new Vector2(dir.magnitude, thickness);
        rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        rect.gameObject.SetActive(true);
    }

    private static Frame NewFrame() {
        var rect = NewRect("Frame", gizmoLayer);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var edges = new Image[4];
        for (int i = 0; i < 4; i++) {
            var edge = WithHalo(NewImage("Edge", rect, IdleColor)).rectTransform;
            // 0 bottom, 1 top, 2 left, 3 right
            edge.anchorMin = i switch { 1 => new Vector2(0f, 1f), 3 => new Vector2(1f, 0f), _ => Vector2.zero };
            edge.anchorMax = i switch { 0 => new Vector2(1f, 0f), 2 => new Vector2(0f, 1f), _ => Vector2.one };
            edge.sizeDelta = i < 2 ? new Vector2(0f, Border) : new Vector2(Border, 0f);
            edge.anchoredPosition = Vector2.zero;
            edges[i] = edge.GetComponent<Image>();
        }
        var frame = new Frame { Rect = rect, Edges = edges };
        rect.gameObject.SetActive(false);
        return frame;
    }

    private static Frame GetFrame(int index) {
        while (frames.Count <= index) {
            var frame = NewFrame();
            foreach (var edge in frame.Edges) {
                edge.color = IdleColor;
            }
            frames.Add(frame);
        }
        return frames[index];
    }

    // ---- panel size / pin ----

    private const float MinPanelHeight = 260f;
    private static RectTransform toolbarArea, hintRect;
    private static bool resizingPanel;
    private static bool panelShown = true;
    private static O5Button pinButton;

    // Pinned: always shown. Unpinned: only while something is selected.
    private static bool PanelWanted => MainCore.Conf.HudPanelPinned.Value || selection.Count > 0;

    private static void BuildResizeGrip() {
        // Left edge: width.
        Grip("WidthGrip", Vector2.zero, new Vector2(0f, 1f), new Vector2(12f, 0f), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.7f),
            new Vector2(3f, 0f), "Drag to resize the panel",
            d => MainCore.Conf.HudPanelWidth.Value = Mathf.Clamp(PanelWidth - d.x / UiScale, MinPanelWidth, MaxPanelWidth),
            () => { });
        // Bottom edge: height (double-click: full height again).
        Grip("HeightGrip", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, 0.5f),
            new Vector2(0f, 3f), "Drag to resize the panel, double-click for full height",
            d => MainCore.Conf.HudPanelHeight.Value = Mathf.Clamp(PanelHeight - d.y / UiScale, MinPanelHeight, AvailableHeight),
            () => MainCore.Conf.HudPanelHeight.Value = 0f);

        // Small pin toggle in the corner: pinned = always shown, else only with a selection.
        var pinCell = NewRect("Pin", panel);
        pinCell.anchorMin = pinCell.anchorMax = pinCell.pivot = Vector2.one;
        pinCell.anchoredPosition = new Vector2(-8f, -8f);
        pinCell.sizeDelta = new Vector2(64f, 24f);
        pinButton = Track(UiStyle.Button(pinCell, "plus_hud_pin", string.Empty, TogglePin, fontSize: 13f));
        UiStyle.Tooltip(pinButton, "Pinned: the panel stays open. Auto: it shows only while something is selected.");
        UpdatePinLabel();
    }

    private static void Grip(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 lineMin, Vector2 lineMax,
        Vector2 lineSize, string tooltip, Action<Vector2> drag, Action doubleClick) {
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
            (EventTriggerType.PointerEnter, _ => line.color = SelectedColor),
            (EventTriggerType.PointerExit, _ => {
                if (!resizingPanel) {
                    line.color = UiStyle.Line;
                }
            }),
            (EventTriggerType.BeginDrag, _ => resizingPanel = true),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe) {
                    drag(pe.delta);
                    ApplyPanelLayout();
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                resizingPanel = false;
                line.color = UiStyle.Line;
                MainCore.ConfMgr.RequestSave();
            }),
            (EventTriggerType.PointerClick, e => {
                if (e is PointerEventData { clickCount: 2 }) {
                    doubleClick();
                    ApplyPanelLayout();
                    MainCore.ConfMgr.RequestSave();
                }
            })
        );
        r.AddToolTip(O5KitAdapters.Ctx, tooltip);
    }

    private static void TogglePin() {
        MainCore.Conf.HudPanelPinned.Value = !MainCore.Conf.HudPanelPinned.Value;
        MainCore.ConfMgr.RequestSave();
        UpdatePinLabel();
        ApplyPanelLayout();
    }

    private static void UpdatePinLabel() {
        if (pinButton != null) {
            pinButton.Text = MainCore.Conf.HudPanelPinned.Value ? "Pinned" : "Auto";
        }
    }

    private static float AvailableHeight => root != null ? Mathf.Max(MinPanelHeight, root.rect.height - Margin * 2f) : 800f;

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
        panelShown = PanelWanted;
        panel.gameObject.SetActive(panelShown);
        float w = PanelWidth;
        panel.sizeDelta = new Vector2(w, PanelHeight);
        // The toolbar and hints use the space the panel leaves.
        float right = panelShown ? w + Margin : 0f;
        float left = LayersShown ? LayersWidth + Margin : 0f;
        if (layersPanel != null) {
            layersPanel.gameObject.SetActive(LayersShown);
            layersPanel.sizeDelta = new Vector2(LayersWidth, AvailableHeight);
        }
        if (toolbarArea != null) {
            toolbarArea.offsetMin = new Vector2(left, toolbarArea.offsetMin.y);
            toolbarArea.offsetMax = new Vector2(-right, 0f);
        }
        if (hintRect != null) {
            hintRect.offsetMin = new Vector2(left + Margin + 4f, hintRect.offsetMin.y);
            hintRect.offsetMax = new Vector2(-right - Margin, 34f);
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
            PlaceLine(img.rectTransform, ToLocal(a), ToLocal(b), 1f);
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
                var back = UiStyle.Rounded("MeasureLabel", gizmoLayer, MeasureColor);
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
            PlaceLine(line.rectTransform, ToLocal(a), ToLocal(b), 1.5f);
            line.rectTransform.SetAsLastSibling();
            var (bg, txt) = measureLabels[i];
            bg.color = color;
            txt.text = label;
            bg.rectTransform.sizeDelta = new Vector2(12f + label.Length * 8f, 18f);
            bg.rectTransform.anchoredPosition = ToLocal((a + b) * 0.5f);
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

    private static void DestroyView() {
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
        xSlider = ySlider = scaleSlider = rotationSlider = null;
        placementButton = fitButton = ungroupButton = pinButton = gridButton = null;
        gridLines.Clear();
        gridBuiltSpacing = -1f;
        gridLayer = null;
        panelShown = true;
        layersPanel = layersContent = null;
        layerRows.Clear();
        dropMarker = null;
        renameField = null;
        layersButton = null;
        dragRow = null;
        layersDirty = true;
        layersSignature = null;
        if (root != null) {
            Object.Destroy(root.gameObject);
        }
        root = gizmoLayer = panel = toolbar = inspectorContent = toolbarArea = hintRect = null;
        singleRows = multiRows = inspectorHost = inspectorPropertiesHeader = null;
    }
}
