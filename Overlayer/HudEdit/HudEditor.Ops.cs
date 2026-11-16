using Overlayer.Core;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

internal enum Placement {
    MainCanvas,
    NewCanvas,
    Sibling,
    Child,
    Parent,
}

internal enum NewKind {
    Text,
    Image,
}

// Create / delete / duplicate / wrap. Deleted objects are parked (inactive) instead of
// disposed, so undo re-attaches the very same instance and older undo steps keep working.
internal static partial class HudEditor {
    private static GameObject trashRoot;
    private static readonly List<OvObject> trash = [];

    // Where an object lives: a canvas (top level) or a parent object.
    private readonly struct Slot(OvCanvas canvas, OvObject parent, int index) {
        public readonly OvCanvas Canvas = canvas;
        public readonly OvObject Parent = parent;
        public readonly int Index = index;
    }

    private static Slot SlotOf(HudElement el) {
        var parent = el.Object.Parent;
        return parent != null
            ? new Slot(el.Canvas, parent, parent.Children.IndexOf(el.Object))
            : new Slot(el.Canvas, null, el.Canvas.OvObjects.IndexOf(el.Object));
    }

    private static void AttachAt(OvObject obj, Slot slot) {
        if (slot.Parent != null) {
            slot.Parent.Attach(obj);
            slot.Parent.SetChildIndex(obj, slot.Index < 0 ? slot.Parent.Children.Count : slot.Index);
        } else {
            slot.Canvas.Attach(obj);
            var list = slot.Canvas.OvObjects;
            if (slot.Index >= 0 && slot.Index < list.Count - 1) {
                list.Remove(obj);
                list.Insert(slot.Index, obj);
                for (int i = 0; i < list.Count; i++) {
                    list[i].GameObject.transform.SetSiblingIndex(i);
                }
            }
        }
        obj.ApplyConfig();
    }

    private static void DetachFrom(OvObject obj, Slot slot) {
        if (slot.Parent != null) {
            obj.Detach();
        } else {
            slot.Canvas.Detach(obj);
        }
    }

    private static void ToTrash(OvObject obj, Slot slot) {
        DetachFrom(obj, slot);
        if (trashRoot == null) {
            trashRoot = new GameObject("OverlayerPlus.HudTrash");
            trashRoot.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(trashRoot);
        }
        obj.GameObject.transform.SetParent(trashRoot.transform, false);
        if (!trash.Contains(obj)) {
            trash.Add(obj);
        }
    }

    private static void FromTrash(OvObject obj, Slot slot) {
        trash.Remove(obj);
        AttachAt(obj, slot);
    }

    // Undo history dies with the editor, so parked objects are really deleted then.
    private static void EmptyTrash() {
        foreach (var obj in trash) {
            try {
                if (obj.GameObject != null && trashRoot != null && obj.GameObject.transform.parent == trashRoot.transform) {
                    obj.Dispose();
                }
            } catch {
            }
        }
        trash.Clear();
    }

    // ---- targets ----

    private static OvCanvas MainCanvas(bool create) {
        string name = string.IsNullOrWhiteSpace(MainCore.Conf.HudMainCanvas.Value) ? "HUD" : MainCore.Conf.HudMainCanvas.Value;
        var canvas = OverlayCore.Canvases.FirstOrDefault(c => c != null && c.Config.Name.Value == name && !HudElement.IsPackage(c));
        if (canvas != null || !create) {
            return canvas;
        }
        canvas = OverlayCore.CreateOvCanvas();
        canvas.Config.Name.Value = name;
        canvas.ApplyConfig();
        return canvas;
    }

    private static OvCanvas NewCanvas() {
        int n = 1;
        while (OverlayCore.Canvases.Any(c => c?.Config.Name.Value == $"HUD {n}")) {
            n++;
        }
        var canvas = OverlayCore.CreateOvCanvas();
        canvas.Config.Name.Value = $"HUD {n}";
        canvas.ApplyConfig();
        return canvas;
    }

    private static Slot ResolveSlot(Placement placement) {
        var anchor = Primary;
        if (anchor != null && anchor.Locked) {
            anchor = null;
        }
        switch (placement) {
            case Placement.Sibling when anchor != null: {
                var s = SlotOf(anchor);
                return new Slot(s.Canvas, s.Parent, s.Index + 1);
            }
            case Placement.Child when anchor != null:
                return new Slot(anchor.Canvas, anchor.Object, -1);
            case Placement.NewCanvas:
                return new Slot(NewCanvas(), null, -1);
            default:
                return new Slot(MainCanvas(true), null, -1);
        }
    }

    private static OvObject NewObject(NewKind kind) {
        var obj = new OvObject();
        obj.Config.RectTransformConfig ??= new RectTransformSettings();
        obj.Config.CanvasGroupConfig ??= new CanvasGroupSettings();
        if (kind == NewKind.Text) {
            obj.Config.Name.Value = "Text";
            obj.Config.TextConfig = new TextMeshProUGUISettings();
            obj.Config.TextEngineConfig = new OvTextSettings();
            obj.Config.ContentSizeFitterConfig ??= new ContentSizeFitterSettings();
        } else {
            obj.Config.Name.Value = "Image";
            obj.Config.ImageConfig = new ImageSettings();
            obj.Config.RectTransformConfig.SizeDelta.StaticValue = new Vector2(160f, 160f);
        }
        obj.ApplyComponent();
        return obj;
    }

    // A canvas created this frame isn't laid out yet (its rect still sits at the origin),
    // so placements are repeated once the next frame has sized it.
    private static readonly List<(OvObject Obj, Vector2 Screen, int Frame)> pendingPlace = [];

    private static void PlaceAtSoon(OvObject obj, Vector2 screen) {
        PlaceAt(obj, screen);
        pendingPlace.Add((obj, screen, Time.frameCount));
    }

    private static void FlushPendingPlace() {
        for (int i = pendingPlace.Count - 1; i >= 0; i--) {
            var (obj, screen, frame) = pendingPlace[i];
            if (Time.frameCount <= frame) {
                continue;
            }
            pendingPlace.RemoveAt(i);
            if (obj.GameObject != null && obj.GameObject.activeInHierarchy) {
                PlaceAt(obj, screen);
            }
            RefreshInspectorSoon();
            SyncPanelValues();
        }
    }

    // Puts the object's pivot on a screen point.
    private static void PlaceAt(OvObject obj, Vector2 screen) {
        var cfg = obj.Config.RectTransformConfig;
        if (cfg == null || obj.RectTransform == null) {
            return;
        }
        cfg.ToUnity(obj.GameObject);
        Vector2 now = obj.RectTransform.position;
        var parent = obj.RectTransform.parent;
        Vector2 delta = parent == null ? screen - now : (Vector2)parent.InverseTransformVector(screen - now);
        cfg.AnchoredPosition.StaticValue += delta;
        cfg.ToUnity(obj.GameObject);
    }

    // ---- operations ----

    public static void Create(NewKind kind, Placement placement, Vector2? screen) {
        if (placement == Placement.Parent) {
            WrapSelection();
            return;
        }
        var slot = ResolveSlot(placement);
        if (slot.Canvas == null) {
            return;
        }
        var obj = NewObject(kind);
        AttachAt(obj, slot);
        PlaceAtSoon(obj, screen ?? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        var placed = new Slot(slot.Canvas, slot.Parent, slot.Parent != null ? slot.Parent.Children.IndexOf(obj) : slot.Canvas.OvObjects.IndexOf(obj));
        undo.Push(new ActionCommand(() => ToTrash(obj, placed), () => FromTrash(obj, placed)));
        AfterStructureChange(obj);
    }

    public static void DeleteSelection() {
        var targets = selection.Where(e => e.IsAlive && !e.Locked).ToList();
        // Deleting a parent already takes its children along.
        targets.RemoveAll(e => targets.Any(o => o != e && IsAncestor(o.Object, e.Object)));
        if (targets.Count == 0) {
            return;
        }
        var removed = targets.Select(e => (e.Object, Slot: SlotOf(e))).ToList();
        // Detach from the back so the stored indices stay valid when restoring front-to-back.
        foreach (var (obj, slot) in Enumerable.Reverse(removed)) {
            ToTrash(obj, slot);
        }
        undo.Push(new ActionCommand(
            () => { foreach (var (obj, slot) in removed) FromTrash(obj, slot); },
            () => { foreach (var (obj, slot) in Enumerable.Reverse(removed)) ToTrash(obj, slot); }));
        AfterStructureChange(null);
    }

    public static void DuplicateSelection() {
        var targets = selection.Where(e => e.IsAlive && !e.Locked).ToList();
        targets.RemoveAll(e => targets.Any(o => o != e && IsAncestor(o.Object, e.Object)));
        if (targets.Count == 0) {
            return;
        }
        var created = new List<(OvObject Obj, Slot Slot)>();
        foreach (var el in targets) {
            var src = SlotOf(el);
            var copy = el.Object.Clone();
            if (!copy.Config.Name.Value.EndsWith(" Copy")) {
                copy.Config.Name.Value += " Copy";
            }
            var slot = new Slot(src.Canvas, src.Parent, src.Index + 1);
            AttachAt(copy, slot);
            var cfg = copy.Config.RectTransformConfig;
            if (cfg != null && !cfg.AnchoredPosition.UseFx) {
                Vector2 screen = (Vector2)el.Rect.position + new Vector2(20f, -20f) * UiScale;
                PlaceAt(copy, screen);
            }
            int index = slot.Parent != null ? slot.Parent.Children.IndexOf(copy) : slot.Canvas.OvObjects.IndexOf(copy);
            created.Add((copy, new Slot(slot.Canvas, slot.Parent, index)));
        }
        undo.Push(new ActionCommand(
            () => { foreach (var (obj, slot) in Enumerable.Reverse(created)) ToTrash(obj, slot); },
            () => { foreach (var (obj, slot) in created) FromTrash(obj, slot); }));
        AfterStructureChange(null, created.Select(c => c.Obj));
    }

    // "Parent" placement: a new group takes the selected object's place and holds it.
    private static void WrapSelection() => GroupSelection();

    // Draw order among siblings: +1 = one step toward the front.
    public static void MoveOrder(int direction) {
        var target = Primary;
        if (target == null || !target.IsAlive || (target.Locked && !target.LayoutDriven)) {
            return;
        }
        var from = SlotOf(target);
        int count = from.Parent != null ? from.Parent.Children.Count : from.Canvas.OvObjects.Count;
        int to = Mathf.Clamp(from.Index + direction, 0, count - 1);
        if (to == from.Index) {
            return;
        }
        var obj = target.Object;
        void MoveTo(int index) {
            if (from.Parent != null) {
                from.Parent.SetChildIndex(obj, index);
                return;
            }
            var list = from.Canvas.OvObjects;
            list.Remove(obj);
            list.Insert(index, obj);
            for (int i = 0; i < list.Count; i++) {
                list[i].GameObject.transform.SetSiblingIndex(i);
            }
        }
        MoveTo(to);
        undo.Push(new ActionCommand(() => MoveTo(from.Index), () => MoveTo(to)));
        AfterStructureChange(obj);
    }

    private static bool IsAncestor(OvObject ancestor, OvObject obj) {
        for (var p = obj.Parent; p != null; p = p.Parent) {
            if (ReferenceEquals(p, ancestor)) {
                return true;
            }
        }
        return false;
    }

    private static void AfterStructureChange(OvObject select, IEnumerable<OvObject> selectMany = null) {
        Save();
        Recollect();
        var wanted = selectMany?.ToList() ?? (select != null ? [select] : []);
        SetSelection(elements.Where(e => wanted.Any(w => ReferenceEquals(w, e.Object))));
    }
}
