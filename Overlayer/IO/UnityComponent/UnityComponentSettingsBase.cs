using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent;

public abstract class UnityComponentSettingsBase : ISettingsFile {
    public FxValue<bool> ComponentEnabled = new(true);

    private bool _lastEnabled = true;

    public virtual bool HasAnyFx => FxUtil.HasFx(ComponentEnabled);

    protected void ToUnity(Behaviour component) {
        _lastEnabled = ComponentEnabled.Value;
        component.enabled = _lastEnabled;
    }

    protected void FromUnity(Behaviour component) {
        ComponentEnabled.Value = component.enabled;
        _lastEnabled = component.enabled;
    }

    protected bool RefreshEnabled(Behaviour component) {
        return FxUtil.ApplyIfChanged(ref _lastEnabled, ComponentEnabled.Value, v => component.enabled = v);
    }

    protected JObject SerializeComponent(JObject properties) {
        if(ComponentEnabled.UseFx || !ComponentEnabled.Value) {
            properties[nameof(ComponentEnabled)] = IOUtils.WriteFx(ComponentEnabled);
        }
        return properties;
    }

    protected void DeserializeComponent(JToken token) => ComponentEnabled = IOUtils.ReadFx(token, nameof(ComponentEnabled), ComponentEnabled);

    public virtual void RefreshFx(GameObject target) { }

    public abstract bool ToUnity(GameObject target);
    public abstract bool FromUnity(GameObject source);

    public abstract JToken Serialize();
    public abstract void Deserialize(JToken token);
}
