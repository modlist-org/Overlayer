using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class GraphicRaycasterSettings : UnityComponentSettingsBase, ICopyable<GraphicRaycasterSettings> {
    public FxValue<bool> Enabled = new(true);

    private bool _lastEnabled = true;

    public override bool HasAnyFx => base.HasAnyFx || FxUtil.HasFx(Enabled);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<GraphicRaycaster>();
        if(com == null) {
            return false;
        }

        _lastEnabled = Enabled.Value;
        com.enabled = _lastEnabled;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<GraphicRaycaster>();
        if(com == null) {
            return false;
        }

        Enabled.Value = com.enabled;
        _lastEnabled = com.enabled;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<GraphicRaycaster>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastEnabled, Enabled.Value, v => com.enabled = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = IOUtils.WriteFx(Enabled),
        };
    }

    public override void Deserialize(JToken token)
        => Enabled = IOUtils.ReadFx(token, nameof(Enabled), Enabled);

    public GraphicRaycasterSettings Copy() {
        return new GraphicRaycasterSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Enabled = Enabled?.Copy()
        };
    }
}
