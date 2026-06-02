using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class OutlineSettings : UnityComponentSettingsBase {
    public bool Enabled = true;
    public Color EffectColor = Color.red;
    public Vector2 EffectDistance = new(1f, -1f);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Outline>();
        if(com == null) {
            return false;
        }

        com.effectColor = EffectColor;
        com.effectDistance = EffectDistance;
        com.enabled = Enabled;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Outline>();
        if(com == null) {
            return false;
        }

        EffectColor = com.effectColor;
        EffectDistance = com.effectDistance;
        Enabled = com.enabled;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(EffectColor)] = IOUtils.Write(EffectColor),
            [nameof(EffectDistance)] = IOUtils.Write(EffectDistance),
            [nameof(Enabled)] = Enabled,
        };
    }

    public override void Deserialize(JToken token) {
        EffectColor = IOUtils.Read(token, nameof(EffectColor), EffectColor);
        EffectDistance = IOUtils.Read(token, nameof(EffectDistance), EffectDistance);
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
    }
}