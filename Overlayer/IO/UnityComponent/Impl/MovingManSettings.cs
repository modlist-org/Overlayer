using GTweens.Easings;
using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class MovingManSettings : UnityComponentSettingsBase, ICopyable<MovingManSettings> {
    public FxValue<string> TagName = FxValue<string>.FromValue(string.Empty);
    public FxValue<MovingManTarget> Target = new(MovingManTarget.TextSize);
    public FxValue<double> StartSize = new(30);
    public FxValue<double> EndSize = new(80);
    public FxValue<double> DefaultSize = new(30);
    public FxValue<double> Speed = new(800);
    public FxValue<bool> Invert = new(false);
    public FxValue<Easing> Ease = new(Easing.OutExpo);

    private string _lastTagName = string.Empty;
    private MovingManTarget _lastTarget = MovingManTarget.TextSize;
    private double _lastStartSize = 30;
    private double _lastEndSize = 80;
    private double _lastDefaultSize = 30;
    private double _lastSpeed = 800;
    private bool _lastInvert;
    private Easing _lastEase = Easing.OutExpo;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(TagName)
        || FxUtil.HasFx(Target)
        || FxUtil.HasFx(StartSize)
        || FxUtil.HasFx(EndSize)
        || FxUtil.HasFx(DefaultSize)
        || FxUtil.HasFx(Speed)
        || FxUtil.HasFx(Invert)
        || FxUtil.HasFx(Ease);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<MovingManComponent>();
        if(component == null) {
            return false;
        }

        _lastTagName = TagName.Value;
        _lastTarget = Target.Value;
        _lastStartSize = StartSize.Value;
        _lastEndSize = EndSize.Value;
        _lastDefaultSize = DefaultSize.Value;
        _lastSpeed = Speed.Value;
        _lastInvert = Invert.Value;
        _lastEase = Ease.Value;
        component.TagName = _lastTagName;
        component.Target = _lastTarget;
        component.StartSize = _lastStartSize;
        component.EndSize = _lastEndSize;
        component.DefaultSize = _lastDefaultSize;
        component.Speed = _lastSpeed;
        component.Invert = _lastInvert;
        component.Ease = _lastEase;
        ToUnity(component);
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<MovingManComponent>();
        if(component == null) {
            return false;
        }

        TagName.Value = component.TagName;
        Target.Value = component.Target;
        StartSize.Value = component.StartSize;
        EndSize.Value = component.EndSize;
        DefaultSize.Value = component.DefaultSize;
        Speed.Value = component.Speed;
        Invert.Value = component.Invert;
        Ease.Value = component.Ease;
        _lastTagName = component.TagName;
        _lastTarget = component.Target;
        _lastStartSize = component.StartSize;
        _lastEndSize = component.EndSize;
        _lastDefaultSize = component.DefaultSize;
        _lastSpeed = component.Speed;
        _lastInvert = component.Invert;
        _lastEase = component.Ease;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<MovingManComponent>();
        if(component == null) {
            return;
        }

        RefreshEnabled(component);
        FxUtil.ApplyIfChanged(ref _lastTagName, TagName.Value, v => component.TagName = v);
        FxUtil.ApplyIfChanged(ref _lastTarget, Target.Value, v => component.Target = v);
        FxUtil.ApplyIfChanged(ref _lastStartSize, StartSize.Value, v => component.StartSize = v);
        FxUtil.ApplyIfChanged(ref _lastEndSize, EndSize.Value, v => component.EndSize = v);
        FxUtil.ApplyIfChanged(ref _lastDefaultSize, DefaultSize.Value, v => component.DefaultSize = v);
        FxUtil.ApplyIfChanged(ref _lastSpeed, Speed.Value, v => component.Speed = v);
        FxUtil.ApplyIfChanged(ref _lastInvert, Invert.Value, v => component.Invert = v);
        FxUtil.ApplyIfChanged(ref _lastEase, Ease.Value, v => component.Ease = v);
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(TagName)] = IOUtils.WriteFx(TagName),
        [nameof(Target)] = IOUtils.WriteFx(Target),
        [nameof(StartSize)] = IOUtils.WriteFx(StartSize),
        [nameof(EndSize)] = IOUtils.WriteFx(EndSize),
        [nameof(DefaultSize)] = IOUtils.WriteFx(DefaultSize),
        [nameof(Speed)] = IOUtils.WriteFx(Speed),
        [nameof(Invert)] = IOUtils.WriteFx(Invert),
        [nameof(Ease)] = IOUtils.WriteFx(Ease)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        TagName = IOUtils.ReadFx(token, nameof(TagName), TagName);
        Target = IOUtils.ReadFx(token, nameof(Target), Target);
        if(!Target.UseFx) {
            Target.Value = ReadTarget(token, Target.Value);
        }
        StartSize = IOUtils.ReadFx(token, nameof(StartSize), StartSize);
        EndSize = IOUtils.ReadFx(token, nameof(EndSize), EndSize);
        DefaultSize = IOUtils.ReadFx(token, nameof(DefaultSize), DefaultSize);
        Speed = IOUtils.ReadFx(token, nameof(Speed), Speed);
        Invert = IOUtils.ReadFx(token, nameof(Invert), Invert);
        Ease = IOUtils.ReadFx(token, nameof(Ease), Ease);
    }

    public MovingManSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled?.Copy(),
        TagName = TagName?.Copy(),
        Target = Target?.Copy(),
        StartSize = StartSize?.Copy(),
        EndSize = EndSize?.Copy(),
        DefaultSize = DefaultSize?.Copy(),
        Speed = Speed?.Copy(),
        Invert = Invert?.Copy(),
        Ease = Ease?.Copy()
    };

    private static MovingManTarget ReadTarget(JToken token, MovingManTarget fallback) {
        var value = token[nameof(Target)];
        if(value?.Type == JTokenType.Integer) {
            int legacyValue = value.Value<int>();
            if(token[nameof(Ease)]?.Type == JTokenType.Integer) {
                return (MovingManTarget)legacyValue;
            }
            if((legacyValue & (1 << 10)) != 0) {
                return (MovingManTarget)(legacyValue | (1 << 11));
            }
            if(legacyValue is >= 0 and <= 9) {
                return (MovingManTarget)(1 << legacyValue);
            }
        }

        if(value?.Type == JTokenType.String && string.Equals(
            value.Value<string>(),
            "SizeDelta",
            StringComparison.OrdinalIgnoreCase
        )) {
            return MovingManTarget.SizeDeltaX | MovingManTarget.SizeDeltaY;
        }

        return fallback;
    }
}
