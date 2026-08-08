using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class Rigidbody2DSettings : UnityComponentSettingsBase, ICopyable<Rigidbody2DSettings> {
    public RigidbodyType2D BodyType = RigidbodyType2D.Dynamic;
    public bool Simulated = true;
    public bool UseAutoMass = false;
    public float Mass = 1f;
    public float LinearDamping = 0f;
    public float AngularDamping = 0.05f;
    public float GravityScale = 1f;
    public CollisionDetectionMode2D CollisionDetectionMode = CollisionDetectionMode2D.Discrete;
    public RigidbodySleepMode2D SleepMode = RigidbodySleepMode2D.StartAwake;
    public RigidbodyInterpolation2D Interpolation = RigidbodyInterpolation2D.None;
    public RigidbodyConstraints2D Constraints = RigidbodyConstraints2D.None;
    public bool FreezeRotation = false;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Rigidbody2D>();
        if (com == null) {
            return false;
        }

        com.bodyType = BodyType;
        com.simulated = Simulated;
        com.useAutoMass = UseAutoMass;
        com.mass = Mass;
        com.drag = LinearDamping;
        com.angularDrag = AngularDamping;
        com.gravityScale = GravityScale;
        com.collisionDetectionMode = CollisionDetectionMode;
        com.sleepMode = SleepMode;
        com.interpolation = Interpolation;
        com.constraints = Constraints;
        com.freezeRotation = FreezeRotation;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Rigidbody2D>();
        if (com == null) {
            return false;
        }

        BodyType = com.bodyType;
        Simulated = com.simulated;
        UseAutoMass = com.useAutoMass;
        Mass = com.mass;
        LinearDamping = com.linearDamping;
        AngularDamping = com.angularDamping;
        GravityScale = com.gravityScale;
        CollisionDetectionMode = com.collisionDetectionMode;
        SleepMode = com.sleepMode;
        Interpolation = com.interpolation;
        Constraints = com.constraints;
        FreezeRotation = com.freezeRotation;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(BodyType)] = IOUtils.WriteEnum(BodyType),
            [nameof(Simulated)] = Simulated,
            [nameof(UseAutoMass)] = UseAutoMass,
            [nameof(Mass)] = Mass,
            [nameof(LinearDamping)] = LinearDamping,
            [nameof(AngularDamping)] = AngularDamping,
            [nameof(GravityScale)] = GravityScale,
            [nameof(CollisionDetectionMode)] = IOUtils.WriteEnum(CollisionDetectionMode),
            [nameof(SleepMode)] = IOUtils.WriteEnum(SleepMode),
            [nameof(Interpolation)] = IOUtils.WriteEnum(Interpolation),
            [nameof(Constraints)] = IOUtils.WriteEnum(Constraints),
            [nameof(FreezeRotation)] = FreezeRotation
        };
    }

    public override void Deserialize(JToken token) {
        BodyType = IOUtils.ReadEnum(token, nameof(BodyType), BodyType);
        Simulated = IOUtils.Read(token, nameof(Simulated), Simulated);
        UseAutoMass = IOUtils.Read(token, nameof(UseAutoMass), UseAutoMass);
        Mass = IOUtils.Read(token, nameof(Mass), Mass);
        LinearDamping = IOUtils.Read(token, nameof(LinearDamping), LinearDamping);
        AngularDamping = IOUtils.Read(token, nameof(AngularDamping), AngularDamping);
        GravityScale = IOUtils.Read(token, nameof(GravityScale), GravityScale);
        CollisionDetectionMode = IOUtils.ReadEnum(token, nameof(CollisionDetectionMode), CollisionDetectionMode);
        SleepMode = IOUtils.ReadEnum(token, nameof(SleepMode), SleepMode);
        Interpolation = IOUtils.ReadEnum(token, nameof(Interpolation), Interpolation);
        Constraints = IOUtils.ReadEnum(token, nameof(Constraints), Constraints);
        FreezeRotation = IOUtils.Read(token, nameof(FreezeRotation), FreezeRotation);
    }

    public Rigidbody2DSettings Copy() {
        return new Rigidbody2DSettings {
            BodyType = BodyType,
            Simulated = Simulated,
            UseAutoMass = UseAutoMass,
            Mass = Mass,
            LinearDamping = LinearDamping,
            AngularDamping = AngularDamping,
            GravityScale = GravityScale,
            CollisionDetectionMode = CollisionDetectionMode,
            SleepMode = SleepMode,
            Interpolation = Interpolation,
            Constraints = Constraints,
            FreezeRotation = FreezeRotation
        };
    }
}