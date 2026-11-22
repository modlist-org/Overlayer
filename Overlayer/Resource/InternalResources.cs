using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Resource;

public static class InternalResources {
    public const string Prefix = "@internal:";

    private static readonly Asset[] FontAssets = [
        Asset.SUIT_Regular,
        Asset.SUIT_Medium,
        Asset.OverlayerSC_Regular,
        Asset.OverlayerSC_Medium,
        Asset.JetBrainsMonoNL_Regular,
        Asset.JetBrainsMonoNL_Medium,
    ];

    private static readonly Asset[] ExtraImageAssets = [
        Asset.CircleOutline256O32,
        Asset.OllyMouse,
    ];

    public static readonly string[] ImageKeys = BuildImageKeys();
    public static readonly string[] FontKeys = BuildFontKeys();

    private static string[] BuildImageKeys() {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in Enum.GetNames(typeof(UISprite))) {
            names.Add(name);
        }
        foreach (Asset asset in ExtraImageAssets) {
            names.Add(asset.ToString());
        }
        foreach (string name in Enum.GetNames(typeof(UISliceSprite))) {
            names.Add(name);
        }
        return [.. names
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => Prefix + n)];
    }

    private static string[] BuildFontKeys()
        => [.. FontAssets.Select(a => Prefix + a)];

    public static bool IsInternalKey(string key)
        => !string.IsNullOrEmpty(key) && key.StartsWith(Prefix, StringComparison.Ordinal);

    public static string ShortName(string key)
        => IsInternalKey(key) ? key[Prefix.Length..] : key;

    public static string DisplayName(string key)
        => $"{MainCore.Tr.Get("INTERNAL_RESOURCES", "Internal")}/{ShortName(key)}";

    public static bool TryGetSprite(string key, out Sprite sprite) {
        sprite = null;
        if (!IsInternalKey(key) || MainCore.Spr == null) {
            return false;
        }

        string name = ShortName(key);
        if (Enum.TryParse(name, out UISprite uiSprite)) {
            sprite = MainCore.Spr.Get(uiSprite);
            return sprite != null;
        }
        if (Enum.TryParse(name, out UISliceSprite sliceSprite)) {
            sprite = MainCore.Spr.Get(sliceSprite);
            return sprite != null;
        }
        if (Enum.TryParse(name, out Asset asset) && !IsFontAsset(asset)) {
            sprite = MainCore.Spr.Get(asset);
            return sprite != null;
        }
        return false;
    }

    public static bool TryGetFont(string key, out TMP_FontAsset font) {
        font = null;
        if (!IsInternalKey(key) || MainCore.Res == null) {
            return false;
        }

        string name = ShortName(key);
        if (Enum.TryParse(name, out Asset asset) && IsFontAsset(asset)) {
            font = MainCore.Res.Get<TMP_FontAsset>(asset);
            return font != null;
        }
        return false;
    }

    public static bool TryGetSpriteKey(Sprite sprite, out string key) {
        if (sprite != null && MainCore.Spr != null) {
            foreach (string candidate in ImageKeys) {
                if (TryGetSprite(candidate, out Sprite internalSprite) && internalSprite == sprite) {
                    key = candidate;
                    return true;
                }
            }
        }
        key = null;
        return false;
    }

    public static bool TryGetFontKey(TMP_FontAsset font, out string key) {
        if (font != null && MainCore.Res != null) {
            foreach (string candidate in FontKeys) {
                if (TryGetFont(candidate, out TMP_FontAsset internalFont) && internalFont == font) {
                    key = candidate;
                    return true;
                }
            }
        }
        key = null;
        return false;
    }

    private static bool IsFontAsset(Asset asset) {
        for (int i = 0; i < FontAssets.Length; i++) {
            if (FontAssets[i] == asset) {
                return true;
            }
        }
        return false;
    }
}
