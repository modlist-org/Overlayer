using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Resource;

public enum Asset {
    SUITRegular,

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
}

internal static class ResourceManager {
    private static bool initialized;

    private static readonly Dictionary<Asset, object> cache = [];

    public const string ResoucePath = "Overlayer.Resource.Embedded.";

    public static void Initialize() {
        if(initialized) {
            return;
        }

        initialized = true;

        string tempDir = Path.Combine(Core.OverlayerPath, "Temp");
        Directory.CreateDirectory(tempDir);

        string fontPath = Path.Combine(tempDir, "SUIT-Regular.otf");

        if(!File.Exists(fontPath)) {
            File.WriteAllBytes(
                fontPath,
                ResourceLoader.Load($"{ResoucePath}Font.SUIT-Regular.otf")
            );
        }

        Font font = new(fontPath);
        cache[Asset.SUITRegular] = TMP_FontAsset.CreateFontAsset(font);

        var imageMap = new (Asset key, string path, FilterMode filter)[] {
            (Asset.OV5LogoOutline256, $"{ResoucePath}Image.OV5LogoOutline256.png", FilterMode.Bilinear),
            (Asset.Circle256, $"{ResoucePath}Image.Circle256.png", FilterMode.Bilinear),
            (Asset.CircleHalf256, $"{ResoucePath}Image.CircleHarf256.png", FilterMode.Bilinear),
            (Asset.X128, $"{ResoucePath}Image.X128.png", FilterMode.Bilinear),
            (Asset.Monitor128, $"{ResoucePath}Image.Monitor128.png", FilterMode.Bilinear),
            (Asset.Gear128, $"{ResoucePath}Image.Gear128.png", FilterMode.Bilinear),
            (Asset.Image128, $"{ResoucePath}Image.Image128.png", FilterMode.Bilinear),
            (Asset.Text128, $"{ResoucePath}Image.Text128.png", FilterMode.Bilinear),
            (Asset.Book128, $"{ResoucePath}Image.Book128.png", FilterMode.Bilinear),
            (Asset.Star128, $"{ResoucePath}Image.Star128.png", FilterMode.Bilinear),
            (Asset.ToggleCircle128, $"{ResoucePath}Image.ToggleCircle128.png", FilterMode.Bilinear),
            (Asset.CircleOutline256, $"{ResoucePath}Image.CircleOutline256.png", FilterMode.Bilinear),
        };

        foreach(var (key, path, filter) in imageMap) {
            Texture2D tex = ByteToTexture2D(ResourceLoader.Load(path));
            tex.filterMode = filter;

            cache[key] = tex;
        }
    }

    public static T Get<T>(Asset key) => (T)cache[key];

    private static Texture2D ByteToTexture2D(byte[] data) {
        Texture2D texture = new(2, 2);
        texture.LoadImage(data);
        return texture;
    }

    public static void Dispose() {
        foreach(var item in cache.Values) {
            if(item is Texture2D tex) {
                Object.Destroy(tex);
            }
        }
        cache.Clear();
    }
}