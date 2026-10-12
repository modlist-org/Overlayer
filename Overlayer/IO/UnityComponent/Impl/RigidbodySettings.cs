using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class RigidbodySettings : UnityComponentSettingsBase, ICopyable<RigidbodySettings> {
    public FxValue<float> Mass = new(1f);
    public FxValue<float> Drag = new(0f);
    public FxValue<float> AngularDrag = new(0.05f);
    public FxValue<bool> UseGravity = new(true);
    public FxValue<bool> IsKinematic = new(false);

    private float _lastMass = 1f;
    private float _lastDrag;
    private float _lastAngularDrag = 0.05f;
    private bool _lastUseGravity = true;
    private bool _lastIsKinematic;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Mass)
        || FxUtil.HasFx(Drag)
        || FxUtil.HasFx(AngularDrag)
        || FxUtil.HasFx(UseGravity)
        || FxUtil.HasFx(IsKinematic);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Rigidbody>();
        if (com == null) {
            return false;
        }

        _lastMass = Mass.Value;
        _lastDrag = Drag.Value;
        _lastAngularDrag = AngularDrag.Value;
        _lastUseGravity = UseGravity.Value;
        _lastIsKinematic = IsKinematic.Value;
        com.mass = _lastMass;
        com.drag = _lastDrag;
        com.angularDrag = _lastAngularDrag;
        com.useGravity = _lastUseGravity;
        com.isKinematic = _lastIsKinematic;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Rigidbody>();
        if (com == null) {
            return false;
        }

        Mass.Value = com.mass;
        Drag.Value = com.drag;
        AngularDrag.Value = com.angularDrag;
        UseGravity.Value = com.useGravity;
        IsKinematic.Value = com.isKinematic;
        _lastMass = com.mass;
        _lastDrag = com.drag;
        _lastAngularDrag = com.angularDrag;
        _lastUseGravity = com.useGravity;
        _lastIsKinematic = com.isKinematic;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Rigidbody>();
        if (com == null) {
            return;
        }

        // Rigidbody has no enabled flag: ComponentEnabled is config-only here.
        if (FxUtil.Changed(ref _lastMass, Mass.Value)) com.mass = _lastMass;
        if (FxUtil.Changed(ref _lastDrag, Drag.Value)) com.drag = _lastDrag;
        if (FxUtil.Changed(ref _lastAngularDrag, AngularDrag.Value)) com.angularDrag = _lastAngularDrag;
        if (FxUtil.Changed(ref _lastUseGravity, UseGravity.Value)) com.useGravity = _lastUseGravity;
        if (FxUtil.Changed(ref _lastIsKinematic, IsKinematic.Value)) com.isKinematic = _lastIsKinematic;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Mass)] = IOUtils.WriteFx(Mass),
            [nameof(Drag)] = IOUtils.WriteFx(Drag),
            [nameof(AngularDrag)] = IOUtils.WriteFx(AngularDrag),
            [nameof(UseGravity)] = IOUtils.WriteFx(UseGravity),
            [nameof(IsKinematic)] = IOUtils.WriteFx(IsKinematic)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Mass = IOUtils.ReadFx(token, nameof(Mass), Mass);
        Drag = IOUtils.ReadFx(token, nameof(Drag), Drag);
        AngularDrag = IOUtils.ReadFx(token, nameof(AngularDrag), AngularDrag);
        UseGravity = IOUtils.ReadFx(token, nameof(UseGravity), UseGravity);
        IsKinematic = IOUtils.ReadFx(token, nameof(IsKinematic), IsKinematic);
    }

    public RigidbodySettings Copy() {
        return new RigidbodySettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Mass = Mass?.Copy(),
            Drag = Drag?.Copy(),
            AngularDrag = AngularDrag?.Copy(),
            UseGravity = UseGravity?.Copy(),
            IsKinematic = IsKinematic?.Copy()
        };
    }
}

// shortcut: constraints/interpolation/collision-detection omitted; extend here when needed.
