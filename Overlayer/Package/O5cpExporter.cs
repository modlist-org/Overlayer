using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.Tag.Core;

namespace Overlayer.Package;

public sealed class O5cpExportOptions {
    public bool IncludeImages = true;
    public bool IncludeFonts = true;
    public bool IncludeScripts = true;
}

public sealed class O5cpExportResult {
    public string ZipPath;
    public List<string> Warnings = [];
}

public static class O5cpExporter {
    public static O5cpExportResult Export(
        Overlay.OvCanvas canvas,
        string zipPath,
        O5cpExportOptions options
    ) {
        var result = new O5cpExportResult { ZipPath = zipPath };
        options ??= new O5cpExportOptions();
        if (canvas == null || string.IsNullOrWhiteSpace(zipPath)) {
            return null;
        }

        JToken canvasJson = canvas.Serialize();
        string canvasText = canvasJson.ToString();
        var scan = O5cpKeyScanner.Scan(canvasJson);

        var manifest = new O5cpManifest {
            Package = new O5cpPackageInfo {
                Name = canvas.Config?.Name?.Value ?? "Canvas",
                Version = canvas.Props?.Version ?? "1.0.0",
                Author = canvas.Props?.Author ?? string.Empty,
                Description = canvas.Props?.Description ?? string.Empty,
                License = canvas.Props?.License ?? string.Empty,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                AppVersion = Info.Version,
                MinAppVersion = Info.Version,
            },
        };
        manifest.Package.Id = O5cpPackage.MakePackageId(
            manifest.Package.Name,
            O5cpPackage.ComputeSha256(System.Text.Encoding.UTF8.GetBytes(canvasText)));
        CollectModules(canvasJson, manifest, result.Warnings);

        var files = new List<(string path, byte[] data)>();
        var packedTextures = new HashSet<string>(StringComparer.Ordinal);

        foreach (string key in scan.FontKeys.Concat(scan.GuessedFontKeys).Distinct()) {
            if (manifest.Fonts.Any(f => f.Key == key)) {
                continue;
            }
            if (!UserResourceManager.Fnt.TryGetPath(key, out string path)
                || string.IsNullOrEmpty(path)
                || !File.Exists(UserResourceManager.FromUser(path))) {
                if (scan.FontKeys.Contains(key)) {
                    result.Warnings.Add($"missing font: {key}");
                }
                continue;
            }
            string diskPath = UserResourceManager.FromUser(path);
            if (!options.IncludeFonts) {
                manifest.ExcludedFonts.Add(new O5cpExcludedEntry {
                    Key = key,
                    Sha256 = HashFileQuiet(diskPath),
                });
                continue;
            }
            byte[] data = File.ReadAllBytes(diskPath);
            manifest.Fonts.Add(new O5cpFontEntry {
                Key = key,
                File = O5cpFormat.FontsPrefix + O5cpPackage.SanitizeSegment(key, Path.GetExtension(diskPath)),
                Sha256 = O5cpPackage.ComputeSha256(data),
                Bytes = data.Length,
            });
            files.Add((manifest.Fonts[^1].File, data));
        }

        foreach (string key in scan.SpriteKeys.Concat(scan.GuessedSpriteKeys).Distinct()) {
            if (manifest.Sprites.Any(s => s.Key == key)) {
                continue;
            }
            if (!UserResourceManager.Spr.TryGet(key, out var sprite)) {
                if (scan.SpriteKeys.Contains(key)) {
                    result.Warnings.Add($"missing sprite: {key}");
                }
                continue;
            }
            string textureKey = sprite.textureKey;
            if (!UserResourceManager.T2D.TryGet(textureKey, out var texture)
                || !UserResourceManager.T2D.TryGetPath(textureKey, out string texPath)
                || string.IsNullOrEmpty(texPath)
                || !File.Exists(UserResourceManager.FromUser(texPath))) {
                result.Warnings.Add($"missing texture for sprite: {key} -> {textureKey}");
                continue;
            }
            var settings = sprite.settings;
            manifest.Sprites.Add(new O5cpSpriteEntry {
                Key = key,
                TextureKey = textureKey,
                Rect = [settings.Rect.Value.x, settings.Rect.Value.y, settings.Rect.Value.width, settings.Rect.Value.height],
                Pivot = [settings.Pivot.Value.x, settings.Pivot.Value.y],
                PixelsPerUnit = settings.PixelsPerUnit.Value,
                Border = [settings.Border.Value.x, settings.Border.Value.y, settings.Border.Value.z, settings.Border.Value.w],
                Folder = UserResourceSettings.FolderOf(UserResourceManager.Config.Data.ImageFolders, key),
            });
            if (packedTextures.Contains(textureKey)) {
                continue;
            }
            packedTextures.Add(textureKey);
            string diskPath = UserResourceManager.FromUser(texPath);
            if (!options.IncludeImages) {
                manifest.ExcludedTextures.Add(new O5cpExcludedEntry {
                    Key = textureKey,
                    Sha256 = HashFileQuiet(diskPath),
                });
                continue;
            }
            byte[] data = File.ReadAllBytes(diskPath);
            manifest.Textures.Add(new O5cpTextureEntry {
                Key = textureKey,
                File = O5cpFormat.TexturesPrefix + O5cpPackage.SanitizeSegment(textureKey, Path.GetExtension(diskPath)),
                Sha256 = O5cpPackage.ComputeSha256(data),
                Bytes = data.Length,
                MipChain = texture.settings.MipChain.Value,
                Linear = texture.settings.Linear.Value,
                Folder = UserResourceSettings.FolderOf(UserResourceManager.Config.Data.ImageFolders, textureKey),
            });
            files.Add((manifest.Textures[^1].File, data));
        }

        foreach (string tokenized in canvas.Props?.ExtraScripts ?? []) {
            string diskPath = UserResourceManager.FromUser(tokenized);
            if (string.IsNullOrEmpty(diskPath) || !File.Exists(diskPath)) {
                result.Warnings.Add($"missing script: {tokenized}");
                continue;
            }
            if (!O5cpFormat.ScriptExt.Contains(Path.GetExtension(diskPath))) {
                result.Warnings.Add($"skipped non-js script: {tokenized}");
                continue;
            }
            byte[] data = File.ReadAllBytes(diskPath);
            if (!options.IncludeScripts) {
                manifest.ExcludedScripts.Add(new O5cpExcludedEntry {
                    Key = Path.GetFileName(diskPath),
                    Sha256 = O5cpPackage.ComputeSha256(data),
                });
                result.Warnings.Add($"excluded script (option off): {tokenized}");
                continue;
            }
            string rel = O5cpFormat.ScriptsPrefix + O5cpPackage.SanitizeSegment(
                Path.GetFileNameWithoutExtension(diskPath), Path.GetExtension(diskPath));
            manifest.Scripts.Add(new O5cpScriptEntry {
                File = rel,
                OriginalName = Path.GetFileName(diskPath),
                Sha256 = O5cpPackage.ComputeSha256(data),
                Bytes = data.Length,
            });
            files.Add((rel, data));
        }

        string thumbTokenized = canvas.Props?.ThumbnailPath;
        if (!string.IsNullOrWhiteSpace(thumbTokenized)) {
            string diskPath = UserResourceManager.FromUser(thumbTokenized);
            if (!File.Exists(diskPath)) {
                result.Warnings.Add($"missing thumbnail: {thumbTokenized}");
            } else if (!O5cpFormat.ThumbnailExt.Contains(Path.GetExtension(diskPath))) {
                result.Warnings.Add($"skipped thumbnail with bad extension: {thumbTokenized}");
            } else {
                byte[] data = File.ReadAllBytes(diskPath);
                string rel = O5cpFormat.AssetsPrefix + "thumbnail" + Path.GetExtension(diskPath).ToLowerInvariant();
                manifest.ThumbnailFile = rel;
                files.Add((rel, data));
            }
        }

        files.Insert(0, (O5cpFormat.ManifestFile, System.Text.Encoding.UTF8.GetBytes(manifest.Serialize().ToString())));
        files.Insert(1, (manifest.CanvasFile, System.Text.Encoding.UTF8.GetBytes(canvasText)));
        O5cpPackage.WriteZip(zipPath, files);
        return result;
    }

    private static void CollectModules(JToken canvasJson, O5cpManifest manifest, List<string> warnings) {
        HashSet<string> tagNames;
        try {
            tagNames = O5cpTagScanner.Scan(canvasJson);
        } catch {
            return;
        }
        if (tagNames.Count == 0) {
            return;
        }
        System.Reflection.Assembly coreAsm;
        try {
            coreAsm = typeof(Overlay.OvCanvas).Assembly;
        } catch {
            return;
        }
        var modules = MainCore.ModuleService?.LoadedModules;
        if (modules == null) {
            return;
        }
        foreach (string name in tagNames) {
            TagCore tag;
            try {
                if (!TagManager.TryGet(name, out tag) || tag?.Member == null) {
                    continue;
                }
            } catch {
                continue;
            }
            System.Reflection.Assembly tagAsm;
            try {
                tagAsm = tag.Member.DeclaringType?.Assembly;
            } catch {
                continue;
            }
            if (tagAsm == null || tagAsm == coreAsm) {
                continue;
            }
            foreach (var module in modules) {
                System.Reflection.Assembly moduleAsm;
                try {
                    moduleAsm = module?.GetType().Assembly;
                } catch {
                    continue;
                }
                if (moduleAsm == tagAsm && manifest.Modules.All(m => m.Name != module.Name)) {
                    manifest.Modules.Add(new O5cpModuleEntry {
                        Name = module.Name ?? string.Empty,
                        Version = module.Version ?? string.Empty,
                        Author = module.Author ?? string.Empty,
                    });
                }
            }
        }
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
