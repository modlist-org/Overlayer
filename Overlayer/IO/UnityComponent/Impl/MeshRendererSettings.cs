using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overlayer.IO.UnityComponent.Impl;

public class MeshRendererSettings : UnityComponentSettingsBase, ICopyable<MeshRendererSettings> {
    public FxValue<ShadowCastingMode> CastShadows = new(ShadowCastingMode.On);
    public FxValue<bool> ReceiveShadows = new(true);

    private ShadowCastingMode _lastCastShadows = ShadowCastingMode.On;
    private bool _lastReceiveShadows = true;
    private bool _lastEnabled = true;

    // Single shared instance so per-object material clones never leak on remove.
    private static Material _defaultMaterial;

    internal static Material DefaultSharedMaterial() {
        if (_defaultMaterial == null) {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _defaultMaterial = temp.GetComponent<MeshRenderer>().sharedMaterial;
            UnityEngine.Object.Destroy(temp);
        }
        return _defaultMaterial;
    }

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(CastShadows)
        || FxUtil.HasFx(ReceiveShadows);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<MeshRenderer>();
        if (com == null) {
            return false;
        }

        _lastCastShadows = CastShadows.Value;
        _lastReceiveShadows = ReceiveShadows.Value;
        _lastEnabled = ComponentEnabled.Value;
        com.shadowCastingMode = _lastCastShadows;
        com.receiveShadows = _lastReceiveShadows;
        com.enabled = _lastEnabled;
        // Fresh AddComponent<MeshRenderer>() has no material -> magenta error shader.
        if (com.sharedMaterial == null) com.sharedMaterial = DefaultSharedMaterial();

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<MeshRenderer>();
        if (com == null) {
            return false;
        }

        CastShadows.Value = com.shadowCastingMode;
        ReceiveShadows.Value = com.receiveShadows;
        ComponentEnabled.Value = com.enabled;
        _lastCastShadows = com.shadowCastingMode;
        _lastReceiveShadows = com.receiveShadows;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<MeshRenderer>();
        if (com == null) {
            return;
        }

        if (FxUtil.Changed(ref _lastEnabled, ComponentEnabled.Value)) com.enabled = _lastEnabled;
        if (FxUtil.Changed(ref _lastCastShadows, CastShadows.Value)) com.shadowCastingMode = _lastCastShadows;
        if (FxUtil.Changed(ref _lastReceiveShadows, ReceiveShadows.Value)) com.receiveShadows = _lastReceiveShadows;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(CastShadows)] = IOUtils.WriteFx(CastShadows),
            [nameof(ReceiveShadows)] = IOUtils.WriteFx(ReceiveShadows)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        CastShadows = IOUtils.ReadFx(token, nameof(CastShadows), CastShadows);
        ReceiveShadows = IOUtils.ReadFx(token, nameof(ReceiveShadows), ReceiveShadows);
    }

    public MeshRendererSettings Copy() {
        return new MeshRendererSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            CastShadows = CastShadows?.Copy(),
            ReceiveShadows = ReceiveShadows?.Copy()
        };
    }
}

// shortcut: materials intentionally unexposed (per-object instances leak on remove); only the shared default is assigned.
