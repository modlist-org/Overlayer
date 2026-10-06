using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public sealed class RainSettings : UnityComponentSettingsBase, ICopyable<RainSettings> {
    public FxValue<bool> Active = new(true);
    public FxValue<float> Speed = new(400f);
    public FxValue<float> Length = new(400f);
    public FxValue<float> Width = new(0f);
    public FxValue<Vector2> Offset = new(Vector2.zero);
    public FxValue<float> FadeIn = new(0f);
    public FxValue<float> FadeOut = new(100f);
    public FxValue<GradientColor> Color = new(new GradientColor(UnityEngine.Color.white, true));
    public FxValue<string> SpriteKey = FxValue<string>.FromValue((string)null);
    public FxValue<int> MaxTrails = new(32);

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Active)
        || FxUtil.HasFx(Speed)
        || FxUtil.HasFx(Length)
        || FxUtil.HasFx(Width)
        || FxUtil.HasFx(Offset)
        || FxUtil.HasFx(FadeIn)
        || FxUtil.HasFx(FadeOut)
        || FxUtil.HasFx(Color)
        || FxUtil.HasFx(SpriteKey)
        || FxUtil.HasFx(MaxTrails);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<OvRainComponent>();
        if(component == null) {
            return false;
        }
        Push(component);
        ToUnity(component);
        return true;
    }

    // The component reads these every frame, so pushing all of them is cheaper than tracking each one.
    private void Push(OvRainComponent component) {
        component.Active = Active.Value;
        component.Speed = Speed.Value;
        component.Length = Length.Value;
        component.Width = Width.Value;
        component.Offset = Offset.Value;
        component.FadeIn = FadeIn.Value;
        component.FadeOut = FadeOut.Value;
        component.Color = Color.Value;
        component.SpriteKey = SpriteKey.Value;
        component.MaxTrails = MaxTrails.Value;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<OvRainComponent>();
        if(component == null) {
            return false;
        }
        Active.Value = component.Active;
        Speed.Value = component.Speed;
        Length.Value = component.Length;
        Width.Value = component.Width;
        Offset.Value = component.Offset;
        FadeIn.Value = component.FadeIn;
        FadeOut.Value = component.FadeOut;
        Color.Value = component.Color;
        SpriteKey.Value = component.SpriteKey;
        MaxTrails.Value = component.MaxTrails;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }
        var component = target.GetComponent<OvRainComponent>();
        if(component == null) {
            return;
        }
        RefreshEnabled(component);
        Push(component);
    }

    public override JToken Serialize() => SerializeComponent(new JObject {
        [nameof(Active)] = IOUtils.WriteFx(Active),
        [nameof(Speed)] = IOUtils.WriteFx(Speed),
        [nameof(Length)] = IOUtils.WriteFx(Length),
        [nameof(Width)] = IOUtils.WriteFx(Width),
        [nameof(Offset)] = IOUtils.WriteFx(Offset),
        [nameof(FadeIn)] = IOUtils.WriteFx(FadeIn),
        [nameof(FadeOut)] = IOUtils.WriteFx(FadeOut),
        [nameof(Color)] = IOUtils.WriteFx(Color),
        [nameof(SpriteKey)] = IOUtils.WriteFx(SpriteKey),
        [nameof(MaxTrails)] = IOUtils.WriteFx(MaxTrails)
    });

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Active = IOUtils.ReadFx(token, nameof(Active), Active);
        Speed = IOUtils.ReadFx(token, nameof(Speed), Speed);
        Length = IOUtils.ReadFx(token, nameof(Length), Length);
        Width = IOUtils.ReadFx(token, nameof(Width), Width);
        Offset = IOUtils.ReadFx(token, nameof(Offset), Offset);
        FadeIn = IOUtils.ReadFx(token, nameof(FadeIn), FadeIn);
        FadeOut = IOUtils.ReadFx(token, nameof(FadeOut), FadeOut);
        Color = IOUtils.ReadFx(token, nameof(Color), Color);
        SpriteKey = IOUtils.ReadFx(token, nameof(SpriteKey), SpriteKey);
        MaxTrails = IOUtils.ReadFx(token, nameof(MaxTrails), MaxTrails);
    }

    public RainSettings Copy() => new() {
        ComponentEnabled = ComponentEnabled?.Copy(),
        Active = Active?.Copy(),
        Speed = Speed?.Copy(),
        Length = Length?.Copy(),
        Width = Width?.Copy(),
        Offset = Offset?.Copy(),
        FadeIn = FadeIn?.Copy(),
        FadeOut = FadeOut?.Copy(),
        Color = Color?.Copy(),
        SpriteKey = SpriteKey?.Copy(),
        MaxTrails = MaxTrails?.Copy()
    };
}
