using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ShadowSettings : UnityComponentSettingsBase, ICopyable<ShadowSettings> {
    public FxValue<Vector2> EffectDistance = new(new Vector2(6, -6));
    public FxValue<Color> EffectColor = new(Color.black);
    public FxValue<bool> UseGraphicAlpha = new(true);

    private Vector2 _lastEffectDistance = new(6, -6);
    private Color _lastEffectColor = Color.black;
    private bool _lastUseGraphicAlpha = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(EffectDistance)
        || FxUtil.HasFx(EffectColor)
        || FxUtil.HasFx(UseGraphicAlpha);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Shadow>();
        if(com == null) {
            return false;
        }

        _lastEffectDistance = EffectDistance.Value;
        _lastEffectColor = EffectColor.Value;
        _lastUseGraphicAlpha = UseGraphicAlpha.Value;
        com.effectDistance = _lastEffectDistance;
        com.effectColor = _lastEffectColor;
        com.useGraphicAlpha = _lastUseGraphicAlpha;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Shadow>();
        if(com == null) {
            return false;
        }

        EffectDistance.Value = com.effectDistance;
        EffectColor.Value = com.effectColor;
        UseGraphicAlpha.Value = com.useGraphicAlpha;
        _lastEffectDistance = com.effectDistance;
        _lastEffectColor = com.effectColor;
        _lastUseGraphicAlpha = com.useGraphicAlpha;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Shadow>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        FxUtil.ApplyIfChanged(ref _lastEffectDistance, EffectDistance.Value, v => com.effectDistance = v);
        FxUtil.ApplyIfChanged(ref _lastEffectColor, EffectColor.Value, v => com.effectColor = v);
        FxUtil.ApplyIfChanged(ref _lastUseGraphicAlpha, UseGraphicAlpha.Value, v => com.useGraphicAlpha = v);
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(EffectDistance)] = IOUtils.WriteFx(EffectDistance),
            [nameof(EffectColor)] = IOUtils.WriteFx(EffectColor),
            [nameof(UseGraphicAlpha)] = IOUtils.WriteFx(UseGraphicAlpha)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        EffectDistance = IOUtils.ReadFx(token, nameof(EffectDistance), EffectDistance);
        EffectColor = IOUtils.ReadFx(token, nameof(EffectColor), EffectColor);
        UseGraphicAlpha = IOUtils.ReadFx(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
    }

    public ShadowSettings Copy() {
        return new ShadowSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            EffectDistance = EffectDistance?.Copy(),
            EffectColor = EffectColor?.Copy(),
            UseGraphicAlpha = UseGraphicAlpha?.Copy()
        };
    }
}
