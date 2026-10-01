using Overlayer.Core;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using O5Kit.Compat;

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

        if(!O5Texture.LoadImage(texture, data)) {
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
#pragma warning disable IDE0028
                    asset.fallbackFontAssetTable ??= new();
#pragma warning restore IDE0028
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
    };
}