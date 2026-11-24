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
    private readonly OvCanvas canvas;
    private readonly Action<string> renamed;
    private readonly Action<RectTransform> built;
    // The cards made last time: some may have been moved out of content (into windows).
    private readonly List<GameObject> cards = [];
    private bool disposed;

    private HudInspector(RectTransform content, OvObject obj, Action changed, Action<RectTransform> built,
        OvCanvas canvas = null, Action<string> renamed = null) {
        this.content = content;
        this.obj = obj;
        this.canvas = canvas;
        this.renamed = renamed;
        this.changed = changed;
        this.built = built;
    }

    /// <summary><paramref name="changed"/> runs after every applied edit, <paramref name="built"/> after every (re)build.</summary>
    public static HudInspector ForObject(RectTransform content, OvObject obj, Action changed = null, Action<RectTransform> built = null) {
        var inspector = new HudInspector(content, obj, changed, built);
        inspector.Rebuild();
        return inspector;
    }

    /// <summary>A canvas's own settings (name, opacity, rendering, scaling, rect), as in the canvas editor.</summary>
    public static HudInspector ForCanvas(RectTransform content, OvCanvas canvas, Action changed, Action<RectTransform> built, Action<string> renamed) {
        var inspector = new HudInspector(content, null, changed, built, canvas, renamed);
        inspector.Rebuild();
        return inspector;
    }

    /// <summary>Rebuilds from the current values (after the object changed elsewhere).</summary>
    public void Rebuild() {
        if (disposed || (obj == null && canvas == null)) {
            return;
        }
        Clear();
        var builder = new OvInspectorBuilder(
            content,
            controls,
            () => {
                obj?.ApplyConfig();
                canvas?.ApplyConfig();
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
            if (obj != null) {
                builder.BuildObject(obj);
            } else {
                builder.BuildCanvas(canvas, name => {
                    renamed?.Invoke(name);
                    Notify();
                });
            }
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(HudInspector)}] Build failed: {e}");
        }
        for (int i = 0; i < content.childCount; i++) {
            cards.Add(content.GetChild(i).gameObject);
        }
        try {
            built?.Invoke(content);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(HudInspector)}] After-build failed: {e.Message}");
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
        foreach (var card in cards) {
            if (card != null) {
                Object.Destroy(card);
            }
        }
        cards.Clear();
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
