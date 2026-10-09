#if !IL2CPP
using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class Rigidbody2DSettings : UnityComponentSettingsBase, ICopyable<Rigidbody2DSettings> {
    public FxValue<RigidbodyType2D> BodyType = new(RigidbodyType2D.Dynamic);
    public FxValue<bool> Simulated = new(true);
    public FxValue<bool> UseAutoMass = new(false);
    public FxValue<float> Mass = new(1f);
    public FxValue<float> LinearDamping = new(0f);
    public FxValue<float> AngularDamping = new(0.05f);
    public FxValue<float> GravityScale = new(1f);
    public FxValue<CollisionDetectionMode2D> CollisionDetectionMode = new(CollisionDetectionMode2D.Discrete);
    public FxValue<RigidbodySleepMode2D> SleepMode = new(RigidbodySleepMode2D.StartAwake);
    public FxValue<RigidbodyInterpolation2D> Interpolation = new(RigidbodyInterpolation2D.None);
    public FxValue<RigidbodyConstraints2D> Constraints = new(RigidbodyConstraints2D.None);
    public FxValue<bool> FreezeRotation = new(false);

    private RigidbodyType2D _lastBodyType = RigidbodyType2D.Dynamic;
    private bool _lastSimulated = true;
    private bool _lastUseAutoMass;
    private float _lastMass = 1f;
    private float _lastLinearDamping;
    private float _lastAngularDamping = 0.05f;
    private float _lastGravityScale = 1f;
    private CollisionDetectionMode2D _lastCollisionDetectionMode = CollisionDetectionMode2D.Discrete;
    private RigidbodySleepMode2D _lastSleepMode = RigidbodySleepMode2D.StartAwake;
    private RigidbodyInterpolation2D _lastInterpolation = RigidbodyInterpolation2D.None;
    private RigidbodyConstraints2D _lastConstraints = RigidbodyConstraints2D.None;
    private bool _lastFreezeRotation;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(BodyType)
        || FxUtil.HasFx(Simulated)
        || FxUtil.HasFx(UseAutoMass)
        || FxUtil.HasFx(Mass)
        || FxUtil.HasFx(LinearDamping)
        || FxUtil.HasFx(AngularDamping)
        || FxUtil.HasFx(GravityScale)
        || FxUtil.HasFx(CollisionDetectionMode)
        || FxUtil.HasFx(SleepMode)
        || FxUtil.HasFx(Interpolation)
        || FxUtil.HasFx(Constraints)
        || FxUtil.HasFx(FreezeRotation);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Rigidbody2D>();
        if (com == null) {
            return false;
        }

        _lastBodyType = BodyType.Value;
        _lastSimulated = Simulated.Value;
        _lastUseAutoMass = UseAutoMass.Value;
        _lastMass = Mass.Value;
        _lastLinearDamping = LinearDamping.Value;
        _lastAngularDamping = AngularDamping.Value;
        _lastGravityScale = GravityScale.Value;
        _lastCollisionDetectionMode = CollisionDetectionMode.Value;
        _lastSleepMode = SleepMode.Value;
        _lastInterpolation = Interpolation.Value;
        _lastConstraints = Constraints.Value;
        _lastFreezeRotation = FreezeRotation.Value;
        com.bodyType = _lastBodyType;
        com.simulated = _lastSimulated;
        com.useAutoMass = _lastUseAutoMass;
        com.mass = _lastMass;
        com.linearDamping = _lastLinearDamping;
        com.angularDamping = _lastAngularDamping;
        com.gravityScale = _lastGravityScale;
        com.collisionDetectionMode = _lastCollisionDetectionMode;
        com.sleepMode = _lastSleepMode;
        com.interpolation = _lastInterpolation;
        com.constraints = _lastConstraints;
        com.freezeRotation = _lastFreezeRotation;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Rigidbody2D>();
        if (com == null) {
            return false;
        }

        BodyType.Value = com.bodyType;
        Simulated.Value = com.simulated;
        UseAutoMass.Value = com.useAutoMass;
        Mass.Value = com.mass;
        LinearDamping.Value = com.linearDamping;
        AngularDamping.Value = com.angularDamping;
        GravityScale.Value = com.gravityScale;
        CollisionDetectionMode.Value = com.collisionDetectionMode;
        SleepMode.Value = com.sleepMode;
        Interpolation.Value = com.interpolation;
        Constraints.Value = com.constraints;
        FreezeRotation.Value = com.freezeRotation;
        _lastBodyType = com.bodyType;
        _lastSimulated = com.simulated;
        _lastUseAutoMass = com.useAutoMass;
        _lastMass = com.mass;
        _lastLinearDamping = com.linearDamping;
        _lastAngularDamping = com.angularDamping;
        _lastGravityScale = com.gravityScale;
        _lastCollisionDetectionMode = com.collisionDetectionMode;
        _lastSleepMode = com.sleepMode;
        _lastInterpolation = com.interpolation;
        _lastConstraints = com.constraints;
        _lastFreezeRotation = com.freezeRotation;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Rigidbody2D>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastBodyType, BodyType.Value)) com.bodyType = _lastBodyType;
        if (FxUtil.Changed(ref _lastSimulated, Simulated.Value)) com.simulated = _lastSimulated;
        if (FxUtil.Changed(ref _lastUseAutoMass, UseAutoMass.Value)) com.useAutoMass = _lastUseAutoMass;
        if (FxUtil.Changed(ref _lastMass, Mass.Value)) com.mass = _lastMass;
        if (FxUtil.Changed(ref _lastLinearDamping, LinearDamping.Value)) com.linearDamping = _lastLinearDamping;
        if (FxUtil.Changed(ref _lastAngularDamping, AngularDamping.Value)) com.angularDamping = _lastAngularDamping;
        if (FxUtil.Changed(ref _lastGravityScale, GravityScale.Value)) com.gravityScale = _lastGravityScale;
        if (FxUtil.Changed(ref _lastCollisionDetectionMode, CollisionDetectionMode.Value)) com.collisionDetectionMode = _lastCollisionDetectionMode;
        if (FxUtil.Changed(ref _lastSleepMode, SleepMode.Value)) com.sleepMode = _lastSleepMode;
        if (FxUtil.Changed(ref _lastInterpolation, Interpolation.Value)) com.interpolation = _lastInterpolation;
        if (FxUtil.Changed(ref _lastConstraints, Constraints.Value)) com.constraints = _lastConstraints;
        if (FxUtil.Changed(ref _lastFreezeRotation, FreezeRotation.Value)) com.freezeRotation = _lastFreezeRotation;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(BodyType)] = IOUtils.WriteFx(BodyType),
            [nameof(Simulated)] = IOUtils.WriteFx(Simulated),
            [nameof(UseAutoMass)] = IOUtils.WriteFx(UseAutoMass),
            [nameof(Mass)] = IOUtils.WriteFx(Mass),
            [nameof(LinearDamping)] = IOUtils.WriteFx(LinearDamping),
            [nameof(AngularDamping)] = IOUtils.WriteFx(AngularDamping),
            [nameof(GravityScale)] = IOUtils.WriteFx(GravityScale),
            [nameof(CollisionDetectionMode)] = IOUtils.WriteFx(CollisionDetectionMode),
            [nameof(SleepMode)] = IOUtils.WriteFx(SleepMode),
            [nameof(Interpolation)] = IOUtils.WriteFx(Interpolation),
            [nameof(Constraints)] = IOUtils.WriteFx(Constraints),
            [nameof(FreezeRotation)] = IOUtils.WriteFx(FreezeRotation)
        };
    }

    public override void Deserialize(JToken token) {
        BodyType = IOUtils.ReadFx(token, nameof(BodyType), BodyType);
        Simulated = IOUtils.ReadFx(token, nameof(Simulated), Simulated);
        UseAutoMass = IOUtils.ReadFx(token, nameof(UseAutoMass), UseAutoMass);
        Mass = IOUtils.ReadFx(token, nameof(Mass), Mass);
        LinearDamping = IOUtils.ReadFx(token, nameof(LinearDamping), LinearDamping);
        AngularDamping = IOUtils.ReadFx(token, nameof(AngularDamping), AngularDamping);
        GravityScale = IOUtils.ReadFx(token, nameof(GravityScale), GravityScale);
        CollisionDetectionMode = IOUtils.ReadFx(token, nameof(CollisionDetectionMode), CollisionDetectionMode);
        SleepMode = IOUtils.ReadFx(token, nameof(SleepMode), SleepMode);
        Interpolation = IOUtils.ReadFx(token, nameof(Interpolation), Interpolation);
        Constraints = IOUtils.ReadFx(token, nameof(Constraints), Constraints);
        FreezeRotation = IOUtils.ReadFx(token, nameof(FreezeRotation), FreezeRotation);
    }

    public Rigidbody2DSettings Copy() {
        return new Rigidbody2DSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            BodyType = BodyType?.Copy(),
            Simulated = Simulated?.Copy(),
            UseAutoMass = UseAutoMass?.Copy(),
            Mass = Mass?.Copy(),
            LinearDamping = LinearDamping?.Copy(),
            AngularDamping = AngularDamping?.Copy(),
            GravityScale = GravityScale?.Copy(),
            CollisionDetectionMode = CollisionDetectionMode?.Copy(),
            SleepMode = SleepMode?.Copy(),
            Interpolation = Interpolation?.Copy(),
            Constraints = Constraints?.Copy(),
            FreezeRotation = FreezeRotation?.Copy()
        };
    }
}
#endif
