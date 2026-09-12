using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
namespace Overlayer.IO.UnityComponent.Impl;

public class OutlineSettings : UnityComponentSettingsBase, ICopyable<OutlineSettings> {
    public FxValue<Color> EffectColor = new(Color.red);
    public FxValue<Vector2> EffectDistance = new(new Vector2(1f, -1f));
    public FxValue<bool> UseGraphicAlpha = new(true);

    private Color _lastEffectColor = Color.red;
    private Vector2 _lastEffectDistance = new(1f, -1f);
    private bool _lastUseGraphicAlpha = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(EffectColor)
        || FxUtil.HasFx(EffectDistance)
        || FxUtil.HasFx(UseGraphicAlpha);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Outline>();
        if(com == null) {
            return false;
        }

        _lastEffectColor = EffectColor.Value;
        _lastEffectDistance = EffectDistance.Value;
        _lastUseGraphicAlpha = UseGraphicAlpha.Value;
        com.effectColor = _lastEffectColor;
        com.effectDistance = _lastEffectDistance;
        com.useGraphicAlpha = _lastUseGraphicAlpha;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Outline>();
        if(com == null) {
            return false;
        }

        EffectColor.Value = com.effectColor;
        EffectDistance.Value = com.effectDistance;
        UseGraphicAlpha.Value = com.useGraphicAlpha;
        _lastEffectColor = com.effectColor;
        _lastEffectDistance = com.effectDistance;
        _lastUseGraphicAlpha = com.useGraphicAlpha;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Outline>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        FxUtil.ApplyIfChanged(ref _lastEffectColor, EffectColor.Value, v => com.effectColor = v);
        FxUtil.ApplyIfChanged(ref _lastEffectDistance, EffectDistance.Value, v => com.effectDistance = v);
        FxUtil.ApplyIfChanged(ref _lastUseGraphicAlpha, UseGraphicAlpha.Value, v => com.useGraphicAlpha = v);
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(EffectColor)] = IOUtils.WriteFx(EffectColor),
            [nameof(EffectDistance)] = IOUtils.WriteFx(EffectDistance),
            [nameof(UseGraphicAlpha)] = IOUtils.WriteFx(UseGraphicAlpha)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        if(token?["Enabled"] != null && token?[nameof(ComponentEnabled)] == null) {
            ComponentEnabled = IOUtils.ReadFx(token, "Enabled", ComponentEnabled);
        }
        EffectColor = IOUtils.ReadFx(token, nameof(EffectColor), EffectColor);
        EffectDistance = IOUtils.ReadFx(token, nameof(EffectDistance), EffectDistance);
        UseGraphicAlpha = IOUtils.ReadFx(token, nameof(UseGraphicAlpha), UseGraphicAlpha);
    }

    public OutlineSettings Copy() {
        return new OutlineSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            EffectColor = EffectColor?.Copy(),
            EffectDistance = EffectDistance?.Copy(),
            UseGraphicAlpha = UseGraphicAlpha?.Copy()
        };
    }
}
