using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public class OvCanvas {
    public Canvas Canvas { get; }

    public readonly List<OvObject> OvObjects = [];

    public OvCanvas() {
        GameObject go = new("OvCanvas");
        go.transform.SetParent(OverlayCore.Transform, false);

        Canvas = go.GetComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 32766;

        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
    }

    public void Dispose() {
        for(int i = OvObjects.Count - 1; i >= 0; i--) {
            OvObjects[i].Dispose();
        }

        OvObjects.Clear();

        if(Canvas != null) {
            Object.Destroy(Canvas.gameObject);
        }
    }
}