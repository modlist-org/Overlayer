using Overlayer.Core;
using Overlayer.IO.User.Impl;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
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
    OverlayerSC_Regular,
    OverlayerSC_Medium,
    JetBrainsMonoNL_Regular,
    JetBrainsMonoNL_Medium,

    OV5LogoOutline128,
    CircleOutline128O16,
    OllyMouse,
}

public sealed class ResourceManager(Assembly assembly, string resourcePath) : IDisposable {
    private readonly Dictionary<string, object> cache = [];

    public byte[] Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        try {
            using Stream stream = assembly.GetManifestResourceStream(resourcePath + path);

            if (stream == null) {
                return null;
            }

            if (stream.Length <= 0) {
                return [];
            }

            byte[] data = new byte[stream.Length];
            int offset = 0;

            while (offset < data.Length) {
                int read = stream.Read(data, offset, data.Length - offset);

                if (read <= 0) {
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
        if (cache.TryGetValue(path, out object cached)) {
            return cached as Texture2D;
        }

        byte[] data = Load(path);

        if (data == null || data.Length == 0) {
            return null;
        }

        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false, false);

        if (!O5Texture.LoadImage(texture, data)) {
            Object.Destroy(texture);
            return null;
        }

        texture.filterMode = filter;
        texture.wrapMode = TextureWrapMode.Clamp;
        cache[path] = texture;
        return texture;
    }

    /// <summary>
    /// Picks a game-shipped font asset for stripped runtimes that cannot
    /// create fonts from TTF bytes. Prefers well-known UI typefaces, else the
    /// first asset found. Never cached here: the caller decides, and a miss
    /// retries later (game fonts may still be streaming in).
    /// </summary>
    /// <summary>
    /// Finds a game-shipped sprite asset by name (case-insensitive).
    /// Used for the per-text SpriteKey setting; null when absent.
    /// </summary>
    internal static TMP_SpriteAsset FindSpriteAsset(string name) {
        if (string.IsNullOrEmpty(name)) {
            return null;
        }
        try {
            TMP_SpriteAsset[] all = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>();
            if (all == null) {
                return null;
            }
            foreach (var asset in all) {
                if (asset == null) {
                    continue;
                }
                string assetName;
                try {
                    assetName = asset.name;
                } catch {
                    continue;
                }
                if (string.Equals(assetName, name, StringComparison.OrdinalIgnoreCase)) {
                    return asset;
                }
            }
        } catch {
        }
        // Runtime asset built from user sprites (named AssetKey at build time).
        if (string.Equals(name, UserSpriteAsset.AssetKey, StringComparison.OrdinalIgnoreCase)) {
            try {
                return UserSpriteAsset.GetOrBuild();
            } catch {
            }
        }
        return null;
    }

    internal static List<string> SpriteAssetNames() {
        var names = new List<string>();
        try {
            TMP_SpriteAsset[] all = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>();
            if (all == null) {
                return names;
            }
            foreach (var asset in all) {
                if (asset == null) {
                    continue;
                }
                string assetName;
                try {
                    assetName = asset.name;
                } catch {
                    continue;
                }
                if (!string.IsNullOrEmpty(assetName) && !names.Contains(assetName)) {
                    names.Add(assetName);
                }
            }
        } catch {
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    internal static TMP_FontAsset StealGameFont() {        try {
            // Project default first: guaranteed fully baked (TMP itself falls back to it).
            try {
                var def = TMP_Settings.defaultFontAsset;
                if (IsUsableFont(def)) {
                    return def;
                }
            } catch {
            }
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts == null || fonts.Length == 0) {
                return null;
            }

            TMP_FontAsset fallback = null;
            foreach (var font in fonts) {
                if (font == null) {
                    continue;
                }
                fallback ??= font;
                string name;
                try {
                    name = font.name?.ToLowerInvariant();
                } catch {
                    continue;
                }
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                if (name.Contains("liberationsans") || name.Contains("arial")
                    || name.Contains("notosans") || name.Contains("default")) {
                    if (IsUsableFont(font)) {
                        return font;
                    }
                    continue;
                }
            }
            return IsUsableFont(fallback) ? fallback : null;
        } catch {
            return null;
        }
    }

    // Touching the lookup table runs the same reader set_font() will run:
    // a broken asset throws here, inside our catch, instead of in the UI.
    private static bool IsUsableFont(TMP_FontAsset font) {
        if (font == null) {
            return false;
        }
        try {
            return font.characterLookupTable != null;
        } catch {
            return false;
        }
    }

    // A 90pt/1024 atlas fills after a few dozen CJK glyphs; 48pt with a 2048 atlas
    // keeps a full UI (and a normal HUD) on one Alpha8 texture. Multi-atlas stays on
    // for unusually long user text.
    internal static TMP_FontAsset CreateRuntimeFontAsset(Font font) =>
        TMP_FontAsset.CreateFontAsset(font, 48, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);

    public TMP_FontAsset LoadFont(string path, string tempPath) {        if (cache.TryGetValue(path, out object cached)) {
            return cached as TMP_FontAsset;
        }

        byte[] data = Load(path);
        if (data == null) {
            return null;
        }

        string directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(tempPath, data);

        TMP_FontAsset asset = O5Kit.Resource.O5Resources.TryCreateFontAssetFromPath(tempPath, 48, 8, 2048, 2048, true);
        if (asset == null) {
            // Stripped games without Font(path)/FreeType/TMP shader fall back to a game font.
            asset = StealGameFont();
            if (asset != null) cache[path] = asset;
            return asset;
        }

        if (path.Contains("JetBrainsMonoNL")) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string style = fileName.Split(['-'])[1];

            if (Enum.TryParse("SUIT_" + style, out Asset suitAsset)) {
                string suitPath = assetMap[suitAsset];
                string suitTempPath = Path.Combine(MainCore.Paths.TempPath, $"SUIT_{style}.otf");

                TMP_FontAsset suitFont = LoadFont(suitPath, suitTempPath);

                if (suitFont != null) {
#pragma warning disable IDE0028
                    asset.fallbackFontAssetTable ??= new();
#pragma warning restore IDE0028
                    if (!asset.fallbackFontAssetTable.Contains(suitFont)) {
                        asset.fallbackFontAssetTable.Add(suitFont);
                    }
                }
            }
        }

        // The bundled Chinese font follows the SUIT weight, so every text that uses
        // SUIT directly (titles, docs, credits) gets Chinese glyphs too.
        // shortcut: OverlayerSC is a Noto Sans SC subset (GB2312 + current UI text);
        // rare characters fall through to the game font. Rebuild the subset if that isn't enough.
        bool suitMedium = path.Contains("SUIT-Medium");
        if (suitMedium || path.Contains("SUIT-Regular")) {
            Asset cjkAsset = suitMedium ? Asset.OverlayerSC_Medium : Asset.OverlayerSC_Regular;
            string cjkPath = assetMap[cjkAsset];
            string cjkTempPath = Path.Combine(MainCore.Paths.TempPath, cjkAsset + ".ttf");

            TMP_FontAsset cjkFont = LoadFont(cjkPath, cjkTempPath);

            if (cjkFont != null) {
#pragma warning disable IDE0028
                asset.fallbackFontAssetTable ??= new();
#pragma warning restore IDE0028
                if (!asset.fallbackFontAssetTable.Contains(cjkFont)) {
                    asset.fallbackFontAssetTable.Add(cjkFont);
                }

                // Last resort for characters outside the bundled subset.
                TMP_FontAsset gameFont = StealGameFont();
                if (gameFont != null && gameFont != asset && gameFont != cjkFont
                    && !asset.fallbackFontAssetTable.Contains(gameFont)) {
                    asset.fallbackFontAssetTable.Add(gameFont);
                }
            }
        }

        cache[path] = asset;
        return asset;
    }

    public T Get<T>(Asset asset) where T : class {
        if (!assetMap.TryGetValue(asset, out string path)) {
            return null;
        }

        return GetInternal<T>(path, asset.ToString());
    }

    public T Get<T>(string path) where T : class {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        return GetInternal<T>(path, fileName);
    }
    public TMP_FontAsset GetFont(string path, string customTempPath) => LoadFont(path, customTempPath);
    private T GetInternal<T>(string path, string assetNameForFont) where T : class {
        object result = null;

        if (typeof(T) == typeof(Texture2D)) {
            result = LoadTexture(path);
        } else if (typeof(T) == typeof(TMP_FontAsset)) {
            string tempPath = Path.Combine(MainCore.Paths.TempPath, assetNameForFont + ".otf");
            result = LoadFont(path, tempPath);
        }

        return result as T;
    }

    public void Dispose() {
        foreach (object item in cache.Values) {
            switch (item) {
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
        [Asset.OverlayerSC_Regular] = "Font.OverlayerSC-Regular.ttf",
        [Asset.OverlayerSC_Medium] = "Font.OverlayerSC-Medium.ttf",
        [Asset.JetBrainsMonoNL_Regular] = "Font.JetBrainsMonoNL-Regular.ttf",
        [Asset.JetBrainsMonoNL_Medium] = "Font.JetBrainsMonoNL-Medium.ttf",
        [Asset.OV5LogoOutline128] = "Image.OV5LogoOutline128.png",
        [Asset.CircleOutline128O16] = "Image.CircleOutline128O16.png",
        [Asset.OllyMouse] = "Image.OllyMouse.png",
    };
}
