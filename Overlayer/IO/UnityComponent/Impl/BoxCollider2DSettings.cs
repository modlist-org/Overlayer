#if !IL2CPP
using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class BoxCollider2DSettings : UnityComponentSettingsBase, ICopyable<BoxCollider2DSettings> {
    public Vector2 Size = Vector2.one;
    public Vector2 Offset = Vector2.zero;
    public bool IsTrigger = false;
    public bool UsedByEffector = false;
    public bool UsedByComposite = false;
    public float EdgeRadius = 0f;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<BoxCollider2D>();
        if (com == null) {
            return false;
        }

        com.size = Size;
        com.offset = Offset;
        com.isTrigger = IsTrigger;
        com.usedByEffector = UsedByEffector;
        com.usedByComposite = UsedByComposite;
        com.edgeRadius = EdgeRadius;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<BoxCollider2D>();
        if (com == null) {
            return false;
        }

        Size = com.size;
        Offset = com.offset;
        IsTrigger = com.isTrigger;
        UsedByEffector = com.usedByEffector;
        UsedByComposite = com.usedByComposite;
        EdgeRadius = com.edgeRadius;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Size)] = IOUtils.Write(Size),
            [nameof(Offset)] =IOUtils.Write(Offset),
            [nameof(IsTrigger)] = IsTrigger,
            [nameof(UsedByEffector)] = UsedByEffector,
            [nameof(UsedByComposite)] = UsedByComposite,
            [nameof(EdgeRadius)] = EdgeRadius
        };
    }

    public override void Deserialize(JToken token) {
        Size = IOUtils.Read(token, nameof(Size), Size);
        Offset = IOUtils.Read(token, nameof(Offset), Offset);
        IsTrigger = IOUtils.Read(token, nameof(IsTrigger), IsTrigger);
        UsedByEffector = IOUtils.Read(token, nameof(UsedByEffector), UsedByEffector);
        UsedByComposite = IOUtils.Read(token, nameof(UsedByComposite), UsedByComposite);
        EdgeRadius = IOUtils.Read(token, nameof(EdgeRadius), EdgeRadius);
    }

    public BoxCollider2DSettings Copy() {
        return new BoxCollider2DSettings {
            Size = Size,
            Offset = Offset,
            IsTrigger = IsTrigger,
            UsedByEffector = UsedByEffector,
            UsedByComposite = UsedByComposite,
            EdgeRadius = EdgeRadius
        };
    }
}
#endif