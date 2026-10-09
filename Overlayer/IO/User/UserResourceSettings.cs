using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.IO.User.Impl;
using Overlayer.Package;

namespace Overlayer.IO.User;

public sealed class UserResourceSettings : ISettingsFile, IDisposable {
    public UserTexture2D T2D { get; } = new();
    public UserSprite Spr { get; } = new();
    public UserFont Fnt { get; } = new();

    /// <summary>UI-only folder per resource key ("icons", "ui/buttons"). Keys stay global, so overlays reference resources unchanged.</summary>
    public Dictionary<string, string> ImageFolders { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> FontFolders { get; } = new(StringComparer.Ordinal);

    public static string FolderOf(Dictionary<string, string> folders, string key) =>
        key != null && folders.TryGetValue(key, out string folder) ? folder : string.Empty;

    /// <summary>"folder/key", or just "key" when unfiled.</summary>
    public static string FullName(Dictionary<string, string> folders, string key) {
        string folder = FolderOf(folders, key);
        return folder.Length == 0 ? key : folder + "/" + key;
    }

    public JToken Serialize() {
        return new JObject {
            [nameof(UserTexture2D)] = T2D.Serialize(),
            [nameof(UserSprite)] = Spr.Serialize(),
            [nameof(UserFont)] = Fnt.Serialize(),
            ["Folders"] = new JObject {
                ["Images"] = SerializeFolders(ImageFolders, Spr.Keys),
                ["Fonts"] = SerializeFolders(FontFolders, Fnt.Keys)
            }
        };
    }

    public void Deserialize(JToken token) {
        if (token is not JObject obj) {
            MainCore.Log.Wrn($"[{nameof(UserResourceSettings)}] Deserialize failed: token is not JObject");
            return;
        }

        T2D.Deserialize(obj[nameof(UserTexture2D)]);
        Spr.Deserialize(obj[nameof(UserSprite)]);
        Fnt.Deserialize(obj[nameof(UserFont)]);
        DeserializeFolders(ImageFolders, obj["Folders"]?["Images"]);
        DeserializeFolders(FontFolders, obj["Folders"]?["Fonts"]);
    }

    private static JObject SerializeFolders(Dictionary<string, string> folders, ICollection<string> keys) {
        var result = new JObject();
        foreach (var (key, folder) in folders) {
            if (O5cpFormat.IsPackageKey(key)) {
                continue;
            }
            if (keys.Contains(key) && !string.IsNullOrEmpty(folder)) {
                result[key] = folder;
            }
        }
        return result;
    }

    private static void DeserializeFolders(Dictionary<string, string> folders, JToken token) {
        folders.Clear();
        if (token is not JObject obj) {
            return;
        }
        foreach (var property in obj.Properties()) {
            if (O5cpFormat.IsPackageKey(property.Name)) {
                continue;
            }
            folders[property.Name] = property.Value.ToString();
        }
    }

    public void Dispose() {
        T2D.Dispose();
        Spr.Dispose();
        Fnt.Dispose();
    }
}