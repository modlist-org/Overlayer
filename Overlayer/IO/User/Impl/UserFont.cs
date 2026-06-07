using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.IO.User.Impl;

public class UserFont : UserResourceBase<TMP_FontAsset>, ISettingsFile {
    public static readonly HashSet<string> Ext = [".ttf", ".otf"];

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

            var font = TMP_FontAsset.CreateFontAsset(new Font(path));

            Cache[key] = (path, font);

            return Result.Success;
        } catch(Exception e) {
            MainCore.Log.Err($"{nameof(UserResourceManager)} Font load failed: {e}");
            return Result.Failed;
        }
    }

    public JToken Serialize() {
        var obj = new JObject();

        foreach(var (key, (path, _)) in Cache) {
            obj[key] = UserResourceManager.ToUser(path);
        }

        return obj;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            MainCore.Log.Wrn($"[{nameof(UserFont)}] Deserialize failed: token is not JObject");
            return;
        }

        foreach(var property in obj.Properties()) {
            var key = property.Name;
            var path = UserResourceManager.FromUser(property.Value.ToString());

            var result = Load(key, path);

            if(result != Result.Success) {
                MainCore.Log.Wrn(
                    $"[{nameof(UserFont)}] {result} {{ \"{property.Name}\": \"{path}\" }}"
                );
            }
        }
    }

    public override void Dispose() {
        foreach(var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value);
        }

        Cache.Clear();
    }
}
