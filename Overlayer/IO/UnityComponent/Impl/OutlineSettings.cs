using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class OutlineSettings : UnityComponentSettingsBase, ICopyable<OutlineSettings> {
    public bool Enabled = true;
    public Color EffectColor = Color.red;
    public Vector2 EffectDistance = new(1f, -1f);
    public bool UseGraphicAlpha = true;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Outline>();
        if(com == null) {
            return false;
        }

        com.effectColor = EffectColor;
        com.effectDistance = EffectDistance;
        com.useGraphicAlpha = UseGraphicAlpha;
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
        UseGraphicAlpha = com.useGraphicAlpha;
        Enabled = com.enabled;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(EffectColor)] = IOUtils.Write(EffectColor),
            [nameof(EffectDistance)] = IOUtils.Write(EffectDistance),
            [nameof(UseGraphicAlpha)] = UseGraphicAlpha,
            [nameof(Enabled)] = Enabled,
        };
    }

    public override void Deserialize(JToken token) {
        EffectColor = IOUtils.Read(token, nameof(EffectColor), EffectColor);
        EffectDistance = IOUtils.Read(token, nameof(EffectDistance), EffectDistance);
        UseGraphicAlpha = IOUtils.Read(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
    }

    public OutlineSettings Copy() {
        return new OutlineSettings {
            EffectColor = EffectColor,
            EffectDistance = EffectDistance,
            UseGraphicAlpha = UseGraphicAlpha,
            Enabled = Enabled
        };
    }
}
