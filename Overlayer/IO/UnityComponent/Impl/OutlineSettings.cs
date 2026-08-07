using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class OutlineSettings : UnityComponentSettingsBase, ICopyable<OutlineSettings> {
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
        ToUnity(com);

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
        FromUnity(com);

        return true;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(EffectColor)] = IOUtils.Write(EffectColor),
            [nameof(EffectDistance)] = IOUtils.Write(EffectDistance),
            [nameof(UseGraphicAlpha)] = UseGraphicAlpha
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        if(token?["Enabled"] != null && token?[nameof(ComponentEnabled)] == null) {
            ComponentEnabled = IOUtils.Read(token, "Enabled", ComponentEnabled);
        }
        EffectColor = IOUtils.Read(token, nameof(EffectColor), EffectColor);
        EffectDistance = IOUtils.Read(token, nameof(EffectDistance), EffectDistance);
        UseGraphicAlpha = IOUtils.Read(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
    }

    public OutlineSettings Copy() {
        return new OutlineSettings {
            ComponentEnabled = ComponentEnabled,
            EffectColor = EffectColor,
            EffectDistance = EffectDistance,
            UseGraphicAlpha = UseGraphicAlpha
        };
    }
}
