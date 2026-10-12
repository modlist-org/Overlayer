using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class CapsuleColliderSettings : UnityComponentSettingsBase, ICopyable<CapsuleColliderSettings> {
    public FxValue<Vector3> Center = new(Vector3.zero);
    public FxValue<float> Radius = new(0.5f);
    public FxValue<float> Height = new(2f);
    public FxValue<int> Direction = new(1);
    public FxValue<bool> IsTrigger = new(false);

    private Vector3 _lastCenter = Vector3.zero;
    private float _lastRadius = 0.5f;
    private float _lastHeight = 2f;
    private int _lastDirection = 1;
    private bool _lastIsTrigger;
    private bool _lastEnabled = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Center)
        || FxUtil.HasFx(Radius)
        || FxUtil.HasFx(Height)
        || FxUtil.HasFx(Direction)
        || FxUtil.HasFx(IsTrigger);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<CapsuleCollider>();
        if (com == null) {
            return false;
        }

        _lastCenter = Center.Value;
        _lastRadius = Radius.Value;
        _lastHeight = Height.Value;
        _lastDirection = Mathf.Clamp(Direction.Value, 0, 2);
        _lastIsTrigger = IsTrigger.Value;
        _lastEnabled = ComponentEnabled.Value;
        com.center = _lastCenter;
        com.radius = _lastRadius;
        com.height = _lastHeight;
        com.direction = _lastDirection;
        com.isTrigger = _lastIsTrigger;
        com.enabled = _lastEnabled;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<CapsuleCollider>();
        if (com == null) {
            return false;
        }

        Center.Value = com.center;
        Radius.Value = com.radius;
        Height.Value = com.height;
        Direction.Value = com.direction;
        IsTrigger.Value = com.isTrigger;
        ComponentEnabled.Value = com.enabled;
        _lastCenter = com.center;
        _lastRadius = com.radius;
        _lastHeight = com.height;
        _lastDirection = com.direction;
        _lastIsTrigger = com.isTrigger;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<CapsuleCollider>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastEnabled, ComponentEnabled.Value)) com.enabled = _lastEnabled;
        if (FxUtil.Changed(ref _lastCenter, Center.Value)) com.center = _lastCenter;
        if (FxUtil.Changed(ref _lastRadius, Radius.Value)) com.radius = _lastRadius;
        if (FxUtil.Changed(ref _lastHeight, Height.Value)) com.height = _lastHeight;
        if (FxUtil.Changed(ref _lastDirection, Mathf.Clamp(Direction.Value, 0, 2))) com.direction = _lastDirection;
        if (FxUtil.Changed(ref _lastIsTrigger, IsTrigger.Value)) com.isTrigger = _lastIsTrigger;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Center)] = IOUtils.WriteFx(Center),
            [nameof(Radius)] = IOUtils.WriteFx(Radius),
            [nameof(Height)] = IOUtils.WriteFx(Height),
            [nameof(Direction)] = IOUtils.WriteFx(Direction),
            [nameof(IsTrigger)] = IOUtils.WriteFx(IsTrigger)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Center = IOUtils.ReadFx(token, nameof(Center), Center);
        Radius = IOUtils.ReadFx(token, nameof(Radius), Radius);
        Height = IOUtils.ReadFx(token, nameof(Height), Height);
        Direction = IOUtils.ReadFx(token, nameof(Direction), Direction);
        IsTrigger = IOUtils.ReadFx(token, nameof(IsTrigger), IsTrigger);
    }

    public CapsuleColliderSettings Copy() {
        return new CapsuleColliderSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Center = Center?.Copy(),
            Radius = Radius?.Copy(),
            Height = Height?.Copy(),
            Direction = Direction?.Copy(),
            IsTrigger = IsTrigger?.Copy()
        };
    }
}
