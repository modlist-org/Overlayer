using Overlayer.Core;
using TMPro;
using UnityEngine;

namespace Overlayer.IO.User;

public static class UserResource {
    private static Dictionary<string, Texture2D> T2D { get; } = [];
    private static Dictionary<string, Sprite> Spr { get; } = [];
    private static Dictionary<string, TMP_FontAsset> Fnt { get; } = [];

    private static readonly HashSet<string> imageExt = [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".tga"
    ];

    public enum LoadTextureResult {
        Success,
        KeyAlreadyExists,
        NotFound,
        InvalidArgument,
        Failed,
    }

    public static LoadTextureResult LoadTexture(
        string key,
        string path,
        bool mipChain,
        bool linear
    ) {
        try {
            if(T2D.ContainsKey(key)) {
                return LoadTextureResult.KeyAlreadyExists;
            }

            if(!File.Exists(path)) {
                return LoadTextureResult.NotFound;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if(!imageExt.Contains(ext)) {
                return LoadTextureResult.InvalidArgument;
            }

            var data = File.ReadAllBytes(path);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain, linear);
            tex.LoadImage(data);

            T2D[key] = tex;

            return LoadTextureResult.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"{nameof(UserResource)} Texture load failed: {e}");
            return LoadTextureResult.Failed;
        }
    }

    public enum LoadSpriteResult {
        Success,
        KeyAlreadyExists,
        NotFound,
        Failed,
    }

    public static LoadSpriteResult LoadSprite(
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
            if(Spr.ContainsKey(key)) {
                sprite = Spr[key];
                return LoadSpriteResult.KeyAlreadyExists;
            }

            if(!T2D.TryGetValue(textureKey, out var texture)) {
                return LoadSpriteResult.NotFound;
            }

            var spr = Sprite.Create(
                texture,
                rect,
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border,
                false
            );

            Spr[key] = spr;
            sprite = spr;

            return LoadSpriteResult.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"{nameof(UserResource)} Sprite load failed: {e}");
            return LoadSpriteResult.Failed;
        }
    }

    private static readonly HashSet<string> fontExt = [
        ".ttf",
        ".otf",
    ];

    public enum LoadFontResult {
        Success,
        KeyAlreadyExists,
        NotFound,
        InvalidArgument,
        Failed,
    }

    public static LoadFontResult LoadFont(
        string key,
        string path
    ) {
        try {
            if(Fnt.ContainsKey(key)) {
                return LoadFontResult.KeyAlreadyExists;
            }

            if(!File.Exists(path)) {
                return LoadFontResult.NotFound;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();

            if(!fontExt.Contains(ext)) {
                return LoadFontResult.InvalidArgument;
            }

            var fontData = File.ReadAllBytes(path);
            var font = TMP_FontAsset.CreateFontAsset(new Font(path));

            Fnt[key] = font;

            return LoadFontResult.Success;
        } catch(Exception e) {
            MainCore.Logger.Err($"{nameof(UserResource)} Font load failed: {e}");
            return LoadFontResult.Failed;
        }
    }
}