using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent;

public abstract class UnitySettingsBase : ISettingsFile {
    public abstract void ToUnity(GameObject target);
    public abstract void FromUnity(GameObject source);

    public abstract JToken Serialize();
    public abstract void Deserialize(JToken token);
}