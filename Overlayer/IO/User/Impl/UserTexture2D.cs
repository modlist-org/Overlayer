using Newtonsoft.Json.Linq;
using O5Kit.Compat;
using Overlayer.Core;
using Overlayer.IO.Unity;
using Overlayer.Package;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserTexture2D : UserResourceBase<(Texture2D texture, Texture2DSettings settings)> {
    public static readonly HashSet<string> Ext = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };
    private readonly Dictionary<string, (string path, Texture2DSettings settings)> _missing = [];

    public ICollection<string> MissingKeys => _missing.Keys;
    public bool IsMissing(string key) => !string.IsNullOrEmpty(key) && _missing.ContainsKey(key);
    public bool HasKey(string key) => !string.IsNullOrEmpty(key) && (Cache.ContainsKey(key) || _missing.ContainsKey(key));

    public bool TryGetMissing(string key, out (string path, Texture2DSettings settings) entry) {
        if (!string.IsNullOrEmpty(key) && _missing.TryGetValue(key, out entry)) {
            return true;
        }
        entry = default;
        return false;
    }

    public bool TryGetAnyPath(string key, out string path) {
        if (TryGetPath(key, out path)) {
            return true;
        }
        if (TryGetMissing(key, out var entry)) {
            path = entry.path;
            return true;
        }
        path = string.Empty;
        return false;
    }

    public enum Result {
        Success,
        KeyAlreadyExists,
        NotFound,
        InvalidArgument,
        Failed,
    }

    public Result Load(
        string key,
        string path,
        bool mipChain,
        bool linear
    ) {
        try {
            if (HasKey(key)) {
                return Result.KeyAlreadyExists;
            }

            if (!File.Exists(path)) {
                return Result.NotFound;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!Ext.Contains(ext)) {
                return Result.InvalidArgument;
            }

            return LoadData(key, path, File.ReadAllBytes(path), mipChain, linear);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture load failed: {e}");
            return Result.Failed;
        }
    }

    public Result LoadData(string key, string path, byte[] data, bool mipChain, bool linear) {
        if (string.IsNullOrWhiteSpace(key) || data == null || data.Length == 0) {
            return Result.InvalidArgument;
        }
        if (HasKey(key)) {
            return Result.KeyAlreadyExists;
        }

        Texture2D texture = null;
        try {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            if (!O5Texture.LoadImage(texture, data)) {
                UnityEngine.Object.Destroy(texture);
                return Result.Failed;
            }
            Cache[key] = (path, (texture, new Texture2DSettings {
                MipChain = mipChain,
                Linear = linear
            }));
            return Result.Success;
        } catch (Exception e) {
            if (texture) {
                UnityEngine.Object.Destroy(texture);
            }

            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture load failed: {e}");
            return Result.Failed;
        }
    }

    public Result ReplaceData(string key, byte[] data, bool mipChain, bool linear) {
        if (!Cache.TryGetValue(key, out var current)) {
            return Result.NotFound;
        }
        if (data == null || data.Length == 0) {
            return Result.InvalidArgument;
        }

        Texture2D replacement = null;
        try {
            replacement = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            if (!O5Texture.LoadImage(replacement, data)) {
                UnityEngine.Object.Destroy(replacement);
                return Result.Failed;
            }

            Cache[key] = (current.path, (replacement, new Texture2DSettings {
                MipChain = mipChain,
                Linear = linear
            }));
            if (current.value.texture) {
                UnityEngine.Object.Destroy(current.value.texture);
            }

            return Result.Success;
        } catch (Exception e) {
            if (replacement) {
                UnityEngine.Object.Destroy(replacement);
            }

            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture replace failed: {e}");
            return Result.Failed;
        }
    }

    public Result TryReload(string key, out Texture2D texture) {
        texture = null;
        if (string.IsNullOrEmpty(key)) {
            return Result.InvalidArgument;
        }
        if (Cache.TryGetValue(key, out var loaded)) {
            texture = loaded.value.texture;
            return Result.Success;
        }
        if (!_missing.TryGetValue(key, out var entry)) {
            return Result.NotFound;
        }

        _missing.Remove(key);
        Result result = Load(key, entry.path, entry.settings.MipChain.Value, entry.settings.Linear.Value);
        if (result != Result.Success) {
            _missing[key] = entry;
            return result;
        }

        texture = Cache[key].value.texture;
        return Result.Success;
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
            if (entry.value.texture) {
                UnityEngine.Object.Destroy(entry.value.texture);
            }
            return true;
        }
        return _missing.Remove(key);
    }

    public JToken Serialize() {
        var obj = new JObject();

        static JObject SerializeEntry(string path, Texture2DSettings settings) => new() {
            ["Path"] = UserResourceManager.ToUser(path),
            [nameof(Texture2DSettings)] = settings.Serialize()
        };

        foreach (var (key, (path, value)) in Cache) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = SerializeEntry(path, value.settings);
        }

        foreach (var (key, (path, settings)) in _missing) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = SerializeEntry(path, settings);
        }

        return obj;
    }

    public void Deserialize(JToken token) {
        if (token is not JObject obj) {
            MainCore.Log.Wrn(
                $"[{nameof(UserTexture2D)}] Deserialize failed: token is not JObject"
            );
            return;
        }

        foreach (var property in obj.Properties()) {
            if (O5cpFormat.IsPackageKey(property.Name)) {
                continue;
            }
            if (property.Value is not JObject entry) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserTexture2D)}] Invalid entry {{ \"{property.Name}\": null }}"
                );
                continue;
            }

            var path = UserResourceManager.FromUser(
                IOUtils.Read(entry, "Path", string.Empty)
            );

            var settings = new Texture2DSettings();

            if (entry[nameof(Texture2DSettings)] is JToken settingsToken) {
                settings.Deserialize(settingsToken);
            }

            var result = Load(
                property.Name,
                path,
                settings.MipChain,
                settings.Linear
            );

            if (result != Result.Success) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserTexture2D)}] {result} {{ \"{property.Name}\": \"{path}\" }}"
                );
                if (result != Result.KeyAlreadyExists && !HasKey(property.Name) && !string.IsNullOrWhiteSpace(property.Name)) {
                    _missing[property.Name] = (path, settings);
                }
            }
        }
    }

    public override void Dispose() {
        foreach (var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value.texture);
        }

        Cache.Clear();
        _missing.Clear();
    }
}
