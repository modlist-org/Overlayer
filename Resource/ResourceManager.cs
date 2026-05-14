using TMPro;
using UnityEngine;

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
}

internal static class ResourceManager {
    private static bool initialized;

    private static readonly Dictionary<Asset, object> cache = [];

    public static void Initialize() {
        if(initialized) {
            return;
        }

        initialized = true;

        string tempDir = Path.Combine(Core.FolderPath, "Temp");
        Directory.CreateDirectory(tempDir);

        // FONT
        string fontPath = Path.Combine(tempDir, "SUIT-Regular.otf");

        if(!File.Exists(fontPath)) {
            File.WriteAllBytes(
                fontPath,
                ResourceLoader.Load("Overlayer.Resource.Font.SUIT-Regular.otf")
            );
        }

        Font font = new(fontPath);
        cache[Asset.SUITRegular] = TMP_FontAsset.CreateFontAsset(font);

        var imageMap = new (Asset key, string path, FilterMode filter)[] {
            (Asset.OV5LogoOutline256, "Overlayer.Resource.Image.OV5LogoOutline256.png", FilterMode.Bilinear),
            (Asset.Circle256, "Overlayer.Resource.Image.Circle256.png", FilterMode.Bilinear),
            (Asset.CircleHalf256, "Overlayer.Resource.Image.CircleHarf256.png", FilterMode.Bilinear),
            (Asset.X128, "Overlayer.Resource.Image.X128.png", FilterMode.Bilinear),
            (Asset.Monitor128, "Overlayer.Resource.Image.Monitor128.png", FilterMode.Bilinear),
            (Asset.Gear128, "Overlayer.Resource.Image.Gear128.png", FilterMode.Bilinear),
            (Asset.Image128, "Overlayer.Resource.Image.Image128.png", FilterMode.Bilinear),
            (Asset.Text128, "Overlayer.Resource.Image.Text128.png", FilterMode.Bilinear),
            (Asset.Book128, "Overlayer.Resource.Image.Book128.png", FilterMode.Bilinear),
            (Asset.Star128, "Overlayer.Resource.Image.Star128.png", FilterMode.Bilinear),
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
}