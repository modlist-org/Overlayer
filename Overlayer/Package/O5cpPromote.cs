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
    /// <summary>User script files the package's scripts were copied to (filled by Apply).</summary>
    public List<string> Scripts = [];
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
                item.RenameTo = SuggestRename(font.Key, k => UserResourceManager.Fnt.TryGet(k, out _));
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
                item.RenameTo = SuggestRename(texture.Key, k => UserResourceManager.T2D.TryGet(k, out _));
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
                item.RenameTo = SuggestRename(sprite.Key, k => UserResourceManager.Spr.TryGet(k, out _));
            }
            plan.Items.Add(item);
        }
        return plan;
    }

    public static OvCanvas Apply(PackageEntry entry, O5cpPromotePlan plan, out List<string> warnings) {
        var canvasJson = ApplyJson(entry, plan, out warnings);
        if (canvasJson == null) {
            return null;
        }
        try {
            var canvas = new OvCanvas();
            canvas.Deserialize(canvasJson);
            if (!canvas.Config.Name.Value.EndsWith(" Copy")) {
                canvas.Config.Name.Value = $"{canvas.Config.Name.Value} Copy";
            }
            return canvas;
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(O5cpPromote)}] Promote failed: {e.Message}");
            warnings.Add($"promote failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Makes the package's resources the user's own (copied next to their other fonts / images)
    /// and returns its canvas JSON pointing at them. <paramref name="copyScripts"/>: also its
    /// scripts, for when the package goes away after this (while it's installed its own copies
    /// run, and the same script loaded twice clashes).
    /// </summary>
    public static JToken ApplyJson(PackageEntry entry, O5cpPromotePlan plan, out List<string> warnings, bool copyScripts = false) {
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
            if (copyScripts) {
                CopyScripts(entry, plan, warnings);
            }
            UserResourceManager.Config.Save();
            return canvasJson;
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
        string source = KeepFile(PackageStore.GetPackageFile(entry, manifestFont.File), MainCore.Paths.UserFontPath, manifestFont.File);
        if (source == null || !File.Exists(source)) {
            warnings.Add($"font file missing in package: {item.Key}");
            return item.Key;
        }
        string targetKey = item.Choice == O5cpConflictChoice.Rename && !string.IsNullOrWhiteSpace(item.RenameTo)
            ? item.RenameTo.Trim()
            : item.Key;
        if (item.Choice == O5cpConflictChoice.Overwrite) {
            UserResourceManager.Fnt.Remove(item.Key);
        } else if (item.Choice == O5cpConflictChoice.Rename) {
            targetKey = FreeName(targetKey, k => UserResourceManager.Fnt.TryGet(k, out _), warnings);
        } else {
            // Import: a key left over without its file.
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
        string source = KeepFile(PackageStore.GetPackageFile(entry, manifestTexture.File), MainCore.Paths.UserImagePath, manifestTexture.File);
        if (source == null || !File.Exists(source)) {
            warnings.Add($"texture file missing in package: {item.Key}");
            return item.Key;
        }
        byte[] data = File.ReadAllBytes(source);
        string targetKey = item.Choice == O5cpConflictChoice.Rename && !string.IsNullOrWhiteSpace(item.RenameTo)
            ? item.RenameTo.Trim()
            : item.Key;
        // Sprites cut from the replaced texture would go with it: keep how they were cut
        // and cut them again from the new one.
        var cutFrom = new List<(string Key, IO.Unity.SpriteSettings Settings)>();
        if (item.Choice == O5cpConflictChoice.Overwrite) {
            UserResourceManager.T2D.Remove(item.Key);
            foreach (string spriteKey in UserResourceManager.Spr.Keys
                .Where(k => UserResourceManager.Spr.TryGet(k, out var s) && s.textureKey == item.Key)
                .ToArray()) {
                if (UserResourceManager.Spr.TryGet(spriteKey, out var old)) {
                    cutFrom.Add((spriteKey, old.settings.Copy()));
                }
                UserResourceManager.Spr.Remove(spriteKey);
            }
        } else if (item.Choice == O5cpConflictChoice.Rename) {
            targetKey = FreeName(targetKey, k => UserResourceManager.T2D.TryGet(k, out _), warnings);
        } else {
            // Import: a key left over without its file.
            UserResourceManager.T2D.Remove(targetKey);
        }
        var result = UserResourceManager.T2D.LoadData(
            targetKey, source, data, manifestTexture.MipChain, manifestTexture.Linear);
        if (result != UserTexture2D.Result.Success) {
            warnings.Add($"texture load failed ({result}): {targetKey}");
            return item.Key;
        }
        foreach (var (spriteKey, s) in cutFrom) {
            if (UserResourceManager.Spr.Load(spriteKey, targetKey, s.Rect.Value, s.Pivot.Value, s.PixelsPerUnit.Value, s.Border.Value, out _)
                != UserSprite.Result.Success) {
                warnings.Add($"sprite couldn't be cut again from the new texture: {spriteKey}");
            }
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
        } else if (item.Choice == O5cpConflictChoice.Rename) {
            targetKey = FreeName(targetKey, k => UserResourceManager.Spr.TryGet(k, out _), warnings);
        } else {
            // Import: a key left over without its file.
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

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="folder"/> under the package's file
    /// name; the same file already there is reused, a different one gets a numbered name.
    /// </summary>
    private static string KeepFile(string source, string folder, string packagePath) {
        if (source == null || !File.Exists(source)) {
            return null;
        }
        string ext = Path.GetExtension(packagePath);
        string stem = O5cpPackage.SanitizeSegment(Path.GetFileNameWithoutExtension(packagePath), string.Empty);
        string hash = HashFileQuiet(source);
        Directory.CreateDirectory(folder);
        string dest = Path.Combine(folder, stem + ext);
        for (int n = 2; File.Exists(dest) && HashFileQuiet(dest) != hash; n++) {
            dest = Path.Combine(folder, $"{stem} ({n}){ext}");
        }
        if (!File.Exists(dest)) {
            File.Copy(source, dest);
        }
        return dest;
    }

    /// <summary>Copies the package's scripts into the user's script folder (and loads them).</summary>
    public static List<string> KeepScripts(PackageEntry entry, out List<string> warnings) {
        warnings = [];
        var plan = new O5cpPromotePlan();
        if (entry?.Manifest != null) {
            CopyScripts(entry, plan, warnings);
        }
        return plan.Scripts;
    }

    // The package's scripts become ordinary user scripts, so its tags keep working after the
    // package is removed. Not named pkg_..., which marks package-owned scripts.
    private static void CopyScripts(PackageEntry entry, O5cpPromotePlan plan, List<string> warnings) {
        if (entry.Manifest.Scripts.Count == 0 || MainCore.V8 == null) {
            return;
        }
        string folder = MainCore.V8.ScriptFolderPath;
        if (string.IsNullOrEmpty(folder)) {
            return;
        }
        Directory.CreateDirectory(folder);
        foreach (var script in entry.Manifest.Scripts) {
            byte[] data = PackageStore.GetPackageBytes(entry, script.File);
            if (data == null) {
                warnings.Add($"script file missing in package: {script.File}");
                continue;
            }
            string stem = O5cpPackage.SanitizeSegment(Path.GetFileNameWithoutExtension(
                string.IsNullOrEmpty(script.OriginalName) ? script.File : script.OriginalName), string.Empty);
            if (stem.StartsWith("pkg_", StringComparison.OrdinalIgnoreCase)) {
                stem = "user_" + stem;
            }
            string hash = O5cpPackage.ComputeSha256(data);
            string dest = Path.Combine(folder, stem + ".js");
            for (int n = 2; File.Exists(dest) && HashFileQuiet(dest) != hash; n++) {
                dest = Path.Combine(folder, $"{stem}_{n}.js");
            }
            if (File.Exists(dest)) {
                plan.Scripts.Add(dest);
                continue;
            }
            try {
                File.WriteAllBytes(dest, data);
                MainCore.V8.ReloadScriptFile(dest);
                plan.Scripts.Add(dest);
            } catch (Exception e) {
                warnings.Add($"script copy failed: {Path.GetFileName(dest)} ({e.Message})");
            }
        }
    }

    private static bool ResourceExists(string key) {
        return UserResourceManager.Fnt.TryGet(key, out _)
            || UserResourceManager.Spr.TryGet(key, out _)
            || UserResourceManager.T2D.TryGet(key, out _);
    }

    private static string SuggestRename(string key, Func<string, bool> taken) {
        int slash = key.LastIndexOf('/');
        string head = slash >= 0 ? key[..(slash + 1)] : string.Empty;
        string tail = slash >= 0 ? key[(slash + 1)..] : key;
        string name = $"{head}{tail} (imported)";
        for (int n = 2; taken(name); n++) {
            name = $"{head}{tail} (imported {n})";
        }
        return name;
    }

    // Rename never replaces what already has that name: it moves on to a free one.
    private static string FreeName(string wanted, Func<string, bool> taken, List<string> warnings) {
        if (!taken(wanted)) {
            return wanted;
        }
        string name = wanted;
        for (int n = 2; taken(name); n++) {
            name = $"{wanted} ({n})";
        }
        warnings.Add($"'{wanted}' is already used, named it '{name}'");
        return name;
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
