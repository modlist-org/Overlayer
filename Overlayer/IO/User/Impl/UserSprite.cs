using Overlayer.Core;
using UnityEngine;

namespace Overlayer.IO.User.Impl;

public class UserSprite : UserResourceBase<Sprite> {
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
                sprite = Cache[key].value;
                return Result.KeyAlreadyExists;
            }

            if(!UserResourceManager.T2D.TryGet(textureKey, out var value)) {
                return Result.NotFound;
            }

            var spr = Sprite.Create(
                value,
                rect,
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border,
                false
            );

            Cache[key] = (textureKey, spr);
            sprite = spr;

            return Result.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"{nameof(UserResourceManager)} Sprite load failed: {e}");
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