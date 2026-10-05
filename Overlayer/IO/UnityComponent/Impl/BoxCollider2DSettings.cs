#if !IL2CPP
using Newtonsoft.Json.Linq;
using Overlayer.Compat;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class BoxCollider2DSettings : UnityComponentSettingsBase, ICopyable<BoxCollider2DSettings> {
    public FxValue<Vector2> Size = new(Vector2.one);
    public FxValue<Vector2> Offset = new(Vector2.zero);
    public FxValue<bool> IsTrigger = new(false);
    public FxValue<bool> UsedByEffector = new(false);
    // int-based (see TmpCompat rationale): Collider2D.CompositeOperation does not
    // exist on old Unity. Values: None=0, Merge=1, Intersect=2, Difference=3, Flip=4.
    public FxValue<int> CompositeOperation = new(ColliderCompat.CompositeNone);
    public FxValue<float> EdgeRadius = new(0f);

    private Vector2 _lastSize = Vector2.one;
    private Vector2 _lastOffset = Vector2.zero;
    private bool _lastIsTrigger;
    private bool _lastUsedByEffector;
    private int _lastCompositeOperation = ColliderCompat.CompositeNone;
    private float _lastEdgeRadius;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Size)
        || FxUtil.HasFx(Offset)
        || FxUtil.HasFx(IsTrigger)
        || FxUtil.HasFx(UsedByEffector)
        || FxUtil.HasFx(CompositeOperation)
        || FxUtil.HasFx(EdgeRadius);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<BoxCollider2D>();
        if(com == null) {
            return false;
        }

        _lastSize = Size.Value;
        _lastOffset = Offset.Value;
        _lastIsTrigger = IsTrigger.Value;
        _lastUsedByEffector = UsedByEffector.Value;
        _lastCompositeOperation = CompositeOperation.Value;
        _lastEdgeRadius = EdgeRadius.Value;
        com.size = _lastSize;
        com.offset = _lastOffset;
        com.isTrigger = _lastIsTrigger;
        com.usedByEffector = _lastUsedByEffector;
        ColliderCompat.SetCompositeOperation(com, _lastCompositeOperation);
        com.edgeRadius = _lastEdgeRadius;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<BoxCollider2D>();
        if(com == null) {
            return false;
        }

        Size.Value = com.size;
        Offset.Value = com.offset;
        IsTrigger.Value = com.isTrigger;
        UsedByEffector.Value = com.usedByEffector;
        CompositeOperation.Value = ColliderCompat.GetCompositeOperation(com);
        EdgeRadius.Value = com.edgeRadius;
        _lastSize = com.size;
        _lastOffset = com.offset;
        _lastIsTrigger = com.isTrigger;
        _lastUsedByEffector = com.usedByEffector;
        _lastCompositeOperation = ColliderCompat.GetCompositeOperation(com);
        _lastEdgeRadius = com.edgeRadius;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<BoxCollider2D>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastSize, Size.Value, v => com.size = v);
        FxUtil.ApplyIfChanged(ref _lastOffset, Offset.Value, v => com.offset = v);
        FxUtil.ApplyIfChanged(ref _lastIsTrigger, IsTrigger.Value, v => com.isTrigger = v);
        FxUtil.ApplyIfChanged(ref _lastUsedByEffector, UsedByEffector.Value, v => com.usedByEffector = v);
        FxUtil.ApplyIfChanged(ref _lastCompositeOperation, CompositeOperation.Value, v => ColliderCompat.SetCompositeOperation(com, v));
        FxUtil.ApplyIfChanged(ref _lastEdgeRadius, EdgeRadius.Value, v => com.edgeRadius = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Size)] = IOUtils.WriteFx(Size),
            [nameof(Offset)] = IOUtils.WriteFx(Offset),
            [nameof(IsTrigger)] = IOUtils.WriteFx(IsTrigger),
            [nameof(UsedByEffector)] = IOUtils.WriteFx(UsedByEffector),
            [nameof(CompositeOperation)] = IOUtils.WriteFx(CompositeOperation),
            [nameof(EdgeRadius)] = IOUtils.WriteFx(EdgeRadius)
        };
    }

    public override void Deserialize(JToken token) {
        Size = IOUtils.ReadFx(token, nameof(Size), Size);
        Offset = IOUtils.ReadFx(token, nameof(Offset), Offset);
        IsTrigger = IOUtils.ReadFx(token, nameof(IsTrigger), IsTrigger);
        UsedByEffector = IOUtils.ReadFx(token, nameof(UsedByEffector), UsedByEffector);
        CompositeOperation = ReadCompositeOperation(token, nameof(CompositeOperation), CompositeOperation);
        EdgeRadius = IOUtils.ReadFx(token, nameof(EdgeRadius), EdgeRadius);
    }

    // Old files store the enum name ("None", "Merge", ...); the field is now
    // int-based, so migrate on read.
    private static FxValue<int> ReadCompositeOperation(JToken token, string key, FxValue<int> fallback) {
        if(token is JObject obj && obj.TryGetValue(key, out var child)) {
            if(child is JValue value && value.Type == JTokenType.String) {
                obj[key] = new JValue(ColliderCompat.CompositeFromName(value.Value<string>(), ColliderCompat.CompositeNone));
            } else if(child is JObject fx && fx.TryGetValue("Value", out var inner)
                && inner is JValue innerValue && innerValue.Type == JTokenType.String) {
                fx["Value"] = new JValue(ColliderCompat.CompositeFromName(innerValue.Value<string>(), ColliderCompat.CompositeNone));
            }
        }
        return IOUtils.ReadFx(token, key, fallback);
    }

    public BoxCollider2DSettings Copy() {
        return new BoxCollider2DSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Size = Size?.Copy(),
            Offset = Offset?.Copy(),
            IsTrigger = IsTrigger?.Copy(),
            UsedByEffector = UsedByEffector?.Copy(),
            CompositeOperation = CompositeOperation?.Copy(),
            EdgeRadius = EdgeRadius?.Copy()
        };
    }
}
#endif
