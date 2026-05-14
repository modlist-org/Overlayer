using Overlayer.Resource;
using UnityEngine;

namespace Overlayer.UI.UISprites;

public enum UISprite {
    OV5LogoOutline256,
    Circle256,
    X128,
    Monitor128,
    Gear128,
    Text128,
    Image128,
    Book128,
    Star128,
}

public enum UISliceSprite {
    Circle256,
    CircleHalf256
}

public static class SpriteDatabase {
    private static readonly Dictionary<UISprite, Sprite> sprites = [];
    private static readonly Dictionary<UISliceSprite, Sprite> sliceSprites = [];

    public static void Initialize() {
        Vector4 border = new(128, 128, 128, 128);

        var logo = ResourceManager.Get<Texture2D>(Asset.OV5LogoOutline256);
        var circle = ResourceManager.Get<Texture2D>(Asset.Circle256);
        var half = ResourceManager.Get<Texture2D>(Asset.CircleHalf256);
        var x = ResourceManager.Get<Texture2D>(Asset.X128);
        var monitor = ResourceManager.Get<Texture2D>(Asset.Monitor128);
        var gear = ResourceManager.Get<Texture2D>(Asset.Gear128);
        var text = ResourceManager.Get<Texture2D>(Asset.Text128);
        var image = ResourceManager.Get<Texture2D>(Asset.Image128);
        var book = ResourceManager.Get<Texture2D>(Asset.Book128);
        var star = ResourceManager.Get<Texture2D>(Asset.Star128);

        sprites[UISprite.OV5LogoOutline256] =
            SpriteFactory.Create(logo);

        sprites[UISprite.Circle256] =
            SpriteFactory.Create(circle);

        sprites[UISprite.X128] =
            SpriteFactory.Create(x);

        sprites[UISprite.Monitor128] =
            SpriteFactory.Create(monitor);

        sprites[UISprite.Gear128] =
            SpriteFactory.Create(gear);

        sprites[UISprite.Text128] =
            SpriteFactory.Create(text);

        sprites[UISprite.Image128] =
            SpriteFactory.Create(image);

        sprites[UISprite.Book128] =
            SpriteFactory.Create(book);

        sprites[UISprite.Star128] =
            SpriteFactory.Create(star);

        sliceSprites[UISliceSprite.Circle256] =
            SpriteFactory.CreateSliced(circle, 1024f, border);

        sliceSprites[UISliceSprite.CircleHalf256] =
            SpriteFactory.CreateSliced(half, 1024f, border);
    }

    public static Sprite Get(UISprite key) => sprites[key];
    public static Sprite Get(UISliceSprite key) => sliceSprites[key];
}