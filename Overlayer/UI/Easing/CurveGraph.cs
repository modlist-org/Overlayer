using O5Kit.Behaviour;
using O5Kit.Core;
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
    private static readonly Color GuideColor = new(1f, 1f, 1f, 0.22f);
    private static readonly Color CurveColor = new(0.569f, 0.604f, 1f, 1f);
    private static readonly Color CompareColor = new(1f, 0.75f, 0.35f, 0.55f);
    private static readonly Color PointColor = new(1f, 1f, 1f, 0.95f);
    private static readonly Color EndpointColor = new(1f, 1f, 1f, 0.45f);
    private static readonly Color SelectedColor = new(1f, 0.35f, 0.75f, 1f);

    public readonly RectTransform Rect;
    public readonly CurveModel Model;
    public Func<string> ComparePreset;
    public Func<bool> EndpointHeights;
    public Action<int> SelectionChanged;

    public RectTransform PreviewTrack;
    public RectTransform PreviewBox;
    public RectTransform CompareBox;

    private readonly Lines grid, guides, compare, curve;
    private readonly RectTransform progressDot;
    private readonly List<Image> pointImages = [];

    private int selected = -1;
    private int hoveredPoint = -1;
    private bool dragging;
    private bool pointerInside;
    private bool focused;
    private bool dirty = true;
    private float clock;

    public int Selected => selected;

    public CurveGraph(Transform parent, CurveModel model) {
        Model = model;
        Rect = NewRect("CurveGraph", parent);
        var bg = Rect.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        bg.raycastTarget = true;

        grid = new Lines(Rect, "Grid", GridColor, 1f, false);
        guides = new Lines(Rect, "Guides", GuideColor, 1.5f, false);
        compare = new Lines(Rect, "Compare", CompareColor, 2f, true);
        curve = new Lines(Rect, "Curve", CurveColor, 3f, true);

        progressDot = Dot("Progress", CurveColor);
        progressDot.sizeDelta = new Vector2(10f, 10f);

        var hint = O5Kit.Factory.O5Factory.ControlText(O5KitAdapters.Ctx, Rect, 14f, true);
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.pivot = new Vector2(0f, 1f);
        hint.rectTransform.offsetMin = new Vector2(Pad, -Pad - 20f);
        hint.rectTransform.offsetMax = new Vector2(-Pad, -6f);
        hint.alignment = TextAlignmentOptions.TopLeft;
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        hint.raycastTarget = false;
        hint.text = MainCore.Tr.Get("EASING_GRAPH_HINT", "Click: add point   Drag: move   Right-click / Del: remove   Shift: snap   Ctrl+Z/Y");

        var trigger = Rect.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, _ => pointerInside = true),
            (EventTriggerType.PointerExit, _ => pointerInside = false),
            (EventTriggerType.PointerDown, e => OnPointerDown(e as PointerEventData)),
            (EventTriggerType.Drag, e => OnDrag(e as PointerEventData)),
            (EventTriggerType.PointerUp, _ => OnPointerUp())
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

    private int PointAt(Vector2 local) {
        int best = -1;
        float bestDist = HitRadius * HitRadius;
        for (int i = 0; i < Model.Points.Count; i++) {
            float d = (ToLocal(Model.Points[i]) - local).sqrMagnitude;
            if (d <= bestDist) {
                bestDist = d;
                best = i;
            }
        }
        return best;
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
        if (!LocalFromScreen(e.position, e.pressEventCamera, out var local)) {
            return;
        }
        int hit = PointAt(local);
        if (e.button == PointerEventData.InputButton.Right) {
            if (hit >= 0 && Model.Remove(hit)) {
                Select(-1);
            }
            return;
        }
        if (e.button != PointerEventData.InputButton.Left) {
            return;
        }
        if (hit < 0) {
            hit = Model.Insert(Snap(ToValue(local)));
            if (hit < 0) {
                Select(-1);
                return;
            }
        }
        Select(hit);
        dragging = true;
    }

    private void OnDrag(PointerEventData e) {
        if (!dragging || selected < 0 || e == null) {
            return;
        }
        if (!LocalFromScreen(e.position, e.pressEventCamera, out var local)) {
            return;
        }
        Model.Move(selected, Snap(ToValue(local)), EndpointHeights?.Invoke() == true);
        SelectionChanged?.Invoke(selected);
    }

    private void OnPointerUp() {
        if (!dragging) {
            return;
        }
        dragging = false;
        Model.Snapshot();
    }

    // ---- per frame ----

    private static bool TypingInField() {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var field = go != null ? go.GetComponent<TMP_InputField>() : null;
        return field != null && field.isFocused;
    }

    public void Tick() {
        if (Rect == null || !Rect.gameObject.activeInHierarchy) {
            return;
        }
        if (Input.GetMouseButtonDown(0) && !pointerInside) {
            focused = false;
        }

        if ((focused || pointerInside) && !TypingInField()) {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Delete) && selected >= 0 && Model.Remove(selected)) {
                Select(-1);
            }
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) {
                AfterHistory(shift ? Model.Redo() : Model.Undo());
            }
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) {
                AfterHistory(Model.Redo());
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

    private void PinScroll() {
        if (scroller == null) {
            scroller = Rect.GetComponentInParent<UIScrollController>();
            scrollContent = Rect.parent as RectTransform;
            while (scrollContent != null && scrollContent.parent != null && scrollContent.parent.GetComponent<RectMask2D>() == null) {
                scrollContent = scrollContent.parent as RectTransform;
            }
            if (scroller == null || scrollContent == null) {
                return;
            }
        }
        bool held = Input.GetMouseButton(1);
        if (!held) {
            if (pinning) {
                pinning = false;
                scroller.enabled = true;
            }
            pinnedY = scrollContent.anchoredPosition.y;
            return;
        }
        if (!pinning && pointerInside) {
            pinning = true;
            scroller.enabled = false;
        }
        if (pinning) {
            scroller.ScrollTo(pinnedY, true);
        }
    }

    private void AfterHistory(bool changed) {
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
        grid.Set(g);
        guides.Set([
            ToLocal(new Vector2(0f, 0f)), ToLocal(new Vector2(1f, 0f)),
            ToLocal(new Vector2(0f, 1f)), ToLocal(new Vector2(1f, 1f)),
        ]);

        int samples = Mathf.Clamp((int)plot.width / 6, 40, 160);
        string text = Model.Text;
        curve.Set(Sample(t => EaseMath.Evaluate(text, t), samples));
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
        progressDot.SetAsLastSibling();
    }

    private List<Vector2> Sample(Func<float, float> f, int samples) {
        var points = new List<Vector2>(samples + 1);
        for (int i = 0; i <= samples; i++) {
            float t = i / (float)samples;
            points.Add(ToLocal(new Vector2(t, Mathf.Clamp(f(t), CurveModel.MinY, CurveModel.MaxY))));
        }
        return points;
    }

    private void Animate() {
        clock = (clock + Time.unscaledDeltaTime) % (MoveSeconds + HoldSeconds);
        float t = Mathf.Clamp01(clock / MoveSeconds);
        float v = Model.Evaluate(t);
        progressDot.anchoredPosition = ToLocal(new Vector2(t, Mathf.Clamp(v, CurveModel.MinY, CurveModel.MaxY)));
        Place(PreviewBox, v);
        string preset = ComparePreset?.Invoke();
        if (CompareBox != null) {
            CompareBox.gameObject.SetActive(!string.IsNullOrEmpty(preset));
            if (!string.IsNullOrEmpty(preset)) {
                Place(CompareBox, EaseMath.Evaluate(preset, t));
            }
        }
    }

    // 0..1 spans the middle of the track; the margins show overshoot. A box that left the
    // scroll view's mask entirely got culled and didn't always come back, so stay inside.
    private const float PreviewMargin = 0.15f;

    private void Place(RectTransform box, float value) {
        if (box == null || PreviewTrack == null) {
            return;
        }
        float width = PreviewTrack.rect.width - box.rect.width;
        float t = PreviewMargin + value * (1f - 2f * PreviewMargin);
        var pos = box.anchoredPosition;
        pos.x = Mathf.Clamp(width * t, 0f, width);
        box.anchoredPosition = pos;
    }

    // Line segments as pooled Images: clipped by the scroll view's RectMask2D like any other
    // UI and needs no custom Graphic. Strip: one polyline, otherwise point pairs.
    private sealed class Lines {
        private readonly RectTransform root;
        private readonly List<RectTransform> pool = [];
        private readonly Color color;
        private readonly float width;
        private readonly bool strip;

        public Lines(RectTransform parent, string name, Color color, float width, bool strip) {
            root = NewRect(name, parent);
            root.anchorMin = root.pivot = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            this.color = color;
            this.width = width;
            this.strip = strip;
        }

        public void Set(List<Vector2> points) {
            int used = 0;
            int step = strip ? 1 : 2;
            for (int i = 0; i + 1 < points.Count; i += step) {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 dir = b - a;
                if (dir.sqrMagnitude < 1e-6f) {
                    continue;
                }
                var seg = Get(used++);
                seg.anchoredPosition = (a + b) * 0.5f;
                // Strips overlap a little at the joints so they don't show gaps.
                seg.sizeDelta = new Vector2(dir.magnitude + (strip ? width : 0f), width);
                seg.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                seg.gameObject.SetActive(true);
            }
            for (int i = used; i < pool.Count; i++) {
                pool[i].gameObject.SetActive(false);
            }
        }

        private RectTransform Get(int index) {
            while (pool.Count <= index) {
                var seg = NewRect("Segment", root);
                seg.anchorMin = seg.anchorMax = Vector2.zero;
                var img = seg.gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                pool.Add(seg);
            }
            return pool[index];
        }
    }
}
