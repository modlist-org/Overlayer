using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.User;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ImageSettings : UnityComponentSettingsBase, ICopyable<ImageSettings> {
    public Color Color = Color.white;
    public string SpriteKey = null;
    public bool PreserveAspect = false;
    public bool RaycastTarget = true;
    public bool UseSpriteMesh = false;
    public Image.Type Type = Image.Type.Simple;
    public bool FillCenter = true;
    public float PixelsPerUnitMultiplier = 1f;
    public Image.FillMethod FillMethod = Image.FillMethod.Horizontal;
    public float FillAmount = 1f;
    public int FillOrigin = 0;
    public bool FillClockwise = true;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Image>();
        if(com == null) {
            return false;
        }

        com.color = Color;
        com.sprite = UserResourceManager.Spr.TryGet(SpriteKey, out var value) ? value.sprite : null;
        com.preserveAspect = PreserveAspect;
        com.raycastTarget = RaycastTarget;
        com.useSpriteMesh = UseSpriteMesh;
        com.type = Type;
        com.fillCenter = FillCenter;
        com.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
        com.fillMethod = FillMethod;
        com.fillAmount = FillAmount;
        com.fillOrigin = FillOrigin;
        com.fillClockwise = FillClockwise;
        ToUnity(com);

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Image>();
        if(com == null) {
            return false;
        }

        Color = com.color;
        if(com.sprite != null) {
            UserResourceManager.Spr.TryGetKey(
                x => x.sprite == com.sprite,
                out SpriteKey
            );
        } else {
            SpriteKey = string.Empty;
        }
        PreserveAspect = com.preserveAspect;
        RaycastTarget = com.raycastTarget;
        UseSpriteMesh = com.useSpriteMesh;
        Type = com.type;
        FillCenter = com.fillCenter;
        PixelsPerUnitMultiplier = com.pixelsPerUnitMultiplier;
        FillMethod = com.fillMethod;
        FillAmount = com.fillAmount;
        FillOrigin = com.fillOrigin;
        FillClockwise = com.fillClockwise;
        FromUnity(com);

        return true;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Color)] = IOUtils.Write(Color),
            [nameof(SpriteKey)] = SpriteKey,
            [nameof(PreserveAspect)] = PreserveAspect,
            [nameof(RaycastTarget)] = RaycastTarget,
            [nameof(UseSpriteMesh)] = UseSpriteMesh,
            [nameof(Type)] = IOUtils.WriteEnum(Type),
            [nameof(FillCenter)] = FillCenter,
            [nameof(PixelsPerUnitMultiplier)] = PixelsPerUnitMultiplier,
            [nameof(FillMethod)] = IOUtils.WriteEnum(FillMethod),
            [nameof(FillAmount)] = FillAmount,
            [nameof(FillOrigin)] = FillOrigin,
            [nameof(FillClockwise)] = FillClockwise
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Color = IOUtils.Read(token, nameof(Color), Color);
        SpriteKey = IOUtils.Read(token, nameof(SpriteKey), SpriteKey);
        PreserveAspect = IOUtils.Read(token, nameof(PreserveAspect), PreserveAspect);
        RaycastTarget = IOUtils.Read(token, nameof(RaycastTarget), RaycastTarget);
        UseSpriteMesh = IOUtils.Read(token, nameof(UseSpriteMesh), UseSpriteMesh);
        Type = IOUtils.ReadEnum(token, nameof(Type), Type);
        FillCenter = IOUtils.Read(token, nameof(FillCenter), FillCenter);
        PixelsPerUnitMultiplier = IOUtils.Read(token, nameof(PixelsPerUnitMultiplier), PixelsPerUnitMultiplier);
        FillMethod = IOUtils.ReadEnum(token, nameof(FillMethod), FillMethod);
        FillAmount = IOUtils.Read(token, nameof(FillAmount), FillAmount);
        FillOrigin = IOUtils.Read(token, nameof(FillOrigin), FillOrigin);
        FillClockwise = IOUtils.Read(token, nameof(FillClockwise), FillClockwise);
    }

    public ImageSettings Copy() {
        return new ImageSettings {
            ComponentEnabled = ComponentEnabled,
            Color = Color,
            SpriteKey = SpriteKey,
            PreserveAspect = PreserveAspect,
            RaycastTarget = RaycastTarget,
            UseSpriteMesh = UseSpriteMesh,
            Type = Type,
            FillCenter = FillCenter,
            PixelsPerUnitMultiplier = PixelsPerUnitMultiplier,
            FillMethod = FillMethod,
            FillAmount = FillAmount,
            FillOrigin = FillOrigin,
            FillClockwise = FillClockwise
        };
    }
}
