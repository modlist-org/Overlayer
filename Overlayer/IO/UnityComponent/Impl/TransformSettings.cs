using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class TransformSettings : UnityComponentSettingsBase, ICopyable<TransformSettings> {
    public FxValue<Vector3> LocalPosition = new(Vector3.zero);
    public FxValue<Vector3> LocalEulerAngles = new(Vector3.zero);
    public FxValue<Vector3> LocalScale = new(Vector3.one);

    private Vector3 _lastLocalPosition = Vector3.zero;
    private Vector3 _lastLocalEulerAngles = Vector3.zero;
    private Vector3 _lastLocalScale = Vector3.one;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(LocalPosition)
        || FxUtil.HasFx(LocalEulerAngles)
        || FxUtil.HasFx(LocalScale);

    public override bool ToUnity(GameObject target) {
        var t = target.transform;
        if (t == null) {
            return false;
        }

        _lastLocalPosition = LocalPosition.Value;
        _lastLocalEulerAngles = LocalEulerAngles.Value;
        _lastLocalScale = LocalScale.Value;
        t.localPosition = _lastLocalPosition;
        t.localEulerAngles = _lastLocalEulerAngles;
        t.localScale = _lastLocalScale;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var t = source.transform;
        if (t == null) {
            return false;
        }

        LocalPosition.Value = t.localPosition;
        LocalEulerAngles.Value = t.localEulerAngles;
        LocalScale.Value = t.localScale;
        _lastLocalPosition = t.localPosition;
        _lastLocalEulerAngles = t.localEulerAngles;
        _lastLocalScale = t.localScale;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var t = target.transform;
        if (t == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastLocalPosition, LocalPosition.Value)) t.localPosition = _lastLocalPosition;
        if (FxUtil.Changed(ref _lastLocalEulerAngles, LocalEulerAngles.Value)) t.localEulerAngles = _lastLocalEulerAngles;
        if (FxUtil.Changed(ref _lastLocalScale, LocalScale.Value)) t.localScale = _lastLocalScale;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(LocalPosition)] = IOUtils.WriteFx(LocalPosition),
            [nameof(LocalEulerAngles)] = IOUtils.WriteFx(LocalEulerAngles),
            [nameof(LocalScale)] = IOUtils.WriteFx(LocalScale)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        LocalPosition = IOUtils.ReadFx(token, nameof(LocalPosition), LocalPosition);
        LocalEulerAngles = IOUtils.ReadFx(token, nameof(LocalEulerAngles), LocalEulerAngles);
        LocalScale = IOUtils.ReadFx(token, nameof(LocalScale), LocalScale);
    }

    public TransformSettings Copy() {
        return new TransformSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            LocalPosition = LocalPosition?.Copy(),
            LocalEulerAngles = LocalEulerAngles?.Copy(),
            LocalScale = LocalScale?.Copy()
        };
    }
}
