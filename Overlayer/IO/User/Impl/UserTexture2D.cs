using Newtonsoft.Json.Linq;
using Overlayer.Compat.OVC;
using Overlayer.Core;
using Overlayer.IO.Unity;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserTexture2D : UserResourceBase<(Texture2D texture, Texture2DSettings settings)> {
    public static readonly HashSet<string> Ext = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };

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
            if(Cache.ContainsKey(key)) {
                return Result.KeyAlreadyExists;
            }

            if(!File.Exists(path)) {
                return Result.NotFound;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if(!Ext.Contains(ext)) {
                return Result.InvalidArgument;
            }

            return LoadData(key, path, File.ReadAllBytes(path), mipChain, linear);
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture load failed: {e}");
            return Result.Failed;
        }
    }

    public Result LoadData(string key, string path, byte[] data, bool mipChain, bool linear) {
        if(string.IsNullOrWhiteSpace(key) || data == null || data.Length == 0) {
            return Result.InvalidArgument;
        }
        if(Cache.ContainsKey(key)) {
            return Result.KeyAlreadyExists;
        }

        Texture2D texture = null;
        try {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            if(!OVC_Texture2D.LoadImage(texture, data)) {
                UnityEngine.Object.Destroy(texture);
                return Result.Failed;
            }
            Cache[key] = (path, (texture, new Texture2DSettings {
                MipChain = mipChain,
                Linear = linear
            }));
            return Result.Success;
        } catch(Exception e) {
            if(texture) {
                UnityEngine.Object.Destroy(texture);
            }

            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture load failed: {e}");
            return Result.Failed;
        }
    }

    public Result ReplaceData(string key, byte[] data, bool mipChain, bool linear) {
        if(!Cache.TryGetValue(key, out var current)) {
            return Result.NotFound;
        }
        if(data == null || data.Length == 0) {
            return Result.InvalidArgument;
        }

        Texture2D replacement = null;
        try {
            replacement = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            if(!OVC_Texture2D.LoadImage(replacement, data)) {
                UnityEngine.Object.Destroy(replacement);
                return Result.Failed;
            }

            Cache[key] = (current.path, (replacement, new Texture2DSettings {
                MipChain = mipChain,
                Linear = linear
            }));
            if(current.value.texture) {
                UnityEngine.Object.Destroy(current.value.texture);
            }

            return Result.Success;
        } catch(Exception e) {
            if(replacement) {
                UnityEngine.Object.Destroy(replacement);
            }

            MainCore.Log.Err($"[{nameof(UserTexture2D)}] Texture replace failed: {e}");
            return Result.Failed;
        }
    }

    public bool Remove(string key) {
        if(!Cache.Remove(key, out var entry)) {
            return false;
        }

        if(entry.value.texture) {
            UnityEngine.Object.Destroy(entry.value.texture);
        }

        return true;
    }

    public JToken Serialize() {
        var obj = new JObject();

        foreach(var (key, (path, value)) in Cache) {
            obj[key] = new JObject {
                ["Path"] = UserResourceManager.ToUser(path),
                [nameof(Texture2DSettings)] = value.settings.Serialize()
            };
        }

        return obj;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            MainCore.Log.Wrn(
                $"[{nameof(UserTexture2D)}] Deserialize failed: token is not JObject"
            );
            return;
        }

        foreach(var property in obj.Properties()) {
            if(property.Value is not JObject entry) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserTexture2D)}] Invalid entry {{ \"{property.Name}\": null }}"
                );
                continue;
            }

            var path = UserResourceManager.FromUser(
                IOUtils.Read(entry, "Path", string.Empty)
            );

            var settings = new Texture2DSettings();

            if(entry[nameof(Texture2DSettings)] is JToken settingsToken) {
                settings.Deserialize(settingsToken);
            }

            var result = Load(
                property.Name,
                path,
                settings.MipChain,
                settings.Linear
            );

            if(result != Result.Success) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserTexture2D)}] {result} {{ \"{property.Name}\": \"{path}\" }}"
                );
            }
        }
    }

    public override void Dispose() {
        foreach(var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value.texture);
        }

        Cache.Clear();
    }
}
