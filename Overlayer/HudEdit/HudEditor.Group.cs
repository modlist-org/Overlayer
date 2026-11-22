using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.HudEdit;

// Group (Ctrl+G) / ungroup (Ctrl+Shift+G). Objects keep their place on screen either way.
internal static partial class HudEditor {
    private static bool SameParent(Slot a, Slot b) => ReferenceEquals(a.Canvas, b.Canvas) && ReferenceEquals(a.Parent, b.Parent);

    private static void DetachAny(OvObject obj, OvCanvas canvas) {
        if (obj.Parent != null) {
            obj.Detach();
        } else {
            canvas.Detach(obj);
        }
    }

    /// <summary>Puts the selection into a new group sized to their combined box.</summary>
    public static void GroupSelection() {
        var targets = selection.Where(e => e.IsAlive && !e.Locked).ToList();
        targets.RemoveAll(e => targets.Any(o => o != e && IsAncestor(o.Object, e.Object)));
        if (targets.Count == 0 || !UnionBounds(targets, out float minX, out float maxX, out float minY, out float maxY)) {
            return;
        }

        // The group lives where the primary (else first) object lives, at the front-most of them.
        var anchor = targets.Contains(Primary) ? Primary : targets[0];
        var anchorSlot = SlotOf(anchor);
        var members = targets
            .Select(e => (Obj: e.Object, Slot: SlotOf(e), Pos: e.Config.AnchoredPosition.StaticValue))
            .OrderBy(m => elements.FindIndex(e => ReferenceEquals(e.Object, m.Obj)))
            .ToList();
        int index = members.Where(m => SameParent(m.Slot, anchorSlot)).Min(m => m.Slot.Index);
        var groupSlot = new Slot(anchorSlot.Canvas, anchorSlot.Parent, index);

        Transform parent = anchor.Rect.parent;
        Vector2 screenSize = new(maxX - minX, maxY - minY);
        Vector2 center = new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 size = parent != null ? (Vector2)parent.InverseTransformVector(screenSize) : screenSize;

        var group = new OvObject();
        group.Config.Name.Value = "Group";
        var g = group.Config.RectTransformConfig ??= new RectTransformSettings();
        g.AnchorMin.StaticValue = g.AnchorMax.StaticValue = g.Pivot.StaticValue = new Vector2(0.5f, 0.5f);
        g.SizeDelta.StaticValue = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        group.ApplyComponent();

        void Do() {
            var world = members.Select(m => (Vector2)m.Obj.RectTransform.position).ToList();
            // Back to front, so the indices still to be detached stay valid.
            foreach (var m in members.OrderByDescending(m => m.Slot.Index)) {
                DetachFrom(m.Obj, m.Slot);
            }
            trash.Remove(group);
            AttachAt(group, groupSlot);
            PlaceAt(group, center);
            for (int i = 0; i < members.Count; i++) {
                group.Attach(members[i].Obj);
                members[i].Obj.ApplyConfig();
                PlaceAt(members[i].Obj, world[i]);
                RebuildLayoutTree(members[i].Obj);
            }
        }

        void Undo() {
            foreach (var m in members) {
                m.Obj.Detach();
            }
            ToTrash(group, groupSlot);
            foreach (var m in members.OrderBy(m => m.Slot.Index)) {
                AttachAt(m.Obj, m.Slot);
                var cfg = m.Obj.Config.RectTransformConfig;
                cfg.AnchoredPosition.StaticValue = m.Pos;
                cfg.ToUnity(m.Obj.GameObject);
            }
        }

        Do();
        undo.Push(new ActionCommand(() => {
            Undo();
            SelectObjects(members.Select(m => m.Obj));
        }, () => {
            Do();
            SelectObjects([group]);
        }));
        AfterStructureChange(group);
    }

    /// <summary>Takes the children out of every selected group and removes the group.</summary>
    public static void UngroupSelection() {
        var groups = selection.Where(e => e.IsAlive && !e.Locked && e.Object.Children.Count > 0).ToList();
        groups.RemoveAll(e => groups.Any(o => o != e && IsAncestor(o.Object, e.Object)));
        if (groups.Count == 0) {
            return;
        }

        var ops = groups.Select(e => (
            Group: e.Object,
            Slot: SlotOf(e),
            Children: e.Object.Children.Select(c => (Obj: c, Before: CaptureOf(c))).ToList()
        )).ToList();

        void Do() {
            // Back to front so earlier groups' indices stay valid.
            foreach (var op in ops.OrderByDescending(o => o.Slot.Index)) {
                var gcfg = op.Group.Config.RectTransformConfig;
                Vector3 groupScale = gcfg?.Scale.StaticValue ?? Vector3.one;
                float groupRotation = gcfg?.Rotation.StaticValue ?? 0f;
                var world = op.Children.Select(c => (Vector2)c.Obj.RectTransform.position).ToList();
                for (int i = 0; i < op.Children.Count; i++) {
                    var child = op.Children[i].Obj;
                    child.Detach();
                    AttachAt(child, new Slot(op.Slot.Canvas, op.Slot.Parent, op.Slot.Index + 1 + i));
                    // Bake the group's own scale / rotation into the child so it looks the same.
                    var cfg = child.Config.RectTransformConfig;
                    if (cfg != null && !cfg.Scale.UseFx) {
                        cfg.Scale.StaticValue = Vector3.Scale(cfg.Scale.StaticValue, groupScale);
                    }
                    if (cfg != null && !cfg.Rotation.UseFx) {
                        cfg.Rotation.StaticValue += groupRotation;
                    }
                    PlaceAt(child, world[i]);
                }
                ToTrash(op.Group, op.Slot);
            }
        }

        void Undo() {
            foreach (var op in ops.OrderBy(o => o.Slot.Index)) {
                FromTrash(op.Group, op.Slot);
                foreach (var (child, before) in op.Children) {
                    DetachAny(child, op.Slot.Canvas);
                    op.Group.Attach(child);
                    RestoreOf(child, before);
                }
            }
        }

        Do();
        var children = ops.SelectMany(o => o.Children.Select(c => c.Obj)).ToList();
        undo.Push(new ActionCommand(() => {
            Undo();
            SelectObjects(ops.Select(o => o.Group));
        }, () => {
            Do();
            SelectObjects(children);
        }));
        AfterStructureChange(null, children);
    }

    private static HudElement.State CaptureOf(OvObject obj) {
        var cfg = obj.Config.RectTransformConfig;
        return cfg == null ? default : new HudElement.State {
            Position = cfg.AnchoredPosition.StaticValue,
            Scale = cfg.Scale.StaticValue,
            Rotation = cfg.Rotation.StaticValue,
        };
    }

    private static void RestoreOf(OvObject obj, HudElement.State state) {
        var cfg = obj.Config.RectTransformConfig;
        if (cfg == null) {
            return;
        }
        cfg.AnchoredPosition.StaticValue = state.Position;
        cfg.Scale.StaticValue = state.Scale;
        cfg.Rotation.StaticValue = state.Rotation;
        cfg.ToUnity(obj.GameObject);
    }

    // After undo/redo the wrappers are rebuilt; select by object.
    private static void SelectObjects(IEnumerable<OvObject> objects) {
        Save();
        Recollect();
        var wanted = objects.ToList();
        SetSelection(elements.Where(e => wanted.Any(w => ReferenceEquals(w, e.Object))));
    }
}
