using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent;

public abstract class UnityComponentSettingsBase : ISettingsFile {
    public bool ComponentEnabled = true;

    protected void ToUnity(Behaviour component) => component.enabled = ComponentEnabled;

    protected void FromUnity(Behaviour component) => ComponentEnabled = component.enabled;

    protected JObject SerializeComponent(JObject properties) {
        if(!ComponentEnabled) {
            properties[nameof(ComponentEnabled)] = false;
        }
        return properties;
    }

    protected void DeserializeComponent(JToken token) => ComponentEnabled = IOUtils.Read(token, nameof(ComponentEnabled), ComponentEnabled);

    public abstract bool ToUnity(GameObject target);
    public abstract bool FromUnity(GameObject source);

    public abstract JToken Serialize();
    public abstract void Deserialize(JToken token);
}
