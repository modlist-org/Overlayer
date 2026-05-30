using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public sealed class OvObject {
    public string Name;

    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;

    public OvObject Parent { get; private set; }
    public readonly List<OvObject> Children = [];

    public OvObject(string name = "OvObject") {
        GameObject = new GameObject(name, typeof(RectTransform));
        RectTransform = (RectTransform)GameObject.transform;
        Name = name;
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

        child.GameObject.transform.SetSiblingIndex(index);

        for(int i = 0; i < Children.Count; i++) {
            Children[i].GameObject.transform.SetSiblingIndex(i);
        }
    }

    public void BringToFront(OvObject child) => SetChildIndex(child, Children.Count - 1);

    public void SendToBack(OvObject child) => SetChildIndex(child, 0);

    public T Add<T>() where T : Component => GameObject.AddComponent<T>();

    public T Get<T>() where T : Component => GameObject.GetComponent<T>();

    public void Dispose() {
        for(int i = Children.Count - 1; i >= 0; i--) {
            Children[i].Dispose();
        }

        Children.Clear();

        Parent?.Children.Remove(this);
        Parent = null;

        if(GameObject == null) {
            return;
        }

        GameObject.transform.SetParent(null);
        Object.Destroy(GameObject);
    }
}