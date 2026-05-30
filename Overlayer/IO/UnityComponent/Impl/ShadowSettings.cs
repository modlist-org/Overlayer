using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class ShadowSettings : ISettingsFile {
    public Vector2 EffectDistance = new(6, -6);
    public Color EffectColor = Color.black;
    public bool UseGraphicAlpha = true;

    public JToken Serialize() {
        return new JObject {
            [nameof(EffectDistance)] = IOUtils.Write(EffectDistance),
            [nameof(EffectColor)] = IOUtils.Write(EffectColor),
            [nameof(UseGraphicAlpha)] = UseGraphicAlpha
        };
    }

    public void Deserialize(JToken token) {
        EffectDistance = IOUtils.Read(token, nameof(EffectDistance), EffectDistance);
        EffectColor = IOUtils.Read(token, nameof(EffectColor), EffectColor);
        UseGraphicAlpha = IOUtils.Read(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
    }
}