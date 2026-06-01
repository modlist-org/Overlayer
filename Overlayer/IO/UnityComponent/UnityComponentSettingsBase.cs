using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent;

public abstract class UnityComponentSettingsBase : ISettingsFile {
    public bool ComponentEnabled;
    public abstract bool ToUnity(GameObject target);
    public abstract bool FromUnity(GameObject source);

    public abstract JToken Serialize();
    public abstract void Deserialize(JToken token);
}