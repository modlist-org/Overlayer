using Overlayer.IO.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public sealed class OvObject {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObjectSettings Config = new();

    private static readonly HashSet<Type> AllowedTypes = [
        typeof(TextMeshProUGUI),
        typeof(Image),
        typeof(Mask),
        typeof(Shadow),
        typeof(RectMask2D),
    ];

    public OvObject() {
        GameObject = new GameObject("OvObject", typeof(RectTransform));
        RectTransform = GameObject.GetComponent<RectTransform>();
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
        Config.RectTransformConfig?.ToUnity(GameObject);
        Config.TextConfig?.ToUnity(GameObject);
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        ApplyMask2D(Config.HasMask2D);
    }

    public void ApplyComponent() {
        Sync<TextMeshProUGUI>(Config.TextConfig != null);
        Sync<Image>(Config.ImageConfig != null);
        Sync<Mask>(Config.MaskConfig != null);
        Sync<Shadow>(Config.ShadowConfig != null);
        Sync<RectMask2D>(Config.RectTransformConfig != null);
    }

    public void AttachChild(OvObject child) {
        if(child == null || child.Parent == this) {
            return;
        }

        child.Parent?.Children.Remove(child);

        child.Parent = this;
        child.GameObject.transform.SetParent(RectTransform, false);

        Children.Add(child);
        child.GameObject.transform.SetSiblingIndex(Children.Count - 1);
    }

    public void Detach() {
        if(Parent == null) {
            return;
        }

        Parent.Children.Remove(this);
        Parent = null;

        GameObject.transform.SetParent(null, false);
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

    public void BringToFront(OvObject child) {
        SetChildIndex(child, Children.Count - 1);
    }

    public void SendToBack(OvObject child) {
        SetChildIndex(child, 0);
    }

    public T Add<T>() where T : Component {
        var type = typeof(T);

        if(!AllowedTypes.Contains(type)) {
            return null;
        }

        return GameObject.GetComponent<T>() ?? GameObject.AddComponent<T>();
    }

    public T Get<T>() where T : Component => GameObject.GetComponent<T>();

    public void Remove<T>() where T : Component {
        var comp = GameObject.GetComponent<T>();

        if(comp != null) {
            Object.Destroy(comp);
        }
    }

    private void Sync<T>(bool enable) where T : Component {
        if(enable) {
            Add<T>();
        } else {
            Remove<T>();
        }
    }

    private void ApplyMask2D(bool enabled) {
        var comp = Get<RectMask2D>();
        if(enabled) {
            if(comp == null) {
                Add<RectMask2D>();
            }
        } else {
            if(comp != null) {
                Remove<RectMask2D>();
            }
        }
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