using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class LightSettings : UnityComponentSettingsBase, ICopyable<LightSettings> {
    public FxValue<LightType> Type = new(LightType.Point);
    public FxValue<Color> Color = new(UnityEngine.Color.white);
    public FxValue<float> Intensity = new(1f);
    public FxValue<float> Range = new(10f);
    public FxValue<float> SpotAngle = new(30f);

    private LightType _lastType = LightType.Point;
    private Color _lastColor = UnityEngine.Color.white;
    private float _lastIntensity = 1f;
    private float _lastRange = 10f;
    private float _lastSpotAngle = 30f;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Type)
        || FxUtil.HasFx(Color)
        || FxUtil.HasFx(Intensity)
        || FxUtil.HasFx(Range)
        || FxUtil.HasFx(SpotAngle);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Light>();
        if (com == null) {
            return false;
        }

        _lastType = Type.Value;
        _lastColor = Color.Value;
        _lastIntensity = Intensity.Value;
        _lastRange = Range.Value;
        _lastSpotAngle = SpotAngle.Value;
        com.type = _lastType;
        com.color = _lastColor;
        com.intensity = _lastIntensity;
        com.range = _lastRange;
        com.spotAngle = _lastSpotAngle;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Light>();
        if (com == null) {
            return false;
        }

        Type.Value = com.type;
        Color.Value = com.color;
        Intensity.Value = com.intensity;
        Range.Value = com.range;
        SpotAngle.Value = com.spotAngle;
        _lastType = com.type;
        _lastColor = com.color;
        _lastIntensity = com.intensity;
        _lastRange = com.range;
        _lastSpotAngle = com.spotAngle;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Light>();
        if (com == null) {
            return;
        }

        RefreshEnabled(com);
        if (FxUtil.Changed(ref _lastType, Type.Value)) com.type = _lastType;
        if (FxUtil.Changed(ref _lastColor, Color.Value)) com.color = _lastColor;
        if (FxUtil.Changed(ref _lastIntensity, Intensity.Value)) com.intensity = _lastIntensity;
        if (FxUtil.Changed(ref _lastRange, Range.Value)) com.range = _lastRange;
        if (FxUtil.Changed(ref _lastSpotAngle, SpotAngle.Value)) com.spotAngle = _lastSpotAngle;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Type)] = IOUtils.WriteFx(Type),
            [nameof(Color)] = IOUtils.WriteFx(Color),
            [nameof(Intensity)] = IOUtils.WriteFx(Intensity),
            [nameof(Range)] = IOUtils.WriteFx(Range),
            [nameof(SpotAngle)] = IOUtils.WriteFx(SpotAngle)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Type = IOUtils.ReadFx(token, nameof(Type), Type);
        Color = IOUtils.ReadFx(token, nameof(Color), Color);
        Intensity = IOUtils.ReadFx(token, nameof(Intensity), Intensity);
        Range = IOUtils.ReadFx(token, nameof(Range), Range);
        SpotAngle = IOUtils.ReadFx(token, nameof(SpotAngle), SpotAngle);
    }

    public LightSettings Copy() {
        return new LightSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Type = Type?.Copy(),
            Color = Color?.Copy(),
            Intensity = Intensity?.Copy(),
            Range = Range?.Copy(),
            SpotAngle = SpotAngle?.Copy()
        };
    }
}

// shortcut: shadows/baking omitted; extend here when needed.
