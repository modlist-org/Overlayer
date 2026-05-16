using UnityEngine;

namespace Overlayer.UI.Objects;

public abstract class UIObject {
    public string Id { get; set; }

    public RectTransform Rect { get; private set; }

    public UIObject(string id, RectTransform rect) {
        Id = id;
        Rect = rect;
    }
}