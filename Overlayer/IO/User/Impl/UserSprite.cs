using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.IO.Unity;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserSprite : UserResourceBase<(Sprite sprite, string textureKey, SpriteSettings settings)>, ISettingsFile { 
    public enum Result {
        Success,
        KeyAlreadyExists,
        NotFound,
        Failed,
    }

    public Result Load(
        string key,
        string textureKey,
        Rect rect,
        Vector2 pivot,
        float pixelsPerUnit,
        Vector4 border,
        out Sprite sprite
    ) {
        sprite = null;

        try {
            if(Cache.ContainsKey(key)) {
                sprite = Cache[key].value.sprite;
                return Result.KeyAlreadyExists;
            }

            if(!UserResourceManager.T2D.TryGet(textureKey, out var value)) {
                return Result.NotFound;
            }

            var settings = new SpriteSettings {
                Rect = rect,
                Pivot = pivot,
                PixelsPerUnit = pixelsPerUnit,
                Border = border
            };

            var spr = settings.ToUnity(value.texture);

            Cache[key] = (
                textureKey,
                (spr, textureKey, settings)
            );

            sprite = spr;
            return Result.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(UserSprite)}] Sprite load failed: {e}");
            return Result.Failed;
        }
    }

    public JToken Serialize() {
        var obj = new JObject();

        foreach(var (key, (_, value)) in Cache) {
            obj[key] = new JObject {
                ["TextureKey"] = value.textureKey,
                [nameof(SpriteSettings)] = value.settings.Serialize()
            };
        }

        return obj;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            MainCore.Logger.Wrn(
                $"[{nameof(UserSprite)}] Deserialize failed: token is not JObject"
            );
            return;
        }

        foreach(var property in obj.Properties()) {
            if(property.Value is not JObject entry) {
                continue;
            }

            var textureKey = IOUtils.Read(
                entry,
                "TextureKey",
                string.Empty
            );

            var settings = new SpriteSettings();

            if(entry[nameof(SpriteSettings)] is JToken settingsToken) {
                settings.Deserialize(settingsToken);
            }

            var result = Load(
                property.Name,
                textureKey,
                settings.Rect,
                settings.Pivot,
                settings.PixelsPerUnit,
                settings.Border,
                out _
            );

            if(result != Result.Success) {
                MainCore.Logger.Wrn(
                    $"[{nameof(UserSprite)}] {result} {{ \"{property.Name}\": \"{textureKey}\" }}"
                );
            }
        }
    }

    public override void Dispose() {
        foreach(var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value.sprite);
        }

        Cache.Clear();
    }
}