using Overlayer.Core;
using TMPro;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserFont : UserResourceBase<TMP_FontAsset> {
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
            MainCore.Logger.Err($"{nameof(UserResourceManager)} Font load failed: {e}");
            return Result.Failed;
        }
    }

    public override void Dispose() {
        foreach(var (_, value) in Cache.Values) {
            UnityEngine.Object.Destroy(value);
        }

        Cache.Clear();
    }
}
