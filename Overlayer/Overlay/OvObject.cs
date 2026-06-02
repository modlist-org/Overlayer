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
        Config.RectTransformConfig.ToUnity(GameObject);
        Config.TextConfig?.ToUnity(GameObject);
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        Config.OutlineConfig?.ToUnity(GameObject);
    }

    public void ApplyComponent() {
        var tmp = GameObject.GetComponent<TextMeshProUGUI>();
        if (Config.TextConfig != null) {
            if (tmp == null) {
                GameObject.AddComponent<TextMeshProUGUI>();
            }
        } else {
            if (tmp != null) {
                Object.Destroy(tmp);
            }
        }
        var img = GameObject.GetComponent<Image>();
        if (Config.ImageConfig != null) {
            if (img == null) {
                GameObject.AddComponent<Image>();
            }
        } else {
            if (img != null) {
                Object.Destroy(img);
            }
        }
        var msk = GameObject.GetComponent<Mask>();
        if (Config.MaskConfig != null) {
            if (msk == null) {
                GameObject.AddComponent<Mask>();
            }
        } else {
            if (msk != null) {
                Object.Destroy(msk);
            }
        }
        var sdw = GameObject.GetComponent<Shadow>();
        if (Config.ShadowConfig != null) {
            if (sdw == null) {
                GameObject.AddComponent<Shadow>();
            }
        } else {
            if (sdw != null) {
                Object.Destroy(sdw);
            }
        }
        var rm2 = GameObject.GetComponent<RectMask2D>();
        if (Config.HasRectMask2D) {
            if (rm2 == null) {
                GameObject.AddComponent<RectMask2D>();
            }
        } else {
            if (rm2 != null) {
                Object.Destroy(rm2);
            }
        }
        var oln = GameObject.GetComponent<Outline>();
        if(Config.OutlineConfig != null) {
            if(oln == null) {
                GameObject.AddComponent<Outline>();
            }
        } else {
            if(oln != null) {
                Object.Destroy(oln);
            }
        }
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