using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Resource;

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

public sealed class SpriteManager(ResourceManager resource) : IDisposable {
    private readonly ResourceManager resource = resource;

    private readonly Dictionary<object, Sprite> cache = [];

    public Sprite Get(Asset asset) {
        if(cache.TryGetValue(asset, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = resource.GetTexture(asset);

        if(tex == null) {
            return null;
        }

        sprite = Sprite.Create(
            tex,
            new Rect(
                0,
                0,
                tex.width,
                tex.height
            ),
            new Vector2(0.5f, 0.5f)
        );

        cache[asset] = sprite;

        return sprite;
    }

    public Sprite GetSliced(Asset asset, float ppui, Vector4 border) {
        object key = (asset, ppui, border);

        if(cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = resource.GetTexture(asset);

        if(tex == null) {
            return null;
        }

        sprite = Sprite.Create(
            tex,
            new Rect(
                0,
                0,
                tex.width,
                tex.height
            ),
            new Vector2(0.5f, 0.5f),
            ppui,
            0,
            SpriteMeshType.FullRect,
            border
        );

        cache[key] = sprite;

        return sprite;
    }

    public Sprite Get(UISprite sprite) {
        return spriteMap.TryGetValue(
            sprite,
            out Asset asset
        ) ? Get(asset) : null;
    }

    public Sprite Get(UISliceSprite sprite) {
        if(!sliceMap.TryGetValue(sprite, out (Asset asset, float ppui) data)) {
            return null;
        }

        return GetSliced(
            data.asset,
            data.ppui,
            new Vector4(128, 128, 128, 128)
        );
    }

    public void Dispose() {
        foreach(Sprite sprite in cache.Values) {
            Object.Destroy(sprite);
        }

        cache.Clear();
    }

    private readonly Dictionary<UISprite, Asset> spriteMap = new() {
        [UISprite.OV5LogoOutline256] = Asset.OV5LogoOutline256,
        [UISprite.Circle256] = Asset.Circle256,
        [UISprite.X128] = Asset.X128,
        [UISprite.Monitor128] = Asset.Monitor128,
        [UISprite.Gear128] = Asset.Gear128,
        [UISprite.Text128] = Asset.Text128,
        [UISprite.Image128] = Asset.Image128,
        [UISprite.Book128] = Asset.Book128,
        [UISprite.Star128] = Asset.Star128,
        [UISprite.ToggleCircle128] = Asset.ToggleCircle128,
        [UISprite.Triangle128] = Asset.Triangle128,
        [UISprite.Power128] = Asset.Power128
    };

    private readonly Dictionary<UISliceSprite, (Asset asset, float ppui)> sliceMap = new() {
        [UISliceSprite.Circle256P1024] = (Asset.Circle256, 1024f),
        [UISliceSprite.Circle256P2048] = (Asset.Circle256, 2048f),
        [UISliceSprite.CircleHalf256P1024] = (Asset.CircleHalf256, 1024f),
        [UISliceSprite.CircleOutline256P1024] = (Asset.CircleOutline256, 1024f),
        [UISliceSprite.CircleOutline256P2048] = (Asset.CircleOutline256, 2048f)
    };
}