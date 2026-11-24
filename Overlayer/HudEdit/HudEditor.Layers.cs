using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
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

// The canvas list (left panel): every canvas with its objects as a tree, hidden ones included.
// Click selects (Shift adds), double-click renames (canvases too), the dots on the right toggle
// visible / locked, the arrow folds. "+" makes a new canvas. Dragging a row onto another moves
// it: the top or bottom part puts it before / after, the middle puts it inside. Objects keep
// their spot on screen.
internal static partial class HudEditor {
    private const float RowHeight = 26f;
    private const float IndentPx = 14f;
    private const float MinLayersWidth = 200f;
    private const float MaxLayersWidth = 600f;

    private static RectTransform layersPanel;
    private static RectTransform layersContent;
    private static UIScrollController layersScroller;
    private static CanvasGroup layersGroup;
    private static bool layersDirty = true;
    private static string layersSignature;
    // Groups start folded; these were opened by hand. The way down to a selected object opens by itself.
    private static readonly HashSet<OvObject> openedObjects = [];
    private static readonly HashSet<OvCanvas> foldedCanvases = [];

    private sealed class LayerRow {
        public RectTransform Rect;
        public Image Back;
        public OvCanvas Canvas;
        public OvObject Object; // null for a canvas row
        public float TextLeft;
    }

    private static readonly List<LayerRow> layerRows = [];
    private static LayerRow dragRow;
    private static Image dropMarker;
    private static (LayerRow Row, int Where)? dropTarget; // Where: -1 before, 0 inside, 1 after
    private static O5InputField renameField;
    private static O5Button layersButton;
    private static OvObject layerAnchorObject;

    private static float LayersWidth => Mathf.Clamp(MainCore.Conf.HudLayersWidth.Value, MinLayersWidth, MaxLayersWidth);
    private static bool LayersShown => MainCore.Conf.HudLayersShown.Value;

    private static Color RowSelected => O5Palette.WithAlpha(Overlayer.UI.UIColors.ObjectActive, 0.32f);
    private static readonly Color RowHover = new(1f, 1f, 1f, 0.06f);

    private static void BuildLayersPanel() {
        layersPanel = UiStyle.Rounded("Layers", root, UiStyle.Surface).rectTransform;
        layersGroup = layersPanel.gameObject.AddComponent<CanvasGroup>();
        layersPanel.anchorMin = layersPanel.anchorMax = layersPanel.pivot = new Vector2(0f, 1f);
        layersPanel.anchoredPosition = new Vector2(Margin, -Margin);

        var title = O5Factory.ControlText(O5KitAdapters.Ctx, layersPanel, 13f, true);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.offsetMin = new Vector2(16f, -32f);
        title.rectTransform.offsetMax = new Vector2(-16f, -10f);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = UiStyle.Caption;
        title.characterSpacing = 6f;
        title.raycastTarget = false;
        title.text = Tr("HUD_LAYERS_TITLE", "CANVAS").ToUpperInvariant();

        // New canvas, at the right of the title.
        var addHost = NewRect("NewCanvas", layersPanel);
        addHost.anchorMin = addHost.anchorMax = addHost.pivot = new Vector2(1f, 1f);
        addHost.anchoredPosition = new Vector2(-8f, -6f);
        addHost.sizeDelta = new Vector2(28f, 28f);
        var add = Track(UiStyle.IconButton(addHost, "plus_hud_new_canvas", MainCore.Spr.Get(UISprite.Plus128), AddCanvas, 8f));
        UiStyle.Tooltip(add, Tr("HUD_NEW_CANVAS", "New canvas"));

        var scrollHost = NewRect("Scroll", layersPanel);
        Stretch(scrollHost);
        scrollHost.offsetMin = new Vector2(6f, 8f);
        scrollHost.offsetMax = new Vector2(-6f, -36f);
        var (_, content, scroller) = O5Factory.ScrollView(O5KitAdapters.Ctx, scrollHost, 1f, 0f);
        layersContent = content;
        layersScroller = scroller;
        layersScroller.rightDragRelativeOnly = true;

        dropMarker = NewImage("DropMarker", layersPanel, SelectedColor);
        dropMarker.gameObject.SetActive(false);

        // Right edge: width.
        var grip = NewImage("WidthGrip", layersPanel, Color.clear);
        grip.raycastTarget = true;
        grip.rectTransform.anchorMin = new Vector2(1f, 0f);
        grip.rectTransform.anchorMax = Vector2.one;
        grip.rectTransform.sizeDelta = new Vector2(10f, 0f);
        UnityUtils.AddEvents(grip.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.Drag, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe) {
                    MainCore.Conf.HudLayersWidth.Value = Mathf.Clamp(LayersWidth + pe.delta.x / UiScale, MinLayersWidth, MaxLayersWidth);
                    ApplyPanelLayout();
                }
            }),
            (EventTriggerType.EndDrag, _ => MainCore.ConfMgr.RequestSave()));
        layersDirty = true;
    }

    private static void ToggleLayers() {
        O5KitAdapters.Ctx.Tooltip.Hide();
        MainCore.Conf.HudLayersShown.Value = !LayersShown;
        MainCore.ConfMgr.RequestSave();
        UpdateLayersButton();
        ApplyPanelLayout();
        layersDirty = true;
    }

    private static void UpdateLayersButton() {
        if (layersButton == null) {
            return;
        }
        layersButton.NormalColor = LayersShown ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        layersButton.UpdateVisual(true);
    }

    // The structure as text: rebuild the rows only when it really changed.
    private static string LayersSignature() {
        var sb = new System.Text.StringBuilder();
        void Walk(OvObject o, int depth) {
            sb.Append(depth).Append(o.GetHashCode()).Append(o.Config.Name.Value).Append(o.Config.Enabled.Value ? '1' : '0').Append('|');
            foreach (var c in o.Children) {
                Walk(c, depth + 1);
            }
        }
        foreach (var canvas in OverlayCore.Canvases) {
            if (canvas == null) {
                continue;
            }
            sb.Append('#').Append(canvas.Config.Name.Value);
            foreach (var o in canvas.OvObjects) {
                Walk(o, 0);
            }
        }
        sb.Append(MainCore.Conf.HudLocked.Value?.Count ?? 0);
        // Selecting inside a folded group opens the way to it.
        foreach (var e in selection) {
            sb.Append('s').Append(e.Object.GetHashCode());
        }
        return sb.ToString();
    }

    private static void TickLayers() {
        if (layersPanel == null || !LayersShown || dragRow != null || renameField != null) {
            return;
        }
        string sig = LayersSignature();
        if (layersDirty || sig != layersSignature) {
            layersSignature = sig;
            layersDirty = false;
            RebuildLayers();
            ScrollToSelectedRow();
        } else {
            HighlightRows();
        }
    }

    private static void RebuildLayers() {
        foreach (var row in layerRows) {
            if (row.Rect != null) {
                UnityEngine.Object.Destroy(row.Rect.gameObject);
            }
        }
        layerRows.Clear();
        foreach (var canvas in OverlayCore.Canvases) {
            if (canvas == null || HudElement.IsPackage(canvas)) {
                continue;
            }
            AddRow(canvas, null, 0);
            if (foldedCanvases.Contains(canvas)) {
                continue;
            }
            foreach (var obj in canvas.OvObjects) {
                AddObjectRows(canvas, obj, 1);
            }
        }
        HighlightRows();
    }

    private static bool IsFolded(OvObject obj) => obj.Children.Count > 0 && !openedObjects.Contains(obj)
        && !selection.Any(e => IsAncestor(obj, e.Object));

    private static void AddObjectRows(OvCanvas canvas, OvObject obj, int depth) {
        AddRow(canvas, obj, depth);
        if (IsFolded(obj)) {
            return;
        }
        foreach (var child in obj.Children) {
            AddObjectRows(canvas, child, depth + 1);
        }
    }

    private static void AddRow(OvCanvas canvas, OvObject obj, int depth) {
        var rect = O5Factory.Row(O5KitAdapters.Ctx, layersContent, RowHeight);
        var back = rect.gameObject.AddComponent<Image>();
        back.color = Color.clear;
        back.raycastTarget = true;
        var row = new LayerRow { Rect = rect, Back = back, Canvas = canvas, Object = obj };
        layerRows.Add(row);

        bool isCanvas = obj == null;
        bool hasChildren = isCanvas ? canvas.OvObjects.Count > 0 : obj.Children.Count > 0;
        bool hidden = isCanvas ? !canvas.Config.Enabled.Value : !obj.Config.Enabled.Value;
        bool locked = !isCanvas && IsUserLocked(canvas, obj);
        float x = 6f + depth * IndentPx;

        // Fold arrow.
        if (hasChildren) {
            bool folded = isCanvas ? foldedCanvases.Contains(canvas) : IsFolded(obj);
            // The triangle sprite points down (open); turned a quarter it points right (folded).
            var arrowHit = NewRect("Fold", rect);
            arrowHit.anchorMin = arrowHit.anchorMax = new Vector2(0f, 0.5f);
            arrowHit.anchoredPosition = new Vector2(x + 7f, 0f);
            arrowHit.sizeDelta = new Vector2(18f, RowHeight);
            var arrowBack = arrowHit.gameObject.AddComponent<Image>();
            arrowBack.color = Color.clear;
            arrowBack.raycastTarget = true;
            var arrow = NewImage("Arrow", arrowHit, UiStyle.Muted, MainCore.Spr.Get(UISprite.Triangle128));
            arrow.preserveAspect = true;
            arrow.rectTransform.sizeDelta = new Vector2(9f, 9f);
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, folded ? 90f : 0f);
            UnityUtils.AddEvents(arrowHit.gameObject.AddComponent<EventTrigger>(), (EventTriggerType.PointerClick, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe) {
                    if (pe.button == PointerEventData.InputButton.Right) {
                        OnRowClick(row, pe);
                        return;
                    }
                    if (pe.button != PointerEventData.InputButton.Left) {
                        return;
                    }
                }
                if (isCanvas) {
                    if (!foldedCanvases.Remove(canvas)) {
                        foldedCanvases.Add(canvas);
                    }
                } else if (!openedObjects.Remove(obj)) {
                    openedObjects.Add(obj);
                }
                layersDirty = true;
            }));
        }
        x += 16f;
        row.TextLeft = x;

        string name = isCanvas ? canvas.Config.Name.Value : obj.Config.Name.Value;
        var label = RowText(rect, isCanvas ? $"<b>{name}</b>" : name, x, -56f, isCanvas ? 14f : 15f,
            hidden ? new Color(1f, 1f, 1f, 0.35f) : isCanvas ? UiStyle.Muted : Color.white);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableWordWrapping = false;

        if (!isCanvas) {
            // Locked (padlock) and visible (eye) toggles on the right.
            Toggle(row, -34f, locked ? UISprite.LockOn128 : UISprite.LockOff128, locked, Tr("HUD_LOCK", "Lock"), () => SetLocked(canvas, obj, !locked), 12f, 0.3f);
            Toggle(row, -12f, hidden ? UISprite.EyeHide128 : UISprite.EyeOpen128, !hidden, Tr("HUD_VISIBLE", "Visible"), () => SetHidden([obj], !hidden), 14f, 0.42f);
        } else {
            if (!canvas.Config.Enabled.UseFx) {
                bool shown = canvas.Config.Enabled.StaticValue;
                Toggle(row, -34f, shown ? UISprite.EyeOpen128 : UISprite.EyeHide128, shown, Tr("HUD_VISIBLE", "Visible"), () => SetCanvasHidden(canvas, shown), 14f, 0.42f);
            }
            DeleteToggle(row);
        }

        UnityUtils.AddEvents(rect.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => {
                if (!IsRowSelected(row)) {
                    back.color = RowHover;
                }
            }),
            (EventTriggerType.PointerExit, _ => HighlightRow(row)),
            (EventTriggerType.PointerClick, e => OnRowClick(row, UnityUtils.AsPointerEventData(e))),
            (EventTriggerType.BeginDrag, e => {
                if (!isCanvas && UnityUtils.AsPointerEventData(e) is { button: PointerEventData.InputButton.Left }) {
                    dragRow = row;
                }
            }),
            (EventTriggerType.Drag, e => OnRowDrag(UnityUtils.AsPointerEventData(e))),
            (EventTriggerType.EndDrag, _ => OnRowDrop()));
    }

    private static TextMeshProUGUI RowText(RectTransform row, string text, float x, float width, float size, Color color) {
        var t = O5Factory.ControlText(O5KitAdapters.Ctx, row, size, true);
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(x, 0f);
        r.offsetMax = new Vector2(x + Mathf.Max(width, 0f), 0f);
        if (width < 0f) {
            // Negative width: stretch to the right edge minus that much.
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMax = new Vector2(width, 0f);
        }
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = color;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    private static void Toggle(LayerRow row, float x, UISprite? sprite, bool on, string tooltip, Action toggle, float size = 9f, float offAlpha = 0.18f) {
        var hit = NewRect("Toggle", row.Rect);
        hit.anchorMin = hit.anchorMax = new Vector2(1f, 0.5f);
        hit.anchoredPosition = new Vector2(x, 0f);
        hit.sizeDelta = new Vector2(20f, RowHeight);
        var hitImage = hit.gameObject.AddComponent<Image>();
        hitImage.color = Color.clear;
        hitImage.raycastTarget = true;
        var mark = NewImage("Mark", hit, on ? Color.white : new Color(1f, 1f, 1f, offAlpha),
            sprite.HasValue ? MainCore.Spr.Get(sprite.Value) : null);
        mark.preserveAspect = true;
        mark.rectTransform.sizeDelta = new Vector2(size, size);
        UnityUtils.AddEvents(hit.gameObject.AddComponent<EventTrigger>(), (EventTriggerType.PointerClick, e => {
            if (UnityUtils.AsPointerEventData(e) is { } pe) {
                if (pe.button == PointerEventData.InputButton.Right) {
                    OnRowClick(row, pe);
                    return;
                }
                if (pe.button != PointerEventData.InputButton.Left) {
                    return;
                }
            }
            toggle();
            layersDirty = true;
        }));
        hit.AddToolTip(O5KitAdapters.Ctx, tooltip);
    }

    // Keep the (first) selected row in view after the list changed.
    private static void ScrollToSelectedRow() {
        var row = layerRows.FirstOrDefault(IsRowSelected);
        var scroller = layersContent != null ? layersContent.GetComponentInParent<UIScrollController>() : null;
        if (row == null || scroller == null || layersContent.parent is not RectTransform viewport) {
            return;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(layersContent);
        float top = -row.Rect.anchoredPosition.y - RowHeight * 0.5f;
        float view = viewport.rect.height;
        float now = layersContent.anchoredPosition.y;
        if (top < now || top + RowHeight > now + view) {
            scroller.ScrollTo(Mathf.Max(0f, top - view * 0.4f), true);
        }
    }

    private static bool IsRowSelected(LayerRow row) => row.Object != null && selection.Any(e => ReferenceEquals(e.Object, row.Object));

    private static void HighlightRow(LayerRow row) {
        if (row.Back != null) {
            row.Back.color = IsRowSelected(row) ? RowSelected : Color.clear;
        }
    }

    private static void HighlightRows() {
        foreach (var row in layerRows) {
            HighlightRow(row);
        }
    }

    private static float lastRowClickTime;
    private static LayerRow lastRowClicked;

    private static void OnRowClick(LayerRow row, PointerEventData e) {
        if (e == null) {
            return;
        }
        if (e.button == PointerEventData.InputButton.Right) {
            OnRowRightClick(row, e);
            return;
        }
        if (e.button != PointerEventData.InputButton.Left) {
            return;
        }
        CloseContextMenu();
        // By object (or canvas): the first click selects, which rebuilds the rows.
        bool sameRow = row.Object != null
            ? ReferenceEquals(row.Object, lastRowClicked?.Object)
            : lastRowClicked?.Object == null && ReferenceEquals(row.Canvas, lastRowClicked?.Canvas);
        bool isDouble = sameRow && Time.unscaledTime - lastRowClickTime <= DoubleClickSeconds;
        lastRowClickTime = Time.unscaledTime;
        lastRowClicked = row;
        if (isDouble) {
            BeginRename(row);
            return;
        }
        if (row.Object == null) {
            layerAnchorObject = null;
            return;
        }
        // Hidden objects aren't on screen, so they can't be selected (show them first).
        var el = ElementOf(row.Object);
        if (el == null) {
            return;
        }
        if (Shift && layerAnchorObject != null) {
            int a = layerRows.FindIndex(r => ReferenceEquals(r.Object, layerAnchorObject));
            int b = layerRows.IndexOf(row);
            if (a >= 0 && b >= 0) {
                int lo = Math.Min(a, b);
                int hi = Math.Max(a, b);
                var range = new List<HudElement>();
                if (Ctrl) {
                    range.AddRange(selection);
                }
                for (int i = lo; i <= hi; i++) {
                    if (layerRows[i].Object != null && ElementOf(layerRows[i].Object) is { } item && !range.Contains(item)) {
                        range.Add(item);
                    }
                }
                SetSelection(range);
                HighlightRows();
                return;
            }
        }
        if (Ctrl || Shift) {
            ToggleSelect(el);
        } else {
            Select(el);
        }
        layerAnchorObject = row.Object;
        HighlightRows();
    }

    private static void OnRowRightClick(LayerRow row, PointerEventData e) {
        if (row == null || (layersScroller != null && layersScroller.RightDragConsumed)) {
            return;
        }
        if (KeysBubbleOpen) {
            CloseKeysBubble();
        }
        if (renameField != null) {
            var prev = renameField;
            renameField = null;
            prev.Dispose();
        }
        if (row.Object == null) {
            OpenLayerCanvasContextMenu(e.position, row.Canvas);
            return;
        }
        var el = ElementOf(row.Object);
        if (el == null) {
            OpenHiddenObjectContextMenu(e.position, row.Canvas, row.Object);
            return;
        }
        if (!selection.Contains(el)) {
            Select(el);
            layerAnchorObject = row.Object;
            HighlightRows();
        }
        OpenLayerObjectContextMenu(e.position, row.Object);
    }

    private static void OpenLayerCanvasContextMenu(Vector2 mouse, OvCanvas canvas) {
        if (canvas == null || HudElement.IsPackage(canvas)) {
            return;
        }
        var targetCanvas = canvas;
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?> {
            (Tr("HUD_RENAME", "Rename"), null, () => {
                var live = layerRows.FirstOrDefault(r => r.Object == null && ReferenceEquals(r.Canvas, targetCanvas));
                if (live != null) {
                    BeginRenameCanvas(live);
                }
            }, false),
            null,
            (Tr("HUD_ADD_TEXT", "Add text"), null, () => CreateOnCanvas(NewKind.Text, targetCanvas), false),
            (Tr("HUD_ADD_IMAGE", "Add image"), null, () => CreateOnCanvas(NewKind.Image, targetCanvas), false),
        };
        if (!targetCanvas.Config.Enabled.UseFx) {
            bool shown = targetCanvas.Config.Enabled.StaticValue;
            items.Add(null);
            items.Add((shown ? Tr("HUD_HIDE", "Hide") : Tr("HUD_SHOW", "Show"), null, () => SetCanvasHidden(targetCanvas, shown), false));
        }
        items.Add(null);
        items.Add((Tr("HUD_DELETE_CANVAS", "Delete canvas"), null, () => DeleteCanvas(targetCanvas), true));
        ShowContextMenu(mouse, items);
    }

    private static void OpenLayerObjectContextMenu(Vector2 mouse, OvObject targetObj) {
        if (targetObj == null || selection.Count == 0) {
            return;
        }
        bool single = selection.Count == 1;
        var primary = Primary;
        bool anyEditable = selection.Any(s => !s.Locked);
        bool allLocked = selection.All(s => s.UserLocked);
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?>();
        if (single) {
            var capturedObj = targetObj;
            items.Add((Tr("HUD_RENAME", "Rename"), null, () => {
                var live = layerRows.FirstOrDefault(r => ReferenceEquals(r.Object, capturedObj));
                if (live != null) {
                    BeginRename(live);
                }
            }, false));
            if (primary != null && CanEditText(primary)) {
                var capturedPrimary = primary;
                items.Add((Tr("HUD_EDIT_TEXT", "Edit text"), null, () => BeginInlineEdit(capturedPrimary), false));
            }
            items.Add(null);
            if (primary != null && !primary.Locked) {
                items.Add((Tr("HUD_ADD_TEXT_INSIDE", "Add text inside"), null, () => Create(NewKind.Text, Placement.Child, null), false));
                items.Add((Tr("HUD_ADD_IMAGE_INSIDE", "Add image inside"), null, () => Create(NewKind.Image, Placement.Child, null), false));
                items.Add(null);
                items.Add((Tr("HUD_BRING_FRONT", "Bring to front"), "Ctrl+Shift+]", () => MoveToEnd(true), false));
                items.Add((Tr("HUD_BRING_FORWARD", "Bring forward"), "Ctrl+]", () => MoveOrder(1), false));
                items.Add((Tr("HUD_SEND_BACKWARD", "Send backward"), "Ctrl+[", () => MoveOrder(-1), false));
                items.Add((Tr("HUD_SEND_BACK", "Send to back"), "Ctrl+Shift+[", () => MoveToEnd(false), false));
                items.Add(null);
            }
        }
        if (anyEditable) {
            items.Add((Tr("HUD_GROUP", "Group"), "Ctrl+G", GroupSelection, false));
            if (selection.Any(s => !s.Locked && s.Object.Children.Count > 0)) {
                items.Add((Tr("HUD_UNGROUP", "Ungroup"), "Ctrl+Shift+G", UngroupSelection, false));
            }
            items.Add(null);
            items.Add((Tr("HUD_COPY", "Copy"), "Ctrl+C", CopySelection, false));
            items.Add((Tr("HUD_PASTE", "Paste"), "Ctrl+V", () => Paste(false), false));
            items.Add((Tr("HUD_COPY_STYLE", "Copy style"), "Ctrl+Alt+C", CopyStyle, false));
            items.Add((Tr("HUD_PASTE_STYLE", "Paste style"), "Ctrl+Alt+V", PasteStyle, false));
            items.Add((Tr("HUD_DUPLICATE", "Duplicate"), "Ctrl+D", DuplicateSelection, false));
            items.Add(null);
        }
        bool nextLock = !allLocked;
        items.Add((allLocked ? Tr("HUD_UNLOCK", "Unlock") : Tr("HUD_LOCK", "Lock"), "Ctrl+Shift+L", () => {
            var targets = selection.Where(s => s.IsAlive).ToList();
            foreach (var s in targets) {
                SetLocked(s.Canvas, s.Object, nextLock);
            }
            layersDirty = true;
        }, false));
        if (anyEditable) {
            items.Add((Tr("HUD_HIDE", "Hide"), "Ctrl+Shift+H", HideSelection, false));
            items.Add((Tr("HUD_RESET_TRANSFORM", "Reset transform"), null, ResetSelection, false));
            items.Add(null);
            items.Add((Tr("HUD_DELETE", "Delete"), "Del", DeleteSelection, true));
        }
        ShowContextMenu(mouse, items);
    }

    private static void OpenHiddenObjectContextMenu(Vector2 mouse, OvCanvas canvas, OvObject obj) {
        if (canvas == null || obj == null || HudElement.IsPackage(canvas)) {
            return;
        }
        var targetCanvas = canvas;
        var targetObj = obj;
        bool locked = IsUserLocked(targetCanvas, targetObj);
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?> {
            (Tr("HUD_RENAME", "Rename"), null, () => {
                var live = layerRows.FirstOrDefault(r => ReferenceEquals(r.Object, targetObj));
                if (live != null) {
                    BeginRename(live);
                }
            }, false),
        };
        if (!targetObj.Config.Enabled.UseFx) {
            items.Add((Tr("HUD_SHOW", "Show"), null, () => SetHidden([targetObj], false), false));
        }
        items.Add((locked ? Tr("HUD_UNLOCK", "Unlock") : Tr("HUD_LOCK", "Lock"), null, () => {
            SetLocked(targetCanvas, targetObj, !locked);
            layersDirty = true;
        }, false));
        items.Add(null);
        items.Add((Tr("HUD_DELETE", "Delete"), null, () => DeleteObject(targetCanvas, targetObj), true));
        ShowContextMenu(mouse, items);
    }

    private static void StyleRenameField(O5InputField field, LayerRow row) {
        field.Rect.anchorMin = Vector2.zero;
        field.Rect.anchorMax = Vector2.one;
        float left = Mathf.Max(18f, row.TextLeft - 6f);
        field.Rect.offsetMin = new Vector2(left, 1f);
        field.Rect.offsetMax = new Vector2(-50f, -1f);

        var input = field.InputField;
        if (input.textComponent != null) {
            input.textComponent.fontSize = 14f;
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent.extraPadding = false;
        }
        if (field.Placeholder != null) {
            field.Placeholder.fontSize = 14f;
            field.Placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            field.Placeholder.extraPadding = false;
        }
        if (input.textViewport != null) {
            input.textViewport.offsetMin = new Vector2(6f, 1f);
            input.textViewport.offsetMax = new Vector2(-6f, -1f);
        }
        input.caretWidth = 1;

        var bgImg = field.Rect.GetComponent<Image>();
        if (bgImg != null) {
            bgImg.pixelsPerUnitMultiplier = 1.25f;
        }
        var hoverTr = field.Rect.Find("Hover");
        var hoverImg = hoverTr != null ? hoverTr.GetComponent<Image>() : null;
        if (hoverImg != null) {
            hoverImg.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256O32P1024);
            hoverImg.pixelsPerUnitMultiplier = 2.5f;
            hoverImg.color = O5KitAdapters.Ctx.Theme.ControlOutlineHover;
        }
    }

    private static void BeginRename(LayerRow row) {
        if (row?.Rect == null) {
            return;
        }
        if (row.Object == null) {
            BeginRenameCanvas(row);
            return;
        }
        if (renameField != null) {
            var prev = renameField;
            renameField = null;
            prev.Dispose();
        }
        var obj = row.Object;
        renameField = O5Factory.Input(O5KitAdapters.Ctx, row.Rect, "plus_hud_rename", new O5InputOptions {
            Value = obj.Config.Name.Value,
            OnEndEdit = value => {
                string before = obj.Config.Name.Value;
                string after = string.IsNullOrWhiteSpace(value) ? before : value.Trim();
                var field = renameField;
                renameField = null;
                MainThread.Enqueue(() => field?.Dispose());
                if (after == before) {
                    layersDirty = true;
                    return;
                }
                void Set(string name) {
                    obj.Config.Name.Value = name;
                    obj.ApplyConfig();
                    Save();
                    layersDirty = true;
                    RefreshPanel();
                }
                Set(after);
                undo.Push(new ActionCommand(() => Set(before), () => Set(after)));
            },
        });
        StyleRenameField(renameField, row);
        renameField.InputField.ActivateInputField();
        renameField.InputField.Select();
    }

    // ---- canvases ----

    private static void AddCanvas() {
        var canvas = NewCanvas();
        foldedCanvases.Remove(canvas);
        // Undo takes it away again only while it's still empty.
        undo.Push(new ActionCommand(() => {
            if (canvas.OvObjects.Count == 0) {
                OverlayCore.DeleteOvCanvas(canvas);
                layersDirty = true;
            }
        }, () => { }));
        Save();
        layersDirty = true;
    }

    // Small X at the row's end; red on hover.
    private static void DeleteToggle(LayerRow row) {
        var hit = NewRect("Delete", row.Rect);
        hit.anchorMin = hit.anchorMax = new Vector2(1f, 0.5f);
        hit.anchoredPosition = new Vector2(-12f, 0f);
        hit.sizeDelta = new Vector2(20f, RowHeight);
        var back = hit.gameObject.AddComponent<Image>();
        back.color = Color.clear;
        back.raycastTarget = true;
        var mark = NewImage("X", hit, new Color(1f, 1f, 1f, 0.3f), MainCore.Spr.Get(UISprite.X128));
        mark.rectTransform.sizeDelta = new Vector2(10f, 10f);
        UnityUtils.AddEvents(hit.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => mark.color = UiStyle.Danger),
            (EventTriggerType.PointerExit, _ => mark.color = new Color(1f, 1f, 1f, 0.3f)),
            (EventTriggerType.PointerClick, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe) {
                    if (pe.button == PointerEventData.InputButton.Right) {
                        OnRowClick(row, pe);
                        return;
                    }
                    if (pe.button != PointerEventData.InputButton.Left) {
                        return;
                    }
                }
                DeleteCanvas(row.Canvas);
            }));
        hit.AddToolTip(O5KitAdapters.Ctx, Tr("HUD_DELETE_CANVAS", "Delete canvas"));
    }

    public static void DeleteCanvas(OvCanvas canvas) {
        if (canvas == null || HudElement.IsPackage(canvas)) {
            return;
        }
        O5KitAdapters.Ctx.Tooltip.Hide();
        // Kept as JSON so undo can make it again, contents and all.
        var saved = Newtonsoft.Json.Linq.JToken.Parse(canvas.Serialize().ToString());
        int index = OverlayCore.Canvases.IndexOf(canvas);
        var current = canvas;
        void Delete() {
            selection.RemoveAll(e => ReferenceEquals(e.Canvas, current));
            OverlayCore.DeleteOvCanvas(current);
            Recollect();
            RefreshPanel();
            layersDirty = true;
        }
        void Restore() {
            current = OverlayCore.RestoreCanvas(saved.DeepClone(), index) ?? current;
            Recollect();
            RefreshPanel();
            layersDirty = true;
        }
        Delete();
        undo.Push(new ActionCommand(Restore, Delete));
    }

    public static void SetCanvasHidden(OvCanvas canvas, bool hidden) {
        if (canvas?.Config == null || canvas.Config.Enabled.UseFx) {
            return;
        }
        void Set(bool hide) {
            canvas.Config.Enabled.StaticValue = !hide;
            canvas.ApplyConfig();
            if (hide) {
                selection.RemoveAll(e => ReferenceEquals(e.Canvas, canvas));
            }
            Save();
            Recollect();
            RefreshPanel();
            layersDirty = true;
        }
        Set(hidden);
        undo.Push(new ActionCommand(() => Set(!hidden), () => Set(hidden)));
    }

    private static void BeginRenameCanvas(LayerRow row) {
        if (row?.Rect == null) {
            return;
        }
        if (renameField != null) {
            var prev = renameField;
            renameField = null;
            prev.Dispose();
        }
        var canvas = row.Canvas;
        renameField = O5Factory.Input(O5KitAdapters.Ctx, row.Rect, "plus_hud_rename", new O5InputOptions {
            Value = canvas.Config.Name.Value,
            OnEndEdit = value => {
                string before = canvas.Config.Name.Value;
                string after = string.IsNullOrWhiteSpace(value) ? before : value.Trim();
                var field = renameField;
                renameField = null;
                MainThread.Enqueue(() => field?.Dispose());
                if (after == before) {
                    layersDirty = true;
                    return;
                }
                void Set(string from, string to) {
                    canvas.Config.Name.Value = to;
                    canvas.ApplyConfig();
                    RenameLocks(from, to);
                    // The main canvas setting follows its canvas.
                    if (MainCore.Conf.HudMainCanvas.Value == from) {
                        MainCore.Conf.HudMainCanvas.Value = to;
                        MainCore.ConfMgr.RequestSave();
                    }
                    Save();
                    layersDirty = true;
                    RefreshPanel();
                }
                Set(before, after);
                undo.Push(new ActionCommand(() => Set(after, before), () => Set(before, after)));
            },
        });
        StyleRenameField(renameField, row);
        renameField.InputField.ActivateInputField();
        renameField.InputField.Select();
    }

    // Locks are stored as "canvas name/object path".
    private static void RenameLocks(string from, string to) {
        var list = MainCore.Conf.HudLocked.Value;
        if (list == null || list.Count == 0) {
            return;
        }
        string prefix = from + "/";
        bool changed = false;
        for (int i = 0; i < list.Count; i++) {
            if (list[i].StartsWith(prefix, StringComparison.Ordinal)) {
                list[i] = to + "/" + list[i].Substring(prefix.Length);
                changed = true;
            }
        }
        if (changed) {
            MainCore.Conf.HudLocked.Value = list;
            MainCore.ConfMgr.RequestSave();
        }
    }

    // ---- drag to move ----

    private static void OnRowDrag(PointerEventData e) {
        if (dragRow == null || e == null) {
            return;
        }
        dropTarget = null;
        foreach (var row in layerRows) {
            if (row == dragRow || !RectTransformUtility.RectangleContainsScreenPoint(row.Rect, e.position, null)) {
                continue;
            }
            // Can't drop into itself or its own children.
            if (row.Object != null && (ReferenceEquals(row.Object, dragRow.Object) || IsAncestor(dragRow.Object, row.Object))) {
                break;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(row.Rect, e.position, null, out var local);
            float t = Mathf.InverseLerp(row.Rect.rect.yMax, row.Rect.rect.yMin, local.y); // 0 top .. 1 bottom
            int where = row.Object == null ? 0 : t < 0.3f ? -1 : t > 0.7f ? 1 : 0;
            dropTarget = (row, where);
            break;
        }
        if (dropTarget is not { } d) {
            dropMarker.rectTransform.SetParent(layersPanel, false);
            dropMarker.gameObject.SetActive(false);
            return;
        }
        // A line above / below the row, or a box over the row it goes into.
        var m = dropMarker.rectTransform;
        m.SetParent(d.Row.Rect, false);
        if (d.Where == 0) {
            dropMarker.color = new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.25f);
            m.anchorMin = Vector2.zero;
            m.anchorMax = Vector2.one;
            m.offsetMin = m.offsetMax = Vector2.zero;
        } else {
            dropMarker.color = SelectedColor;
            float y = d.Where < 0 ? 1f : 0f;
            m.anchorMin = new Vector2(0f, y);
            m.anchorMax = new Vector2(1f, y);
            m.pivot = new Vector2(0.5f, 0.5f);
            m.anchoredPosition = Vector2.zero;
            m.sizeDelta = new Vector2(0f, 2f);
        }
        dropMarker.gameObject.SetActive(true);
        m.SetAsLastSibling();
    }
    private static void OnRowDrop() {
        var source = dragRow;
        var target = dropTarget;
        dragRow = null;
        dropTarget = null;
        // Back to the panel before the rows get rebuilt (and destroyed).
        dropMarker.rectTransform.SetParent(layersPanel, false);
        dropMarker.gameObject.SetActive(false);
        if (source?.Object == null || target is not { } d) {
            return;
        }
        var obj = source.Object;
        var from = ObjectSlot(source.Canvas, obj);
        Slot to;
        if (d.Where == 0) {
            to = new Slot(d.Row.Canvas, d.Row.Object, -1); // inside (canvas row: its top level)
        } else {
            var t = ObjectSlot(d.Row.Canvas, d.Row.Object);
            to = new Slot(t.Canvas, t.Parent, t.Index + (d.Where > 0 ? 1 : 0));
        }
        MoveObject(obj, from, to);
    }

    private static Slot ObjectSlot(OvCanvas canvas, OvObject obj) => obj.Parent != null
        ? new Slot(canvas, obj.Parent, obj.Parent.Children.IndexOf(obj))
        : new Slot(canvas, null, canvas.OvObjects.IndexOf(obj));

    /// <summary>Moves an object to another place in the tree, keeping where it is on screen.</summary>
    private static void MoveObject(OvObject obj, Slot from, Slot to) {
        // Leaving a list shifts the later siblings up by one.
        if (SameParent(from, to) && to.Index > from.Index) {
            to = new Slot(to.Canvas, to.Parent, to.Index - 1);
        }
        if (SameParent(from, to) && to.Index == from.Index) {
            return;
        }
        var cfg = obj.Config.RectTransformConfig;
        Vector2 beforePos = cfg?.AnchoredPosition.StaticValue ?? Vector2.zero;
        Slot landed = to;

        void Move(Slot a, Slot b, Vector2? restore) {
            Vector2 world = obj.RectTransform != null ? (Vector2)obj.RectTransform.position : Vector2.zero;
            DetachFrom(obj, a);
            AttachAt(obj, b);
            if (restore.HasValue && cfg != null) {
                cfg.AnchoredPosition.StaticValue = restore.Value;
                cfg.ToUnity(obj.GameObject);
            } else if (obj.RectTransform != null) {
                PlaceAt(obj, world);
            }
            Save();
            Recollect();
            layersDirty = true;
        }

        Move(from, to, null);
        int index = to.Parent != null ? to.Parent.Children.IndexOf(obj) : to.Canvas.OvObjects.IndexOf(obj);
        landed = new Slot(to.Canvas, to.Parent, index);
        undo.Push(new ActionCommand(() => Move(landed, from, beforePos), () => Move(from, landed, null)));
        SelectObjects([obj]);
    }
}
