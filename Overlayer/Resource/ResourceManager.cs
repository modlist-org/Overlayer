using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Resource;

public sealed class ResourceManager(Assembly assembly, string resourcePath) : IDisposable {
    private readonly Dictionary<string, object>
        cache = [];

    public byte[] Load(
        string path
    ) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        try {
            using Stream stream =
                assembly.GetManifestResourceStream(
                    resourcePath + path
                );

            if (stream == null) {
                return null;
            }

            if (stream.Length <= 0) {
                return [];
            }

            byte[] data = new byte[stream.Length];

            int offset = 0;

            while (offset < data.Length) {
                int read = stream.Read(
                    data,
                    offset,
                    data.Length - offset
                );

                if (read <= 0) {
                    break;
                }

                offset += read;
            }

            return offset == data.Length
                ? data
                : null;

        }
        catch {
            return null;
        }
    }

    public Texture2D LoadTexture(
        string path,
        FilterMode filter = FilterMode.Bilinear
    ) {
        if (
            cache.TryGetValue(
                path,
                out object cached
            )
        ) {
            return cached as Texture2D;
        }

        byte[] data = Load(path);

        if (
            data == null ||
            data.Length == 0
        ) {
            return null;
        }

        Texture2D texture = new(
            2,
            2,
            TextureFormat.RGBA32,
            false,
            true
        );

        if (!texture.LoadImage(data)) {
            Object.Destroy(texture);

            return null;
        }

        texture.filterMode = filter;

        cache[path] = texture;

        return texture;
    }

    public TMP_FontAsset LoadFont(
        string path,
        string tempPath
    ) {
        if (
            cache.TryGetValue(
                path,
                out object cached
            )
        ) {
            return cached as TMP_FontAsset;
        }

        byte[] data = Load(path);

        if (data == null) {
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

        TMP_FontAsset asset =
            TMP_FontAsset.CreateFontAsset(
                font
            );

        cache[path] = asset;

        return asset;
    }

    public T Get<T>(
        string path
    )
        where T : class {

        if (
            cache.TryGetValue(
                path,
                out object value
            )
        ) {
            return value as T;
        }

        return null;
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
}