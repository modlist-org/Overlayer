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
    public Image.Type Type = Image.Type.Simple;
    public Image.FillMethod FillMethod = Image.FillMethod.Horizontal;
    public float FillAmount = 1f;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Image>();
        if (com == null) {
            return false;
        }
        
        com.color = Color;
        com.sprite = UserResourceManager.Spr.TryGet(SpriteKey, out var value) ? value.sprite : null;
        com.preserveAspect = PreserveAspect;
        com.type = Type;
        com.fillMethod = FillMethod;
        com.fillAmount = FillAmount;
        
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Image>();
        if (com == null) {
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
        Type = com.type;
        FillMethod = com.fillMethod;
        FillAmount = com.fillAmount;
        
        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Color)] = IOUtils.Write(Color),
            [nameof(SpriteKey)] = SpriteKey,
            [nameof(PreserveAspect)] = PreserveAspect,
            [nameof(Type)] = IOUtils.WriteEnum(Type),
            [nameof(FillMethod)] = IOUtils.WriteEnum(FillMethod),
            [nameof(FillAmount)] = FillAmount
        };
    }

    public override void Deserialize(JToken token) {
        Color = IOUtils.Read(token, nameof(Color), Color);
        SpriteKey = IOUtils.Read(token, nameof(SpriteKey), SpriteKey);
        PreserveAspect = IOUtils.Read(token, nameof(PreserveAspect), PreserveAspect);
        Type = IOUtils.ReadEnum(token, nameof(Type), Type);
        FillMethod = IOUtils.ReadEnum(token, nameof(FillMethod), FillMethod);
        FillAmount = IOUtils.Read(token, nameof(FillAmount), FillAmount);
    }

    public ImageSettings Copy() {
        return new ImageSettings {
            Color = Color,
            SpriteKey = SpriteKey,
            PreserveAspect = PreserveAspect,
            Type = Type,
            FillMethod = FillMethod,
            FillAmount = FillAmount
        };
    }
}