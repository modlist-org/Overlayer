using Newtonsoft.Json.Linq;
using Overlayer.Core;
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

    private readonly Action<Camera> onCameraChangedHandler;
    
    public OvCanvasSettings Config = new();

    public OvCanvas() {
        GameObject = new GameObject("OvCanvas");
        GameObject.transform.SetParent(OverlayCore.Transform, false);
        RectTransform = GameObject.AddComponent<RectTransform>();
        Canvas = GameObject.AddComponent<Canvas>();
        CanvasGroup = GameObject.AddComponent<CanvasGroup>();
        CanvasScaler = GameObject.AddComponent<CanvasScaler>();
        GraphicRaycaster = GameObject.AddComponent<GraphicRaycaster>();

        var mainCam = MainCore.Cam.Camera;
        if (mainCam != null) {
            Canvas.worldCamera = mainCam;
        }

        onCameraChangedHandler = camera => {
            Canvas?.worldCamera = camera;
        };
        MainCore.Cam.OnCameraChanged += onCameraChangedHandler;

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
        index = Math.Clamp(index, 0, RectTransform.parent.childCount - 1);
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

                Attach(obj);
                obj.ApplyComponent();
                obj.ApplyConfig();
            }
        }

        ApplyConfig();
    }

    internal void RefreshLayouts() {
        ApplyConfig();
        foreach(var obj in OvObjects) {
            obj.RefreshLayout();
        }

        Canvas.ForceUpdateCanvases();
        foreach(var obj in OvObjects) {
            obj.RebuildLayout();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void Dispose() {
        MainCore.Cam?.OnCameraChanged -= onCameraChangedHandler;

        for(int i = OvObjects.Count - 1; i >= 0; i--) {
            OvObjects[i].Dispose();
        }

        OvObjects.Clear();

        if(Canvas != null) {
            Object.Destroy(Canvas.gameObject);
        }
    }
}
