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
        if(com == null) {
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
        if(com == null) {
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
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Rigidbody2D>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastBodyType, BodyType.Value, v => com.bodyType = v);
        FxUtil.ApplyIfChanged(ref _lastSimulated, Simulated.Value, v => com.simulated = v);
        FxUtil.ApplyIfChanged(ref _lastUseAutoMass, UseAutoMass.Value, v => com.useAutoMass = v);
        FxUtil.ApplyIfChanged(ref _lastMass, Mass.Value, v => com.mass = v);
        FxUtil.ApplyIfChanged(ref _lastLinearDamping, LinearDamping.Value, v => com.linearDamping = v);
        FxUtil.ApplyIfChanged(ref _lastAngularDamping, AngularDamping.Value, v => com.angularDamping = v);
        FxUtil.ApplyIfChanged(ref _lastGravityScale, GravityScale.Value, v => com.gravityScale = v);
        FxUtil.ApplyIfChanged(ref _lastCollisionDetectionMode, CollisionDetectionMode.Value, v => com.collisionDetectionMode = v);
        FxUtil.ApplyIfChanged(ref _lastSleepMode, SleepMode.Value, v => com.sleepMode = v);
        FxUtil.ApplyIfChanged(ref _lastInterpolation, Interpolation.Value, v => com.interpolation = v);
        FxUtil.ApplyIfChanged(ref _lastConstraints, Constraints.Value, v => com.constraints = v);
        FxUtil.ApplyIfChanged(ref _lastFreezeRotation, FreezeRotation.Value, v => com.freezeRotation = v);
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
