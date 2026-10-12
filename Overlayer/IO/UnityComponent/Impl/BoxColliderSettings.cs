using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class BoxColliderSettings : UnityComponentSettingsBase, ICopyable<BoxColliderSettings> {
    public FxValue<Vector3> Center = new(Vector3.zero);
    public FxValue<Vector3> Size = new(Vector3.one);
    public FxValue<bool> IsTrigger = new(false);

    private Vector3 _lastCenter = Vector3.zero;
    private Vector3 _lastSize = Vector3.one;
    private bool _lastIsTrigger;
    private bool _lastEnabled = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Center)
        || FxUtil.HasFx(Size)
        || FxUtil.HasFx(IsTrigger);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<BoxCollider>();
        if (com == null) {
            return false;
        }

        _lastCenter = Center.Value;
        _lastSize = Size.Value;
        _lastIsTrigger = IsTrigger.Value;
        _lastEnabled = ComponentEnabled.Value;
        com.center = _lastCenter;
        com.size = _lastSize;
        com.isTrigger = _lastIsTrigger;
        com.enabled = _lastEnabled;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<BoxCollider>();
        if (com == null) {
            return false;
        }

        Center.Value = com.center;
        Size.Value = com.size;
        IsTrigger.Value = com.isTrigger;
        ComponentEnabled.Value = com.enabled;
        _lastCenter = com.center;
        _lastSize = com.size;
        _lastIsTrigger = com.isTrigger;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<BoxCollider>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastEnabled, ComponentEnabled.Value)) com.enabled = _lastEnabled;
        if (FxUtil.Changed(ref _lastCenter, Center.Value)) com.center = _lastCenter;
        if (FxUtil.Changed(ref _lastSize, Size.Value)) com.size = _lastSize;
        if (FxUtil.Changed(ref _lastIsTrigger, IsTrigger.Value)) com.isTrigger = _lastIsTrigger;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Center)] = IOUtils.WriteFx(Center),
            [nameof(Size)] = IOUtils.WriteFx(Size),
            [nameof(IsTrigger)] = IOUtils.WriteFx(IsTrigger)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Center = IOUtils.ReadFx(token, nameof(Center), Center);
        Size = IOUtils.ReadFx(token, nameof(Size), Size);
        IsTrigger = IOUtils.ReadFx(token, nameof(IsTrigger), IsTrigger);
    }

    public BoxColliderSettings Copy() {
        return new BoxColliderSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Center = Center?.Copy(),
            Size = Size?.Copy(),
            IsTrigger = IsTrigger?.Copy()
        };
    }
}
