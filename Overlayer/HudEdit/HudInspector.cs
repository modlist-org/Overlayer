using O5Kit.Core;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.UI.Overlay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.HudEdit;

// The canvas editor's property inspector, built into the HUD editor's side panel, so every
// component, Fx toggle and add/remove-component control behaves exactly like in the canvas editor.
internal sealed class HudInspector : IDisposable {
    private readonly RectTransform content;
    private readonly List<O5Object> controls = [];
    private readonly Action changed;
    private readonly OvObject obj;
    private bool disposed;

    private HudInspector(RectTransform content, OvObject obj, Action changed) {
        this.content = content;
        this.obj = obj;
        this.changed = changed;
    }

    /// <summary><paramref name="changed"/> runs after every applied edit.</summary>
    public static HudInspector ForObject(RectTransform content, OvObject obj, Action changed = null) {
        var inspector = new HudInspector(content, obj, changed);
        inspector.Rebuild();
        return inspector;
    }

    /// <summary>Rebuilds from the current values (after the object changed elsewhere).</summary>
    public void Rebuild() {
        if (disposed || obj == null) {
            return;
        }
        Clear();
        var builder = new OvInspectorBuilder(
            content,
            controls,
            () => {
                obj.ApplyConfig();
                Notify();
            },
            () => {
                OverlayCore.SaveAllCanvases();
                OverlayCore.SaveAllObjects();
                Notify();
            },
            // Adding/removing a component changes the layout: rebuild next frame
            // (the builder is still running inside the click handler).
            () => MainThread.Enqueue(Rebuild),
            Notify
        );
        try {
            builder.BuildObject(obj);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(HudInspector)}] Build failed: {e}");
        }
    }

    private void Notify() {
        try {
            changed?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(HudInspector)}] Change handler failed: {e.Message}");
        }
    }

    private void Clear() {
        foreach (var control in controls) {
            try {
                control.Dispose();
            } catch {
            }
        }
        controls.Clear();
        if (content == null) {
            return;
        }
        for (int i = content.childCount - 1; i >= 0; i--) {
            Object.Destroy(content.GetChild(i).gameObject);
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        Clear();
        disposed = true;
    }
}
