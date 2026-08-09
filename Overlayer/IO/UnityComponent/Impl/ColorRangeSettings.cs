using GTweens.Easings;
using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class ColorRangeSettings : UnityComponentSettingsBase, ICopyable<ColorRangeSettings> {
    public string TagName = string.Empty;
    public double Minimum;
    public double Maximum = 100;
    public Color MinimumColor = Color.black;
    public Color MaximumColor = Color.white;
    public Easing Ease = Easing.Linear;

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<ColorRangeComponent>();
        if(component == null) return false;

        component.TagName = TagName;
        component.Minimum = Minimum;
        component.Maximum = Maximum;
        component.MinimumColor = MinimumColor;
        component.MaximumColor = MaximumColor;
        component.Ease = Ease;
        ToUnity(component);
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<ColorRangeComponent>();
        if(component == null) return false;

        TagName = component.TagName;
        Minimum = component.Minimum;
        Maximum = component.Maximum;
        MinimumColor = component.MinimumColor;
        MaximumColor = component.MaximumColor;
        Ease = component.Ease;
        FromUnity(component);
        return true;
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(TagName)] = TagName,
        [nameof(Minimum)] = Minimum,
        [nameof(Maximum)] = Maximum,
        [nameof(MinimumColor)] = IOUtils.Write(MinimumColor),
        [nameof(MaximumColor)] = IOUtils.Write(MaximumColor),
        [nameof(Ease)] = IOUtils.WriteEnum(Ease)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        TagName = IOUtils.Read(token, nameof(TagName), TagName);
        Minimum = IOUtils.Read(token, nameof(Minimum), Minimum);
        Maximum = IOUtils.Read(token, nameof(Maximum), Maximum);
        MinimumColor = IOUtils.Read(token, nameof(MinimumColor), MinimumColor);
        MaximumColor = IOUtils.Read(token, nameof(MaximumColor), MaximumColor);
        Ease = IOUtils.ReadEnum(token, nameof(Ease), Ease);
    }

    public ColorRangeSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled,
        TagName = TagName,
        Minimum = Minimum,
        Maximum = Maximum,
        MinimumColor = MinimumColor,
        MaximumColor = MaximumColor,
        Ease = Ease
    };
}
