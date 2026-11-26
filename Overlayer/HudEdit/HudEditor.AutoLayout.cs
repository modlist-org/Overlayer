using Overlayer.IO.Fx;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.HudEdit;

// Auto layout (like Figma's): a group lines its children up in a row or a column with a gap
// and padding, and its box hugs them. Turning it on keeps the current look as far as it can
// (order by position, the gap they already have); turning it off leaves everything where it is.
internal static partial class HudEditor {
    internal enum Flow { None, Row, Column }

    internal static Flow FlowOf(OvObject o) => o.Config.HorizontalLayoutGroupConfig != null ? Flow.Row
        : o.Config.VerticalLayoutGroupConfig != null ? Flow.Column : Flow.None;

    // Row and column settings have the same fields; use whichever is on.
    private static FxValue<float> GapOf(OvObject o) => o.Config.HorizontalLayoutGroupConfig?.Spacing ?? o.Config.VerticalLayoutGroupConfig?.Spacing;
    private static FxValue<Vector2> PadHOf(OvObject o) => o.Config.HorizontalLayoutGroupConfig?.PaddingH ?? o.Config.VerticalLayoutGroupConfig?.PaddingH;
    private static FxValue<Vector2> PadVOf(OvObject o) => o.Config.HorizontalLayoutGroupConfig?.PaddingV ?? o.Config.VerticalLayoutGroupConfig?.PaddingV;
    private static FxValue<TextAnchor> AlignOf(OvObject o) => o.Config.HorizontalLayoutGroupConfig?.ChildAlignment ?? o.Config.VerticalLayoutGroupConfig?.ChildAlignment;

    private static bool CanAutoLayout(HudElement el) => el != null && el.IsAlive && !el.Locked && el.Object.Children.Count > 0
        && el.Object.Config.GridLayoutGroupConfig == null;

    private sealed class LayoutState {
        public HorizontalLayoutGroupSettings H;
        public VerticalLayoutGroupSettings V;
        public ContentSizeFitterSettings Fitter;
        public Pin Rect;
        public List<OvObject> Order;
        public List<Vector2> ChildPositions;
    }

    private static LayoutState CaptureLayout(OvObject o) => new() {
        H = o.Config.HorizontalLayoutGroupConfig?.Copy(),
        V = o.Config.VerticalLayoutGroupConfig?.Copy(),
        Fitter = o.Config.ContentSizeFitterConfig?.Copy(),
        Rect = PinOf(o.Config.RectTransformConfig),
        Order = o.Children.ToList(),
        ChildPositions = o.Children.Select(c => c.Config.RectTransformConfig?.AnchoredPosition.StaticValue ?? Vector2.zero).ToList(),
    };

    private static void RestoreLayout(OvObject o, LayoutState s) {
        if (FlowOf(o) != (s.H != null ? Flow.Row : s.V != null ? Flow.Column : Flow.None)) {
            RemoveLayoutGroupNow(o);
        }
        o.Config.HorizontalLayoutGroupConfig = s.H?.Copy();
        o.Config.VerticalLayoutGroupConfig = s.V?.Copy();
        o.Config.ContentSizeFitterConfig = s.Fitter?.Copy();
        for (int i = 0; i < s.Order.Count; i++) {
            if (o.Children.Contains(s.Order[i])) {
                o.SetChildIndex(s.Order[i], i);
            }
            if (s.Order[i].Config.RectTransformConfig != null) {
                s.Order[i].Config.RectTransformConfig.AnchoredPosition.StaticValue = s.ChildPositions[i];
                s.Order[i].ApplyConfig();
            }
        }
        var c = o.Config.RectTransformConfig;
        c.AnchorMin.StaticValue = s.Rect.AnchorMin;
        c.AnchorMax.StaticValue = s.Rect.AnchorMax;
        c.Pivot.StaticValue = s.Rect.Pivot;
        c.AnchoredPosition.StaticValue = s.Rect.Position;
        c.SizeDelta.StaticValue = s.Rect.Size;
        o.ApplyComponent();
        o.ApplyConfig();
        RebuildLayoutTree(o);
    }

    // A GameObject takes a single layout group, and Destroy waits for the end of the frame.
    private static void RemoveLayoutGroupNow(OvObject o) {
        var group = o.GameObject.GetComponent<LayoutGroup>();
        if (group != null) {
            UnityEngine.Object.DestroyImmediate(group);
        }
    }

    private static void RebuildLayoutTree(OvObject obj) {
        if (obj?.GameObject == null || !obj.GameObject.activeInHierarchy) {
            return;
        }
        foreach (var child in obj.Children) {
            RebuildLayoutTree(child);
        }
        obj.TextUpdater?.Flush();
        if (obj.RectTransform != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(obj.RectTransform);
        }
    }

    public static void SetFlow(Flow flow) {
        var el = Primary;
        if (selection.Count != 1 || !CanAutoLayout(el) || FlowOf(el.Object) == flow) {
            return;
        }
        CommitQuickEdit();
        var obj = el.Object;
        foreach (var child in obj.Children) {
            RebuildLayoutTree(child);
        }
        var before = CaptureLayout(obj);
        Flow was = FlowOf(obj);
        el.GetScreenCorners(corners);
        Vector2 topLeft = corners[1];

        // Where every child is now, to leave them there when the layout goes away.
        var placed = obj.Children.Where(c => c.RectTransform != null).Select(c => (Obj: c, Screen: (Vector2)c.RectTransform.position)).ToList();

        // Keep gap / padding / alignment when switching between row and column.
        float gap = GapOf(obj)?.StaticValue ?? -1f;
        Vector2 padH = PadHOf(obj)?.StaticValue ?? Vector2.zero, padV = PadVOf(obj)?.StaticValue ?? Vector2.zero;
        TextAnchor align = AlignOf(obj)?.StaticValue ?? (flow == Flow.Row ? TextAnchor.MiddleLeft : TextAnchor.UpperCenter);

        RemoveLayoutGroupNow(obj);
        obj.Config.HorizontalLayoutGroupConfig = null;
        obj.Config.VerticalLayoutGroupConfig = null;

        if (flow == Flow.None) {
            // The box hugged the children only for the layout.
            if (obj.GameObject.GetComponent<Graphic>() == null) {
                obj.Config.ContentSizeFitterConfig = null;
            }
            obj.ApplyComponent();
            obj.ApplyConfig();
            foreach (var (child, screen) in placed) {
                child.ApplyConfig();
                PlaceAt(child, screen);
                RebuildLayoutTree(child);
            }
        } else {
            // Turning it on: order by where they are on screen, so nothing gets shuffled.
            // Switching row <-> column keeps the order they already have.
            var sorted = was != Flow.None ? placed.Select(p => p.Obj).ToList()
                : placed.OrderBy(p => flow == Flow.Row ? p.Screen.x : -p.Screen.y).Select(p => p.Obj).ToList();
            if (gap < 0f) {
                gap = CurrentGap(sorted, flow, obj.RectTransform);
            }
            for (int i = 0; i < sorted.Count; i++) {
                obj.SetChildIndex(sorted[i], i);
            }
            if (flow == Flow.Row) {
                obj.Config.HorizontalLayoutGroupConfig = new HorizontalLayoutGroupSettings();
            } else {
                obj.Config.VerticalLayoutGroupConfig = new VerticalLayoutGroupSettings();
            }
            // Children keep their own size; only their place is set.
            var hg = obj.Config.HorizontalLayoutGroupConfig;
            var vg = obj.Config.VerticalLayoutGroupConfig;
            GapOf(obj).StaticValue = gap;
            PadHOf(obj).StaticValue = padH;
            PadVOf(obj).StaticValue = padV;
            AlignOf(obj).StaticValue = align;
            foreach (var flag in new[] {
                hg?.ChildControlWidth, hg?.ChildControlHeight, hg?.ChildForceExpandWidth, hg?.ChildForceExpandHeight,
                vg?.ChildControlWidth, vg?.ChildControlHeight, vg?.ChildForceExpandWidth, vg?.ChildForceExpandHeight,
            }) {
                if (flag != null) {
                    flag.StaticValue = false;
                }
            }
            if (hg != null) {
                hg.ChildScaleWidth.StaticValue = hg.ChildScaleHeight.StaticValue = true;
            }
            if (vg != null) {
                vg.ChildScaleWidth.StaticValue = vg.ChildScaleHeight.StaticValue = true;
            }
            obj.Config.ContentSizeFitterConfig ??= new ContentSizeFitterSettings();
            // Grows to the right and down from its top-left corner.
            obj.Config.RectTransformConfig.Pivot.StaticValue = new Vector2(0f, 1f);
            obj.ApplyComponent();
            obj.ApplyConfig();
            RebuildLayoutTree(obj);
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(obj.RectTransform, corners);
            var cfg = obj.Config.RectTransformConfig;
            cfg.SizeDelta.StaticValue = obj.RectTransform.sizeDelta;
            cfg.AnchoredPosition.StaticValue += el.ScreenToParentDelta(topLeft - (Vector2)corners[1]);
            cfg.ToUnity(obj.GameObject);
            LayoutRebuilder.ForceRebuildLayoutImmediate(obj.RectTransform);
        }
        var after = CaptureLayout(obj);
        void Set(LayoutState s) {
            RestoreLayout(obj, s);
            Save();
            Recollect();
            RefreshPanel();
        }
        undo.Push(new ActionCommand(() => Set(before), () => Set(after)));
        Save();
        Recollect();
        RefreshPanel();
    }

    // The average space between neighbours as they are now (in the group's own units).
    private static float CurrentGap(List<OvObject> sorted, Flow flow, RectTransform group) {
        if (sorted.Count < 2) {
            return 8f;
        }
        var c = new Vector3[4];
        float total = 0f;
        int count = 0;
        for (int i = 1; i < sorted.Count; i++) {
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(sorted[i - 1].RectTransform, c);
            float prevEnd = flow == Flow.Row ? c[2].x : c[0].y;
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(sorted[i].RectTransform, c);
            float nextStart = flow == Flow.Row ? c[0].x : c[1].y;
            total += flow == Flow.Row ? nextStart - prevEnd : prevEnd - nextStart;
            count++;
        }
        float scale = Mathf.Max(Mathf.Abs(flow == Flow.Row ? group.lossyScale.x : group.lossyScale.y), 1e-4f);
        return Mathf.Clamp(Mathf.Round(total / count / scale), 0f, 16f);
    }

    /// <summary>Wraps the selection in a new group laid out as a row or a column.</summary>
    public static void GroupAs(Flow flow) {
        GroupSelection();
        if (selection.Count == 1 && Primary is { } g && g.Object.Children.Count > 0) {
            SetFlow(flow);
        }
    }

    // Lines children up across the flow: start (top / left), center, end.
    private static TextAnchor CrossAlign(Flow flow, int where) => flow == Flow.Row
        ? where switch { 0 => TextAnchor.UpperLeft, 1 => TextAnchor.MiddleLeft, _ => TextAnchor.LowerLeft }
        : where switch { 0 => TextAnchor.UpperLeft, 1 => TextAnchor.UpperCenter, _ => TextAnchor.UpperRight };

    private static int CrossAlignIndex(TextAnchor a) => a switch {
        TextAnchor.MiddleLeft or TextAnchor.UpperCenter or TextAnchor.MiddleCenter => 1,
        TextAnchor.LowerLeft or TextAnchor.UpperRight or TextAnchor.LowerRight or TextAnchor.MiddleRight or TextAnchor.LowerCenter => 2,
        _ => 0,
    };
}
