using Overlayer.Core;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserTexture2D : UserResourceBase<Texture2D> {
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

            Cache[key] = (path, tex);

            return Result.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"{nameof(UserResourceManager)} Texture load failed: {e}");
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
