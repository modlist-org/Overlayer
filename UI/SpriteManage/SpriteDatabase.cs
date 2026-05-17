using Overlayer.Resource;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.UI.SpriteManage;

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
    ToggleCircle128,
    Triangle128,
    Power128,
}

public enum UISliceSprite {
    Circle256P1024,
    Circle256P2048,
    CircleHalf256P1024,
    CircleOutline256P1024,
    CircleOutline256P2048,
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
        var toggleCircle = ResourceManager.Get<Texture2D>(Asset.ToggleCircle128);
        var circleOutline = ResourceManager.Get<Texture2D>(Asset.CircleOutline256);
        var triangle = ResourceManager.Get<Texture2D>(Asset.Triangle128);
        var power = ResourceManager.Get<Texture2D>(Asset.Power128);

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

        sprites[UISprite.ToggleCircle128] =
            SpriteFactory.Create(toggleCircle);

        sprites[UISprite.Triangle128] =
            SpriteFactory.Create(triangle);

        sprites[UISprite.Power128] =
           SpriteFactory.Create(power);

        sliceSprites[UISliceSprite.Circle256P1024] =
            SpriteFactory.CreateSliced(circle, 1024f, border);

        sliceSprites[UISliceSprite.Circle256P2048] =
            SpriteFactory.CreateSliced(circle, 2048f, border);

        sliceSprites[UISliceSprite.CircleHalf256P1024] =
            SpriteFactory.CreateSliced(half, 1024f, border);

        sliceSprites[UISliceSprite.CircleOutline256P1024] =
            SpriteFactory.CreateSliced(circleOutline, 1024f, border);

        sliceSprites[UISliceSprite.CircleOutline256P2048] =
            SpriteFactory.CreateSliced(circleOutline, 2048f, border);
    }

    public static Sprite Get(UISprite key) => sprites[key];
    public static Sprite Get(UISliceSprite key) => sliceSprites[key];

    public static void Dispose() {
        foreach(var sprite in sprites.Values) {
            Object.Destroy(sprite);
        }
        sprites.Clear();
        foreach(var sprite in sliceSprites.Values) {
            Object.Destroy(sprite);
        }
        sliceSprites.Clear();
    }
}