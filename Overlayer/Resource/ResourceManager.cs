using Overlayer.Core;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using Overlayer.Compat.OVC;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Resource;

public enum Asset {
    SUIT_Regular,
    SUIT_Medium,
    JetBrainsMonoNL_Regular,
    JetBrainsMonoNL_Medium,

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
    CircleOutline256O32,
    CircleOutline256O64,
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

        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false, false);

        if(!OVC_Texture2D.LoadImage(texture, data)) {
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

        string directory = Path.GetDirectoryName(tempPath);
        if(!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(tempPath, data);

        Font font = new(tempPath);
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);

        if(path.Contains("JetBrainsMonoNL")) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string style = fileName.Split('-')[1];

            if(Enum.TryParse("SUIT_" + style, out Asset suitAsset)) {
                string suitPath = assetMap[suitAsset];
                string suitTempPath = Path.Combine(MainCore.Paths.TempPath, $"SUIT_{style}.otf");

                TMP_FontAsset suitFont = LoadFont(suitPath, suitTempPath);

                if(suitFont != null) {
                    asset.fallbackFontAssetTable ??= [];
                    if(!asset.fallbackFontAssetTable.Contains(suitFont)) {
                        asset.fallbackFontAssetTable.Add(suitFont);
                    }
                }
            }
        }

        cache[path] = asset;
        return asset;
    }

    public T Get<T>(Asset asset) where T : class {
        if(!assetMap.TryGetValue(asset, out string path)) {
            return null;
        }

        return GetInternal<T>(path, asset.ToString());
    }

    public T Get<T>(string path) where T : class {
        if(string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        return GetInternal<T>(path, fileName);
    }
    public TMP_FontAsset GetFont(string path, string customTempPath) => LoadFont(path, customTempPath);
    private T GetInternal<T>(string path, string assetNameForFont) where T : class {
        object result = null;

        if(typeof(T) == typeof(Texture2D)) {
            result = LoadTexture(path);
        } else if(typeof(T) == typeof(TMP_FontAsset)) {
            string tempPath = Path.Combine(MainCore.Paths.TempPath, assetNameForFont + ".otf");
            result = LoadFont(path, tempPath);
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
        [Asset.JetBrainsMonoNL_Regular] = "Font.JetBrainsMonoNL-Regular.ttf",
        [Asset.JetBrainsMonoNL_Medium] = "Font.JetBrainsMonoNL-Medium.ttf",
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
        [Asset.CircleOutline256O32] = "Image.CircleOutline256O32.png",
        [Asset.CircleOutline256O64] = "Image.CircleOutline256O64.png",
        [Asset.Triangle128] = "Image.Triangle128.png",
        [Asset.Power128] = "Image.Power128.png",
        [Asset.MagnifyingGlass128] = "Image.MagnifyingGlass128.png"
    };
}