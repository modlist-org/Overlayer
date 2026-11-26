using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// One editable HUD piece: an object (at any depth) inside an overlay canvas.
internal sealed class HudElement {
    public readonly OvCanvas Canvas;
    public readonly OvObject Object;
    public readonly RectTransform Rect;
    public readonly RectTransformSettings Config;
    private readonly LockKind lockKind;
    public readonly int Depth;
    /// <summary>Locked with the lock toggle (can't be picked on screen).</summary>
    public readonly bool UserLocked;

    private readonly TextMeshProUGUI text;

    private enum LockKind { None, Package, User, Layout }

    private HudElement(OvCanvas canvas, OvObject obj, LockKind lockKind, int depth, bool userLocked) {
        UserLocked = userLocked;
        Canvas = canvas;
        Object = obj;
        Rect = obj.RectTransform;
        Config = obj.Config.RectTransformConfig;
        this.lockKind = lockKind;
        Depth = depth;
        text = obj.GameObject.GetComponent<TextMeshProUGUI>();
    }

    public string Name => Object.GameObject != null ? Object.GameObject.name : "?";
    public bool IsAlive => Rect != null && Object.GameObject != null && Object.GameObject.activeInHierarchy;
    public bool Locked => lockKind != LockKind.None;
    /// <summary>Placed by its parent's layout group: can't be moved, but can change order.</summary>
    public bool LayoutDriven => lockKind == LockKind.Layout;
    /// <summary>Nothing about it can change (a layout group only takes its position).</summary>
    public bool ReadOnly => Locked && !LayoutDriven;
    public string LockReason => lockKind switch {
        LockKind.Package => HudEditor.Tr("HUD_LOCK_PACKAGE", "Installed package (read-only)"),
        LockKind.User => HudEditor.Tr("HUD_LOCK_USER", "Locked"),
        LockKind.Layout => HudEditor.Tr("HUD_LOCK_LAYOUT", "Positioned by a layout group"),
        _ => null,
    };

    // A tag/JS expression owns the value; dragging would fight it every frame.
    public bool CanMove => !Locked && !Config.AnchoredPosition.UseFx;
    public bool CanScale => !Locked && !Config.Scale.UseFx;
    public bool CanRotate => !Locked && !Config.Rotation.UseFx;
    // Width / height: not for groups (their box is their children) or sizes a fitter sets.
    public bool CanResizeX => CanResize(0);
    public bool CanResizeY => CanResize(1);

    internal bool IsFitterDriven(int axis) {
        var fitter = Object.GameObject.GetComponent<ContentSizeFitter>();
        return fitter != null && fitter.enabled
            && (axis == 0 ? fitter.horizontalFit : fitter.verticalFit) != ContentSizeFitter.FitMode.Unconstrained;
    }

    internal void SyncFittedLayout() {
        if (IsFitterDriven(0) || IsFitterDriven(1)) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
        }
    }

    internal void RefreshTextBounds() {
        if (text != null && text.enabled && !string.IsNullOrEmpty(text.text)) {
            text.ForceMeshUpdate();
        }
    }

    private bool CanResize(int axis) => !Locked && !Config.SizeDelta.UseFx && !IsGroup && !IsFitterDriven(axis);

    public struct State {
        public Vector2 Position;
        public Vector3 Scale;
        public float Rotation;
        public Vector2 Size;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
    }

    public bool CanAnchor => !Locked && !Config.AnchorMin.UseFx && !Config.AnchorMax.UseFx && !Config.Pivot.UseFx;

    public State Capture() => new() {
        Position = Config.AnchoredPosition.StaticValue,
        Scale = Config.Scale.StaticValue,
        Rotation = Config.Rotation.StaticValue,
        Size = Config.SizeDelta.StaticValue,
        AnchorMin = Config.AnchorMin.StaticValue,
        AnchorMax = Config.AnchorMax.StaticValue,
        Pivot = Config.Pivot.StaticValue,
    };

    public void Apply(State state) {
        if (CanAnchor) {
            Config.AnchorMin.StaticValue = state.AnchorMin;
            Config.AnchorMax.StaticValue = state.AnchorMax;
            Config.Pivot.StaticValue = state.Pivot;
        }
        if (CanMove) {
            Config.AnchoredPosition.StaticValue = state.Position;
        }
        if (CanScale) {
            Config.Scale.StaticValue = state.Scale;
        }
        if (CanRotate) {
            Config.Rotation.StaticValue = state.Rotation;
        }
        if (!Locked && !Config.SizeDelta.UseFx) {
            bool writeX = !IsFitterDriven(0);
            bool writeY = !IsFitterDriven(1);
            if (writeX || writeY) {
                Config.SizeDelta.StaticValue = new Vector2(
                    writeX ? state.Size.x : Config.SizeDelta.StaticValue.x,
                    writeY ? state.Size.y : Config.SizeDelta.StaticValue.y);
            }
        }
        Config.ToUnity(Object.GameObject);
    }

    // Moves by a screen-space offset, converted into the parent's local space.
    public Vector2 ScreenToParentDelta(Vector2 screenDelta) {
        var parent = Rect.parent;
        return parent == null ? screenDelta : (Vector2)parent.InverseTransformVector(new Vector3(screenDelta.x, screenDelta.y, 0f));
    }

    /// <summary>
    /// True when this object actually renders something visible on its own (not an empty container,
    /// disabled/fully-transparent Graphic, empty TMP text, or stencil-only Mask).
    /// </summary>
    public static bool IsVisiblyRendered(OvObject obj) {
        if (obj?.GameObject == null) {
            return false;
        }
        if (obj.GameObject.GetComponent<OvRainComponent>() != null) {
            return true;
        }
        var mesh = O5Kit.Behaviour.UnityUtils.SafeGetComponent<MeshRenderer>(obj.GameObject);
        if (mesh != null && mesh.enabled) {
            return true;
        }
        var graphic = obj.GameObject.GetComponent<Graphic>();
        if (graphic == null || !graphic.enabled || graphic.color.a <= 0.001f) {
            return false;
        }
        if (O5Kit.Behaviour.UnityUtils.TryCastComponent<TextMeshProUGUI>(graphic) is { } tmp && string.IsNullOrWhiteSpace(tmp.text)) {
            return false;
        }
        var mask = obj.GameObject.GetComponent<Mask>();
        if (mask != null && mask.enabled && !mask.showMaskGraphic) {
            return false;
        }
        return true;
    }

    public bool HasVisibleRender => IsVisiblyRendered(Object);

    /// <summary>
    /// Holds children and renders nothing itself. Never blocks clicks on its children
    /// (selectable from the canvas tree or right-click context menu).
    /// </summary>
    public bool IsGroup => Object.Children.Count > 0 && !HasVisibleRender;

    public void GetScreenCorners(Vector3[] corners) {
        if (TextCorners(Rect, text, corners)) {
            return;
        }
        // Nothing drawn of its own (a group): its box is what its children show, so a
        // full-screen layout group doesn't cover the whole screen for picking and snapping.
        if (IsGroup && ContentBounds(Object, out Vector2 min, out Vector2 max)) {
            corners[0] = new Vector3(min.x, min.y);
            corners[1] = new Vector3(min.x, max.y);
            corners[2] = new Vector3(max.x, max.y);
            corners[3] = new Vector3(max.x, min.y);
            return;
        }
        O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(Rect, corners);
    }

    private static bool TextCorners(RectTransform rect, TextMeshProUGUI tmp, Vector3[] corners) {
        if (tmp == null || !tmp.enabled || string.IsNullOrEmpty(tmp.text)) {
            return false;
        }
        Bounds b = tmp.textBounds;
        if (b.size.x <= 0.01f || b.size.y <= 0.01f) {
            return false;
        }
        Vector3 min = b.min, max = b.max;
        corners[0] = rect.TransformPoint(new Vector3(min.x, min.y, 0f));
        corners[1] = rect.TransformPoint(new Vector3(min.x, max.y, 0f));
        corners[2] = rect.TransformPoint(new Vector3(max.x, max.y, 0f));
        corners[3] = rect.TransformPoint(new Vector3(max.x, min.y, 0f));
        return true;
    }

    private static readonly Vector3[] scratch = new Vector3[4];

    // Screen box of everything visible under (and including) obj; groups add up their children.
    private static bool ContentBounds(OvObject obj, out Vector2 min, out Vector2 max) {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;
        foreach (var child in obj.Children) {
            if (child?.GameObject == null || !child.GameObject.activeInHierarchy || child.RectTransform == null) {
                continue;
            }
            bool drawn = IsVisiblyRendered(child);
            if (drawn) {
                if (!TextCorners(child.RectTransform, child.GameObject.GetComponent<TextMeshProUGUI>(), scratch)) {
                    O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(child.RectTransform, scratch);
                }
                foreach (var p in scratch) {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                any = true;
            }
            if (child.Children.Count > 0 && ContentBounds(child, out var cMin, out var cMax)) {
                min = Vector2.Min(min, cMin);
                max = Vector2.Max(max, cMax);
                any = true;
            }
        }
        return any;
    }

    public Vector2 ScreenPivot => Rect.position;

    /// <summary>Resizes the rect to the visible text (keeps the pivot in place).</summary>
    public bool FitToText() {
        if (text == null || Config.SizeDelta.UseFx) {
            return false;
        }
        text.ForceMeshUpdate();
        Vector2 size = text.GetPreferredValues(text.text);
        if (size.x <= 0f || size.y <= 0f) {
            return false;
        }
        Config.SizeDelta.StaticValue = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
        Config.ToUnity(Object.GameObject);
        return true;
    }

    private static PropertyInfo isPackageProperty;

    internal static bool IsPackage(OvCanvas canvas) {
        isPackageProperty ??= typeof(OvCanvas).GetProperty("IsPackage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        // Unknown => treat as read-only rather than silently editing a package.
        return isPackageProperty == null || isPackageProperty.GetValue(canvas) is true;
    }

    // Depth-first, parents before children: later entries draw on top.
    public static List<HudElement> Collect() {
        var list = new List<HudElement>();
        foreach (var canvas in OverlayCore.Canvases) {
            if (canvas?.GameObject == null || !canvas.GameObject.activeInHierarchy) {
                continue;
            }
            LockKind canvasLock = IsPackage(canvas) ? LockKind.Package : LockKind.None;
            foreach (var obj in canvas.OvObjects) {
                Add(list, canvas, obj, canvasLock, 0);
            }
        }
        return list;
    }

    private static void Add(List<HudElement> list, OvCanvas canvas, OvObject obj, LockKind inheritedLock, int depth) {
        if (obj?.GameObject == null || !obj.GameObject.activeInHierarchy) {
            return;
        }
        if (obj.RectTransform != null && obj.Config?.RectTransformConfig != null) {
            var parent = obj.GameObject.transform.parent;
            bool layoutDriven = parent != null && parent.GetComponent<LayoutGroup>() != null;
            bool userLocked = HudEditor.IsUserLocked(canvas, obj);
            LockKind reason = inheritedLock != LockKind.None ? inheritedLock : userLocked ? LockKind.User : layoutDriven ? LockKind.Layout : LockKind.None;
            list.Add(new HudElement(canvas, obj, reason, depth, userLocked || inheritedLock == LockKind.User));
            // A locked object's children are locked with it.
            if (userLocked && inheritedLock == LockKind.None) {
                inheritedLock = LockKind.User;
            }
        }
        foreach (var child in obj.Children) {
            Add(list, canvas, child, inheritedLock, depth + 1);
        }
    }
}
