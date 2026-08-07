using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class MaskSettings : UnityComponentSettingsBase, ICopyable<MaskSettings> {
    public bool ShowMaskGraphic = true;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Mask>();
        if(com == null) {
            return false;
        }

        com.showMaskGraphic = ShowMaskGraphic;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Mask>();
        if(com == null) {
            return false;
        }

        ShowMaskGraphic = com.showMaskGraphic;
        FromUnity(com);

        return true;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(ShowMaskGraphic)] = ShowMaskGraphic,
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        ShowMaskGraphic = IOUtils.Read(token, nameof(ShowMaskGraphic), ShowMaskGraphic);
    }

    public MaskSettings Copy() {
        return new MaskSettings {
            ComponentEnabled = ComponentEnabled,
            ShowMaskGraphic = ShowMaskGraphic,
        };
    }
}
