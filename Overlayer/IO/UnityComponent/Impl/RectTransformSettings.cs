using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class RectTransformSettings : UnityComponentSettingsBase, ICopyable<RectTransformSettings> {
    public FxValue<Vector2> AnchoredPosition = new(Vector2.zero);
    public FxValue<float> AnchoredPositionZ = new(0f);
    public FxValue<Vector2> SizeDelta = new(new Vector2(200f, 200f));
    public FxValue<Vector2> RotationXY = new(Vector2.zero);
    public FxValue<float> Rotation = new(0f);
    public FxValue<Vector3> Scale = new(Vector3.one);
    public FxValue<Vector2> Pivot = new(new Vector2(0.5f, 0.5f));

    public FxValue<Vector2> AnchorMin = new(new Vector2(0.5f, 0.5f));
    public FxValue<Vector2> AnchorMax = new(new Vector2(0.5f, 0.5f));
    public FxValue<Vector2> OffsetMin = new(Vector2.zero);
    public FxValue<Vector2> OffsetMax = new(Vector2.zero);

    private Vector2 _lastAnchoredPosition = Vector2.zero;
    private float _lastAnchoredPositionZ;
    private Vector2 _lastSizeDelta = new(200f, 200f);
    private Vector2 _lastRotationXY = Vector2.zero;
    private float _lastRotation;
    private Vector3 _lastScale = Vector3.one;
    private Vector2 _lastPivot = new(0.5f, 0.5f);
    private Vector2 _lastAnchorMin = new(0.5f, 0.5f);
    private Vector2 _lastAnchorMax = new(0.5f, 0.5f);

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(AnchoredPosition)
        || FxUtil.HasFx(AnchoredPositionZ)
        || FxUtil.HasFx(SizeDelta)
        || FxUtil.HasFx(RotationXY)
        || FxUtil.HasFx(Rotation)
        || FxUtil.HasFx(Scale)
        || FxUtil.HasFx(Pivot)
        || FxUtil.HasFx(AnchorMin)
        || FxUtil.HasFx(AnchorMax);

    public float GetOffsetMin(int axis) => AnchoredPosition.Value[axis] - (SizeDelta.Value[axis] * Pivot.Value[axis]);

    public float GetOffsetMax(int axis) => AnchoredPosition.Value[axis] + (SizeDelta.Value[axis] * (1f - Pivot.Value[axis]));

    public void SetOffsetMin(int axis, float value) {
        float max = GetOffsetMax(axis);
        var size = SizeDelta.Value;
        size[axis] = max - value;
        SizeDelta.Value = size;
        var pos = AnchoredPosition.Value;
        pos[axis] = value + (size[axis] * Pivot.Value[axis]);
        AnchoredPosition.Value = pos;
    }

    public void SetOffsetMax(int axis, float value) {
        float min = GetOffsetMin(axis);
        var size = SizeDelta.Value;
        size[axis] = value - min;
        SizeDelta.Value = size;
        var pos = AnchoredPosition.Value;
        pos[axis] = min + (size[axis] * Pivot.Value[axis]);
        AnchoredPosition.Value = pos;
    }

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        _lastAnchorMin = AnchorMin.Value;
        _lastAnchorMax = AnchorMax.Value;
        _lastPivot = Pivot.Value;
        _lastAnchoredPosition = AnchoredPosition.Value;
        _lastAnchoredPositionZ = AnchoredPositionZ.Value;
        _lastSizeDelta = SizeDelta.Value;
        var rotXY = RotationXY.Value;
        _lastRotationXY = rotXY;
        _lastRotation = Rotation.Value;
        _lastScale = Scale.Value;
        com.anchorMin = _lastAnchorMin;
        com.anchorMax = _lastAnchorMax;
        com.pivot = _lastPivot;
        com.anchoredPosition3D = new Vector3(_lastAnchoredPosition.x, _lastAnchoredPosition.y, _lastAnchoredPositionZ);
        com.sizeDelta = _lastSizeDelta;
        com.localEulerAngles = new Vector3(rotXY.x, rotXY.y, _lastRotation);
        com.localScale = _lastScale;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<RectTransform>();
        if(com == null) {
            return false;
        }

        AnchorMin.Value = com.anchorMin;
        AnchorMax.Value = com.anchorMax;
        Pivot.Value = com.pivot;
        AnchoredPosition.Value = com.anchoredPosition;
        AnchoredPositionZ.Value = com.anchoredPosition3D.z;
        SizeDelta.Value = com.sizeDelta;
        RotationXY.Value = new Vector2(com.localEulerAngles.x, com.localEulerAngles.y);
        Rotation.Value = com.localEulerAngles.z;
        Scale.Value = com.localScale;
        OffsetMin.Value = com.offsetMin;
        OffsetMax.Value = com.offsetMax;
        _lastAnchorMin = com.anchorMin;
        _lastAnchorMax = com.anchorMax;
        _lastPivot = com.pivot;
        _lastAnchoredPosition = com.anchoredPosition;
        _lastAnchoredPositionZ = com.anchoredPosition3D.z;
        _lastSizeDelta = com.sizeDelta;
        _lastRotationXY = new Vector2(com.localEulerAngles.x, com.localEulerAngles.y);
        _lastRotation = com.localEulerAngles.z;
        _lastScale = com.localScale;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<RectTransform>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastAnchorMin, AnchorMin.Value, v => com.anchorMin = v);
        FxUtil.ApplyIfChanged(ref _lastAnchorMax, AnchorMax.Value, v => com.anchorMax = v);
        FxUtil.ApplyIfChanged(ref _lastPivot, Pivot.Value, v => com.pivot = v);
        bool posChanged = !EqualityComparer<Vector2>.Default.Equals(_lastAnchoredPosition, AnchoredPosition.Value);
        bool zChanged = !EqualityComparer<float>.Default.Equals(_lastAnchoredPositionZ, AnchoredPositionZ.Value);
        if(posChanged || zChanged) {
            _lastAnchoredPosition = AnchoredPosition.Value;
            _lastAnchoredPositionZ = AnchoredPositionZ.Value;
            com.anchoredPosition3D = new Vector3(_lastAnchoredPosition.x, _lastAnchoredPosition.y, _lastAnchoredPositionZ);
        }
        FxUtil.ApplyIfChanged(ref _lastSizeDelta, SizeDelta.Value, v => com.sizeDelta = v);
        bool rotChanged = !EqualityComparer<Vector2>.Default.Equals(_lastRotationXY, RotationXY.Value);
        bool rotZChanged = !EqualityComparer<float>.Default.Equals(_lastRotation, Rotation.Value);
        if(rotChanged || rotZChanged) {
            _lastRotationXY = RotationXY.Value;
            _lastRotation = Rotation.Value;
            com.localEulerAngles = new Vector3(_lastRotationXY.x, _lastRotationXY.y, _lastRotation);
        }
        FxUtil.ApplyIfChanged(ref _lastScale, Scale.Value, v => com.localScale = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(AnchorMin)] = IOUtils.WriteFx(AnchorMin),
            [nameof(AnchorMax)] = IOUtils.WriteFx(AnchorMax),
            [nameof(AnchoredPosition)] = IOUtils.WriteFx(AnchoredPosition),
            [nameof(AnchoredPositionZ)] = IOUtils.WriteFx(AnchoredPositionZ),
            [nameof(SizeDelta)] = IOUtils.WriteFx(SizeDelta),
            [nameof(RotationXY)] = IOUtils.WriteFx(RotationXY),
            [nameof(Rotation)] = IOUtils.WriteFx(Rotation),
            [nameof(Scale)] = IOUtils.WriteFx(Scale),
            [nameof(Pivot)] = IOUtils.WriteFx(Pivot),
            [nameof(OffsetMin)] = IOUtils.WriteFx(new FxValue<Vector2>(new Vector2(GetOffsetMin(0), GetOffsetMin(1)))),
            [nameof(OffsetMax)] = IOUtils.WriteFx(new FxValue<Vector2>(new Vector2(GetOffsetMax(0), GetOffsetMax(1))))
        };
    }

    public override void Deserialize(JToken token) {
        AnchorMin = IOUtils.ReadFx(token, nameof(AnchorMin), AnchorMin);
        AnchorMax = IOUtils.ReadFx(token, nameof(AnchorMax), AnchorMax);
        AnchoredPosition = IOUtils.ReadFx(token, nameof(AnchoredPosition), AnchoredPosition);
        AnchoredPositionZ = IOUtils.ReadFx(token, nameof(AnchoredPositionZ), AnchoredPositionZ);
        SizeDelta = IOUtils.ReadFx(token, nameof(SizeDelta), SizeDelta);
        RotationXY = IOUtils.ReadFx(token, nameof(RotationXY), RotationXY);
        Rotation = IOUtils.ReadFx(token, nameof(Rotation), Rotation);
        Scale = IOUtils.ReadFx(token, nameof(Scale), Scale);
        Pivot = IOUtils.ReadFx(token, nameof(Pivot), Pivot);
        OffsetMin = IOUtils.ReadFx(token, nameof(OffsetMin), OffsetMin);
        OffsetMax = IOUtils.ReadFx(token, nameof(OffsetMax), OffsetMax);
    }

    public RectTransformSettings Copy() {
        return new RectTransformSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            AnchoredPosition = AnchoredPosition?.Copy(),
            AnchoredPositionZ = AnchoredPositionZ?.Copy(),
            SizeDelta = SizeDelta?.Copy(),
            RotationXY = RotationXY?.Copy(),
            Rotation = Rotation?.Copy(),
            Scale = Scale?.Copy(),
            Pivot = Pivot?.Copy(),
            AnchorMin = AnchorMin?.Copy(),
            AnchorMax = AnchorMax?.Copy(),
            OffsetMin = new FxValue<Vector2>(new Vector2(GetOffsetMin(0), GetOffsetMin(1))),
            OffsetMax = new FxValue<Vector2>(new Vector2(GetOffsetMax(0), GetOffsetMax(1)))
        };
    }
}
