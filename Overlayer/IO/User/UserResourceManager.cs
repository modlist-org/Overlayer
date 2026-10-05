using Overlayer.Core;
using Overlayer.IO.Fx;
using Overlayer.IO.User.Impl;

namespace Overlayer.IO.User;

public static class UserResourceManager {
    public static SettingsFile<UserResourceSettings> Config { get; } = new(MainCore.Paths.UserResourcePath);
    public static UserTexture2D T2D => Config.Data.T2D;
    public static UserSprite Spr => Config.Data.Spr;
    public static UserFont Fnt => Config.Data.Fnt;

    public static void Initialize() {
        try {
            FxConverters.RegisterDefaultConverters();
            Config.Load();
            MainCore.Log.Msg($"[{nameof(UserResourceManager)}] Initialized");
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(UserResourceManager)}] Initialize failed: {e}");
        }
    }

    private const string ModPathToken = "{ModPath}";

    public static string ToUser(string path) {
        if(string.IsNullOrEmpty(path)) {
            return path;
        }

        return ReplaceOrdinalIgnoreCase(path, MainCore.Paths.RootPath, ModPathToken);
    }

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

    private static string ReplaceOrdinalIgnoreCase(string input, string oldValue, string newValue) {
        if(string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue)) {
            return input;
        }
        var sb = new System.Text.StringBuilder(input.Length);
        int start = 0;
        int index;
        while((index = input.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase)) >= 0) {
            sb.Append(input, start, index - start);
            sb.Append(newValue);
            start = index + oldValue.Length;
        }
        sb.Append(input, start, input.Length - start);
        return sb.ToString();
    }

    public static void Dispose() {
        try {
            Config.Save();
            Config.Dispose();
            MainCore.Log.Msg($"[{nameof(UserResourceManager)}] Disposed");
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(UserResourceManager)}] Dispose failed: {e}");
        }
    }
}
