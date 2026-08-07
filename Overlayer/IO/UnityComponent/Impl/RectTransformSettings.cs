using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class RectTransformSettings : UnityComponentSettingsBase, ICopyable<RectTransformSettings> {
    public Vector2 AnchoredPosition = Vector2.zero;
    public float AnchoredPositionZ = 0f;
    public Vector2 SizeDelta = new(200f, 200f);
    public Vector2 RotationXY = Vector2.zero;
    public float Rotation = 0f;
    public Vector3 Scale = Vector3.one;
    public Vector2 Pivot = new(0.5f, 0.5f);

    public Vector2 AnchorMin = new(0.5f, 0.5f);
    public Vector2 AnchorMax = new(0.5f, 0.5f);
    public Vector2 OffsetMin = Vector2.zero;
    public Vector2 OffsetMax = Vector2.zero;

    public float GetOffsetMin(int axis) {
        return AnchoredPosition[axis] - SizeDelta[axis] * Pivot[axis];
    }

    public float GetOffsetMax(int axis) {
        return AnchoredPosition[axis] + SizeDelta[axis] * (1f - Pivot[axis]);
    }

    public void SetOffsetMin(int axis, float value) {
        float max = GetOffsetMax(axis);
        SizeDelta[axis] = max - value;
        AnchoredPosition[axis] = value + SizeDelta[axis] * Pivot[axis];
    }

    public void SetOffsetMax(int axis, float value) {
        float min = GetOffsetMin(axis);
        SizeDelta[axis] = value - min;
        AnchoredPosition[axis] = min + SizeDelta[axis] * Pivot[axis];
    }

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        com.anchorMin = AnchorMin;
        com.anchorMax = AnchorMax;
        com.pivot = Pivot;
        com.anchoredPosition3D = new Vector3(AnchoredPosition.x, AnchoredPosition.y, AnchoredPositionZ);
        com.sizeDelta = SizeDelta;
        com.localEulerAngles = new Vector3(RotationXY.x, RotationXY.y, Rotation);
        com.localScale = Scale;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        AnchorMin = com.anchorMin;
        AnchorMax = com.anchorMax;
        Pivot = com.pivot;
        AnchoredPosition = com.anchoredPosition;
        AnchoredPositionZ = com.anchoredPosition3D.z;
        SizeDelta = com.sizeDelta;
        RotationXY = new Vector2(com.localEulerAngles.x, com.localEulerAngles.y);
        Rotation = com.localEulerAngles.z;
        Scale = com.localScale;
        OffsetMin = com.offsetMin;
        OffsetMax = com.offsetMax;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(AnchorMin)] = IOUtils.Write(AnchorMin),
            [nameof(AnchorMax)] = IOUtils.Write(AnchorMax),
            [nameof(AnchoredPosition)] = IOUtils.Write(AnchoredPosition),
            [nameof(AnchoredPositionZ)] = AnchoredPositionZ,
            [nameof(SizeDelta)] = IOUtils.Write(SizeDelta),
            [nameof(RotationXY)] = IOUtils.Write(RotationXY),
            [nameof(Rotation)] = Rotation,
            [nameof(Scale)] = IOUtils.Write(Scale),
            [nameof(Pivot)] = IOUtils.Write(Pivot),
            [nameof(OffsetMin)] = IOUtils.Write(new Vector2(GetOffsetMin(0), GetOffsetMin(1))),
            [nameof(OffsetMax)] = IOUtils.Write(new Vector2(GetOffsetMax(0), GetOffsetMax(1)))
        };
    }

    public override void Deserialize(JToken token) {
        AnchorMin = IOUtils.Read(token, nameof(AnchorMin), AnchorMin);
        AnchorMax = IOUtils.Read(token, nameof(AnchorMax), AnchorMax);
        AnchoredPosition = IOUtils.Read(token, nameof(AnchoredPosition), AnchoredPosition);
        AnchoredPositionZ = IOUtils.Read(token, nameof(AnchoredPositionZ), AnchoredPositionZ);
        SizeDelta = IOUtils.Read(token, nameof(SizeDelta), SizeDelta);
        RotationXY = IOUtils.Read(token, nameof(RotationXY), RotationXY);
        Rotation = IOUtils.Read(token, nameof(Rotation), Rotation);
        Scale = IOUtils.Read(token, nameof(Scale), Scale);
        Pivot = IOUtils.Read(token, nameof(Pivot), Pivot);
        OffsetMin = IOUtils.Read(token, nameof(OffsetMin), OffsetMin);
        OffsetMax = IOUtils.Read(token, nameof(OffsetMax), OffsetMax);
    }

    public RectTransformSettings Copy() {
        return new RectTransformSettings {
            AnchoredPosition = AnchoredPosition,
            AnchoredPositionZ = AnchoredPositionZ,
            SizeDelta = SizeDelta,
            RotationXY = RotationXY,
            Rotation = Rotation,
            Scale = Scale,
            Pivot = Pivot,
            AnchorMin = AnchorMin,
            AnchorMax = AnchorMax,
            OffsetMin = new Vector2(GetOffsetMin(0), GetOffsetMin(1)),
            OffsetMax = new Vector2(GetOffsetMax(0), GetOffsetMax(1))
        };
    }
}
