using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.IO.Unity;
using Overlayer.Package;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserSprite : UserResourceBase<(Sprite sprite, string textureKey, SpriteSettings settings)>, ISettingsFile {
    private readonly Dictionary<string, (string textureKey, SpriteSettings settings)> _missing = [];

    public ICollection<string> MissingKeys => _missing.Keys;
    public bool IsMissing(string key) => !string.IsNullOrEmpty(key) && _missing.ContainsKey(key);
    public bool HasKey(string key) => !string.IsNullOrEmpty(key) && (Cache.ContainsKey(key) || _missing.ContainsKey(key));

    public bool TryGetMissing(string key, out string textureKey, out SpriteSettings settings) {
        if (!string.IsNullOrEmpty(key) && _missing.TryGetValue(key, out var entry)) {
            textureKey = entry.textureKey;
            settings = entry.settings;
            return true;
        }
        textureKey = string.Empty;
        settings = null;
        return false;
    }

    public bool IsTextureKeyReferenced(string textureKey) {
        if (string.IsNullOrEmpty(textureKey)) {
            return false;
        }
        return Cache.Values.Any(entry => string.Equals(entry.value.textureKey, textureKey, StringComparison.Ordinal))
            || _missing.Values.Any(entry => string.Equals(entry.textureKey, textureKey, StringComparison.Ordinal));
    }

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
            if (HasKey(key)) {
                if (Cache.TryGetValue(key, out var existing)) {
                    sprite = existing.value.sprite;
                }
                return Result.KeyAlreadyExists;
            }

            if (!UserResourceManager.T2D.TryGet(textureKey, out var value)) {
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
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(UserSprite)}] Sprite load failed: {e}");
            return Result.Failed;
        }
    }

    public JToken Serialize() {
        var obj = new JObject();

        static JObject SerializeEntry(string textureKey, SpriteSettings settings) => new() {
            ["TextureKey"] = textureKey,
            [nameof(SpriteSettings)] = settings.Serialize()
        };

        foreach (var (key, (_, value)) in Cache) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = SerializeEntry(value.textureKey, value.settings);
        }

        foreach (var (key, entry) in _missing) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = SerializeEntry(entry.textureKey, entry.settings);
        }

        return obj;
    }

    public override bool TryRenameKey(string oldKey, string newKey) {
        if (
            string.IsNullOrWhiteSpace(oldKey) ||
            string.IsNullOrWhiteSpace(newKey) ||
            string.Equals(oldKey, newKey, StringComparison.Ordinal) ||
            HasKey(newKey)
        ) {
            return false;
        }

        if (_missing.TryGetValue(oldKey, out var entry)) {
            _missing.Remove(oldKey);
            _missing[newKey] = entry;
            return true;
        }

        return base.TryRenameKey(oldKey, newKey);
    }

    public bool Remove(string key) {
        if (string.IsNullOrEmpty(key)) {
            return false;
        }
        if (Cache.Remove(key, out var entry)) {
            if (entry.value.sprite) {
                UnityEngine.Object.Destroy(entry.value.sprite);
            }
            return true;
        }
        return _missing.Remove(key);
    }

    public bool RenameTextureKey(string oldKey, string newKey) {
        bool changed = false;

        foreach (var (key, entry) in Cache.ToArray()) {
            if (!string.Equals(entry.value.textureKey, oldKey, StringComparison.Ordinal)) {
                continue;
            }

            Cache[key] = (
                entry.path,
                (entry.value.sprite, newKey, entry.value.settings)
            );
            changed = true;
        }

        foreach (var (key, entry) in _missing.ToArray()) {
            if (!string.Equals(entry.textureKey, oldKey, StringComparison.Ordinal)) {
                continue;
            }

            _missing[key] = (newKey, entry.settings);
            changed = true;
        }

        return changed;
    }

    public bool TryReloadForTexture(string textureKey, Texture2D texture) {
        if (string.IsNullOrWhiteSpace(textureKey) || !texture) {
            return false;
        }

        bool loadedAny = false;
        foreach (var (key, entry) in _missing.ToArray()) {
            if (!string.Equals(entry.textureKey, textureKey, StringComparison.Ordinal)) {
                continue;
            }

            SpriteSettings effective = NormalizeSpriteSettings(entry.settings, texture);
            try {
                Sprite sprite = effective.ToUnity(texture);
                if (!sprite) {
                    continue;
                }
                _missing.Remove(key);
                Cache[key] = (textureKey, (sprite, textureKey, effective));
                loadedAny = true;
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(UserSprite)}] Sprite reload failed for '{key}': {e}");
            }
        }

        return loadedAny;
    }

    private static SpriteSettings NormalizeSpriteSettings(SpriteSettings source, Texture2D texture) {
        SpriteSettings effective = source?.Copy() ?? new SpriteSettings();
        Rect rect = effective.Rect.Value;
        if (rect.width <= 0f || rect.height <= 0f) {
            rect = new Rect(0f, 0f, texture.width, texture.height);
        } else if (rect.x < 0f || rect.y < 0f || rect.xMax > texture.width || rect.yMax > texture.height) {
            float x = Mathf.Clamp(rect.x, 0f, texture.width);
            float y = Mathf.Clamp(rect.y, 0f, texture.height);
            float w = Mathf.Min(rect.width, texture.width - x);
            float h = Mathf.Min(rect.height, texture.height - y);
            rect = w > 0f && h > 0f
                ? new Rect(x, y, w, h)
                : new Rect(0f, 0f, texture.width, texture.height);
        }
        effective.Rect.Value = rect;

        Vector4 border = effective.Border.Value;
        float left = Mathf.Clamp(border.x, 0f, rect.width);
        float bottom = Mathf.Clamp(border.y, 0f, rect.height);
        float right = Mathf.Clamp(border.z, 0f, Math.Max(0f, rect.width - left));
        float top = Mathf.Clamp(border.w, 0f, Math.Max(0f, rect.height - bottom));
        effective.Border.Value = new Vector4(left, bottom, right, top);

        if (effective.PixelsPerUnit.Value <= 0f) {
            effective.PixelsPerUnit.Value = 100f;
        }
        return effective;
    }

    public bool RebuildTexture(string textureKey, Texture2D texture) {
        if (string.IsNullOrWhiteSpace(textureKey) || !texture) {
            return false;
        }

        var replacements = new List<(string key, Sprite oldSprite, Sprite newSprite)>();
        try {
            foreach (var (key, entry) in Cache) {
                if (!string.Equals(entry.value.textureKey, textureKey, StringComparison.Ordinal)) {
                    continue;
                }

                replacements.Add((
                    key,
                    entry.value.sprite,
                    entry.value.settings.ToUnity(texture)
                ));
            }

            foreach (var (key, oldSprite, newSprite) in replacements) {
                var (path, value) = Cache[key];
                Cache[key] = (
                    path,
                    (newSprite, value.textureKey, value.settings)
                );
                if (oldSprite) {
                    UnityEngine.Object.Destroy(oldSprite);
                }
            }

            return replacements.Count > 0;
        } catch (Exception e) {
            foreach (var (key, oldSprite, newSprite) in replacements) {
                if (newSprite) {
                    UnityEngine.Object.Destroy(newSprite);
                }
            }
            MainCore.Log.Err($"[{nameof(UserSprite)}] Sprite rebuild failed: {e}");
            return false;
        }
    }

    public bool UpdateBorder(string key, Vector4 border) {
        if (!Cache.TryGetValue(key, out var entry) ||
            !UserResourceManager.T2D.TryGet(entry.value.textureKey, out var textureValue)) {
            return false;
        }

        SpriteSettings settings = entry.value.settings.Copy();
        settings.Border = border;
        Sprite sprite = settings.ToUnity(textureValue.texture);
        if (!sprite) {
            return false;
        }

        Cache[key] = (
            entry.path,
            (sprite, entry.value.textureKey, settings)
        );
        if (entry.value.sprite) {
            UnityEngine.Object.Destroy(entry.value.sprite);
        }

        return true;
    }

    public void Deserialize(JToken token) {
        if (token is not JObject obj) {
            MainCore.Log.Wrn(
                $"[{nameof(UserSprite)}] Deserialize failed: token is not JObject"
            );
            return;
        }

        foreach (var property in obj.Properties()) {
            if (O5cpFormat.IsPackageKey(property.Name)) {
                continue;
            }
            if (property.Value is not JObject entry) {
                continue;
            }

            var textureKey = IOUtils.Read(
                entry,
                "TextureKey",
                string.Empty
            );

            var settings = new SpriteSettings();

            if (entry[nameof(SpriteSettings)] is JToken settingsToken) {
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

            if (result != Result.Success) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserSprite)}] {result} {{ \"{property.Name}\": \"{textureKey}\" }}"
                );
                if (result != Result.KeyAlreadyExists && !HasKey(property.Name) && !string.IsNullOrWhiteSpace(property.Name)) {
                    _missing[property.Name] = (textureKey, settings);
                }
            }
        }
    }

    public override void Dispose() {
        foreach (var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value.sprite);
        }

        Cache.Clear();
        _missing.Clear();
    }
}
