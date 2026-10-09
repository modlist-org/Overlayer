using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.IO.User.Impl;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.Package;

public enum O5cpConflictChoice {
    Reuse,
    Import,
    Overwrite,
    Rename,
    Skip,
}

public sealed class O5cpPromoteItem {
    public string Kind;
    public string Key;
    public string Sha256 = string.Empty;
    public bool IsConflict;
    public O5cpConflictChoice Choice;
    public string RenameTo = string.Empty;
}

public sealed class O5cpPromotePlan {
    public List<O5cpPromoteItem> Items = [];
    public List<string> Warnings = [];
}

public static class O5cpPromote {
    public static O5cpPromotePlan BuildPlan(PackageEntry entry) {
        var plan = new O5cpPromotePlan();
        if (entry?.Manifest == null) {
            return plan;
        }
        foreach (var font in entry.Manifest.Fonts) {
            var item = new O5cpPromoteItem { Kind = "font", Key = font.Key, Sha256 = font.Sha256 ?? string.Empty };
            if (!UserResourceManager.Fnt.TryGetPath(font.Key, out string globalPath)
                || string.IsNullOrEmpty(globalPath)
                || !File.Exists(UserResourceManager.FromUser(globalPath))) {
                item.Choice = O5cpConflictChoice.Import;
            } else if (HashFileQuiet(UserResourceManager.FromUser(globalPath)) == item.Sha256
                && !string.IsNullOrEmpty(item.Sha256)) {
                item.Choice = O5cpConflictChoice.Reuse;
            } else {
                item.IsConflict = true;
                item.Choice = O5cpConflictChoice.Rename;
                item.RenameTo = SuggestRename(font.Key);
            }
            plan.Items.Add(item);
        }
        foreach (var texture in entry.Manifest.Textures) {
            var item = new O5cpPromoteItem { Kind = "texture", Key = texture.Key, Sha256 = texture.Sha256 ?? string.Empty };
            if (!UserResourceManager.T2D.TryGetPath(texture.Key, out string globalPath)
                || string.IsNullOrEmpty(globalPath)
                || !File.Exists(UserResourceManager.FromUser(globalPath))) {
                item.Choice = O5cpConflictChoice.Import;
            } else if (HashFileQuiet(UserResourceManager.FromUser(globalPath)) == item.Sha256
                && !string.IsNullOrEmpty(item.Sha256)) {
                item.Choice = O5cpConflictChoice.Reuse;
            } else {
                item.IsConflict = true;
                item.Choice = O5cpConflictChoice.Rename;
                item.RenameTo = SuggestRename(texture.Key);
            }
            plan.Items.Add(item);
        }
        foreach (var sprite in entry.Manifest.Sprites) {
            var item = new O5cpPromoteItem { Kind = "sprite", Key = sprite.Key };
            if (!UserResourceManager.Spr.TryGet(sprite.Key, out var global)) {
                item.Choice = O5cpConflictChoice.Import;
            } else if (SpriteMatches(global, sprite, plan)) {
                item.Choice = O5cpConflictChoice.Reuse;
            } else {
                item.IsConflict = true;
                item.Choice = O5cpConflictChoice.Rename;
                item.RenameTo = SuggestRename(sprite.Key);
            }
            plan.Items.Add(item);
        }
        return plan;
    }

    public static OvCanvas Apply(PackageEntry entry, O5cpPromotePlan plan, out List<string> warnings) {
        warnings = [];
        if (entry?.Manifest == null || plan == null) {
            return null;
        }
        try {
            string prefix = PackageStore.KeyPrefix(entry.Id);

            var finalKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in plan.Items.Where(i => i.Kind is "font" or "texture")) {
                finalKeys[item.Key] = ResolveResource(entry, item, warnings);
            }
            foreach (var item in plan.Items.Where(i => i.Kind == "sprite")) {
                ResolveSprite(entry, item, finalKeys, warnings);
            }
            byte[] canvasBytes = PackageStore.GetPackageBytes(entry, entry.Manifest.CanvasFile);
            if (canvasBytes == null) {
                warnings.Add($"canvas file missing in package: {entry.Manifest.CanvasFile}");
                return null;
            }
            JToken canvasJson = JToken.Parse(System.Text.Encoding.UTF8.GetString(canvasBytes));
            string Unprefix(string key) {
                if (key.StartsWith(prefix, StringComparison.Ordinal)) {
                    string orig = key[prefix.Length..];
                    return finalKeys.TryGetValue(orig, out string mapped) ? mapped : orig;
                }
                return key;
            }
            O5cpKeyMapper.RewriteStaticKeys(canvasJson, Unprefix);
            O5cpKeyMapper.RewriteKeyLiterals(canvasJson, literal => {
                if (finalKeys.TryGetValue(literal, out string direct)) {
                    return direct;
                }
                if (literal.StartsWith(prefix, StringComparison.Ordinal)) {
                    string orig = literal[prefix.Length..];
                    return finalKeys.TryGetValue(orig, out string mapped) ? mapped : orig;
                }
                return null;
            });

            var scan = O5cpKeyScanner.Scan(canvasJson);
            foreach (string key in scan.FontKeys.Concat(scan.SpriteKeys)) {
                if (!ResourceExists(key)) {
                    warnings.Add($"missing resource, left empty: {key}");
                }
            }
            var canvas = new OvCanvas();
            canvas.Deserialize(canvasJson);
            if (!canvas.Config.Name.Value.EndsWith(" Copy")) {
                canvas.Config.Name.Value = $"{canvas.Config.Name.Value} Copy";
            }
            UserResourceManager.Config.Save();
            return canvas;
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(O5cpPromote)}] Promote failed: {e.Message}");
            warnings.Add($"promote failed: {e.Message}");
            return null;
        }
    }

    private static string ResolveResource(
        PackageEntry entry,
        O5cpPromoteItem item,
        List<string> warnings
    ) {
        return item.Kind switch {
            "font" => ResolveFont(entry, item, warnings),
            "texture" => ResolveTexture(entry, item, warnings),
            _ => item.Key,
        };
    }

    private static string ResolveFont(PackageEntry entry, O5cpPromoteItem item, List<string> warnings) {
        var manifestFont = entry.Manifest.Fonts.FirstOrDefault(f => f.Key == item.Key);
        if (manifestFont == null || item.Choice is O5cpConflictChoice.Reuse or O5cpConflictChoice.Skip) {
            if (item.Choice == O5cpConflictChoice.Skip) {
                warnings.Add($"skipped font: {item.Key}");
            }
            return item.Key;
        }
        string source = PackageStore.GetPackageFile(entry, manifestFont.File);
        if (source == null || !File.Exists(source)) {
            warnings.Add($"font file missing in package: {item.Key}");
            return item.Key;
        }
        string targetKey = item.Choice == O5cpConflictChoice.Rename && !string.IsNullOrWhiteSpace(item.RenameTo)
            ? item.RenameTo.Trim()
            : item.Key;
        if (item.Choice == O5cpConflictChoice.Overwrite) {
            UserResourceManager.Fnt.Remove(item.Key);
        } else {
            UserResourceManager.Fnt.Remove(targetKey);
        }
        var result = UserResourceManager.Fnt.Load(targetKey, source);
        if (result != UserFont.Result.Success) {
            warnings.Add($"font load failed ({result}): {targetKey}");
            return item.Key;
        }
        return targetKey;
    }

    private static string ResolveTexture(PackageEntry entry, O5cpPromoteItem item, List<string> warnings) {
        var manifestTexture = entry.Manifest.Textures.FirstOrDefault(t => t.Key == item.Key);
        if (manifestTexture == null || item.Choice is O5cpConflictChoice.Reuse or O5cpConflictChoice.Skip) {
            if (item.Choice == O5cpConflictChoice.Skip) {
                warnings.Add($"skipped texture: {item.Key}");
            }
            return item.Key;
        }
        string source = PackageStore.GetPackageFile(entry, manifestTexture.File);
        if (source == null || !File.Exists(source)) {
            warnings.Add($"texture file missing in package: {item.Key}");
            return item.Key;
        }
        byte[] data = File.ReadAllBytes(source);
        string targetKey = item.Choice == O5cpConflictChoice.Rename && !string.IsNullOrWhiteSpace(item.RenameTo)
            ? item.RenameTo.Trim()
            : item.Key;
        if (item.Choice == O5cpConflictChoice.Overwrite) {
            UserResourceManager.T2D.Remove(item.Key);
            foreach (string spriteKey in UserResourceManager.Spr.Keys
                .Where(k => UserResourceManager.Spr.TryGet(k, out var s) && s.textureKey == item.Key)
                .ToArray()) {
                UserResourceManager.Spr.Remove(spriteKey);
            }
        } else {
            UserResourceManager.T2D.Remove(targetKey);
        }
        var result = UserResourceManager.T2D.LoadData(
            targetKey, source, data, manifestTexture.MipChain, manifestTexture.Linear);
        if (result != UserTexture2D.Result.Success) {
            warnings.Add($"texture load failed ({result}): {targetKey}");
            return item.Key;
        }
        if (!string.IsNullOrEmpty(manifestTexture.Folder)) {
            UserResourceManager.Config.Data.ImageFolders[targetKey] = manifestTexture.Folder;
        }
        return targetKey;
    }

    private static void ResolveSprite(
        PackageEntry entry,
        O5cpPromoteItem item,
        Dictionary<string, string> finalKeys,
        List<string> warnings
    ) {
        var manifestSprite = entry.Manifest.Sprites.FirstOrDefault(s => s.Key == item.Key);
        if (manifestSprite == null || item.Choice is O5cpConflictChoice.Reuse or O5cpConflictChoice.Skip) {
            if (item.Choice == O5cpConflictChoice.Skip) {
                warnings.Add($"skipped sprite: {item.Key}");
            }
            return;
        }
        string textureKey = finalKeys.TryGetValue(manifestSprite.TextureKey, out string mapped)
            ? mapped
            : manifestSprite.TextureKey;
        if (!UserResourceManager.T2D.TryGet(textureKey, out _)) {
            warnings.Add($"sprite skipped, texture missing: {item.Key}");
            return;
        }
        string targetKey = item.Choice == O5cpConflictChoice.Rename && !string.IsNullOrWhiteSpace(item.RenameTo)
            ? item.RenameTo.Trim()
            : item.Key;
        if (item.Choice == O5cpConflictChoice.Overwrite) {
            UserResourceManager.Spr.Remove(item.Key);
        } else {
            UserResourceManager.Spr.Remove(targetKey);
        }
        var result = UserResourceManager.Spr.Load(
            targetKey,
            textureKey,
            new Rect(manifestSprite.Rect[0], manifestSprite.Rect[1], manifestSprite.Rect[2], manifestSprite.Rect[3]),
            new Vector2(manifestSprite.Pivot[0], manifestSprite.Pivot[1]),
            manifestSprite.PixelsPerUnit,
            new Vector4(manifestSprite.Border[0], manifestSprite.Border[1], manifestSprite.Border[2], manifestSprite.Border[3]),
            out _);
        if (result != UserSprite.Result.Success) {
            warnings.Add($"sprite load failed ({result}): {targetKey}");
            return;
        }
        if (!string.IsNullOrEmpty(manifestSprite.Folder)) {
            UserResourceManager.Config.Data.ImageFolders[targetKey] = manifestSprite.Folder;
        }
        finalKeys[item.Key] = targetKey;
    }

    private static bool SpriteMatches(
        (Sprite sprite, string textureKey, IO.Unity.SpriteSettings settings) global,
        O5cpSpriteEntry manifest,
        O5cpPromotePlan plan
    ) {

        var textureItem = plan.Items.FirstOrDefault(i => i.Kind == "texture" && i.Key == manifest.TextureKey);
        string expectedTexture = textureItem?.Choice switch {
            O5cpConflictChoice.Rename when !string.IsNullOrWhiteSpace(textureItem.RenameTo) => textureItem.RenameTo.Trim(),
            O5cpConflictChoice.Skip => null,
            _ => manifest.TextureKey,
        };
        if (expectedTexture == null || global.textureKey != expectedTexture) {
            return false;
        }
        try {
            return Math.Abs(global.settings.Rect.Value.x - manifest.Rect[0]) < 0.01f
                && Math.Abs(global.settings.Rect.Value.y - manifest.Rect[1]) < 0.01f
                && Math.Abs(global.settings.Rect.Value.width - manifest.Rect[2]) < 0.01f
                && Math.Abs(global.settings.Rect.Value.height - manifest.Rect[3]) < 0.01f
                && Math.Abs(global.settings.Pivot.Value.x - manifest.Pivot[0]) < 0.001f
                && Math.Abs(global.settings.Pivot.Value.y - manifest.Pivot[1]) < 0.001f
                && Math.Abs(global.settings.PixelsPerUnit.Value - manifest.PixelsPerUnit) < 0.01f;
        } catch {
            return false;
        }
    }

    private static bool ResourceExists(string key) {
        return UserResourceManager.Fnt.TryGet(key, out _)
            || UserResourceManager.Spr.TryGet(key, out _)
            || UserResourceManager.T2D.TryGet(key, out _);
    }

    private static string SuggestRename(string key) {
        int slash = key.LastIndexOf('/');
        string head = slash >= 0 ? key[..(slash + 1)] : string.Empty;
        string tail = slash >= 0 ? key[(slash + 1)..] : key;
        return $"{head}{tail} (imported)";
    }

    private static string HashFileQuiet(string path) {
        try {
            using var stream = File.OpenRead(path);
            return O5cpPackage.ComputeSha256(stream);
        } catch {
            return string.Empty;
        }
    }
}
