using UnityEngine;
using Object = UnityEngine.Object;
using O5Kit.Resource;

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
    Expand128,
    Upload128,
    Download128,
}

public enum UISliceSprite {
    Circle256P1024,
    Circle256P2048,
    CircleHalf256P1024,
    CircleOutline256O32P1024,
    CircleOutline256O64P1024,
    CircleOutline256O64P2048,
}

public sealed class SpriteManager(ResourceManager resource) : IDisposable {
    private const int SmoothCircleSize = 128;
    // Slight inset keeps edge texels at alpha 0 so small dots never clip into flat sides.
    private const float SmoothCirclePad = 0.035f;

    private readonly ResourceManager resource = resource;
    private readonly O5Resources o5 = O5Resources.Bundled();
    private readonly Dictionary<object, Sprite> cache = [];
    private Texture2D smoothCircleTex;

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
        if (string.IsNullOrEmpty(assetName)) {
            return null;
        }

        if (cache.TryGetValue(assetName, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(assetName) ?? resource.Get<Texture2D>(assetName);
        if (tex == null) {
            return null;
        }

        sprite = Create(tex);
        cache[assetName] = sprite;

        return sprite;
    }

    public Sprite GetSliced(string assetName, float ppui, Vector4 border) {
        if (string.IsNullOrEmpty(assetName)) {
            return null;
        }

        object key = (assetName, ppui, border);

        if (cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(assetName) ?? resource.Get<Texture2D>(assetName);
        if (tex == null) {
            return null;
        }

        sprite = CreateSliced(tex, ppui, border);
        cache[key] = sprite;

        return sprite;
    }

    public Sprite Get(Asset asset) {
        if (cache.TryGetValue(asset, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = resource.Get<Texture2D>(asset);
        if (tex == null) {
            return null;
        }

        sprite = Create(tex);
        cache[asset] = sprite;

        return sprite;
    }

    public Sprite GetSliced(Asset asset, float ppui, Vector4 border) {
        object key = (asset, ppui, border);

        if (cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = resource.Get<Texture2D>(asset);
        if (tex == null) {
            return null;
        }

        sprite = CreateSliced(tex, ppui, border);
        cache[key] = sprite;

        return sprite;
    }

    private Texture2D SmoothCircleTexture() {
        if (smoothCircleTex != null) {
            return smoothCircleTex;
        }
        int size = SmoothCircleSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            mipMapBias = -0.5f,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[size * size];
        float center = size * 0.5f;
        float radius = size * (0.5f - SmoothCirclePad);
        for (int y = 0; y < size; y++) {
            float dy = y + 0.5f - center;
            for (int x = 0; x < size; x++) {
                float dx = x + 0.5f - center;
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        return smoothCircleTex = tex;
    }

    public Sprite GetSmoothSliced(float radius) {
        if (radius <= 0f) {
            return null;
        }
        object key = ("smooth_circle_sliced", radius);
        if (cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }
        float half = SmoothCircleSize * 0.5f;
        sprite = CreateSliced(SmoothCircleTexture(), half * 100f / radius, new Vector4(half, half, half, half));
        cache[key] = sprite;
        return sprite;
    }

    private Sprite Get(O5Asset asset) {
        if (cache.TryGetValue(asset, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(asset);
        if (tex == null) {
            return null;
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        sprite = Create(tex);
        cache[asset] = sprite;
        return sprite;
    }

    private Sprite GetSliced(O5Asset asset, float ppui, Vector4 border) {
        object key = (asset, ppui, border);
        if (cache.TryGetValue(key, out Sprite sprite)) {
            return sprite;
        }

        Texture2D tex = o5.GetTexture(asset);
        if (tex == null) {
            return null;
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        sprite = CreateSliced(tex, ppui, border);
        cache[key] = sprite;
        return sprite;
    }

    public Sprite Get(UISprite sprite) {
        if (sprite == UISprite.Circle256) {
            if (cache.TryGetValue(sprite, out Sprite cached)) {
                return cached;
            }
            Sprite smooth = Create(SmoothCircleTexture());
            cache[sprite] = smooth;
            return smooth;
        }

        if (o5SpriteMap.TryGetValue(sprite, out O5Asset o5Asset)) {
            return Get(o5Asset);
        }

        return spriteMap.TryGetValue(sprite, out Asset asset) ? Get(asset) : null;
    }

    public Sprite Get(UISliceSprite sprite) {
        if (o5SliceMap.TryGetValue(sprite, out (O5Asset asset, float ppui) o5Data)) {
            return GetSliced(
                o5Data.asset,
                o5Data.ppui,
                new Vector4(128, 128, 128, 128)
            );
        }

        if (!sliceMap.TryGetValue(sprite, out (Asset asset, float ppui) data)) {
            return null;
        }

        return GetSliced(
            data.asset,
            data.ppui,
            new Vector4(128, 128, 128, 128)
        );
    }

    public void Dispose() {
        foreach (Sprite sprite in cache.Values) {
            Object.Destroy(sprite);
        }

        cache.Clear();
        if (smoothCircleTex != null) {
            Object.Destroy(smoothCircleTex);
            smoothCircleTex = null;
        }
        o5.Dispose();
    }

    private readonly Dictionary<UISprite, Asset> spriteMap = new() {
        [UISprite.OV5LogoOutline256] = Asset.OV5LogoOutline256,
        [UISprite.Monitor128] = Asset.Monitor128,
        [UISprite.Gear128] = Asset.Gear128,
        [UISprite.Text128] = Asset.Text128,
        [UISprite.Image128] = Asset.Image128,
        [UISprite.Book128] = Asset.Book128,
        [UISprite.Star128] = Asset.Star128,
        [UISprite.Power128] = Asset.Power128,
        [UISprite.MagnifyingGlass128] = Asset.MagnifyingGlass128,
        [UISprite.Box128] = Asset.Box128,
        [UISprite.Cube128] = Asset.Cube128,
        [UISprite.Clone128] = Asset.Clone128,
        [UISprite.Plus128] = Asset.Plus128,
        [UISprite.Ping128] = Asset.Ping128,
        [UISprite.CodeBlock128] = Asset.CodeBlock128,
        [UISprite.F128] = Asset.F128,
        [UISprite.Expand128] = Asset.Expand128,
        [UISprite.Upload128] = Asset.Upload128,
        [UISprite.Download128] = Asset.Download128,
    };

    private readonly Dictionary<UISprite, O5Asset> o5SpriteMap = new() {
        [UISprite.X128] = O5Asset.X128,
        [UISprite.ToggleCircle128] = O5Asset.ToggleCircle128,
        [UISprite.Triangle128] = O5Asset.Triangle128,
    };

    private readonly Dictionary<UISliceSprite, (Asset asset, float ppui)> sliceMap = new() {
        [UISliceSprite.CircleOutline256O32P1024] = (Asset.CircleOutline256O32, 1024f),
    };

    private readonly Dictionary<UISliceSprite, (O5Asset asset, float ppui)> o5SliceMap = new() {
        [UISliceSprite.Circle256P1024] = (O5Asset.Circle256, 1024f),
        [UISliceSprite.Circle256P2048] = (O5Asset.Circle256, 2048f),
        [UISliceSprite.CircleHalf256P1024] = (O5Asset.CircleHalf256, 1024f),
        [UISliceSprite.CircleOutline256O64P1024] = (O5Asset.CircleOutline256O64, 1024f),
        [UISliceSprite.CircleOutline256O64P2048] = (O5Asset.CircleOutline256O64, 2048f),
    };
}
