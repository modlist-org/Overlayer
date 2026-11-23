using O5Kit.Behaviour;
using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Compat;
using Overlayer.Core;
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

namespace Overlayer.UI.Easing;

// The interactive graph: click empty space to add a point, drag to move, right-click or
// Delete to remove, Shift snaps to 0.05, Ctrl+Z/Y undo/redo. Also runs the looping preview.
// A plain class ticked by the Easing page (no custom Unity component, so nothing to register for IL2CPP).
internal sealed class CurveGraph {
    private const float Pad = 18f;
    private const float PointSize = 14f;
    private const float HitRadius = 13f;
    private const float MoveSeconds = 1.1f;
    private const float HoldSeconds = 0.5f;

    private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
    private static readonly Color GuideColor = new(1f, 0.92f, 0.28f, 0.55f);
    private static readonly Color GuideLabelColor = new(1f, 0.92f, 0.28f, 0.92f);
    private static Color CurveColor => UIColors.ObjectActive;
    private static readonly Color CompareColor = new(1f, 0.75f, 0.35f, 0.55f);
    private static readonly Color PointColor = Color.white;
    private static readonly Color EndpointColor = new(0.51f, 0.51f, 0.54f, 1f);
    private static readonly Color SelectedColor = new(1f, 0.35f, 0.75f, 1f);
    private static readonly Color HandleColor = new(0.55f, 0.9f, 1f, 1f);
    private const float HandleLength = 56f;
    private const float HandleHit = 11f;

    public readonly RectTransform Rect;
    public readonly CurveModel Model;
    public Func<string> ComparePreset;
    public Func<bool> EndpointHeights;
    public Action<int> SelectionChanged;

    private RectTransform previewTrack, previewDot, compareDot;
    private RectTransform previewUnitRail, previewZeroLine, previewOneLine, previewUnderLine, previewOverLine;
    private TextMeshProUGUI previewZeroLabel, previewOneLabel, previewZeroOneLabel, previewUnderLabel, previewOverLabel;
    private PreviewScale previewScale = PreviewScale.Compute(0f, 1f, 1f);
    private float lastPreviewWidth = -1f;

    public void BindPreview(
        RectTransform track, RectTransform dot, RectTransform compareDot, RectTransform unitRail,
        RectTransform zeroLine, RectTransform oneLine, RectTransform underLine, RectTransform overLine,
        TextMeshProUGUI zeroLabel, TextMeshProUGUI oneLabel, TextMeshProUGUI zeroOneLabel,
        TextMeshProUGUI underLabel, TextMeshProUGUI overLabel) {
        previewTrack = track;
        previewDot = dot;
        this.compareDot = compareDot;
        previewUnitRail = unitRail;
        previewZeroLine = zeroLine;
        previewOneLine = oneLine;
        previewUnderLine = underLine;
        previewOverLine = overLine;
        previewZeroLabel = zeroLabel;
        previewOneLabel = oneLabel;
        previewZeroOneLabel = zeroOneLabel;
        previewUnderLabel = underLabel;
        previewOverLabel = overLabel;
        lastPreviewWidth = -1f;
    }

    private readonly Lines grid, guides, compare, curve, handleLines;
    private readonly RectTransform inHandle, outHandle;
    private readonly TextMeshProUGUI startLabel, endLabel;
    private readonly TextMeshProUGUI hint;
    private bool hintAnim;
    private readonly RectTransform progressDot;
    private readonly List<Image> pointImages = [];

    private int selected = -1;
    private int hoveredPoint = -1;
    private bool dragging;
    // Dragging a tangent handle instead of the point: 0 none, -1 in, 1 out.
    private int handleDrag;
    private bool pointerInside;
    private bool focused;
    private bool dirty = true;
    private float clock;
    private Vector2 lastSize;
    private float lastScale = -1f;
    private string lastLang;
    private Vector2 dragStart;
    private bool dragMoved;
    private Camera pressCamera;

    public int Selected => selected;

    public CurveGraph(Transform parent, CurveModel model) {
        Model = model;
        Rect = NewRect("CurveGraph", parent);
        var bg = Rect.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        bg.raycastTarget = true;

        grid = new Lines(Rect, "Grid", GridColor, 1f, false, false);
        guides = new Lines(Rect, "Guides", GuideColor, 1.5f, false, true);
        startLabel = GuideLabel("StartLabel", MainCore.Tr.Get("EASING_START", "Start"), TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
        endLabel = GuideLabel("EndLabel", MainCore.Tr.Get("EASING_END", "End"), TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f));
        compare = new Lines(Rect, "Compare", CompareColor, 2f, true, true);
        curve = new Lines(Rect, "Curve", () => CurveColor, 3f, true, true);
        handleLines = new Lines(Rect, "HandleLines", HandleColor, 1.5f, false, true);

        progressDot = Dot("Progress", CurveColor);
        progressDot.sizeDelta = new Vector2(10f, 10f);
        inHandle = Dot("InHandle", HandleColor);
        outHandle = Dot("OutHandle", HandleColor);
        inHandle.sizeDelta = outHandle.sizeDelta = new Vector2(11f, 11f);

        hint = O5Kit.Factory.O5Factory.ControlText(O5KitAdapters.Ctx, Rect, 14f, true);
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.pivot = new Vector2(0f, 1f);
        hint.rectTransform.offsetMin = new Vector2(Pad, -Pad - 20f);
        hint.rectTransform.offsetMax = new Vector2(-Pad, -6f);
        hint.alignment = TextAlignmentOptions.TopLeft;
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        hint.raycastTarget = false;
        UpdateHint();

        var trigger = Rect.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, _ => pointerInside = true),
            (EventTriggerType.PointerExit, _ => pointerInside = false),
            (EventTriggerType.PointerDown, e => OnPointerDown(UnityUtils.AsPointerEventData(e))),
            (EventTriggerType.PointerUp, e => OnPointerUp(UnityUtils.AsPointerEventData(e)))
        );

        model.Changed += MarkDirty;
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private RectTransform Dot(string name, Color color) {
        var r = NewRect(name, Rect);
        r.anchorMin = r.anchorMax = Vector2.zero;
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISprite.Circle256);
        img.color = color;
        img.raycastTarget = false;
        r.sizeDelta = new Vector2(PointSize, PointSize);
        return r;
    }

    private TextMeshProUGUI GuideLabel(string name, string text, TextAlignmentOptions align, Vector2 pivot) {
        var t = O5Kit.Factory.O5Factory.ControlText(O5KitAdapters.Ctx, Rect, 12f, true);
        t.gameObject.name = name;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
        t.rectTransform.pivot = pivot;
        t.rectTransform.sizeDelta = new Vector2(64f, 18f);
        t.alignment = align;
        t.color = GuideLabelColor;
        t.raycastTarget = false;
        TmpCompat.SetNoWrap(t);
        t.text = text;
        return t;
    }

    public void MarkDirty() => dirty = true;

    public void Select(int index) {
        if (index >= Model.Points.Count) {
            index = -1;
        }
        if (selected == index) {
            return;
        }
        selected = index;
        dirty = true;
        SelectionChanged?.Invoke(selected);
    }

    // ---- mapping ----

    private Rect Plot {
        get {
            var r = Rect.rect;
            return new Rect(Pad, Pad, r.width - Pad * 2f, r.height - Pad * 2f);
        }
    }

    private Vector2 ToLocal(Vector2 value) {
        var p = Plot;
        return new Vector2(p.x + value.x * p.width, p.y + Mathf.InverseLerp(CurveModel.MinY, CurveModel.MaxY, value.y) * p.height);
    }

    private Vector2 ToValue(Vector2 local) {
        var p = Plot;
        return new Vector2((local.x - p.x) / p.width, Mathf.Lerp(CurveModel.MinY, CurveModel.MaxY, (local.y - p.y) / p.height));
    }

    // Graph-local position with the origin at the bottom-left corner.
    private bool LocalFromScreen(Vector2 screen, Camera cam, out Vector2 local) {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, cam, out local)) {
            return false;
        }
        local -= Rect.rect.min;
        return true;
    }

    private int PointAt(Vector2 local) => PointAt(local, HitRadius);

    private int PointAt(Vector2 local, float radius) {
        int best = -1;
        float bestDist = radius * radius;
        for (int i = 0; i < Model.Points.Count; i++) {
            float d = (ToLocal(Model.Points[i]) - local).sqrMagnitude;
            if (d <= bestDist) {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    internal void UpdateHint() {
        hintAnim = Model.IsAnim;
        lastLang = MainCore.Tr.Language;
        if (startLabel != null) {
            startLabel.text = MainCore.Tr.Get("EASING_START", "Start");
        }
        if (endLabel != null) {
            endLabel.text = MainCore.Tr.Get("EASING_END", "End");
        }
        if (hint != null) {
            hint.text = MainCore.Tr.Get("EASING_GRAPH_HINT", "Click: add point   Drag: move   Right-click / Del: remove   Shift: snap   Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z")
                + (hintAnim ? MainCore.Tr.Get("EASING_GRAPH_HINT_ANIM", "   Handles: slope (Alt: split)") : string.Empty);
        }
    }

    // ---- tangent handles (anim) ----

    // Graph-local units per 1.0 of x and of y.
    private float UnitX => Plot.width;
    private float UnitY => Plot.height / (CurveModel.MaxY - CurveModel.MinY);

    private bool ShowHandles => Model.IsAnim && selected >= 0 && selected < Model.Points.Count;

    private bool HasHandle(int side) => side < 0 ? selected > 0 : selected < Model.Points.Count - 1;

    // Handle position: a fixed screen length along the slope, behind (in) or ahead (out) of the key.
    private Vector2 HandlePosition(int side) {
        var t = Model.Tangents[selected];
        float slope = side < 0 ? t.x : t.y;
        var dir = new Vector2(UnitX, slope * UnitY).normalized;
        return ToLocal(Model.Points[selected]) + dir * (side * HandleLength);
    }

    private int HandleAt(Vector2 local) {
        if (!ShowHandles) {
            return 0;
        }
        foreach (int side in new[] { 1, -1 }) {
            if (HasHandle(side) && (HandlePosition(side) - local).sqrMagnitude <= HandleHit * HandleHit) {
                return side;
            }
        }
        return 0;
    }

    private void DragHandle(Vector2 local) {
        // Vector from the key toward "ahead"; the in-handle sits behind, so mirror it.
        Vector2 v = (local - ToLocal(Model.Points[selected])) * handleDrag;
        v.x = Mathf.Max(v.x, 0.5f);
        float slope = (v.y / UnitY) / (v.x / UnitX);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        Model.SetSlope(selected, handleDrag > 0, slope, alt);
    }

    private static Vector2 Snap(Vector2 v) {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return shift ? new Vector2(Mathf.Round(v.x / 0.05f) * 0.05f, Mathf.Round(v.y / 0.05f) * 0.05f) : v;
    }

    // ---- pointer ----

    private void OnPointerDown(PointerEventData e) {
        if (e == null) {
            return;
        }
        focused = true;
        pressCamera = e.pressEventCamera;
        if (!LocalFromScreen(e.position, pressCamera, out var local)) {
            return;
        }
        int hit = PointAt(local);
        if (e.button == PointerEventData.InputButton.Right) {
            if (hit >= 0) {
                BeginPin();
                if (Model.Remove(hit)) {
                    Select(-1);
                }
            } else if (PointAt(local, HitRadius * 1.5f) >= 0) {
                BeginPin();
            }
            return;
        }
        if (e.button != PointerEventData.InputButton.Left) {
            return;
        }
        dragStart = local;
        handleDrag = HandleAt(local);
        if (handleDrag != 0) {
            dragging = true;
            dragMoved = true;
            return;
        }
        bool inserted = hit < 0;
        if (inserted) {
            hit = Model.Insert(Snap(ToValue(local)));
            if (hit < 0) {
                Select(-1);
                return;
            }
        }
        Select(hit);
        dragging = true;
        dragMoved = inserted;
    }

    private void OnPointerUp(PointerEventData e) {
        if (!dragging || (e != null && e.button != PointerEventData.InputButton.Left)) {
            return;
        }
        EndDrag();
    }

    private void EndDrag() {
        if (!dragging) {
            return;
        }
        dragging = false;
        handleDrag = 0;
        if (dragMoved) {
            Model.Snapshot();
        }
        dragMoved = false;
    }

    // ---- per frame ----

    private static bool TypingInField() {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var field = go != null ? go.GetComponent<TMP_InputField>() : null;
        return field != null && field.isFocused;
    }

    public void Tick() {
        if (Rect == null || !Rect.gameObject.activeInHierarchy) {
            ReleasePin();
            return;
        }
        string lang = MainCore.Tr.Language;
        if (lastLang != lang) {
            UpdateHint();
        }
        float scale = UICore.Canvas != null && UICore.Canvas.scaleFactor > 0f ? UICore.Canvas.scaleFactor : 1f;
        Vector2 size = Rect.rect.size;
        if (!Mathf.Approximately(scale, lastScale) || size != lastSize) {
            lastScale = scale;
            lastSize = size;
            dirty = true;
        }
        if (Input.GetMouseButtonDown(0) && !pointerInside) {
            focused = false;
        }

        if ((focused || pointerInside) && !TypingInField()) {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Delete) && selected >= 0 && Model.Remove(selected)) {
                Select(-1);
            }
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) {
                ApplyHistory(shift ? Model.Redo() : Model.Undo());
            } else if (ctrl && Input.GetKeyDown(KeyCode.Y)) {
                ApplyHistory(Model.Redo());
            }
        }

        if (dragging) {
            if (!Input.GetMouseButton(0)) {
                EndDrag();
            } else if (selected >= 0 && selected < Model.Points.Count
                && LocalFromScreen(Input.mousePosition, pressCamera, out var dragLocal)) {
                if (handleDrag != 0) {
                    DragHandle(dragLocal);
                    SelectionChanged?.Invoke(selected);
                } else if (dragMoved || (dragLocal - dragStart).sqrMagnitude >= 4f) {
                    dragMoved = true;
                    Model.Move(selected, Snap(ToValue(dragLocal)), EndpointHeights?.Invoke() == true);
                    SelectionChanged?.Invoke(selected);
                }
            }
        }

        int hover = -1;
        if (pointerInside && !dragging && LocalFromScreen(Input.mousePosition, null, out var local)) {
            hover = PointAt(local);
        }
        if (hover != hoveredPoint) {
            hoveredPoint = hover;
            dirty = true;
        }

        PinScroll();
        if (dirty) {
            Redraw();
        } else if (previewTrack != null && !Mathf.Approximately(previewTrack.rect.width, lastPreviewWidth)) {
            UpdatePreviewScale();
        }
        Animate();
    }

    // The page's scroll view jumps to the pointer on right-press (right-drag scrolling), so
    // right-clicking a point to delete it would throw the page around. Pin the scroll while
    // the right button is held over the graph. Button state is per frame, so this holds no
    // matter whether the scroll view updates before or after us.
    private UIScrollController scroller;
    private RectTransform scrollContent;
    private float pinnedY;
    private bool pinning;
    private bool rightPressedOutside;

    private void EnsureScroller() {
        if (scroller != null) {
            return;
        }
        scroller = Rect.GetComponentInParent<UIScrollController>();
        scrollContent = Rect.parent as RectTransform;
        while (scrollContent != null && scrollContent.parent != null && scrollContent.parent.GetComponent<RectMask2D>() == null) {
            scrollContent = scrollContent.parent as RectTransform;
        }
    }

    private void BeginPin() {
        EnsureScroller();
        if (scroller == null || scrollContent == null) {
            return;
        }
        if (!pinning) {
            pinning = true;
            scroller.enabled = false;
        }
        scroller.ScrollTo(pinnedY, true);
    }

    public void ReleasePin() {
        if (!pinning) {
            return;
        }
        pinning = false;
        if (scroller != null) {
            scroller.enabled = true;
        }
    }

    private void PinScroll() {
        EnsureScroller();
        if (scroller == null || scrollContent == null) {
            return;
        }
        bool held = O5Input.GetMouseButton(1);
        if (!held) {
            rightPressedOutside = false;
            ReleasePin();
            if (!O5Input.GetMouseButtonDown(1)) {
                pinnedY = scrollContent.anchoredPosition.y;
            }
            return;
        }
        if (O5Input.GetMouseButtonDown(1) && !pinning && !rightPressedOutside) {
            Vector2 mouse = O5Input.MousePosition;
            bool over = pointerInside || RectTransformUtility.RectangleContainsScreenPoint(Rect, mouse, null);
            if (over && LocalFromScreen(mouse, null, out var pressLocal) && PointAt(pressLocal, HitRadius * 1.5f) >= 0) {
                BeginPin();
            } else {
                rightPressedOutside = true;
            }
        } else if (pinning) {
            scroller.ScrollTo(pinnedY, true);
        }
    }

    public void ApplyHistory(bool changed) {
        if (!changed) {
            return;
        }
        Select(selected < Model.Points.Count ? selected : -1);
        SelectionChanged?.Invoke(selected);
    }

    private void Redraw() {
        dirty = false;
        var plot = Plot;
        if (plot.width <= 0f || plot.height <= 0f) {
            dirty = true; // layout not done yet
            return;
        }

        var g = new List<Vector2>();
        for (int i = 0; i <= 4; i++) {
            float x = i / 4f;
            g.Add(ToLocal(new Vector2(x, CurveModel.MinY)));
            g.Add(ToLocal(new Vector2(x, CurveModel.MaxY)));
        }
        foreach (float y in new[] { -0.5f, -0.25f, 0.25f, 0.5f, 0.75f, 1.25f, 1.5f }) {
            g.Add(ToLocal(new Vector2(0f, y)));
            g.Add(ToLocal(new Vector2(1f, y)));
        }
        float minLine = 1.25f / (lastScale > 0f ? lastScale : 1f);
        grid.Set(g, minLine);
        guides.Set([
            ToLocal(new Vector2(0f, 0f)), ToLocal(new Vector2(1f, 0f)),
            ToLocal(new Vector2(0f, 1f)), ToLocal(new Vector2(1f, 1f)),
        ], minLine);
        Vector2 startPos = ToLocal(new Vector2(0f, 0f));
        Vector2 endPos = ToLocal(new Vector2(0f, 1f));
        startLabel.rectTransform.anchoredPosition = new Vector2(startPos.x + 12f, startPos.y - 3f);
        endLabel.rectTransform.anchoredPosition = new Vector2(endPos.x + 12f, endPos.y + 3f);

        int samples = Mathf.Clamp((int)plot.width / 6, 40, 160);
        string text = Model.Text;
        curve.Set(Sample(t => EaseMath.Evaluate(text, t), samples, Model.Points));
        string preset = ComparePreset?.Invoke();
        compare.Set(string.IsNullOrEmpty(preset) ? [] : Sample(t => EaseMath.Evaluate(preset, t), samples));

        while (pointImages.Count < Model.Points.Count) {
            pointImages.Add(Dot("Point", PointColor).GetComponent<Image>());
        }
        for (int i = 0; i < pointImages.Count; i++) {
            var img = pointImages[i];
            bool used = i < Model.Points.Count;
            img.gameObject.SetActive(used);
            if (!used) {
                continue;
            }
            bool endpoint = Model.IsEndpoint(i);
            img.rectTransform.anchoredPosition = ToLocal(Model.Points[i]);
            float size = PointSize * (i == selected ? 1.3f : i == hoveredPoint ? 1.15f : 1f);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.color = i == selected ? SelectedColor : endpoint && EndpointHeights?.Invoke() != true ? EndpointColor : PointColor;
            img.rectTransform.SetAsLastSibling();
        }

        if (hintAnim != Model.IsAnim) {
            UpdateHint();
        }
        var handleSegments = new List<Vector2>();
        foreach (var (side, dot) in new[] { (-1, inHandle), (1, outHandle) }) {
            bool show = ShowHandles && HasHandle(side);
            dot.gameObject.SetActive(show);
            if (!show) {
                continue;
            }
            Vector2 pos = HandlePosition(side);
            dot.anchoredPosition = pos;
            dot.SetAsLastSibling();
            handleSegments.Add(ToLocal(Model.Points[selected]));
            handleSegments.Add(pos);
        }
        handleLines.Set(handleSegments, minLine);
        progressDot.SetAsLastSibling();
        UpdatePreviewScale();
    }

    private void UpdatePreviewScale() {
        if (previewTrack == null) {
            return;
        }
        float width = previewTrack.rect.width;
        if (width <= 1f) {
            lastPreviewWidth = width;
            return;
        }
        lastPreviewWidth = width;

        float sMin = 0f, sMax = 1f;
        void Check(float v) {
            if (float.IsNaN(v) || float.IsInfinity(v)) {
                return;
            }
            v = Mathf.Clamp(v, -1e4f, 1e4f);
            if (v < sMin) sMin = v;
            if (v > sMax) sMax = v;
        }

        string text = Model.Text;
        for (int i = 0; i <= 128; i++) {
            Check(EaseMath.Evaluate(text, i / 128f));
        }
        for (int i = 0; i < Model.Points.Count; i++) {
            Check(EaseMath.Evaluate(text, Model.Points[i].x));
        }
        string preset = ComparePreset?.Invoke();
        if (!string.IsNullOrEmpty(preset)) {
            for (int i = 0; i <= 128; i++) {
                Check(EaseMath.Evaluate(preset, i / 128f));
            }
        }

        previewScale = PreviewScale.Compute(sMin, sMax, width);

        if (previewUnitRail != null) {
            previewUnitRail.anchoredPosition = new Vector2(previewScale.X0, 0f);
            previewUnitRail.sizeDelta = new Vector2(Mathf.Max(0f, previewScale.X1 - previewScale.X0), 1.5f);
        }
        if (previewUnderLine != null) {
            previewUnderLine.gameObject.SetActive(previewScale.HasUnder);
            previewUnderLine.anchoredPosition = new Vector2(0f, 0f);
        }
        if (previewOverLine != null) {
            previewOverLine.gameObject.SetActive(previewScale.HasOver);
            previewOverLine.anchoredPosition = new Vector2(width, 0f);
        }
        if (previewZeroLine != null) {
            float lineX = previewScale.MergeLines ? (previewScale.X0 + previewScale.X1) * 0.5f : previewScale.X0;
            previewZeroLine.anchoredPosition = new Vector2(lineX, 0f);
        }
        if (previewOneLine != null) {
            previewOneLine.gameObject.SetActive(!previewScale.MergeLines);
            previewOneLine.anchoredPosition = new Vector2(previewScale.X1, 0f);
        }

        if (previewUnderLabel != null) {
            previewUnderLabel.gameObject.SetActive(previewScale.HasUnder);
            previewUnderLabel.text = previewScale.UnderText;
            previewUnderLabel.rectTransform.anchoredPosition = Vector2.zero;
        }
        if (previewOverLabel != null) {
            previewOverLabel.gameObject.SetActive(previewScale.HasOver);
            previewOverLabel.text = previewScale.OverText;
            previewOverLabel.rectTransform.anchoredPosition = new Vector2(width, 0f);
        }
        if (previewZeroLabel != null) {
            previewZeroLabel.gameObject.SetActive(!previewScale.MergedZeroOne);
            previewZeroLabel.rectTransform.anchoredPosition = new Vector2(previewScale.ZeroLabelX, 0f);
        }
        if (previewOneLabel != null) {
            previewOneLabel.gameObject.SetActive(!previewScale.MergedZeroOne);
            previewOneLabel.rectTransform.anchoredPosition = new Vector2(previewScale.OneLabelX, 0f);
        }
        if (previewZeroOneLabel != null) {
            previewZeroOneLabel.gameObject.SetActive(previewScale.MergedZeroOne);
            previewZeroOneLabel.rectTransform.anchoredPosition = new Vector2(previewScale.MergedLabelX, 0f);
        }

        previewDot?.SetAsLastSibling();
        compareDot?.SetAsLastSibling();
    }

    // Evenly spaced samples miss a sharp turn between them (points close together), and the
    // line then cuts past the point. With keys, each segment between two keys gets samples of
    // its own and the keys themselves are always on the line.
    private List<Vector2> Sample(Func<float, float> f, int samples, IReadOnlyList<Vector2> keys = null) {
        var points = new List<Vector2>(samples + 1);
        void Add(float t) => points.Add(ToLocal(new Vector2(t, Mathf.Clamp(f(t), CurveModel.MinY, CurveModel.MaxY))));
        if (keys == null || keys.Count < 2) {
            for (int i = 0; i <= samples; i++) {
                Add(i / (float)samples);
            }
            return points;
        }
        if (keys[0].x > 0f) {
            Add(0f);
        }
        for (int k = 0; k + 1 < keys.Count; k++) {
            float from = keys[k].x, to = keys[k + 1].x;
            int steps = Mathf.Max(8, Mathf.CeilToInt((to - from) * samples));
            for (int i = 0; i < steps; i++) {
                Add(Mathf.Lerp(from, to, i / (float)steps));
            }
        }
        Add(keys[keys.Count - 1].x);
        if (keys[keys.Count - 1].x < 1f) {
            Add(1f);
        }
        return points;
    }

    private void Animate() {
        clock = (clock + Time.unscaledDeltaTime) % (MoveSeconds + HoldSeconds);
        float t = Mathf.Clamp01(clock / MoveSeconds);
        float v = Model.Evaluate(t);
        progressDot.anchoredPosition = ToLocal(new Vector2(t, Mathf.Clamp(v, CurveModel.MinY, CurveModel.MaxY)));
        string preset = ComparePreset?.Invoke();
        bool comparing = !string.IsNullOrEmpty(preset);
        Place(previewDot, v, comparing ? 7f : 0f);
        if (compareDot != null) {
            compareDot.gameObject.SetActive(comparing);
            if (comparing) {
                Place(compareDot, EaseMath.Evaluate(preset, t), -7f);
            }
        }
    }

    private void Place(RectTransform dot, float value, float y) {
        if (dot == null || previewTrack == null) {
            return;
        }
        dot.anchoredPosition = new Vector2(previewScale.MapX(value, previewTrack.rect.width), y);
    }

    // Line segments as pooled Images: clipped by the scroll view's RectMask2D like any other
    // UI and needs no custom Graphic. Strip: one polyline, otherwise point pairs.
    private sealed class Lines {
        private readonly RectTransform root;
        private readonly List<RectTransform> pool = [];
        private readonly Color color;
        private readonly Func<Color> colorProvider;
        private readonly float width;
        private readonly bool strip;
        private readonly bool antialiased;

        public Lines(RectTransform parent, string name, Color color, float width, bool strip, bool antialiased = false)
            : this(parent, name, null, color, width, strip, antialiased) { }

        public Lines(RectTransform parent, string name, Func<Color> colorProvider, float width, bool strip, bool antialiased = false)
            : this(parent, name, colorProvider, colorProvider(), width, strip, antialiased) { }

        private Lines(RectTransform parent, string name, Func<Color> colorProvider, Color color, float width, bool strip, bool antialiased) {
            root = NewRect(name, parent);
            root.anchorMin = root.pivot = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            this.colorProvider = colorProvider;
            this.color = color;
            this.width = width;
            this.strip = strip;
            this.antialiased = antialiased;
        }

        private Color CurrentColor => colorProvider != null ? colorProvider() : color;

        public void Set(List<Vector2> points, float minThickness = 0f) {
            float w = Mathf.Max(width, minThickness) + (antialiased ? 1f : 0f);
            Color currentColor = CurrentColor;
            int used = 0;
            int step = strip ? 1 : 2;
            for (int i = 0; i + 1 < points.Count; i += step) {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 dir = b - a;
                if (dir.sqrMagnitude < 1e-6f) {
                    continue;
                }
                var seg = Get(used++, currentColor);
                seg.anchoredPosition = (a + b) * 0.5f;
                // Strips overlap a little at the joints so they don't show gaps.
                seg.sizeDelta = new Vector2(dir.magnitude + (strip ? w : 0f), w);
                seg.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                seg.gameObject.SetActive(true);
            }
            for (int i = used; i < pool.Count; i++) {
                pool[i].gameObject.SetActive(false);
            }
        }

        private RectTransform Get(int index, Color currentColor) {
            while (pool.Count <= index) {
                var seg = NewRect("Segment", root);
                seg.anchorMin = seg.anchorMax = Vector2.zero;
                var img = seg.gameObject.AddComponent<Image>();
                if (antialiased) {
                    img.sprite = Overlayer.UI.Utility.UILineSprite.AntialiasedLine;
                }
                img.color = currentColor;
                img.raycastTarget = false;
                pool.Add(seg);
            }
            var rect = pool[index];
            if (colorProvider != null) {
                rect.GetComponent<Image>().color = currentColor;
            }
            return rect;
        }
    }
}
