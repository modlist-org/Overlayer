using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.IO.User;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ImageSettings : UnityComponentSettingsBase, ICopyable<ImageSettings> {
    public FxValue<Color> Color = new(UnityEngine.Color.white);
    public FxValue<string> SpriteKey = FxValue<string>.FromValue((string)null);
    public FxValue<bool> PreserveAspect = new(false);
    public FxValue<bool> RaycastTarget = new(true);
    public FxValue<bool> UseSpriteMesh = new(false);
    public FxValue<Image.Type> Type = new(Image.Type.Simple);
    public FxValue<bool> FillCenter = new(true);
    public FxValue<float> PixelsPerUnitMultiplier = new(1f);
    public FxValue<Image.FillMethod> FillMethod = new(Image.FillMethod.Horizontal);
    public FxValue<float> FillAmount = new(1f);
    public FxValue<int> FillOrigin = new(0);
    public FxValue<bool> FillClockwise = new(true);

    private Color _lastColor = UnityEngine.Color.white;
    private string _lastSpriteKey;
    private bool _lastPreserveAspect;
    private bool _lastRaycastTarget = true;
    private bool _lastUseSpriteMesh;
    private Image.Type _lastType = Image.Type.Simple;
    private bool _lastFillCenter = true;
    private float _lastPixelsPerUnitMultiplier = 1f;
    private Image.FillMethod _lastFillMethod = Image.FillMethod.Horizontal;
    private float _lastFillAmount = 1f;
    private int _lastFillOrigin;
    private bool _lastFillClockwise = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Color)
        || FxUtil.HasFx(SpriteKey)
        || FxUtil.HasFx(PreserveAspect)
        || FxUtil.HasFx(RaycastTarget)
        || FxUtil.HasFx(UseSpriteMesh)
        || FxUtil.HasFx(Type)
        || FxUtil.HasFx(FillCenter)
        || FxUtil.HasFx(PixelsPerUnitMultiplier)
        || FxUtil.HasFx(FillMethod)
        || FxUtil.HasFx(FillAmount)
        || FxUtil.HasFx(FillOrigin)
        || FxUtil.HasFx(FillClockwise);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Image>();
        if(com == null) {
            return false;
        }

        _lastColor = Color.Value;
        _lastSpriteKey = SpriteKey.Value;
        _lastPreserveAspect = PreserveAspect.Value;
        _lastRaycastTarget = RaycastTarget.Value;
        _lastUseSpriteMesh = UseSpriteMesh.Value;
        _lastType = Type.Value;
        _lastFillCenter = FillCenter.Value;
        _lastPixelsPerUnitMultiplier = PixelsPerUnitMultiplier.Value;
        _lastFillMethod = FillMethod.Value;
        _lastFillAmount = FillAmount.Value;
        _lastFillOrigin = FillOrigin.Value;
        _lastFillClockwise = FillClockwise.Value;
        com.color = _lastColor;
        com.sprite = UserResourceManager.Spr.TryGet(_lastSpriteKey, out var value) ? value.sprite : null;
        com.preserveAspect = _lastPreserveAspect;
        com.raycastTarget = _lastRaycastTarget;
        com.useSpriteMesh = _lastUseSpriteMesh;
        com.type = _lastType;
        com.fillCenter = _lastFillCenter;
        com.pixelsPerUnitMultiplier = _lastPixelsPerUnitMultiplier;
        com.fillMethod = _lastFillMethod;
        com.fillAmount = _lastFillAmount;
        com.fillOrigin = _lastFillOrigin;
        com.fillClockwise = _lastFillClockwise;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Image>();
        if(com == null) {
            return false;
        }

        Color.Value = com.color;
        if(com.sprite != null) {
            UserResourceManager.Spr.TryGetKey(
                x => x.sprite == com.sprite,
                out var key
            );
            SpriteKey.Value = key;
        } else {
            SpriteKey.Value = string.Empty;
        }
        PreserveAspect.Value = com.preserveAspect;
        RaycastTarget.Value = com.raycastTarget;
        UseSpriteMesh.Value = com.useSpriteMesh;
        Type.Value = com.type;
        FillCenter.Value = com.fillCenter;
        PixelsPerUnitMultiplier.Value = com.pixelsPerUnitMultiplier;
        FillMethod.Value = com.fillMethod;
        FillAmount.Value = com.fillAmount;
        FillOrigin.Value = com.fillOrigin;
        FillClockwise.Value = com.fillClockwise;
        _lastColor = com.color;
        _lastSpriteKey = SpriteKey.Value;
        _lastPreserveAspect = com.preserveAspect;
        _lastRaycastTarget = com.raycastTarget;
        _lastUseSpriteMesh = com.useSpriteMesh;
        _lastType = com.type;
        _lastFillCenter = com.fillCenter;
        _lastPixelsPerUnitMultiplier = com.pixelsPerUnitMultiplier;
        _lastFillMethod = com.fillMethod;
        _lastFillAmount = com.fillAmount;
        _lastFillOrigin = com.fillOrigin;
        _lastFillClockwise = com.fillClockwise;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Image>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        FxUtil.ApplyIfChanged(ref _lastColor, Color.Value, v => com.color = v);
        if(FxUtil.ApplyIfChanged(ref _lastSpriteKey, SpriteKey.Value, _ => { })) {
            com.sprite = UserResourceManager.Spr.TryGet(_lastSpriteKey, out var value) ? value.sprite : null;
        }
        FxUtil.ApplyIfChanged(ref _lastPreserveAspect, PreserveAspect.Value, v => com.preserveAspect = v);
        FxUtil.ApplyIfChanged(ref _lastRaycastTarget, RaycastTarget.Value, v => com.raycastTarget = v);
        FxUtil.ApplyIfChanged(ref _lastUseSpriteMesh, UseSpriteMesh.Value, v => com.useSpriteMesh = v);
        FxUtil.ApplyIfChanged(ref _lastType, Type.Value, v => com.type = v);
        FxUtil.ApplyIfChanged(ref _lastFillCenter, FillCenter.Value, v => com.fillCenter = v);
        FxUtil.ApplyIfChanged(ref _lastPixelsPerUnitMultiplier, PixelsPerUnitMultiplier.Value, v => com.pixelsPerUnitMultiplier = v);
        FxUtil.ApplyIfChanged(ref _lastFillMethod, FillMethod.Value, v => com.fillMethod = v);
        FxUtil.ApplyIfChanged(ref _lastFillAmount, FillAmount.Value, v => com.fillAmount = v);
        FxUtil.ApplyIfChanged(ref _lastFillOrigin, FillOrigin.Value, v => com.fillOrigin = v);
        FxUtil.ApplyIfChanged(ref _lastFillClockwise, FillClockwise.Value, v => com.fillClockwise = v);
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Color)] = IOUtils.WriteFx(Color),
            [nameof(SpriteKey)] = IOUtils.WriteFx(SpriteKey),
            [nameof(PreserveAspect)] = IOUtils.WriteFx(PreserveAspect),
            [nameof(RaycastTarget)] = IOUtils.WriteFx(RaycastTarget),
            [nameof(UseSpriteMesh)] = IOUtils.WriteFx(UseSpriteMesh),
            [nameof(Type)] = IOUtils.WriteFx(Type),
            [nameof(FillCenter)] = IOUtils.WriteFx(FillCenter),
            [nameof(PixelsPerUnitMultiplier)] = IOUtils.WriteFx(PixelsPerUnitMultiplier),
            [nameof(FillMethod)] = IOUtils.WriteFx(FillMethod),
            [nameof(FillAmount)] = IOUtils.WriteFx(FillAmount),
            [nameof(FillOrigin)] = IOUtils.WriteFx(FillOrigin),
            [nameof(FillClockwise)] = IOUtils.WriteFx(FillClockwise)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Color = IOUtils.ReadFx(token, nameof(Color), Color);
        SpriteKey = IOUtils.ReadFx(token, nameof(SpriteKey), SpriteKey);
        PreserveAspect = IOUtils.ReadFx(token, nameof(PreserveAspect), PreserveAspect);
        RaycastTarget = IOUtils.ReadFx(token, nameof(RaycastTarget), RaycastTarget);
        UseSpriteMesh = IOUtils.ReadFx(token, nameof(UseSpriteMesh), UseSpriteMesh);
        Type = IOUtils.ReadFx(token, nameof(Type), Type);
        FillCenter = IOUtils.ReadFx(token, nameof(FillCenter), FillCenter);
        PixelsPerUnitMultiplier = IOUtils.ReadFx(token, nameof(PixelsPerUnitMultiplier), PixelsPerUnitMultiplier);
        FillMethod = IOUtils.ReadFx(token, nameof(FillMethod), FillMethod);
        FillAmount = IOUtils.ReadFx(token, nameof(FillAmount), FillAmount);
        FillOrigin = IOUtils.ReadFx(token, nameof(FillOrigin), FillOrigin);
        FillClockwise = IOUtils.ReadFx(token, nameof(FillClockwise), FillClockwise);
    }

    public ImageSettings Copy() {
        return new ImageSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Color = Color?.Copy(),
            SpriteKey = SpriteKey?.Copy(),
            PreserveAspect = PreserveAspect?.Copy(),
            RaycastTarget = RaycastTarget?.Copy(),
            UseSpriteMesh = UseSpriteMesh?.Copy(),
            Type = Type?.Copy(),
            FillCenter = FillCenter?.Copy(),
            PixelsPerUnitMultiplier = PixelsPerUnitMultiplier?.Copy(),
            FillMethod = FillMethod?.Copy(),
            FillAmount = FillAmount?.Copy(),
            FillOrigin = FillOrigin?.Copy(),
            FillClockwise = FillClockwise?.Copy()
        };
    }
}
