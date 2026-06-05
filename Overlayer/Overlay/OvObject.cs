using Newtonsoft.Json.Linq;
using Overlayer.IO;
using Overlayer.IO.Interface;
using Overlayer.IO.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public sealed class OvObject : ISettingsFile {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObjectSettings Config = new();

    public OvObject() {
        GameObject = new GameObject("OvObject",
            typeof(RectTransform),
            typeof(CanvasGroup)
        );
        RectTransform = GameObject.GetComponent<RectTransform>();
        CanvasGroup = GameObject.GetComponent<CanvasGroup>();

        ApplyConfig();
    }

    public OvObject CreateOvObject() {
        var obj = new OvObject();
        obj.GameObject.transform.SetParent(RectTransform, false);
        Children.Add(obj);
        return obj;
    }

    public void ApplyConfig() {
        GameObject.name = Config.Name;
        Config.RectTransformConfig.ToUnity(GameObject);
        Config.CanvasGroupConfig.ToUnity(GameObject);
        Config.TextConfig?.ToUnity(GameObject);
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        Config.OutlineConfig?.ToUnity(GameObject);
    }

    public void ApplyComponent() {
        if(GameObject == null) {
            return;
        }

        EnsureComponent<TextMeshProUGUI>(Config.TextConfig != null);
        EnsureComponent<Image>(Config.ImageConfig != null);
        EnsureComponent<Mask>(Config.MaskConfig != null);
        EnsureComponent<Shadow>(Config.ShadowConfig != null);
        EnsureComponent<RectMask2D>(Config.HasRectMask2D);
        EnsureComponent<Outline>(Config.OutlineConfig != null);
    }

    private T EnsureComponent<T>(bool enabled) where T : Component {
        if(GameObject == null) {
            return null;
        }

        var comp = GameObject.GetComponent<T>();

        if(!enabled) {
            if(comp != null) {
                Object.Destroy(comp);
            }

            return null;
        }

        if(comp == null) {
            comp = GameObject.AddComponent<T>();
        }

        return comp;
    }

    public void Attach(OvObject child) {
        if(child == null || child.GameObject == null) {
            return;
        }

        if(child == this) {
            return;
        }

        if(child.Parent == this) {
            return;
        }

        child.Parent?.Children.Remove(child);
        child.Parent = this;

        if(!Children.Contains(child)) {
            Children.Add(child);
        }

        child.GameObject.transform.SetParent(RectTransform, false);
        child.GameObject.transform.SetSiblingIndex(Children.Count - 1);
    }

    public void Detach() {
        if(Parent == null) {
            return;
        }

        var oldParent = Parent;
        Parent = null;
        oldParent.Children.Remove(this);

        if(GameObject != null && OverlayCore.Transform != null) {
            GameObject.transform.SetParent(OverlayCore.Transform, false);
        }
    }

    public void SetChildIndex(OvObject child, int index) {
        if(child == null || child.Parent != this) {
            return;
        }

        int currentIndex = Children.IndexOf(child);
        if(currentIndex < 0) {
            return;
        }

        Children.RemoveAt(currentIndex);

        index = Mathf.Clamp(index, 0, Children.Count);
        Children.Insert(index, child);

        for(int i = 0; i < Children.Count; i++) {
            Children[i].GameObject.transform.SetSiblingIndex(i);
        }
    }

    public void BringToFront(OvObject child) => SetChildIndex(child, Children.Count - 1);

    public void SendToBack(OvObject child) => SetChildIndex(child, 0);

    public JToken Serialize() {
        var json = new JObject {
            [nameof(Config)] = Config.Serialize()
        };

        if(Children != null && Children.Count > 0) {
            json[nameof(Children)] = new JArray(Children.Select(x => x.Serialize()));
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

        if(token[nameof(Children)] is JArray array) {
            for(int i = Children.Count - 1; i >= 0; i--) {
                Children[i].Dispose();
            }
            Children.Clear();

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
        for(int i = Children.Count - 1; i >= 0; i--) {
            Children[i].Dispose();
        }

        Children.Clear();

        Parent?.Children.Remove(this);
        Parent = null;

        if(GameObject != null) {
            GameObject.transform.SetParent(null);
            Object.Destroy(GameObject);
        }
    }
}