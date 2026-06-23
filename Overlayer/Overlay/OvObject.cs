using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.Overlay;
using Overlayer.TextEngine.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Overlay;

public sealed class OvObject : ISettingsFile {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObjectSettings Config = new();

    public OvObject() {
        GameObject = new GameObject("OvObject");
        RectTransform = GameObject.AddComponent<RectTransform>();
        CanvasGroup = GameObject.AddComponent<CanvasGroup>();

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
        if(Config.TextConfig != null) {
            Config.TextConfig.ToUnity(GameObject);
            GameObject.GetComponent<TextEngineUpdater>()?.SetText(Config.TextConfig.Text);
        }
        Config.ImageConfig?.ToUnity(GameObject);
        Config.MaskConfig?.ToUnity(GameObject);
        Config.ShadowConfig?.ToUnity(GameObject);
        Config.OutlineConfig?.ToUnity(GameObject);
    }

    public void ApplyComponent() {
        if(GameObject == null) {
            return;
        }

        bool tc = Config.TextConfig != null;
        EnsureComponent<TextMeshProUGUI>(tc);
        EnsureComponent<TextEngineUpdater>(tc);
        if(tc) {
            var tmp = GameObject.GetComponent<TextMeshProUGUI>();
            var updater = GameObject.GetComponent<TextEngineUpdater>();
            if(updater != null && tmp != null) {
                updater.Init(tmp);
            }
        }
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

        index = Math.Clamp(index, 0, Children.Count);
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

#if ML && IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class TextEngineUpdater
#if ML && IL2CPP
        (IntPtr ptr) : MonoBehaviour(ptr)
#else
        : MonoBehaviour
#endif
    {
        public TextMeshProUGUI Tmp;
        public TextEngineCore Engine;

        public void Init(TextMeshProUGUI tmp) {
            Tmp = tmp;
            Engine = new();
        }

        public void SetText(string text) => Engine.Text = text;

        public void Update() => Tmp.text = Engine.Get();

        public void OnDestroy() {
            Engine?.Dispose();
        }
    }
}