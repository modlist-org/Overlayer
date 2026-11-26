using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.Package;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.IO.User.Impl;

public class UserFont : UserResourceBase<TMP_FontAsset>, ISettingsFile {
    public static readonly HashSet<string> Ext = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };
    private readonly Dictionary<string, string> _missing = [];

    public ICollection<string> MissingKeys => _missing.Keys;
    public bool IsMissing(string key) => !string.IsNullOrEmpty(key) && _missing.ContainsKey(key);
    public bool HasKey(string key) => !string.IsNullOrEmpty(key) && (Cache.ContainsKey(key) || _missing.ContainsKey(key));

    public bool TryGetMissingPath(string key, out string path) {
        if (!string.IsNullOrEmpty(key) && _missing.TryGetValue(key, out path)) {
            return true;
        }
        path = string.Empty;
        return false;
    }

    public bool TryGetAnyPath(string key, out string path) =>
        TryGetPath(key, out path) || TryGetMissingPath(key, out path);

#if ML && IL2CPP
    private static readonly HashSet<TMP_FontAsset> StolenFonts = new();

    private static bool IsStolen(TMP_FontAsset font) {
        if (font == null) {
            return false;
        }
        lock (StolenFonts) {
            try {
                return StolenFonts.Contains(font);
            } catch {
                return false;
            }
        }
    }
#endif

    public enum Result {
        Success,
        KeyAlreadyExists,
        NotFound,
        InvalidArgument,
        Failed,
    }

    public Result Load(
        string key,
        string path
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

            var font = O5Kit.Resource.O5Resources.TryCreateFontAssetFromPath(path, 48, 8, 2048, 2048, true);
            if (font != null) {
                Cache[key] = (path, font);
                return Result.Success;
            }
#if ML && IL2CPP
            // Stripped runtimes that cannot load TTF bytes fall back to a game font
            // so text stays readable. Stolen assets are tracked and never destroyed.
            var stolen = Overlayer.Resource.ResourceManager.StealGameFont();
            if (stolen == null) {
                return Result.Failed;
            }

            Cache[key] = (path, stolen);
            lock (StolenFonts) {
                StolenFonts.Add(stolen);
            }

            return Result.Success;
#else
            return Result.Failed;
#endif
        } catch (Exception e) {
            MainCore.Log.Err($"{nameof(UserResourceManager)} Font load failed: {e}");
            return Result.Failed;
        }
    }

    public Result TryReload(string key) {
        if (string.IsNullOrEmpty(key)) {
            return Result.InvalidArgument;
        }
        if (Cache.ContainsKey(key)) {
            return Result.Success;
        }
        if (!_missing.TryGetValue(key, out string path)) {
            return Result.NotFound;
        }

        _missing.Remove(key);
        Result result = Load(key, path);
        if (result != Result.Success) {
            _missing[key] = path;
        }
        return result;
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

        if (_missing.TryGetValue(oldKey, out string path)) {
            _missing.Remove(oldKey);
            _missing[newKey] = path;
            return true;
        }

        return base.TryRenameKey(oldKey, newKey);
    }

    public bool Remove(string key) {
        if (string.IsNullOrEmpty(key)) {
            return false;
        }
        if (Cache.Remove(key, out var entry)) {
#if ML && IL2CPP
            if (IsStolen(entry.value)) {
                return true;
            }
#endif
            if (entry.value) {
                UnityEngine.Object.Destroy(entry.value);
            }

            return true;
        }

        return _missing.Remove(key);
    }

    public JToken Serialize() {
        var obj = new JObject();

        foreach (var (key, (path, _)) in Cache) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = UserResourceManager.ToUser(path);
        }

        foreach (var (key, path) in _missing) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            obj[key] = UserResourceManager.ToUser(path);
        }

        return obj;
    }

    public void Deserialize(JToken token) {
        if (token is not JObject obj) {
            MainCore.Log.Wrn($"[{nameof(UserFont)}] Deserialize failed: token is not JObject");
            return;
        }

        foreach (var property in obj.Properties()) {
            var key = property.Name;
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            var path = UserResourceManager.FromUser(property.Value.ToString());

            var result = Load(key, path);

            if (result != Result.Success) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserFont)}] {result} {{ \"{property.Name}\": \"{path}\" }}"
                );
                if (result != Result.KeyAlreadyExists && !HasKey(key) && !string.IsNullOrWhiteSpace(key)) {
                    _missing[key] = path;
                }
            }
        }
    }

    public override void Dispose() {
        foreach (var (_, value) in Cache.Values) {
#if ML && IL2CPP
            if (IsStolen(value)) {
                continue;
            }
#endif
            UnityEngine.Object.Destroy(value);
        }

        Cache.Clear();
        _missing.Clear();
    }
}
