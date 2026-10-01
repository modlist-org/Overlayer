using O5Kit.Resource;
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
    MagnifyingGlass128,
    Box128,
    Cube128,
    Clone128,
    Plus128,
    Ping128,
    CodeBlock128,
    F128,
}

public enum UISliceSprite {
    Circle256P1024,
    Circle256P2048,
    CircleHalf256P1024,
    CircleOutline256O32P1024,
    CircleOutline256O64P1024,
    CircleOutline256O64P2048,
}

/// <summary>Sprite cache over O5Kit's canonical artwork (no duplicated PNGs).
/// An optional module-owned <see cref="ResourceManager"/> serves string lookups
/// that O5Kit doesn't carry (e.g. a module's own icons).</summary>
public sealed class SpriteManager : IDisposable {
    private readonly O5Resources o5 = O5Resources.Bundled();
    private readonly ResourceManager resource;

    /// <param name="resource">Module-owned assets for string lookups. Null serves O5Kit art only.</param>
    public SpriteManager(ResourceManager resource = null) {
        this.resource = resource;
    }

    private readonly Dictionary<object, Sprite> cache = [];

    public static Sprite Create(Texture2D texture)
        => texture == null ? null : Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

    public static Sprite CreateSliced(Texture2D texture, float ppui, Vector4 border)
        => texture == null ? null : Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            ppui,
            0,
            SpriteMeshType.FullRect,
            border
        );

    public Sprite Get(string assetName) {
        if(string.IsNullOrEmpty(assetName)) {
            return null;
        }

        if(cache.TryGetValue(assetName, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(assetName)
            ?? resource?.Get<Texture2D>(assetName);
        if(tex == null) {
            return null;
        }

        sprite = Create(tex);
        cache[assetName] = sprite;

        return sprite;
    }

    public Sprite GetSliced(string assetName, float ppui, Vector4 border) {
        if(string.IsNullOrEmpty(assetName)) {
            return null;
        }

        object key = (assetName, ppui, border);

        if(cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(assetName)
            ?? resource?.Get<Texture2D>(assetName);
        if(tex == null) {
            return null;
        }

        sprite = CreateSliced(tex, ppui, border);
        cache[key] = sprite;

        return sprite;
    }

    private Sprite Get(O5Asset asset) {
        if(cache.TryGetValue(asset, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(asset);
        if(tex == null) {
            return null;
        }

        sprite = Create(tex);
        cache[asset] = sprite;

        return sprite;
    }

    private Sprite GetSliced(O5Asset asset, float ppui, Vector4 border) {
        object key = (asset, ppui, border);

        if(cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(asset);
        if(tex == null) {
            return null;
        }

        sprite = CreateSliced(tex, ppui, border);
        cache[key] = sprite;

        return sprite;
    }

    public Sprite Get(UISprite sprite) => spriteMap.TryGetValue(sprite, out O5Asset asset) ? Get(asset) : null;

    public Sprite Get(UISliceSprite sprite) {
        if(!sliceMap.TryGetValue(sprite, out (O5Asset asset, float ppui) data)) {
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
        o5.Dispose();
    }

    private readonly Dictionary<UISprite, O5Asset> spriteMap = new() {
        [UISprite.OV5LogoOutline256] = O5Asset.OV5LogoOutline256,
        [UISprite.Circle256] = O5Asset.Circle256,
        [UISprite.X128] = O5Asset.X128,
        [UISprite.Monitor128] = O5Asset.Monitor128,
        [UISprite.Gear128] = O5Asset.Gear128,
        [UISprite.Text128] = O5Asset.Text128,
        [UISprite.Image128] = O5Asset.Image128,
        [UISprite.Book128] = O5Asset.Book128,
        [UISprite.Star128] = O5Asset.Star128,
        [UISprite.ToggleCircle128] = O5Asset.ToggleCircle128,
        [UISprite.Triangle128] = O5Asset.Triangle128,
        [UISprite.Power128] = O5Asset.Power128,
        [UISprite.MagnifyingGlass128] = O5Asset.MagnifyingGlass128,
        [UISprite.Box128] = O5Asset.Box128,
        [UISprite.Cube128] = O5Asset.Cube128,
        [UISprite.Clone128] = O5Asset.Clone128,
        [UISprite.Plus128] = O5Asset.Plus128,
        [UISprite.Ping128] = O5Asset.Ping128,
        [UISprite.CodeBlock128] = O5Asset.CodeBlock128,
        [UISprite.F128] = O5Asset.F128,
    };

    private readonly Dictionary<UISliceSprite, (O5Asset asset, float ppui)> sliceMap = new() {
        [UISliceSprite.Circle256P1024] = (O5Asset.Circle256, 1024f),
        [UISliceSprite.Circle256P2048] = (O5Asset.Circle256, 2048f),
        [UISliceSprite.CircleHalf256P1024] = (O5Asset.CircleHalf256, 1024f),
        [UISliceSprite.CircleOutline256O32P1024] = (O5Asset.CircleOutline256O32, 1024f),
        [UISliceSprite.CircleOutline256O64P1024] = (O5Asset.CircleOutline256O64, 1024f),
        [UISliceSprite.CircleOutline256O64P2048] = (O5Asset.CircleOutline256O64, 2048f),
    };
}