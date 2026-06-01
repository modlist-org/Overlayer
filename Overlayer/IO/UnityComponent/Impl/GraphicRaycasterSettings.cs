using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class GraphicRaycasterSettings : UnityComponentSettingsBase {
    public bool Enabled = true;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<GraphicRaycasterSettings>();
        if (com == null) {
            return false;
        }
        
        com.Enabled = Enabled;
        
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<GraphicRaycasterSettings>();
        if (com == null) {
            return false;
        }
        
        Enabled = com.Enabled;
        
        return true;
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