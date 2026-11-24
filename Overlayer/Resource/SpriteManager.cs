using UnityEngine;
using Object = UnityEngine.Object;
using O5Kit.Resource;

namespace Overlayer.Resource;

public enum UISprite {
    OV5LogoOutline128,
    Circle128,
}

public enum UISliceSprite {
    Circle128P512,
    Circle128P1024,
    CircleHalf128P512,
    CircleOutline128O16P512,
    CircleOutline128O32P512,
    CircleOutline128O32P1024,
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
        if (sprite == UISprite.Circle128) {
            if (cache.TryGetValue(sprite, out Sprite cached)) {
                return cached;
            }
            Sprite smooth = Create(SmoothCircleTexture());
            cache[sprite] = smooth;
            return smooth;
        }

        return spriteMap.TryGetValue(sprite, out Asset asset) ? Get(asset) : null;
    }

    public Sprite Get(UISliceSprite sprite) {
        Vector4 border = sprite == UISliceSprite.CircleHalf128P512
            ? new Vector4(64, 0, 64, 64)
            : new Vector4(64, 64, 64, 64);

        if (o5SliceMap.TryGetValue(sprite, out (O5Asset asset, float ppui) o5Data)) {
            return GetSliced(
                o5Data.asset,
                o5Data.ppui,
                border
            );
        }

        if (!sliceMap.TryGetValue(sprite, out (Asset asset, float ppui) data)) {
            return null;
        }

        return GetSliced(
            data.asset,
            data.ppui,
            border
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
        [UISprite.OV5LogoOutline128] = Asset.OV5LogoOutline128,
    };

    private readonly Dictionary<UISliceSprite, (Asset asset, float ppui)> sliceMap = new() {
        [UISliceSprite.CircleOutline128O16P512] = (Asset.CircleOutline128O16, 512f),
    };

    private readonly Dictionary<UISliceSprite, (O5Asset asset, float ppui)> o5SliceMap = new() {
        [UISliceSprite.Circle128P512] = (O5Asset.Circle128, 512f),
        [UISliceSprite.Circle128P1024] = (O5Asset.Circle128, 1024f),
        [UISliceSprite.CircleHalf128P512] = (O5Asset.CircleHalf128, 512f),
        [UISliceSprite.CircleOutline128O32P512] = (O5Asset.CircleOutline128O32, 512f),
        [UISliceSprite.CircleOutline128O32P1024] = (O5Asset.CircleOutline128O32, 1024f),
    };
}
