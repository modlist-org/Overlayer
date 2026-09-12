using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class MaskSettings : UnityComponentSettingsBase, ICopyable<MaskSettings> {
    public FxValue<bool> ShowMaskGraphic = new(true);

    private bool _lastShowMaskGraphic = true;

    public override bool HasAnyFx => base.HasAnyFx || FxUtil.HasFx(ShowMaskGraphic);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Mask>();
        if(com == null) {
            return false;
        }

        _lastShowMaskGraphic = ShowMaskGraphic.Value;
        com.showMaskGraphic = _lastShowMaskGraphic;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Mask>();
        if(com == null) {
            return false;
        }

        ShowMaskGraphic.Value = com.showMaskGraphic;
        _lastShowMaskGraphic = com.showMaskGraphic;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Mask>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        FxUtil.ApplyIfChanged(ref _lastShowMaskGraphic, ShowMaskGraphic.Value, v => com.showMaskGraphic = v);
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(ShowMaskGraphic)] = IOUtils.WriteFx(ShowMaskGraphic),
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        ShowMaskGraphic = IOUtils.ReadFx(token, nameof(ShowMaskGraphic), ShowMaskGraphic);
    }

    public MaskSettings Copy() {
        return new MaskSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            ShowMaskGraphic = ShowMaskGraphic?.Copy(),
        };
    }
}
