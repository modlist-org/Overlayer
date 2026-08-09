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
            MainCore.Log.Err($"[{nameof(UserSprite)}] Sprite load failed: {e}");
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

    public bool Remove(string key) {
        if(!Cache.Remove(key, out var entry)) {
            return false;
        }

        if(entry.value.sprite) {
            UnityEngine.Object.Destroy(entry.value.sprite);
        }

        return true;
    }

    public bool RenameTextureKey(string oldKey, string newKey) {
        bool changed = false;

        foreach(var (key, entry) in Cache.ToArray()) {
            if(!string.Equals(entry.value.textureKey, oldKey, StringComparison.Ordinal)) {
                continue;
            }

            Cache[key] = (
                entry.path,
                (entry.value.sprite, newKey, entry.value.settings)
            );
            changed = true;
        }

        return changed;
    }

    public bool RebuildTexture(string textureKey, Texture2D texture) {
        if(string.IsNullOrWhiteSpace(textureKey) || !texture) {
            return false;
        }

        var replacements = new List<(string key, Sprite oldSprite, Sprite newSprite)>();
        try {
            foreach(var (key, entry) in Cache) {
                if(!string.Equals(entry.value.textureKey, textureKey, StringComparison.Ordinal)) {
                    continue;
                }

                replacements.Add((
                    key,
                    entry.value.sprite,
                    entry.value.settings.ToUnity(texture)
                ));
            }

            foreach(var (key, oldSprite, newSprite) in replacements) {
                var (path, value) = Cache[key];
                Cache[key] = (
                    path,
                    (newSprite, value.textureKey, value.settings)
                );
                if(oldSprite) {
                    UnityEngine.Object.Destroy(oldSprite);
                }
            }

            return replacements.Count > 0;
        } catch(Exception e) {
            foreach(var (key, oldSprite, newSprite) in replacements) {
                if(newSprite) {
                    UnityEngine.Object.Destroy(newSprite);
                }
            }
            MainCore.Log.Err($"[{nameof(UserSprite)}] Sprite rebuild failed: {e}");
            return false;
        }
    }

    public bool UpdateBorder(string key, Vector4 border) {
        if(!Cache.TryGetValue(key, out var entry) ||
            !UserResourceManager.T2D.TryGet(entry.value.textureKey, out var textureValue)) {
            return false;
        }

        SpriteSettings settings = entry.value.settings.Copy();
        settings.Border = border;
        Sprite sprite = settings.ToUnity(textureValue.texture);
        if(!sprite) {
            return false;
        }

        Cache[key] = (
            entry.path,
            (sprite, entry.value.textureKey, settings)
        );
        if(entry.value.sprite) {
            UnityEngine.Object.Destroy(entry.value.sprite);
        }

        return true;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            MainCore.Log.Wrn(
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
                MainCore.Log.Wrn(
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
