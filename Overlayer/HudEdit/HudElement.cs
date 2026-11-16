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
    public readonly string LockReason;
    public readonly int Depth;

    private readonly TextMeshProUGUI text;

    private HudElement(OvCanvas canvas, OvObject obj, string lockReason, int depth) {
        Canvas = canvas;
        Object = obj;
        Rect = obj.RectTransform;
        Config = obj.Config.RectTransformConfig;
        LockReason = lockReason;
        Depth = depth;
        text = obj.GameObject.GetComponent<TextMeshProUGUI>();
    }

    public string Name => Object.GameObject != null ? Object.GameObject.name : "?";
    public bool IsAlive => Rect != null && Object.GameObject != null && Object.GameObject.activeInHierarchy;
    public bool Locked => LockReason != null;

    // A tag/JS expression owns the value; dragging would fight it every frame.
    public bool CanMove => !Locked && !Config.AnchoredPosition.UseFx;
    public bool CanScale => !Locked && !Config.Scale.UseFx;
    public bool CanRotate => !Locked && !Config.Rotation.UseFx;

    public struct State {
        public Vector2 Position;
        public Vector3 Scale;
        public float Rotation;
    }

    public State Capture() => new() {
        Position = Config.AnchoredPosition.StaticValue,
        Scale = Config.Scale.StaticValue,
        Rotation = Config.Rotation.StaticValue,
    };

    public void Apply(State state) {
        if (CanMove) {
            Config.AnchoredPosition.StaticValue = state.Position;
        }
        if (CanScale) {
            Config.Scale.StaticValue = state.Scale;
        }
        if (CanRotate) {
            Config.Rotation.StaticValue = state.Rotation;
        }
        Config.ToUnity(Object.GameObject);
    }

    // Moves by a screen-space offset, converted into the parent's local space.
    public Vector2 ScreenToParentDelta(Vector2 screenDelta) {
        var parent = Rect.parent;
        return parent == null ? screenDelta : (Vector2)parent.InverseTransformVector(new Vector3(screenDelta.x, screenDelta.y, 0f));
    }

    /// <summary>
    /// Screen-space corners BL, TL, TR, BR (overlay canvases render in screen pixels).
    /// Text uses the visible glyph bounds: a TMP rect is often much bigger than its text.
    /// </summary>
    public void GetScreenCorners(Vector3[] corners) {
        if (text != null && text.enabled && !string.IsNullOrEmpty(text.text)) {
            Bounds b = text.textBounds;
            if (b.size.x > 0.01f && b.size.y > 0.01f) {
                Vector3 min = b.min, max = b.max;
                corners[0] = Rect.TransformPoint(new Vector3(min.x, min.y, 0f));
                corners[1] = Rect.TransformPoint(new Vector3(min.x, max.y, 0f));
                corners[2] = Rect.TransformPoint(new Vector3(max.x, max.y, 0f));
                corners[3] = Rect.TransformPoint(new Vector3(max.x, min.y, 0f));
                return;
            }
        }
        Rect.GetWorldCorners(corners);
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
            string canvasLock = IsPackage(canvas) ? "Installed package (read-only)" : null;
            foreach (var obj in canvas.OvObjects) {
                Add(list, canvas, obj, canvasLock, 0);
            }
        }
        return list;
    }

    private static void Add(List<HudElement> list, OvCanvas canvas, OvObject obj, string inheritedLock, int depth) {
        if (obj?.GameObject == null || !obj.GameObject.activeInHierarchy) {
            return;
        }
        if (obj.RectTransform != null && obj.Config?.RectTransformConfig != null) {
            var parent = obj.GameObject.transform.parent;
            bool layoutDriven = parent != null && parent.GetComponent<LayoutGroup>() != null;
            list.Add(new HudElement(canvas, obj, inheritedLock ?? (layoutDriven ? "Positioned by a layout group" : null), depth));
        }
        foreach (var child in obj.Children) {
            Add(list, canvas, child, inheritedLock, depth + 1);
        }
    }
}
