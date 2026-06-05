using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class RectTransformSettings : UnityComponentSettingsBase, ICopyable<RectTransformSettings> {
    public enum RectMode {
        CenterFixed,
        Stretch
    }

    public RectMode Mode = RectMode.CenterFixed;

    public Vector2 Anchor = new(0.5f, 0.5f);
    public Vector2 AnchoredPosition = Vector2.zero;
    public Vector2 SizeDelta = new(200f, 200f);
    public Vector2 Pivot = new(0.5f, 0.5f);

    public Vector2 AnchorMin = Vector2.zero;
    public Vector2 AnchorMax = Vector2.one;
    public Vector2 OffsetMin = Vector2.zero;
    public Vector2 OffsetMax = Vector2.zero;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        if(Mode == RectMode.CenterFixed) {
            com.anchorMin = Anchor;
            com.anchorMax = Anchor;
            com.pivot = Pivot;
            com.anchoredPosition = AnchoredPosition;
            com.sizeDelta = SizeDelta;
        } else if(Mode == RectMode.Stretch) {
            com.anchorMin = AnchorMin;
            com.anchorMax = AnchorMax;
            com.offsetMin = OffsetMin;
            com.offsetMax = OffsetMax;
        }

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        if(com.anchorMin == com.anchorMax) {
            Mode = RectMode.CenterFixed;

            Anchor = com.anchorMin;
            Pivot = com.pivot;
            AnchoredPosition = com.anchoredPosition;
            SizeDelta = com.sizeDelta;
        } else {
            Mode = RectMode.Stretch;

            AnchorMin = com.anchorMin;
            AnchorMax = com.anchorMax;
            OffsetMin = com.offsetMin;
            OffsetMax = com.offsetMax;
        }

        return true;
    }

    public override JToken Serialize() {
        var obj = new JObject {
            [nameof(Mode)] = IOUtils.WriteEnum(Mode),
        };

        if(Mode == RectMode.Stretch) {
            obj[nameof(Anchor)] = IOUtils.Write(Anchor);
            obj[nameof(AnchoredPosition)] = IOUtils.Write(AnchoredPosition);
            obj[nameof(SizeDelta)] = IOUtils.Write(SizeDelta);
            obj[nameof(Pivot)] = IOUtils.Write(Pivot);
        } else {
            obj[nameof(AnchorMin)] = IOUtils.Write(AnchorMin);
            obj[nameof(AnchorMax)] = IOUtils.Write(AnchorMax);
            obj[nameof(OffsetMin)] = IOUtils.Write(OffsetMin);
            obj[nameof(OffsetMax)] = IOUtils.Write(OffsetMax);
        } return obj;
    }

    public override void Deserialize(JToken token) {
        Mode = IOUtils.ReadEnum(token, nameof(Mode), Mode);

        if(Mode == RectMode.Stretch) {
            Anchor = IOUtils.Read(token, nameof(Anchor), Anchor);
            AnchoredPosition = IOUtils.Read(token, nameof(AnchoredPosition), AnchoredPosition);
            SizeDelta = IOUtils.Read(token, nameof(SizeDelta), SizeDelta);
            Pivot = IOUtils.Read(token, nameof(Pivot), Pivot);
        } else {
            AnchorMin = IOUtils.Read(token, nameof(AnchorMin), AnchorMin);
            AnchorMax = IOUtils.Read(token, nameof(AnchorMax), AnchorMax);
            OffsetMin = IOUtils.Read(token, nameof(OffsetMin), OffsetMin);
            OffsetMax = IOUtils.Read(token, nameof(OffsetMax), OffsetMax);
        }
    }

    public RectTransformSettings Copy() {
        return new RectTransformSettings {
            Mode = Mode,
            Anchor = Anchor,
            AnchoredPosition = AnchoredPosition,
            SizeDelta = SizeDelta,
            Pivot = Pivot,
            AnchorMin = AnchorMin,
            AnchorMax = AnchorMax,
            OffsetMin = OffsetMin,
            OffsetMax = OffsetMax
        };
    }
}