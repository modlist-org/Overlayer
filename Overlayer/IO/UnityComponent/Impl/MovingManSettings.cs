using GTweens.Easings;
using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class MovingManSettings : UnityComponentSettingsBase, ICopyable<MovingManSettings> {
    public string TagName = string.Empty;
    public MovingManTarget Target = MovingManTarget.TextSize;
    public double StartSize = 30;
    public double EndSize = 80;
    public double DefaultSize = 30;
    public double Speed = 800;
    public bool Invert;
    public Easing Ease = Easing.OutExpo;

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<MovingManComponent>();
        if(component == null) {
            return false;
        }

        component.TagName = TagName;
        component.Target = Target;
        component.StartSize = StartSize;
        component.EndSize = EndSize;
        component.DefaultSize = DefaultSize;
        component.Speed = Speed;
        component.Invert = Invert;
        component.Ease = Ease;
        ToUnity(component);
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<MovingManComponent>();
        if(component == null) {
            return false;
        }

        TagName = component.TagName;
        Target = component.Target;
        StartSize = component.StartSize;
        EndSize = component.EndSize;
        DefaultSize = component.DefaultSize;
        Speed = component.Speed;
        Invert = component.Invert;
        Ease = component.Ease;
        FromUnity(component);
        return true;
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(TagName)] = TagName,
        [nameof(Target)] = IOUtils.WriteEnum(Target),
        [nameof(StartSize)] = StartSize,
        [nameof(EndSize)] = EndSize,
        [nameof(DefaultSize)] = DefaultSize,
        [nameof(Speed)] = Speed,
        [nameof(Invert)] = Invert,
        [nameof(Ease)] = IOUtils.WriteEnum(Ease)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        TagName = IOUtils.Read(token, nameof(TagName), TagName);
        Target = ReadTarget(token, Target);
        StartSize = IOUtils.Read(token, nameof(StartSize), StartSize);
        EndSize = IOUtils.Read(token, nameof(EndSize), EndSize);
        DefaultSize = IOUtils.Read(token, nameof(DefaultSize), DefaultSize);
        Speed = IOUtils.Read(token, nameof(Speed), Speed);
        Invert = IOUtils.Read(token, nameof(Invert), Invert);
        Ease = IOUtils.ReadEnum(token, nameof(Ease), Ease);
    }

    public MovingManSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled,
        TagName = TagName,
        Target = Target,
        StartSize = StartSize,
        EndSize = EndSize,
        DefaultSize = DefaultSize,
        Speed = Speed,
        Invert = Invert,
        Ease = Ease
    };

    private static MovingManTarget ReadTarget(JToken token, MovingManTarget fallback) {
        var value = token[nameof(Target)];
        if(value?.Type == JTokenType.Integer) {
            int legacyValue = value.Value<int>();
            if(legacyValue is >= 0 and <= 9) {
                return (MovingManTarget)(1 << legacyValue);
            }
        }

        return IOUtils.ReadEnum(token, nameof(Target), fallback);
    }
}
