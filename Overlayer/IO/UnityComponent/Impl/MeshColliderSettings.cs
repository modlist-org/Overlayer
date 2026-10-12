using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class MeshColliderSettings : UnityComponentSettingsBase, ICopyable<MeshColliderSettings> {
    public FxValue<bool> Convex = new(false);
    public FxValue<bool> IsTrigger = new(false);

    private bool _lastConvex;
    private bool _lastIsTrigger;
    private bool _lastEnabled = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Convex)
        || FxUtil.HasFx(IsTrigger);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<MeshCollider>();
        if (com == null) {
            return false;
        }

        _lastConvex = Convex.Value;
        _lastIsTrigger = IsTrigger.Value;
        _lastEnabled = ComponentEnabled.Value;
        com.convex = _lastConvex;
        com.isTrigger = _lastIsTrigger;
        com.enabled = _lastEnabled;
        // Follows the object's own MeshFilter; no separate mesh picker.
        var filter = target.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null && com.sharedMesh != filter.sharedMesh) {
            com.sharedMesh = filter.sharedMesh;
        }

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<MeshCollider>();
        if (com == null) {
            return false;
        }

        Convex.Value = com.convex;
        IsTrigger.Value = com.isTrigger;
        ComponentEnabled.Value = com.enabled;
        _lastConvex = com.convex;
        _lastIsTrigger = com.isTrigger;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<MeshCollider>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastEnabled, ComponentEnabled.Value)) com.enabled = _lastEnabled;
        if (FxUtil.Changed(ref _lastConvex, Convex.Value)) com.convex = _lastConvex;
        if (FxUtil.Changed(ref _lastIsTrigger, IsTrigger.Value)) com.isTrigger = _lastIsTrigger;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Convex)] = IOUtils.WriteFx(Convex),
            [nameof(IsTrigger)] = IOUtils.WriteFx(IsTrigger)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Convex = IOUtils.ReadFx(token, nameof(Convex), Convex);
        IsTrigger = IOUtils.ReadFx(token, nameof(IsTrigger), IsTrigger);
    }

    public MeshColliderSettings Copy() {
        return new MeshColliderSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Convex = Convex?.Copy(),
            IsTrigger = IsTrigger?.Copy()
        };
    }
}

// shortcut: cooking options omitted; uses the MeshFilter's mesh when present.
