using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class RectTransformSettings : UnityComponentSettingsBase {
    public Vector2 AnchorMin = new(0.5f, 0.5f);
    public Vector2 AnchorMax = new(0.5f, 0.5f);
    public Vector2 AnchoredPosition = new(0.5f, 0.5f);
    public Vector2 SizeDelta = new(200f, 200f);
    public Vector2 Pivot = new(0.5f, 0.5f);
    public Vector2 OffsetMin = Vector2.zero;
    public Vector2 OffsetMax = Vector2.zero;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<RectTransform>();
        if (com == null) {
            return false;
        }
        
        com.anchorMin = AnchorMin;
        com.anchorMax = AnchorMax;
        com.anchoredPosition = AnchoredPosition;
        com.sizeDelta = SizeDelta;
        com.pivot = Pivot;
        com.offsetMin = OffsetMin;
        com.offsetMax = OffsetMax;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<RectTransform>();
        if (com == null) {
            return false;
        }
        
        AnchorMin = com.anchorMin;
        AnchorMax = com.anchorMax;
        AnchoredPosition = com.anchoredPosition;
        SizeDelta = com.sizeDelta;
        Pivot = com.pivot;
        OffsetMin = com.offsetMin;
        OffsetMax = com.offsetMax;
        
        return true;
    }

    public override JToken Serialize() {
        var obj = new JObject {
            [nameof(AnchorMin)] = IOUtils.Write(AnchorMin),
            [nameof(AnchorMax)] = IOUtils.Write(AnchorMax),
            [nameof(AnchoredPosition)] = IOUtils.Write(AnchoredPosition),
            [nameof(SizeDelta)] = IOUtils.Write(SizeDelta),
            [nameof(Pivot)] = IOUtils.Write(Pivot),
            [nameof(OffsetMin)] = IOUtils.Write(OffsetMin),
            [nameof(OffsetMax)] = IOUtils.Write(OffsetMax)
        };

        return obj;
    }

    public override void Deserialize(JToken token) {
        AnchorMin = IOUtils.Read(token, nameof(AnchorMin), AnchorMin);
        AnchorMax = IOUtils.Read(token, nameof(AnchorMax), AnchorMax);
        AnchoredPosition = IOUtils.Read(token, nameof(AnchoredPosition), AnchoredPosition);
        SizeDelta = IOUtils.Read(token, nameof(SizeDelta), SizeDelta);
        Pivot = IOUtils.Read(token, nameof(Pivot), Pivot);
        OffsetMin = IOUtils.Read(token, nameof(OffsetMin), OffsetMin);
        OffsetMax = IOUtils.Read(token, nameof(OffsetMax), OffsetMax);
    }
}