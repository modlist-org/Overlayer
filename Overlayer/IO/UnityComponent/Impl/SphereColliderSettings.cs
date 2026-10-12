using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class SphereColliderSettings : UnityComponentSettingsBase, ICopyable<SphereColliderSettings> {
    public FxValue<Vector3> Center = new(Vector3.zero);
    public FxValue<float> Radius = new(0.5f);
    public FxValue<bool> IsTrigger = new(false);

    private Vector3 _lastCenter = Vector3.zero;
    private float _lastRadius = 0.5f;
    private bool _lastIsTrigger;
    private bool _lastEnabled = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Center)
        || FxUtil.HasFx(Radius)
        || FxUtil.HasFx(IsTrigger);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<SphereCollider>();
        if (com == null) {
            return false;
        }

        _lastCenter = Center.Value;
        _lastRadius = Radius.Value;
        _lastIsTrigger = IsTrigger.Value;
        _lastEnabled = ComponentEnabled.Value;
        com.center = _lastCenter;
        com.radius = _lastRadius;
        com.isTrigger = _lastIsTrigger;
        com.enabled = _lastEnabled;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<SphereCollider>();
        if (com == null) {
            return false;
        }

        Center.Value = com.center;
        Radius.Value = com.radius;
        IsTrigger.Value = com.isTrigger;
        ComponentEnabled.Value = com.enabled;
        _lastCenter = com.center;
        _lastRadius = com.radius;
        _lastIsTrigger = com.isTrigger;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<SphereCollider>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastEnabled, ComponentEnabled.Value)) com.enabled = _lastEnabled;
        if (FxUtil.Changed(ref _lastCenter, Center.Value)) com.center = _lastCenter;
        if (FxUtil.Changed(ref _lastRadius, Radius.Value)) com.radius = _lastRadius;
        if (FxUtil.Changed(ref _lastIsTrigger, IsTrigger.Value)) com.isTrigger = _lastIsTrigger;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Center)] = IOUtils.WriteFx(Center),
            [nameof(Radius)] = IOUtils.WriteFx(Radius),
            [nameof(IsTrigger)] = IOUtils.WriteFx(IsTrigger)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Center = IOUtils.ReadFx(token, nameof(Center), Center);
        Radius = IOUtils.ReadFx(token, nameof(Radius), Radius);
        IsTrigger = IOUtils.ReadFx(token, nameof(IsTrigger), IsTrigger);
    }

    public SphereColliderSettings Copy() {
        return new SphereColliderSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Center = Center?.Copy(),
            Radius = Radius?.Copy(),
            IsTrigger = IsTrigger?.Copy()
        };
    }
}
