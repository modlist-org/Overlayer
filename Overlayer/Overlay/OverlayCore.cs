using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public static class OverlayCore {
    public static GameObject Core { get; private set; }
    public static Transform Transform => Core.transform;

    public static readonly List<OvCanvas> Canvases = [];

    public static void Initialize(GameObject parent) {
        if(parent == null) {
            return;
        }
        if(Core != null) {
            return;
        }

        Core = new GameObject("OvObjectCore");
        Core.transform.SetParent(parent.transform, false);
    }

    public static OvCanvas CreateOvCanvas() {
        var canvas = new OvCanvas();
        canvas.RectTransform.SetParent(Transform, false);
        Canvases.Add(canvas);
        return canvas;
    }

    public static void Dispose() {
        if(Core == null) {
            return;
        }

        for(int i = Canvases.Count - 1; i >= 0; i--) {
            Canvases[i].Dispose();
        }

        Canvases.Clear();

        Object.Destroy(Core);
        Core = null;
    }
}