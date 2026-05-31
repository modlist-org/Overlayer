using Overlayer.Core;
using Overlayer.IO.User.Impl;

namespace Overlayer.IO.User;

public static class UserResourceManager {
    public static UserTexture2D T2D { get; } = new();
    public static UserSprite Spr { get; } = new();
    public static UserFont Fnt { get; } = new();

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
        Fnt.Dispose();
        Spr.Dispose();
        T2D.Dispose();
    }
}