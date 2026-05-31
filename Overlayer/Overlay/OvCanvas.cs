using Overlayer.IO.Overlay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public class OvCanvas {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly Canvas Canvas;
    public readonly CanvasScaler CanvasScaler;
    public readonly GraphicRaycaster GraphicRaycaster;
    public readonly List<OvObject> OvObjects = [];

    public OvCanvasSettings Config = new();

    public OvCanvas() {
        GameObject = new("OvCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject.transform.SetParent(OverlayCore.Transform, false);
        RectTransform = GameObject.GetComponent<RectTransform>();
        Canvas = GameObject.GetComponent<Canvas>();
        CanvasScaler = GameObject.GetComponent<CanvasScaler>();
        GraphicRaycaster = GameObject.GetComponent<GraphicRaycaster>();
        ApplyConfig();
    }

    public OvObject CreateOvObject(OvObjectSettings config = null) {
        var obj = new OvObject {
            Config = config ?? new OvObjectSettings()
        };
        obj.GameObject.transform.SetParent(RectTransform, false);
        OvObjects.Add(obj);

        return obj;
    }

    public void ApplyConfig() {
        GameObject.name = Config.Name;
        Config.RectTransformConfig.ToUnity(GameObject);
        Config.CanvasConfig.ToUnity(GameObject);
        Config.CanvasScalerConfig.ToUnity(GameObject);
        Config.RectTransformConfig.ToUnity(GameObject);
    }

    public void SetOrder(int index) {
        if(RectTransform == null || RectTransform.parent == null) {
            return;
        }
        index = Mathf.Clamp(index, 0, RectTransform.parent.childCount - 1);
        RectTransform.SetSiblingIndex(index);
    }

    public void BringToFront() {
        if(RectTransform == null || RectTransform.parent == null) {
            return;
        }
        RectTransform.SetSiblingIndex(RectTransform.parent.childCount - 1);
    }

    public void SendToBack() {
        if(RectTransform == null || RectTransform.parent == null) {
            return;
        }
        RectTransform.SetSiblingIndex(0);
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