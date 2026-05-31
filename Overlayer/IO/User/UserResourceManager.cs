using Overlayer.Core;
using Overlayer.IO.User.Impl;

namespace Overlayer.IO.User;

public static class UserResourceManager {
    public static SettingsFile<UserResourceSettings> Config { get; } = new(MainCore.Paths.UserResourcePath);
    public static UserTexture2D T2D => Config.Data.T2D;
    public static UserSprite Spr => Config.Data.Spr;
    public static UserFont Fnt => Config.Data.Fnt;

    public static void Initialize() {
        try {
            Config.Load();
            MainCore.Logger.Msg($"[{nameof(UserResourceManager)}] Initialized");
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(UserResourceManager)}] Initialize failed: {e}");
        }
    }

    private const string ModPathToken = "{ModPath}";

    /// <summary>
    /// Converts an absolute internal path into a user-readable path using tokens.
    /// Example: C:\Game\Mods\file.png → {ModPath}/file.png
    /// </summary>
    public static string ToUser(string path) {
        if(string.IsNullOrEmpty(path)) {
            return path;
        }

        return path.Replace(MainCore.Paths.RootPath, ModPathToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a user-provided token path into an absolute internal path.
    /// Example: {ModPath}/file.png → C:\Game\Mods\file.png
    /// </summary>
    public static string FromUser(string path) {
        if(string.IsNullOrEmpty(path)) {
            return path;
        }

        if(path.StartsWith(ModPathToken, StringComparison.OrdinalIgnoreCase)) {
            var relative = path.Substring(ModPathToken.Length).TrimStart('/', '\\');

            return Path.Combine(MainCore.Paths.RootPath, relative);
        }

        return path;
    }

    public static void Dispose() {
        try {
            Config.Save();
            Config.Dispose();
            MainCore.Logger.Msg($"[{nameof(UserResourceManager)}] Disposed");
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(UserResourceManager)}] Dispose failed: {e}");
        }
    }
}