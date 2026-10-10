using Overlayer.Core;
using Overlayer.IO.Fx;
using Overlayer.IO.User.Impl;

namespace Overlayer.IO.User;

public static class UserResourceManager {
    private static SettingsFile<UserResourceSettings> _config;
    public static SettingsFile<UserResourceSettings> Config =>
        _config ??= new(MainCore.Paths?.UserResourcePath ?? Path.Combine(Environment.CurrentDirectory, "UserData", "Overlayer", "UserResources.json"));

    public static UserTexture2D T2D => Config.Data.T2D;
    public static UserSprite Spr => Config.Data.Spr;
    public static UserFont Fnt => Config.Data.Fnt;

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
