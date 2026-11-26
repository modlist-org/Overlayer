using O5Kit.Core;
using O5Kit.Factory;
using O5Kit.Resource;
using Overlayer.Compat;
using Overlayer.Core;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Resource;

/// <summary>
/// Loads and caches the bundled vector icon TMP fonts (<c>O5KitIcons.ttf</c> and <c>OverlayerIcons.ttf</c>).
/// </summary>
public sealed class IconManager : IDisposable {
    private const string OverlayerFontResource = "Overlayer.Resource.IconFont.OverlayerIcons.ttf";

    private readonly Assembly _assembly;
    private readonly O5Resources _o5Resources = O5Resources.Bundled();
    private TMP_FontAsset _o5KitFont;
    private TMP_FontAsset _overlayerFont;
    private bool _o5Tried;
    private bool _overlayerTried;
    private bool _disposed;

    public IconManager(Assembly assembly) {
        _assembly = assembly;
    }

    public TMP_FontAsset O5KitFont {
        get {
            if (_disposed || _o5Tried) {
                return _o5KitFont;
            }
            _o5Tried = true;
            _o5KitFont = _o5Resources.LoadIconFontAsset("O5KitIcons.ttf");
            return _o5KitFont;
        }
    }

    public TMP_FontAsset OverlayerFont {
        get {
            if (_disposed || _overlayerTried) {
                return _overlayerFont;
            }
            _overlayerTried = true;
            _overlayerFont = LoadOverlayerFont();
            return _overlayerFont;
        }
    }

    public TMP_FontAsset GetFont(UIIcon icon)
        => UIIconGlyphs.IsO5Kit(icon) ? O5KitFont : OverlayerFont;

    public O5Icon Get(UIIcon icon)
        => new(GetFont(icon), UIIconGlyphs.GetGlyph(icon));

    public TextMeshProUGUI Create(
        Transform parent,
        string name,
        UIIcon icon,
        Color color,
        float? fixedFontSize = null
    ) {
        var tmp = O5FactoryIcons.CreateIcon(parent, name, GetFont(icon), UIIconGlyphs.GetGlyph(icon), color, fixedFontSize);
        TmpCompat.SetNoWrap(tmp);
        return tmp;
    }

    public void Apply(TextMeshProUGUI tmp, UIIcon icon)
        => O5FactoryIcons.ApplyGlyph(tmp, GetFont(icon), UIIconGlyphs.GetGlyph(icon));

    private TMP_FontAsset LoadOverlayerFont() {
        try {
            using Stream stream = _assembly.GetManifestResourceStream(OverlayerFontResource);
            if (stream == null) {
                return null;
            }
            byte[] data = new byte[stream.Length];
            int read = 0;
            while (read < data.Length) {
                int n = stream.Read(data, read, data.Length - read);
                if (n <= 0) {
                    break;
                }
                read += n;
            }

            string hash = ComputeShortHash(data);
            string dir = Path.Combine(MainCore.Paths.TempPath, "Icons");
            Directory.CreateDirectory(dir);
            string tempPath = Path.Combine(dir, $"OverlayerIcons_{hash}.ttf");
            if (!File.Exists(tempPath)) {
                File.WriteAllBytes(tempPath, data);
            }

            return O5Resources.TryCreateFontAssetFromPath(tempPath, 90, 6, 1024, 1024, true);
        } catch {
            return null;
        }
    }

    private static string ComputeShortHash(byte[] data) {
        using var sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(data);
        return BitConverter.ToString(digest, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        _o5KitFont = null;
        _overlayerFont = null;
        _o5Resources.Dispose();
    }
}
