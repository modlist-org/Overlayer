using Overlayer.Core;
using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Resource;

public enum Asset {
    SUIT_Regular,
    SUIT_Medium,

    OV5LogoOutline256,
    Circle256,
    CircleHalf256,
    X128,
    Monitor128,
    Gear128,
    Image128,
    Text128,
    Book128,
    Star128,
    ToggleCircle128,
    CircleOutline256,
    Triangle128,
    Power128,
    MagnifyingGlass128,
}

public sealed class ResourceManager(Assembly assembly, string resourcePath) : IDisposable {
    private readonly Dictionary<string, object> cache = [];

    public byte[] Load(string path) {
        if(string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        try {
            using Stream stream = assembly.GetManifestResourceStream(resourcePath + path);

            if(stream == null) {
                return null;
            }

            if(stream.Length <= 0) {
                return [];
            }

            byte[] data = new byte[stream.Length];

            int offset = 0;

            while(offset < data.Length) {
                int read = stream.Read(data, offset, data.Length - offset);

                if(read <= 0) {
                    break;
                }

                offset += read;
            }

            return offset == data.Length ? data : null;
        } catch {
            return null;
        }
    }

    public Texture2D LoadTexture(string path, FilterMode filter = FilterMode.Bilinear) {
        if(cache.TryGetValue(path, out object cached)) {
            return cached as Texture2D;
        }

        byte[] data = Load(path);

        if(data == null || data.Length == 0) {
            return null;
        }

        Texture2D texture = new(
            2,
            2,
            TextureFormat.RGBA32,
            false,
            true
        );

        if(!texture.LoadImage(data)) {
            Object.Destroy(texture);
            return null;
        }

        texture.filterMode = filter;
        cache[path] = texture;
        return texture;
    }

    public TMP_FontAsset LoadFont(string path, string tempPath) {
        if(cache.TryGetValue(path, out object cached)) {
            return cached as TMP_FontAsset;
        }

        byte[] data = Load(path);

        if(data == null) {
            return null;
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(tempPath)
        );

        File.WriteAllBytes(
            tempPath,
            data
        );

        Font font = new(tempPath);

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);

        cache[path] = asset;

        return asset;
    }

    public T Get<T>(Asset asset) where T : class {
        if(!assetMap.TryGetValue(asset, out string path)) {
            return null;
        }

        object result = null;

        if(typeof(T) == typeof(Texture2D)) {
            result = LoadTexture(path);
        } else if(typeof(T) == typeof(TMP_FontAsset)) {
            result = LoadFont(path, Path.Combine(MainCore.Paths.TempPath, asset.ToString() + ".otf"));
        }

        return result as T;
    }

    public void Dispose() {
        foreach(object item in cache.Values) {
            switch(item) {
                case Texture2D texture:
                    Object.Destroy(texture);
                    break;

                case TMP_FontAsset font:
                    Object.Destroy(font);
                    break;
            }
        }

        cache.Clear();
    }

    private readonly Dictionary<Asset, string> assetMap = new() {
        [Asset.SUIT_Regular] = "Font.SUIT-Regular.otf",
        [Asset.SUIT_Medium] = "Font.SUIT-Medium.otf",
        [Asset.OV5LogoOutline256] = "Image.OV5LogoOutline256.png",
        [Asset.Circle256] = "Image.Circle256.png",
        [Asset.CircleHalf256] = "Image.CircleHalf256.png",
        [Asset.X128] = "Image.X128.png",
        [Asset.Monitor128] = "Image.Monitor128.png",
        [Asset.Gear128] = "Image.Gear128.png",
        [Asset.Image128] = "Image.Image128.png",
        [Asset.Text128] = "Image.Text128.png",
        [Asset.Book128] = "Image.Book128.png",
        [Asset.Star128] = "Image.Star128.png",
        [Asset.ToggleCircle128] = "Image.ToggleCircle128.png",
        [Asset.CircleOutline256] = "Image.CircleOutline256.png",
        [Asset.Triangle128] = "Image.Triangle128.png",
        [Asset.Power128] = "Image.Power128.png",
        [Asset.MagnifyingGlass128] = "Image.MagnifyingGlass128.png"
    };
}