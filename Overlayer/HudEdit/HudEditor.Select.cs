using Overlayer.Core;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.HudEdit;

// Figma-style picking:
// - a click picks at the level you're working in: the outermost group, or a sibling of what's
//   selected once you've gone inside a group; double-click goes one level deeper; Ctrl+click
//   picks the deepest object right away.
// - dragging on empty space draws a selection box (Shift adds, Ctrl picks inside groups).
// - locked objects can't be picked on screen (only from the layers list).
internal static partial class HudEditor {
    private const float DoubleClickSeconds = 0.35f;

    private static float lastClickTime = -1f;
    private static HudElement lastClickElement;
    private static Vector2 marqueeStart;
    private static readonly List<HudElement> marqueeBase = [];

    private static HudElement ElementOf(OvObject obj) => obj == null ? null : elements.FirstOrDefault(e => ReferenceEquals(e.Object, obj));

    /// <summary>From the outermost ancestor down to <paramref name="deep"/> (only objects the editor shows).</summary>
    private static List<HudElement> Chain(HudElement deep) {
        var chain = new List<HudElement>();
        for (var o = deep?.Object; o != null; o = o.Parent) {
            var el = ElementOf(o);
            if (el != null) {
                chain.Insert(0, el);
            }
        }
        return chain;
    }

    private static bool SameLevel(HudElement a, HudElement b)
        => ReferenceEquals(a.Canvas, b.Canvas) && ReferenceEquals(a.Object.Parent, b.Object.Parent);

    private static HudElement ResolvePick(HudElement deep, bool deepest) {
        if (deep == null || deepest) {
            return deep;
        }
        var chain = Chain(deep);
        if (chain.Count == 0) {
            return deep;
        }
        // Stay at the level of the current selection (or inside it), else the outermost group.
        var p = Primary;
        if (p != null && p.IsAlive) {
            foreach (var el in chain) {
                if (SameLevel(el, p)) {
                    return el;
                }
            }
        }
        return chain[0];
    }

    // Top to bottom, every object whose box holds the point (for "Select layer").
    private static List<HudElement> AllUnder(Vector2 mouse) {
        var list = new List<HudElement>();
        for (int i = elements.Count - 1; i >= 0; i--) {
            var el = elements[i];
            if (!el.IsAlive || el.UserLocked || el.IsGroup) {
                continue;
            }
            el.GetScreenCorners(corners);
            if (InQuad(mouse, corners)) {
                list.Add(el);
            }
        }
        return list;
    }

    /// <summary>A click on <paramref name="picked"/>; a second quick click on it goes a level deeper.</summary>
    private static HudElement DiveOnDoubleClick(HudElement picked, HudElement deep) {
        bool isDouble = picked != null && ReferenceEquals(picked, lastClickElement)
            && Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
        lastClickTime = Time.unscaledTime;
        lastClickElement = picked;
        if (!isDouble || deep == null) {
            return picked;
        }
        var chain = Chain(deep);
        int i = chain.IndexOf(picked);
        if (i < 0 || i + 1 >= chain.Count) {
            return picked;
        }
        lastClickElement = chain[i + 1];
        return chain[i + 1];
    }

    // ---- selection box ----

    private static void BeginMarquee(Vector2 mouse) {
        drag = DragMode.Marquee;
        marqueeStart = mouse;
        dragMouseStart = mouse;
        dragChanged = false;
        marqueeBase.Clear();
        if (Shift) {
            marqueeBase.AddRange(selection);
        }
    }

    private static void UpdateMarquee(Vector2 mouse) {
        if (!dragChanged && (mouse - marqueeStart).sqrMagnitude < 16f) {
            return;
        }
        dragChanged = true;
        float x0 = Mathf.Min(marqueeStart.x, mouse.x), x1 = Mathf.Max(marqueeStart.x, mouse.x);
        float y0 = Mathf.Min(marqueeStart.y, mouse.y), y1 = Mathf.Max(marqueeStart.y, mouse.y);
        var hits = new List<HudElement>();
        foreach (var el in elements) {
            if (!el.IsAlive || el.UserLocked) {
                continue;
            }
            // Outermost objects only, unless Ctrl: then the innermost ones.
            bool leaf = !elements.Any(o => !ReferenceEquals(o, el) && IsAncestor(el.Object, o.Object));
            if (Ctrl ? !leaf : el.Depth != 0) {
                continue;
            }
            var b = BoxOf(el);
            // A box around the whole drag (a full-screen layout, a background) is what you
            // dragged inside of, not something you meant to pick.
            bool around = b.Contains(new Box(x0, x1, y0, y1));
            if (!around && b.MaxX >= x0 && b.MinX <= x1 && b.MaxY >= y0 && b.MinY <= y1) {
                hits.Add(el);
            }
        }
        selection.Clear();
        selection.AddRange(marqueeBase.Concat(hits).Distinct());
        RefreshPanel();
    }

    private static void EndMarquee() {
        drag = DragMode.None;
        if (!dragChanged && !Shift) {
            Select(null);
        }
    }

    private static Rect MarqueeRect(Vector2 mouse) => Rect.MinMaxRect(
        Mathf.Min(marqueeStart.x, mouse.x), Mathf.Min(marqueeStart.y, mouse.y),
        Mathf.Max(marqueeStart.x, mouse.x), Mathf.Max(marqueeStart.y, mouse.y));

    // ---- stacking order ----

    /// <summary>Front (true) or back (false) of its siblings.</summary>
    public static void MoveToEnd(bool front) {
        var target = Primary;
        if (target == null || !target.IsAlive || target.Locked) {
            return;
        }
        var slot = SlotOf(target);
        int count = slot.Parent != null ? slot.Parent.Children.Count : slot.Canvas.OvObjects.Count;
        MoveOrder((front ? count - 1 : 0) - slot.Index);
    }

    // ---- lock / hide ----

    // Locks live in the settings by canvas name + object path, so they survive restarts.
    internal static string LockKey(OvCanvas canvas, OvObject obj) {
        var parts = new List<string>();
        for (var o = obj; o != null; o = o.Parent) {
            var siblings = o.Parent != null ? o.Parent.Children : canvas.OvObjects;
            parts.Insert(0, $"{siblings.IndexOf(o)}:{o.Config.Name.Value}");
        }
        return canvas.Config.Name.Value + "/" + string.Join("/", parts);
    }

    internal static bool IsUserLocked(OvCanvas canvas, OvObject obj)
        => MainCore.Conf.HudLocked.Value?.Contains(LockKey(canvas, obj)) == true;

    public static void SetLocked(OvCanvas canvas, OvObject obj, bool locked) {
        var list = MainCore.Conf.HudLocked.Value ?? [];
        string key = LockKey(canvas, obj);
        list.Remove(key);
        if (locked) {
            list.Add(key);
        }
        MainCore.Conf.HudLocked.Value = list;
        MainCore.ConfMgr.RequestSave();
        if (locked) {
            selection.RemoveAll(e => ReferenceEquals(e.Object, obj));
        }
        Recollect();
        RefreshPanel();
    }

    public static void ToggleLockSelection() {
        var targets = selection.Where(e => e.IsAlive).ToList();
        foreach (var el in targets) {
            SetLocked(el.Canvas, el.Object, !el.UserLocked);
        }
    }

    /// <summary>Shows / hides an object for real (its Visible setting), undoable.</summary>
    public static void SetHidden(IEnumerable<OvObject> objects, bool hidden) {
        // Only the ones that actually change (visible ones when hiding, hidden ones when showing).
        // (Visible = Enabled, so when hiding pick Enabled == true, when showing Enabled == false.)
        var list = objects.Where(o => o?.Config != null && !o.Config.Enabled.UseFx && o.Config.Enabled.StaticValue == hidden).ToList();
        if (list.Count == 0) {
            return;
        }
        void Set(bool hide) {
            foreach (var o in list) {
                o.Config.Enabled.StaticValue = !hide;
                o.ApplyConfig();
            }
            Save();
            Recollect();
            RefreshPanel();
        }
        Set(hidden);
        undo.Push(new ActionCommand(() => Set(!hidden), () => Set(hidden)));
    }

    public static void HideSelection() => SetHidden(selection.Where(e => e.IsAlive && !e.Locked).Select(e => e.Object).ToList(), true);
}
