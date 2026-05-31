using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Unity;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserTexture2D : UserResourceBase<(Texture2D texture, Texture2DSettings settings)> {
    public static readonly HashSet<string> Ext = [".png", ".jpg", ".jpeg", ".bmp", ".tga"];

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

            var data = File.ReadAllBytes(path);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            tex.LoadImage(data);

            Cache[key] = (
                path,
                (tex, new())
            );

            return Result.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(UserTexture2D)}] Texture load failed: {e}");
            return Result.Failed;
        }
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
            MainCore.Logger.Wrn(
                $"[{nameof(UserTexture2D)}] Deserialize failed: token is not JObject"
            );
            return;
        }

        foreach(var property in obj.Properties()) {
            if(property.Value is not JObject entry) {
                MainCore.Logger.Wrn(
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
                MainCore.Logger.Wrn(
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
