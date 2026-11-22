using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO.Fx;
using Overlayer.IO.User.Impl;
using Overlayer.Overlay;
using Overlayer.Resource;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.IO.User;

public static class UserResourceManager {
    private static SettingsFile<UserResourceSettings> _config;
    public static SettingsFile<UserResourceSettings> Config =>
        _config ??= new(MainCore.Paths?.UserResourcePath ?? Path.Combine(Environment.CurrentDirectory, "UserData", "Overlayer", "UserResources.json"));

    public static UserTexture2D T2D => Config.Data.T2D;
    public static UserSprite Spr => Config.Data.Spr;
    public static UserFont Fnt => Config.Data.Fnt;

    public static event Action Changed;
    public static void NotifyChanged() => Changed?.Invoke();

    public static bool TryGetSprite(string key, out Sprite sprite) {
        if (InternalResources.TryGetSprite(key, out sprite)) {
            return true;
        }
        if (Spr.TryGet(key, out var entry) && entry.sprite != null) {
            sprite = entry.sprite;
            return true;
        }
        sprite = null;
        return false;
    }

    public static bool TryGetFont(string key, out TMP_FontAsset font) {
        if (InternalResources.TryGetFont(key, out font)) {
            return true;
        }
        if (Fnt.TryGet(key, out font) && font != null) {
            return true;
        }
        font = null;
        return false;
    }

    private static bool IsUnderUserPath(string path, string baseDir) {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(baseDir)) {
            return false;
        }
        try {
            string fullPath = Path.GetFullPath(FromUser(path));
            string userDir = Path.GetFullPath(baseDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
    }

    public static bool IsUnderUserImagePath(string path) => IsUnderUserPath(path, MainCore.Paths?.UserImagePath);

    public static bool IsUnderUserFontPath(string path) => IsUnderUserPath(path, MainCore.Paths?.UserFontPath);

    public static bool IsReferencedImage(string key) {
        if (string.IsNullOrEmpty(key) || InternalResources.IsInternalKey(key)) {
            return false;
        }
        if (Config.Data.ReferencedImages.Contains(key)) {
            return true;
        }
        string texKey = Spr.TryGet(key, out var spr) && !string.IsNullOrEmpty(spr.textureKey)
            ? spr.textureKey
            : Spr.TryGetMissing(key, out string missingTexKey, out _) && !string.IsNullOrEmpty(missingTexKey)
                ? missingTexKey
                : key;
        if (T2D.TryGetAnyPath(texKey, out string path) && !string.IsNullOrEmpty(path)) {
            return !IsUnderUserImagePath(path);
        }
        return false;
    }

    public static bool IsReferencedFont(string key) {
        if (string.IsNullOrEmpty(key) || InternalResources.IsInternalKey(key)) {
            return false;
        }
        if (Config.Data.ReferencedFonts.Contains(key)) {
            return true;
        }
        if (Fnt.TryGetAnyPath(key, out string path) && !string.IsNullOrEmpty(path)) {
            return !IsUnderUserFontPath(path);
        }
        return false;
    }

    public static bool IsMissingImage(string key) {
        if (string.IsNullOrEmpty(key) || InternalResources.IsInternalKey(key)) {
            return false;
        }
        return Spr.IsMissing(key) || (!Spr.HasKey(key) && T2D.IsMissing(key));
    }

    public static bool IsMissingFont(string key) =>
        !string.IsNullOrEmpty(key) && !InternalResources.IsInternalKey(key) && Fnt.IsMissing(key);

    public static bool TryReloadImage(string key, out UserTexture2D.Result result) {
        result = UserTexture2D.Result.NotFound;
        if (string.IsNullOrEmpty(key)) {
            return false;
        }

        string texKey = Spr.TryGetMissing(key, out string missingTexKey, out _) && !string.IsNullOrEmpty(missingTexKey)
            ? missingTexKey
            : Spr.TryGet(key, out var loadedSpr) && !string.IsNullOrEmpty(loadedSpr.textureKey)
                ? loadedSpr.textureKey
                : key;

        Texture2D texture;
        if (T2D.IsMissing(texKey)) {
            result = T2D.TryReload(texKey, out texture);
            if (result != UserTexture2D.Result.Success) {
                return false;
            }
        } else if (T2D.TryGet(texKey, out var existing)) {
            texture = existing.texture;
            result = UserTexture2D.Result.Success;
        } else {
            return false;
        }

        Spr.TryReloadForTexture(texKey, texture);
        if (!Spr.TryGet(key, out _)) {
            result = UserTexture2D.Result.Failed;
            return false;
        }

        Config.RequestSave(50);
        ReapplyLiveOverlays();
        NotifyChanged();
        return true;
    }

    public static bool TryReloadFont(string key, out UserFont.Result result) {
        if (string.IsNullOrEmpty(key)) {
            result = UserFont.Result.NotFound;
            return false;
        }

        result = Fnt.TryReload(key);
        if (result != UserFont.Result.Success) {
            return false;
        }

        O5KitAdapters.RefreshFonts();
        Config.RequestSave(50);
        ReapplyLiveOverlays();
        NotifyChanged();
        return true;
    }

    public static void ReapplyLiveOverlays() {
        static void ApplyTree(OvObject obj) {
            if (obj == null || obj.GameObject == null) {
                return;
            }
            obj.ApplyConfig();
            foreach (OvObject child in obj.Children) {
                ApplyTree(child);
            }
        }

        foreach (OvCanvas canvas in OverlayCore.Canvases) {
            if (canvas == null) {
                continue;
            }
            foreach (OvObject obj in canvas.OvObjects) {
                ApplyTree(obj);
            }
        }
        foreach (OvObject obj in OverlayCore.Objects) {
            ApplyTree(obj);
        }
        OverlayCore.RequestLayoutRefresh();
    }

    public static void Initialize() {
        try {
            FxConverters.RegisterDefaultConverters();
            Config.Load();
            MainCore.Log.Msg($"[{nameof(UserResourceManager)}] Initialized");
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(UserResourceManager)}] Initialize failed: {e}");
        }
    }

    private const string ModPathToken = "{ModPath}";

    public static string ToUser(string path) {
        if (string.IsNullOrEmpty(path)) {
            return path;
        }

        string root = MainCore.Paths.RootPath;
        if (!string.IsNullOrEmpty(root) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
            return ModPathToken + path.Substring(root.Length);
        }

        return path;
    }

    public static string FromUser(string path) {
        if (string.IsNullOrEmpty(path)) {
            return path;
        }

        if (path.StartsWith(ModPathToken, StringComparison.OrdinalIgnoreCase)) {
            var relative = path[ModPathToken.Length..].TrimStart('/', '\\');

            return Path.Combine(MainCore.Paths.RootPath, relative);
        }

        return path;
    }

    public static void Dispose() {
        try {
            Config.Save();
            Config.Dispose();
            MainCore.Log.Msg($"[{nameof(UserResourceManager)}] Disposed");
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(UserResourceManager)}] Dispose failed: {e}");
        }
    }
}
