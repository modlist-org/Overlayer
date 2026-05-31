using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class GraphicRaycasterSettings : UnityComponentSettingsBase {
    public bool Enabled = true;

    public override void ToUnity(GameObject target) {
        var com = target.GetComponent<GraphicRaycasterSettings>();
        com.Enabled = Enabled;
    }

    public override void FromUnity(GameObject source) {
        var com = source.GetComponent<GraphicRaycasterSettings>();
        Enabled = com.Enabled;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = Enabled,
        };
    }

    public override void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
    }
}