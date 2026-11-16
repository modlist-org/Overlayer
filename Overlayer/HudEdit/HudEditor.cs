using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// osu!lazer skin editor style: manipulate HUD objects directly on the game screen,
// fine-tune in the side panel. Lives inside Overlayer's UI canvas so everything that
// treats "Overlayer UI open" as "block game input" keeps working while editing.
internal static partial class HudEditor {
    private enum DragMode { None, Move, Scale, Rotate, Marquee, Resize }

    private const float HandleHitPx = 12f;
    private const float HandleInsidePx = 6f;
    private const int RecollectFrames = 30;

    public static bool IsOpen { get; private set; }

    private static bool uiWasOpen;
    private static List<HudElement> elements = [];
    private static int collectedFrame = -1000;
    private static HudElement hovered;
    private static readonly List<HudElement> selection = [];

    /// <summary>Last clicked element; handles, panel and "relative to selection" placements use it.</summary>
    private static HudElement Primary => selection.Count > 0 ? selection[selection.Count - 1] : null;

    private static DragMode drag;
    private static List<(HudElement Element, HudElement.State Start)> dragStart = [];
    private static Vector2 dragMouseStart;
    private static bool dragChanged;

    private static readonly UndoStack undo = new();
    private static readonly Vector3[] corners = new Vector3[4];
    private static readonly List<float> guideX = [];
    private static readonly List<float> guideY = [];

    private const string ShortcutId = "overlayer.hud_editor";

    public static UI.Utility.KeyBind Key => UI.Utility.KeyBind.Parse(MainCore.Conf.HudEditorKey.Value);

    public static void RegisterShortcut() => Key.Register(ShortcutId, Toggle);

    public static void UnregisterShortcut() => UI.Utility.KeyBind.Unregister(ShortcutId);

    public static void Toggle() {
        if (IsOpen) {
            Close();
        } else {
            Open();
        }
    }

    public static void Open() {
        if (IsOpen || !Core.MainCore.IsModEnabled || UICore.CanvasObj == null) {
            return;
        }
        if (ModuleAPI.PlaybackState.IsPlaying && !ModuleAPI.PlaybackState.IsPaused && !MainCore.Conf.HudEditorInPlay.Value) {
            MainCore.Log.Msg("[HudEditor] Pause first (or allow it in Settings) to edit the HUD while playing.");
            return;
        }
        uiWasOpen = UICore.CanvasObj.activeSelf;
        UICore.Open(true);
        if (UICore.Panel != null) {
            UICore.Panel.gameObject.SetActive(false);
        }
        BuildView();
        IsOpen = true;
        collectedFrame = -1000;
        selection.Clear();
        hovered = null;
        drag = DragMode.None;
        undo.Clear();
        RefreshPanel();
    }

    public static void Close() {
        if (!IsOpen) {
            return;
        }
        IsOpen = false;
        EndDrag();
        CommitPanelEdit();
        FlushSave();
        DestroyView();
        selection.Clear();
        hovered = null;
        elements.Clear();
        undo.Clear();
        pendingPlace.Clear();
        EmptyTrash();
        if (UICore.Panel != null) {
            UICore.Panel.gameObject.SetActive(true);
        }
        if (!uiWasOpen) {
            UICore.Close(true);
        }
    }

    public static void Tick() {
        if (!IsOpen) {
            return;
        }
        // Overlayer's own toggle shortcut (or anything else) hid its canvas: leave cleanly.
        if (UICore.CanvasObj == null || !UICore.CanvasObj.activeSelf || root == null) {
            Close();
            return;
        }

        if (pendingPlace.Count > 0) {
            FlushPendingPlace();
        }
        if (Time.frameCount - collectedFrame >= RecollectFrames) {
            Recollect();
        }
        if (selection.RemoveAll(e => !e.IsAlive) > 0) {
            RefreshPanel();
        }

        HandleKeys();
        if (!IsOpen) {
            return;
        }
        HandleMouse();
        if (saveDue > 0f && Time.realtimeSinceStartup >= saveDue) {
            FlushSave();
        }
        Render();
    }

    private static void Recollect() {
        collectedFrame = Time.frameCount;
        var fresh = HudElement.Collect();
        // Keep the same wrapper for the same object so undo entries and selection survive.
        for (int i = 0; i < fresh.Count; i++) {
            foreach (var old in elements) {
                if (ReferenceEquals(old.Object, fresh[i].Object) && old.LockReason == fresh[i].LockReason) {
                    fresh[i] = old;
                    break;
                }
            }
        }
        elements = fresh;
        if (selection.RemoveAll(e => !elements.Contains(e)) > 0) {
            RefreshPanel();
        }
    }

    // ---- selection ----

    private static void SetSelection(IEnumerable<HudElement> items) {
        CommitPanelEdit();
        selection.Clear();
        selection.AddRange(items.Where(e => e != null).Distinct());
        RefreshPanel();
    }

    private static void Select(HudElement element) => SetSelection(element == null ? [] : [element]);

    private static void ToggleSelect(HudElement element) {
        if (element == null) {
            return;
        }
        CommitPanelEdit();
        if (!selection.Remove(element)) {
            selection.Add(element);
        }
        RefreshPanel();
    }

    private static bool TypingInField() {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var field = go != null ? go.GetComponent<TMP_InputField>() : null;
        return field != null && field.isFocused;
    }

    private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    private static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

    // ---- keyboard ----

    private static void HandleKeys() {
        if (TypingInField()) {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (ContextMenuOpen) {
                CloseContextMenu();
            } else if (drag != DragMode.None) {
                CancelDrag();
            } else if (selection.Count > 0) {
                Select(null);
            } else {
                Close();
            }
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.Z)) {
            AfterHistory(Shift ? undo.Redo() : undo.Undo());
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.Y)) {
            AfterHistory(undo.Redo());
            return;
        }
        if (drag != DragMode.None) {
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.D)) {
            DuplicateSelection();
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.C)) {
            if (Alt) {
                CopyStyle();
            } else {
                CopySelection();
            }
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.X)) {
            CopySelection();
            DeleteSelection();
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.V)) {
            if (Alt) {
                PasteStyle();
            } else {
                Paste(inPlace: Shift);
            }
            return;
        }
        if (Ctrl && (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.LeftBracket))) {
            bool front = Input.GetKeyDown(KeyCode.RightBracket);
            if (Shift) {
                MoveToEnd(front);
            } else {
                MoveOrder(front ? 1 : -1);
            }
            return;
        }
        if (Ctrl && Shift && Input.GetKeyDown(KeyCode.L)) {
            ToggleLockSelection();
            return;
        }
        if (Ctrl && Shift && Input.GetKeyDown(KeyCode.H)) {
            HideSelection();
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.A)) {
            SetSelection(elements.Where(e => e.IsAlive && !e.UserLocked && e.Depth == 0));
            return;
        }
        if (Ctrl && Input.GetKeyDown(KeyCode.G)) {
            if (Shift) {
                UngroupSelection();
            } else {
                GroupSelection();
            }
            return;
        }
        if (Input.GetKeyDown(KeyCode.Delete)) {
            DeleteSelection();
            return;
        }

        Vector2 nudge = Vector2.zero;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            nudge.x -= 1f;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            nudge.x += 1f;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow)) {
            nudge.y += 1f;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow)) {
            nudge.y -= 1f;
        }
        if (nudge == Vector2.zero) {
            return;
        }
        var changes = new List<(HudElement, HudElement.State, HudElement.State)>();
        foreach (var el in selection) {
            if (!el.CanMove) {
                continue;
            }
            var before = el.Capture();
            var after = before;
            after.Position += el.ScreenToParentDelta(nudge * (Shift ? 10f : 1f) * UiScale);
            el.Apply(after);
            changes.Add((el, before, after));
        }
        Commit(changes);
    }

    private static void AfterHistory(bool changed) {
        if (!changed) {
            return;
        }
        Save();
        Recollect();
        RefreshPanel();
    }

    // ---- mouse ----

    private static Vector2 lastMouse;

    private static void HandleMouse() {
        Vector2 mouse = Input.mousePosition;
        lastMouse = mouse;

        if (drag == DragMode.Marquee) {
            if (Input.GetMouseButton(0)) {
                UpdateMarquee(mouse);
            } else {
                EndMarquee();
            }
            return;
        }
        if (drag != DragMode.None) {
            if (Input.GetMouseButton(0)) {
                UpdateDrag(mouse);
            } else {
                EndDrag();
            }
            return;
        }

        bool overUi = PointerOverEditorUi(mouse);
        var deep = overUi ? null : HitTest(mouse);
        // Hover shows what a click would pick.
        hovered = ResolvePick(deep, Ctrl);
        UpdateMeasure(overUi);
        if (overUi) {
            return;
        }

        if (Input.GetMouseButtonDown(1)) {
            if (hovered != null && !selection.Contains(hovered)) {
                Select(hovered);
            }
            OpenContextMenu(mouse, hovered);
            return;
        }
        if (!Input.GetMouseButtonDown(0)) {
            return;
        }
        if (ContextMenuOpen) {
            CloseContextMenu();
            return;
        }

        if (selection.Count == 1) {
            var handle = HitHandle(selection[0], mouse);
            if (handle != DragMode.None) {
                BeginDrag(handle, mouse);
                return;
            }
        }
        var picked = DiveOnDoubleClick(hovered, deep);
        if (pendingInlineEdit != null) {
            var target = pendingInlineEdit;
            pendingInlineEdit = null;
            BeginInlineEdit(target);
            return;
        }
        if (picked == null) {
            BeginMarquee(mouse);
            return;
        }
        if (Shift) {
            ToggleSelect(picked);
            if (selection.Contains(picked)) {
                BeginDrag(DragMode.Move, mouse);
            }
            return;
        }
        // Grabbing a member of a multi-selection drags the whole selection.
        if (!selection.Contains(picked)) {
            Select(picked);
        } else if (!ReferenceEquals(Primary, picked)) {
            selection.Remove(picked);
            selection.Add(picked);
            RefreshPanel();
        }
        BeginDrag(DragMode.Move, mouse);
    }

    // Topmost first: later canvases and later/deeper objects render above earlier ones.
    private static HudElement HitTest(Vector2 mouse) {
        for (int i = elements.Count - 1; i >= 0; i--) {
            var el = elements[i];
            if (!el.IsAlive || el.UserLocked || el.IsGroup) {
                continue;
            }
            el.GetScreenCorners(corners);
            if (InQuad(mouse, corners)) {
                return el;
            }
        }
        return null;
    }

    private static bool InQuad(Vector2 p, Vector3[] q) {
        bool? sign = null;
        for (int i = 0; i < 4; i++) {
            Vector2 a = q[i], b = q[(i + 1) % 4];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (Mathf.Abs(cross) < 1e-4f) {
                continue;
            }
            bool s = cross > 0f;
            if (sign == null) {
                sign = s;
            } else if (sign != s) {
                return false;
            }
        }
        return sign != null;
    }

    private static DragMode HitHandle(HudElement el, Vector2 mouse) {
        float r = HandleHitPx * UiScale;
        el.GetScreenCorners(corners);
        if (el.CanRotate && (mouse - RotateHandleScreen(corners)).sqrMagnitude <= r * r) {
            return DragMode.Rotate;
        }
        if (el.CanScale) {
            // Inside the box only the handle square itself scales; anything further in moves,
            // otherwise grabbing near a corner to move it scaled it instead.
            float inner = HandleInsidePx * UiScale;
            bool inside = InQuad(mouse, corners);
            for (int i = 0; i < 4; i++) {
                float d2 = (mouse - (Vector2)corners[i]).sqrMagnitude;
                if (d2 <= (inside ? inner * inner : r * r)) {
                    scaleCorner = i;
                    return DragMode.Scale;
                }
            }
        }
        // Anywhere along an edge (of the real box) changes width / height.
        el.Rect.GetWorldCorners(corners);
        float edgeR = EdgeHitPx * UiScale;
        for (int i = 0; i < 4; i++) {
            bool horizontal = i % 2 == 0; // 0 left, 2 right change the width; 1 top, 3 bottom the height
            if (!(horizontal ? el.CanResizeX : el.CanResizeY)) {
                continue;
            }
            Vector2 a = corners[i], b = corners[(i + 1) % 4];
            if (DistanceToSegment(mouse, a, b) <= edgeR) {
                resizeEdge = i;
                return DragMode.Resize;
            }
        }
        return DragMode.None;
    }

    private const float EdgeHitPx = 5f;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    private static Vector2 RotateHandleScreen(Vector3[] c) {
        Vector2 topMid = (c[1] + c[2]) * 0.5f;
        Vector2 up = ((Vector2)(c[1] - c[0])).normalized;
        if (up == Vector2.zero) {
            up = Vector2.up;
        }
        return topMid + up * (RotateHandleOffset * UiScale);
    }

    // ---- drag ----

    private static void BeginDrag(DragMode mode, Vector2 mouse) {
        var targets = mode == DragMode.Move ? selection.Where(e => e.CanMove).ToList() : [Primary];
        if (targets.Count == 0 || targets[0] == null) {
            return;
        }
        CommitPanelEdit();
        drag = mode;
        dragStart = targets.Select(e => (e, e.Capture())).ToList();
        dragMouseStart = mouse;
        dragChanged = false;
        if (mode == DragMode.Scale) {
            targets[0].GetScreenCorners(corners);
            scaleCornerStart = corners[scaleCorner];
            scaleOppositeStart = corners[(scaleCorner + 2) % 4];
            scaleCenterStart = (corners[0] + corners[2]) * 0.5f;
        }
        if (mode == DragMode.Resize) {
            targets[0].Rect.GetWorldCorners(corners);
            resizeEdgeStart = EdgeMid(corners, resizeEdge);
            resizeOppositeStart = EdgeMid(corners, (resizeEdge + 2) % 4);
            resizeCenterStart = (corners[0] + corners[2]) * 0.5f;
        }
    }

    // Edge resizing: 0 left, 1 top, 2 right, 3 bottom (between corner i and i + 1).
    private static int resizeEdge;
    private static Vector2 resizeEdgeStart, resizeOppositeStart, resizeCenterStart;

    private static Vector2 EdgeMid(Vector3[] c, int edge) => (c[edge] + c[(edge + 1) % 4]) * 0.5f;

    // Corner scaling: which corner was grabbed (0 BL, 1 TL, 2 TR, 3 BR) and where things started.
    private static int scaleCorner;
    private static Vector2 scaleCornerStart, scaleOppositeStart, scaleCenterStart;

    private static void UpdateDrag(Vector2 mouse) {
        if (!dragChanged && (mouse - dragMouseStart).sqrMagnitude < 4f) {
            return;
        }
        dragChanged = true;
        guideX.Clear();
        guideY.Clear();
        measures.Clear();

        switch (drag) {
            case DragMode.Move: {
                Vector2 screenDelta = mouse - dragMouseStart;
                ApplyMove(screenDelta);
                if (!Alt && MainCore.Conf.HudSnap.Value) {
                    Vector2 snap = SnapOffset(dragStart.Select(d => d.Element).ToList());
                    if (snap != Vector2.zero) {
                        ApplyMove(screenDelta + snap);
                    }
                }
                break;
            }
            case DragMode.Scale: {
                // The grabbed corner follows the pointer and the opposite corner stays put
                // (Alt: the middle stays put). Measured along the box's diagonal, so dragging
                // outward grows and inward shrinks whatever the pivot or rotation is.
                var (el, start) = dragStart[0];
                Vector2 fixedPoint = Alt ? scaleCenterStart : scaleOppositeStart;
                Vector2 diagonal = scaleCornerStart - fixedPoint;
                if (diagonal.sqrMagnitude < 1f) {
                    break;
                }
                float ratio = Vector2.Dot(mouse - fixedPoint, diagonal) / diagonal.sqrMagnitude;
                if (Shift) {
                    ratio = Mathf.Round(ratio * start.Scale.x / 0.05f) * 0.05f / Mathf.Max(start.Scale.x, 1e-4f);
                }
                ratio = Mathf.Max(ratio, 0.01f);
                var state = start;
                state.Scale = new Vector3(start.Scale.x * ratio, start.Scale.y * ratio, start.Scale.z);
                el.Apply(state);
                // Scaling happens around the pivot; shift back so the fixed point didn't move.
                el.GetScreenCorners(corners);
                Vector2 now = Alt ? (Vector2)(corners[0] + corners[2]) * 0.5f : (Vector2)corners[(scaleCorner + 2) % 4];
                if (el.CanMove) {
                    state.Position = start.Position + el.ScreenToParentDelta(fixedPoint - now);
                    el.Apply(state);
                }
                break;
            }
            case DragMode.Resize: {
                // The grabbed edge follows the pointer, the opposite edge stays (Alt: the middle).
                var (el, start) = dragStart[0];
                Vector2 fixedPoint = Alt ? resizeCenterStart : resizeOppositeStart;
                Vector2 outward = resizeEdgeStart - fixedPoint;
                if (outward.sqrMagnitude < 1e-4f) {
                    break;
                }
                float startLen = outward.magnitude;
                float len = Vector2.Dot(mouse - fixedPoint, outward / startLen);
                bool widthAxis = resizeEdge % 2 == 0;
                float startSize = widthAxis ? start.Size.x : start.Size.y;
                // Screen pixels per unit of size along this axis (scale, canvas scale).
                float perUnit = startLen / Mathf.Max(Alt ? startSize * 0.5f : startSize, 1e-4f);
                float size = Alt ? len * 2f / perUnit : len / perUnit;
                if (Shift) {
                    size = Mathf.Round(size / 10f) * 10f;
                }
                size = Mathf.Max(size, 1f);
                var state = start;
                state.Size = widthAxis ? new Vector2(size, start.Size.y) : new Vector2(start.Size.x, size);
                el.Apply(state);
                el.Rect.GetWorldCorners(corners);
                Vector2 now = Alt ? (Vector2)(corners[0] + corners[2]) * 0.5f : EdgeMid(corners, (resizeEdge + 2) % 4);
                if (el.CanMove) {
                    state.Position = start.Position + el.ScreenToParentDelta(fixedPoint - now);
                    el.Apply(state);
                }
                break;
            }
            case DragMode.Rotate: {
                var (el, start) = dragStart[0];
                Vector2 pivot = el.ScreenPivot;
                float a0 = Mathf.Atan2(dragMouseStart.y - pivot.y, dragMouseStart.x - pivot.x);
                float a1 = Mathf.Atan2(mouse.y - pivot.y, mouse.x - pivot.x);
                float rot = start.Rotation + Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg);
                if (Shift) {
                    rot = Mathf.Round(rot / 15f) * 15f;
                }
                var state = start;
                state.Rotation = Mathf.Repeat(rot + 180f, 360f) - 180f;
                el.Apply(state);
                break;
            }
        }
        SyncPanelValues();
    }

    private static void ApplyMove(Vector2 screenDelta) {
        foreach (var (el, start) in dragStart) {
            var state = start;
            state.Position = start.Position + el.ScreenToParentDelta(screenDelta);
            el.Apply(state);
        }
    }

    // Screen-space snapping of the dragged group's box to screen edges/center and other HUD boxes.
    // Returns an extra screen offset.
    private static Vector2 SnapOffset(List<HudElement> moving) {
        if (!UnionBounds(moving, out float minX, out float maxX, out float minY, out float maxY)) {
            return Vector2.zero;
        }
        float threshold = MainCore.Conf.HudSnapDistance.Value * UiScale;

        float bestX = threshold, dx = 0f, lineX = float.NaN;
        float bestY = threshold, dy = 0f, lineY = float.NaN;
        void TryX(float line) {
            foreach (float v in new[] { minX, (minX + maxX) * 0.5f, maxX }) {
                float d = line - v;
                if (Mathf.Abs(d) < bestX) { bestX = Mathf.Abs(d); dx = d; lineX = line; }
            }
        }
        void TryY(float line) {
            foreach (float v in new[] { minY, (minY + maxY) * 0.5f, maxY }) {
                float d = line - v;
                if (Mathf.Abs(d) < bestY) { bestY = Mathf.Abs(d); dy = d; lineY = line; }
            }
        }

        TryX(0f); TryX(Screen.width * 0.5f); TryX(Screen.width);
        TryY(0f); TryY(Screen.height * 0.5f); TryY(Screen.height);
        var guideXs = new List<float>();
        var guideYs = new List<float>();
        GuideLines(guideXs, guideYs);
        foreach (float x in guideXs) {
            TryX(x);
        }
        foreach (float y in guideYs) {
            TryY(y);
        }
        var self = new Box(minX, maxX, minY, maxY);
        var others = new List<Box>();
        var other = new Vector3[4];
        foreach (var o in elements) {
            if (!o.IsAlive || moving.Contains(o) || moving.Any(m => IsAncestor(m.Object, o.Object))) {
                continue;
            }
            o.GetScreenCorners(other);
            Bounds(other, out float oMinX, out float oMaxX, out float oMinY, out float oMaxY);
            TryX(oMinX); TryX((oMinX + oMaxX) * 0.5f); TryX(oMaxX);
            TryY(oMinY); TryY((oMinY + oMaxY) * 0.5f); TryY(oMaxY);
            var box = new Box(oMinX, oMaxX, oMinY, oMaxY);
            // Boxes around the moving one (its parents, backgrounds) aren't neighbours to space against.
            if (!box.Contains(self)) {
                others.Add(box);
            }
        }

        // Equal spacing with the neighbours wins when it's nearer than a line.
        float? ex = EqualSpacing(self, others, true, threshold);
        if (ex.HasValue && (float.IsNaN(lineX) || Mathf.Abs(ex.Value) < Mathf.Abs(dx))) {
            dx = ex.Value;
            lineX = float.NaN;
        }
        float? ey = EqualSpacing(self, others, false, threshold);
        if (ey.HasValue && (float.IsNaN(lineY) || Mathf.Abs(ey.Value) < Mathf.Abs(dy))) {
            dy = ey.Value;
            lineY = float.NaN;
        }

        // Grid on: whatever didn't snap to an object or the screen lands on the nearest grid
        // line (edge or middle, whichever is closest). The grid runs through the screen centre.
        if (MainCore.Conf.HudGrid.Value) {
            float s = GridSpacing;
            float GridDelta(float a, float b, float centre) {
                float best = float.MaxValue;
                foreach (float v in new[] { a, (a + b) * 0.5f, b }) {
                    float d = centre + Mathf.Round((v - centre) / s) * s - v;
                    if (Mathf.Abs(d) < Mathf.Abs(best)) {
                        best = d;
                    }
                }
                return best;
            }
            if (float.IsNaN(lineX) && !ex.HasValue) {
                dx = GridDelta(minX, maxX, Screen.width * 0.5f);
            }
            if (float.IsNaN(lineY) && !ey.HasValue) {
                dy = GridDelta(minY, maxY, Screen.height * 0.5f);
            }
        }

        if (!float.IsNaN(lineX)) {
            guideX.Add(lineX);
        }
        if (!float.IsNaN(lineY)) {
            guideY.Add(lineY);
        }
        measures.Clear();
        AddSpacingGuides(self.Shift(dx, dy), others, UnitsPerPixel(moving.FirstOrDefault()));
        return new Vector2(dx, dy);
    }

    private static bool UnionBounds(IEnumerable<HudElement> items, out float minX, out float maxX, out float minY, out float maxY) {
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        bool any = false;
        var c = new Vector3[4];
        foreach (var el in items) {
            if (el == null || !el.IsAlive) {
                continue;
            }
            el.GetScreenCorners(c);
            Bounds(c, out float a, out float b, out float d, out float e);
            minX = Mathf.Min(minX, a); maxX = Mathf.Max(maxX, b);
            minY = Mathf.Min(minY, d); maxY = Mathf.Max(maxY, e);
            any = true;
        }
        return any;
    }

    private static void Bounds(Vector3[] c, out float minX, out float maxX, out float minY, out float maxY) {
        minX = maxX = c[0].x;
        minY = maxY = c[0].y;
        for (int i = 1; i < 4; i++) {
            minX = Mathf.Min(minX, c[i].x);
            maxX = Mathf.Max(maxX, c[i].x);
            minY = Mathf.Min(minY, c[i].y);
            maxY = Mathf.Max(maxY, c[i].y);
        }
    }

    private static void EndDrag() {
        if (drag == DragMode.None) {
            return;
        }
        if (drag == DragMode.Marquee) {
            drag = DragMode.None;
            return;
        }
        drag = DragMode.None;
        guideX.Clear();
        guideY.Clear();
        measures.Clear();
        if (!dragChanged) {
            return;
        }
        Commit(dragStart.Select(d => (d.Element, d.Start, d.Element.Capture())).ToList());
    }

    private static void CancelDrag() {
        if (drag == DragMode.None) {
            return;
        }
        if (drag == DragMode.Marquee) {
            drag = DragMode.None;
            SetSelection(marqueeBase.ToList());
            return;
        }
        drag = DragMode.None;
        guideX.Clear();
        guideY.Clear();
        measures.Clear();
        foreach (var (el, start) in dragStart) {
            el.Apply(start);
        }
        SyncPanelValues();
    }

    // ---- align / distribute (screen space, by visible boxes) ----

    internal enum AlignMode { Left, CenterX, Right, Top, CenterY, Bottom, DistributeX, DistributeY }

    internal static void Align(AlignMode mode) {
        var items = selection.Where(e => e.IsAlive && e.CanMove).ToList();
        if (items.Count < 2) {
            return;
        }
        if (!UnionBounds(items, out float minX, out float maxX, out float minY, out float maxY)) {
            return;
        }
        var boxes = items.Select(e => {
            var c = new Vector3[4];
            e.GetScreenCorners(c);
            Bounds(c, out float a, out float b, out float d, out float f);
            return (El: e, MinX: a, MaxX: b, MinY: d, MaxY: f);
        }).ToList();

        var changes = new List<(HudElement, HudElement.State, HudElement.State)>();
        void Shift(HudElement el, Vector2 screenDelta) {
            if (screenDelta.sqrMagnitude < 1e-6f) {
                return;
            }
            var before = el.Capture();
            var after = before;
            after.Position += el.ScreenToParentDelta(screenDelta);
            el.Apply(after);
            changes.Add((el, before, after));
        }

        switch (mode) {
            case AlignMode.Left: foreach (var b in boxes) Shift(b.El, new Vector2(minX - b.MinX, 0f)); break;
            case AlignMode.Right: foreach (var b in boxes) Shift(b.El, new Vector2(maxX - b.MaxX, 0f)); break;
            case AlignMode.CenterX: foreach (var b in boxes) Shift(b.El, new Vector2((minX + maxX - b.MinX - b.MaxX) * 0.5f, 0f)); break;
            case AlignMode.Bottom: foreach (var b in boxes) Shift(b.El, new Vector2(0f, minY - b.MinY)); break;
            case AlignMode.Top: foreach (var b in boxes) Shift(b.El, new Vector2(0f, maxY - b.MaxY)); break;
            case AlignMode.CenterY: foreach (var b in boxes) Shift(b.El, new Vector2(0f, (minY + maxY - b.MinY - b.MaxY) * 0.5f)); break;
            case AlignMode.DistributeX:
            case AlignMode.DistributeY: {
                if (boxes.Count < 3) {
                    return;
                }
                bool x = mode == AlignMode.DistributeX;
                var sorted = boxes.OrderBy(b => x ? b.MinX : b.MinY).ToList();
                float total = sorted.Sum(b => x ? b.MaxX - b.MinX : b.MaxY - b.MinY);
                float span = x ? maxX - minX : maxY - minY;
                float gap = (span - total) / (sorted.Count - 1);
                float cursor = x ? minX : minY;
                foreach (var b in sorted) {
                    float start = x ? b.MinX : b.MinY;
                    Shift(b.El, x ? new Vector2(cursor - start, 0f) : new Vector2(0f, cursor - start));
                    cursor += (x ? b.MaxX - b.MinX : b.MaxY - b.MinY) + gap;
                }
                break;
            }
        }
        Commit(changes);
    }

    // ---- history / save ----

    private static void Commit(List<(HudElement Element, HudElement.State Before, HudElement.State After)> changes) {
        changes = changes.Where(c => !SameState(c.Before, c.After)).ToList();
        if (changes.Count == 0) {
            return;
        }
        undo.Push(new TransformCommand(changes));
        Save();
        SyncPanelValues();
        RefreshInspectorSoon();
    }

    private static bool SameState(HudElement.State a, HudElement.State b)
        => a.Position == b.Position && a.Scale == b.Scale && Mathf.Approximately(a.Rotation, b.Rotation);

    // Edits come in bursts (arrow keys, slider drags); write once things settle.
    private static float saveDue;

    private static void Save() => saveDue = Time.realtimeSinceStartup + 0.5f;

    private static void FlushSave() {
        if (saveDue <= 0f) {
            return;
        }
        saveDue = 0f;
        try {
            OverlayCore.SaveAllCanvases();
        } catch (Exception e) {
            MainCore.Log.Err($"[HudEditor] Save failed: {e.Message}");
        }
    }
}
