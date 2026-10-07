using O5Kit.Core;
using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class ColorRangeSettings : UnityComponentSettingsBase, ICopyable<ColorRangeSettings> {
    public FxValue<string> TagName = FxValue<string>.FromValue(string.Empty);
    public FxValue<double> Minimum = new(0);
    public FxValue<double> Maximum = new(100);
    public FxValue<GradientColor> MinimumColor = new(new GradientColor(Color.black, true));
    public FxValue<GradientColor> MaximumColor = new(new GradientColor(Color.white, true));
    public FxValue<O5Ease> Ease = new(O5Ease.Linear);

    private string _lastTagName = string.Empty;
    private double _lastMinimum;
    private double _lastMaximum = 100;
    private GradientColor _lastMinimumColor = new(Color.black, true);
    private GradientColor _lastMaximumColor = new(Color.white, true);
    private O5Ease _lastEase = O5Ease.Linear;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(TagName)
        || FxUtil.HasFx(Minimum)
        || FxUtil.HasFx(Maximum)
        || FxUtil.HasFx(MinimumColor)
        || FxUtil.HasFx(MaximumColor)
        || FxUtil.HasFx(Ease);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<ColorRangeComponent>();
        if(component == null) {
            return false;
        }

        _lastTagName = TagName.Value;
        _lastMinimum = Minimum.Value;
        _lastMaximum = Maximum.Value;
        _lastMinimumColor = MinimumColor.Value;
        _lastMaximumColor = MaximumColor.Value;
        _lastEase = Ease.Value;
        component.TagName = _lastTagName;
        component.Minimum = _lastMinimum;
        component.Maximum = _lastMaximum;
        component.MinimumColor = _lastMinimumColor;
        component.MaximumColor = _lastMaximumColor;
        component.Ease = _lastEase;
        ToUnity(component);
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<ColorRangeComponent>();
        if(component == null) {
            return false;
        }

        TagName.Value = component.TagName;
        Minimum.Value = component.Minimum;
        Maximum.Value = component.Maximum;
        MinimumColor.Value = component.MinimumColor;
        MaximumColor.Value = component.MaximumColor;
        Ease.Value = component.Ease;
        _lastTagName = component.TagName;
        _lastMinimum = component.Minimum;
        _lastMaximum = component.Maximum;
        _lastMinimumColor = component.MinimumColor;
        _lastMaximumColor = component.MaximumColor;
        _lastEase = component.Ease;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<ColorRangeComponent>();
        if(component == null) {
            return;
        }

        RefreshEnabled(component);
        if(FxUtil.Changed(ref _lastTagName, TagName.Value)) component.TagName = _lastTagName;
        if(FxUtil.Changed(ref _lastMinimum, Minimum.Value)) component.Minimum = _lastMinimum;
        if(FxUtil.Changed(ref _lastMaximum, Maximum.Value)) component.Maximum = _lastMaximum;
        if(FxUtil.Changed(ref _lastMinimumColor, MinimumColor.Value)) component.MinimumColor = _lastMinimumColor;
        if(FxUtil.Changed(ref _lastMaximumColor, MaximumColor.Value)) component.MaximumColor = _lastMaximumColor;
        if(FxUtil.Changed(ref _lastEase, Ease.Value)) component.Ease = _lastEase;
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(TagName)] = IOUtils.WriteFx(TagName),
        [nameof(Minimum)] = IOUtils.WriteFx(Minimum),
        [nameof(Maximum)] = IOUtils.WriteFx(Maximum),
        [nameof(MinimumColor)] = IOUtils.WriteFx(MinimumColor),
        [nameof(MaximumColor)] = IOUtils.WriteFx(MaximumColor),
        [nameof(Ease)] = IOUtils.WriteFx(Ease)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        TagName = IOUtils.ReadFx(token, nameof(TagName), TagName);
        Minimum = IOUtils.ReadFx(token, nameof(Minimum), Minimum);
        Maximum = IOUtils.ReadFx(token, nameof(Maximum), Maximum);
        MinimumColor = IOUtils.ReadFx(token, nameof(MinimumColor), MinimumColor);
        MaximumColor = IOUtils.ReadFx(token, nameof(MaximumColor), MaximumColor);
        Ease = IOUtils.ReadFx(token, nameof(Ease), Ease);
    }

    public ColorRangeSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled?.Copy(),
        TagName = TagName?.Copy(),
        Minimum = Minimum?.Copy(),
        Maximum = Maximum?.Copy(),
        MinimumColor = MinimumColor?.Copy(),
        MaximumColor = MaximumColor?.Copy(),
        Ease = Ease?.Copy()
    };
}
