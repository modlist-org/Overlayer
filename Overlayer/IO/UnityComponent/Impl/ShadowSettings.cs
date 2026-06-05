using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ShadowSettings : UnityComponentSettingsBase, ICopyable<ShadowSettings> {
    public Vector2 EffectDistance = new(6, -6);
    public Color EffectColor = Color.black;
    public bool UseGraphicAlpha = true;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Shadow>();
        if (com == null) {
            return false;
        }
        
        com.effectDistance = EffectDistance;
        com.effectColor = EffectColor;
        com.useGraphicAlpha = UseGraphicAlpha;
        
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Shadow>();
        if (com == null) {
            return false;
        }
        
        EffectDistance = com.effectDistance;
        EffectColor = com.effectColor;
        UseGraphicAlpha = com.useGraphicAlpha;
        
        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(EffectDistance)] = IOUtils.Write(EffectDistance),
            [nameof(EffectColor)] = IOUtils.Write(EffectColor),
            [nameof(UseGraphicAlpha)] = UseGraphicAlpha
        };
    }

    public override void Deserialize(JToken token) {
        EffectDistance = IOUtils.Read(token, nameof(EffectDistance), EffectDistance);
        EffectColor = IOUtils.Read(token, nameof(EffectColor), EffectColor);
        UseGraphicAlpha = IOUtils.Read(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
    }

    public ShadowSettings Copy() {
        return new ShadowSettings {
            EffectDistance = EffectDistance,
            EffectColor = EffectColor,
            UseGraphicAlpha = UseGraphicAlpha
        };
    }
}