using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.Overlay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public class OvCanvas : ISettingsFile {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;
    public readonly Canvas Canvas;
    public readonly CanvasScaler CanvasScaler;
    public readonly GraphicRaycaster GraphicRaycaster;
    public readonly List<OvObject> OvObjects = [];

    public OvCanvasSettings Config = new();

    public OvCanvas() {
        GameObject = new("OvCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        GameObject.transform.SetParent(OverlayCore.Transform, false);
        RectTransform = GameObject.GetComponent<RectTransform>();
        Canvas = GameObject.GetComponent<Canvas>();
        CanvasGroup = GameObject.GetComponent<CanvasGroup>();
        CanvasScaler = GameObject.GetComponent<CanvasScaler>();
        GraphicRaycaster = GameObject.GetComponent<GraphicRaycaster>();

        ApplyConfig();
    }

    public OvObject CreateOvObject() {
        var obj = new OvObject();
        Attach(obj);
        return obj;
    }

    public void ApplyConfig() {
        GameObject.name = Config.Name;
        Config.RectTransformConfig.ToUnity(GameObject);
        Config.CanvasGroupConfig.ToUnity(GameObject);
        Config.CanvasConfig.ToUnity(GameObject);
        Config.CanvasScalerConfig.ToUnity(GameObject);
        Config.GraphicRaycasterConfig.ToUnity(GameObject);
    }

    public void Attach(OvObject obj) {
        if(obj == null) {
            return;
        }

        if(obj.GameObject == null) {
            return;
        }

        if(obj.GameObject.transform.parent == RectTransform) {
            return;
        }

        obj.GameObject.transform.SetParent(RectTransform, false);

        if(!OvObjects.Contains(obj)) {
            OvObjects.Add(obj);
        }
    }

    public void Detach(OvObject obj) {
        if(obj == null) {
            return;
        }

        if(obj.GameObject == null) {
            return;
        }

        if(!OvObjects.Remove(obj)) {
            return;
        }

        obj.GameObject.transform.SetParent(OverlayCore.Transform, false);
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

    public JToken Serialize() {
        var json = new JObject {
            [nameof(Config)] = Config.Serialize()
        };

        if(OvObjects != null && OvObjects.Count > 0) {
            json[nameof(OvObjects)] = new JArray(OvObjects.Select(x => x.Serialize()));
        }

        return json;
    }

    public void Deserialize(JToken token) {
        if(token == null) {
            return;
        }

        if(token[nameof(Config)] != null) {
            Config.Deserialize(token[nameof(Config)]);
        }

        if(token[nameof(OvObjects)] is JArray array) {
            for(int i = OvObjects.Count - 1; i >= 0; i--) {
                OvObjects[i].Dispose();
            }
            OvObjects.Clear();

            foreach(var item in array) {
                var obj = new OvObject();
                obj.Deserialize(item);

                obj.ApplyComponent();
                obj.ApplyConfig();

                Attach(obj);
            }
        }

        ApplyConfig();
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