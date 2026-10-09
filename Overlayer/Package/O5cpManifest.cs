using Newtonsoft.Json.Linq;

namespace Overlayer.Package;

public static class O5cpFormat {
    public const string Format = "o5cp";
    public const int FormatVersion = 1;

    public const string PackageKeyPrefix = "pkg:";

    public static bool IsPackageKey(string key) =>
        !string.IsNullOrEmpty(key) && key.StartsWith(PackageKeyPrefix, StringComparison.Ordinal);

    public static bool IsPackageScript(string path) {
        string name;
        try {
            name = Path.GetFileName(path);
        } catch {
            return false;
        }
        if (string.IsNullOrEmpty(name)
            || !name.StartsWith("pkg_", StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || name.Length < 13
            || name[11] != '_') {
            return false;
        }
        for (int i = 4; i < 11; i++) {
            char c = name[i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex) {
                return false;
            }
        }
        return true;
    }

    public const string ManifestFile = "manifest.json";
    public const string DefaultCanvasFile = "canvas.json";

    public const string FontsPrefix = "resources/fonts/";
    public const string TexturesPrefix = "resources/textures/";
    public const string ScriptsPrefix = "scripts/";
    public const string AssetsPrefix = "assets/";

    public const long MaxExtractedBytes = 256L * 1024 * 1024;
    public const int MaxEntries = 2000;

    public static readonly HashSet<string> FontExt =
        new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };
    public static readonly HashSet<string> TextureExt =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };
    public static readonly HashSet<string> ScriptExt =
        new(StringComparer.OrdinalIgnoreCase) { ".js" };
    public static readonly HashSet<string> ThumbnailExt =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
}

public sealed class O5cpPackageInfo {
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Version = "1.0.0";
    public string Author = string.Empty;
    public string Description = string.Empty;
    public string License = string.Empty;
    public string CreatedAt = string.Empty;
    public string AppVersion = string.Empty;
    public string MinAppVersion = string.Empty;
}

public sealed class O5cpFontEntry {
    public string Key = string.Empty;
    public string File = string.Empty;
    public string Sha256 = string.Empty;
    public long Bytes;
}

public sealed class O5cpTextureEntry {
    public string Key = string.Empty;
    public string File = string.Empty;
    public string Sha256 = string.Empty;
    public long Bytes;
    public bool MipChain;
    public bool Linear;
    public string Folder = string.Empty;
}

public sealed class O5cpSpriteEntry {
    public string Key = string.Empty;
    public string TextureKey = string.Empty;
    public float[] Rect = [0f, 0f, 0f, 0f];
    public float[] Pivot = [0.5f, 0.5f];
    public float PixelsPerUnit = 100f;
    public float[] Border = [0f, 0f, 0f, 0f];
    public string Folder = string.Empty;
}

public sealed class O5cpScriptEntry {
    public string File = string.Empty;
    public string OriginalName = string.Empty;
    public string Sha256 = string.Empty;
    public long Bytes;
}

public sealed class O5cpModuleEntry {
    public string Name = string.Empty;
    public string Version = string.Empty;
    public string Author = string.Empty;
}

public sealed class O5cpExcludedEntry {
    public string Key = string.Empty;
    public string Sha256 = string.Empty;
}

public sealed class O5cpManifest {
    public int FormatVersion = O5cpFormat.FormatVersion;
    public O5cpPackageInfo Package = new();
    public string CanvasFile = O5cpFormat.DefaultCanvasFile;
    public string ThumbnailFile;
    public List<O5cpFontEntry> Fonts = [];
    public List<O5cpTextureEntry> Textures = [];
    public List<O5cpSpriteEntry> Sprites = [];
    public List<O5cpScriptEntry> Scripts = [];
    public List<O5cpModuleEntry> Modules = [];
    public List<O5cpExcludedEntry> ExcludedFonts = [];
    public List<O5cpExcludedEntry> ExcludedTextures = [];
    public List<O5cpExcludedEntry> ExcludedScripts = [];

    public JToken Serialize() {
        var resources = new JObject {
            ["fonts"] = new JArray(Fonts.Select(f => new JObject {
                ["key"] = f.Key,
                ["file"] = f.File,
                ["sha256"] = f.Sha256,
                ["bytes"] = f.Bytes,
            })),
            ["textures"] = new JArray(Textures.Select(t => new JObject {
                ["key"] = t.Key,
                ["file"] = t.File,
                ["sha256"] = t.Sha256,
                ["bytes"] = t.Bytes,
                ["mipChain"] = t.MipChain,
                ["linear"] = t.Linear,
                ["folder"] = t.Folder ?? string.Empty,
            })),
            ["sprites"] = new JArray(Sprites.Select(s => new JObject {
                ["key"] = s.Key,
                ["textureKey"] = s.TextureKey,
                ["rect"] = new JArray(s.Rect ?? [0f, 0f, 0f, 0f]),
                ["pivot"] = new JArray(s.Pivot ?? [0.5f, 0.5f]),
                ["pixelsPerUnit"] = s.PixelsPerUnit,
                ["border"] = new JArray(s.Border ?? [0f, 0f, 0f, 0f]),
                ["folder"] = s.Folder ?? string.Empty,
            })),
            ["scripts"] = new JArray(Scripts.Select(s => new JObject {
                ["file"] = s.File,
                ["originalName"] = s.OriginalName ?? string.Empty,
                ["sha256"] = s.Sha256,
                ["bytes"] = s.Bytes,
            })),
        };
        var manifest = new JObject {
            ["format"] = O5cpFormat.Format,
            ["formatVersion"] = O5cpFormat.FormatVersion,
            ["package"] = new JObject {
                ["id"] = Package.Id ?? string.Empty,
                ["name"] = Package.Name ?? string.Empty,
                ["version"] = Package.Version ?? "1.0.0",
                ["author"] = Package.Author ?? string.Empty,
                ["description"] = Package.Description ?? string.Empty,
                ["license"] = Package.License ?? string.Empty,
                ["createdAt"] = Package.CreatedAt ?? string.Empty,
                ["appVersion"] = Package.AppVersion ?? string.Empty,
                ["minAppVersion"] = Package.MinAppVersion ?? string.Empty,
            },
            ["canvas"] = new JObject {
                ["file"] = CanvasFile ?? O5cpFormat.DefaultCanvasFile,
            },
            ["resources"] = resources,
            ["modules"] = new JArray(Modules.Select(m => new JObject {
                ["name"] = m.Name ?? string.Empty,
                ["version"] = m.Version ?? string.Empty,
                ["author"] = m.Author ?? string.Empty,
            })),
            ["excluded"] = new JObject {
                ["fonts"] = new JArray(ExcludedFonts.Select(e => new JObject {
                    ["key"] = e.Key,
                    ["sha256"] = e.Sha256 ?? string.Empty,
                })),
                ["textures"] = new JArray(ExcludedTextures.Select(e => new JObject {
                    ["key"] = e.Key,
                    ["sha256"] = e.Sha256 ?? string.Empty,
                })),
                ["scripts"] = new JArray(ExcludedScripts.Select(e => new JObject {
                    ["key"] = e.Key,
                    ["sha256"] = e.Sha256 ?? string.Empty,
                })),
            },

            ["extensions"] = new JObject(),
        };
        if (!string.IsNullOrEmpty(ThumbnailFile)) {
            manifest["thumbnail"] = new JObject {
                ["file"] = ThumbnailFile,
            };
        }
        return manifest;
    }

    public static bool TryParse(JToken token, out O5cpManifest manifest, out string error) {
        manifest = new O5cpManifest();
        if (token is not JObject obj) {
            error = "manifest is not an object";
            return false;
        }
        if ((string)obj["format"] != O5cpFormat.Format) {
            error = $"not an {O5cpFormat.Format} package";
            return false;
        }

        manifest.FormatVersion = (int?)obj["formatVersion"] ?? O5cpFormat.FormatVersion;
        if (obj["package"] is JObject pkg) {
            manifest.Package.Id = (string)pkg["id"] ?? string.Empty;
            manifest.Package.Name = (string)pkg["name"] ?? string.Empty;
            manifest.Package.Version = (string)pkg["version"] ?? "1.0.0";
            manifest.Package.Author = (string)pkg["author"] ?? string.Empty;
            manifest.Package.Description = (string)pkg["description"] ?? string.Empty;
            manifest.Package.License = (string)pkg["license"] ?? string.Empty;
            manifest.Package.CreatedAt = (string)pkg["createdAt"] ?? string.Empty;
            manifest.Package.AppVersion = (string)pkg["appVersion"] ?? string.Empty;
            manifest.Package.MinAppVersion = (string)pkg["minAppVersion"] ?? string.Empty;
        }
        manifest.CanvasFile = (string)(obj["canvas"] as JObject)?["file"];
        if (string.IsNullOrWhiteSpace(manifest.CanvasFile)) {
            manifest.CanvasFile = O5cpFormat.DefaultCanvasFile;
        }
        manifest.ThumbnailFile = (string)(obj["thumbnail"] as JObject)?["file"];
        if (obj["modules"] is JArray modules) {
            foreach (var m in modules) {
                if (m is not JObject mo) continue;
                manifest.Modules.Add(new O5cpModuleEntry {
                    Name = (string)mo["name"] ?? string.Empty,
                    Version = (string)mo["version"] ?? string.Empty,
                    Author = (string)mo["author"] ?? string.Empty,
                });
            }
        }
        if (obj["resources"] is JObject res) {
            foreach (var f in res["fonts"] as JArray ?? []) {
                if (f is not JObject fo) continue;
                manifest.Fonts.Add(new O5cpFontEntry {
                    Key = (string)fo["key"] ?? string.Empty,
                    File = (string)fo["file"] ?? string.Empty,
                    Sha256 = (string)fo["sha256"] ?? string.Empty,
                    Bytes = (long?)fo["bytes"] ?? 0,
                });
            }
            foreach (var t in res["textures"] as JArray ?? []) {
                if (t is not JObject to) continue;
                manifest.Textures.Add(new O5cpTextureEntry {
                    Key = (string)to["key"] ?? string.Empty,
                    File = (string)to["file"] ?? string.Empty,
                    Sha256 = (string)to["sha256"] ?? string.Empty,
                    Bytes = (long?)to["bytes"] ?? 0,
                    MipChain = (bool?)to["mipChain"] ?? false,
                    Linear = (bool?)to["linear"] ?? false,
                    Folder = (string)to["folder"] ?? string.Empty,
                });
            }
            foreach (var s in res["sprites"] as JArray ?? []) {
                if (s is not JObject so) continue;
                manifest.Sprites.Add(new O5cpSpriteEntry {
                    Key = (string)so["key"] ?? string.Empty,
                    TextureKey = (string)so["textureKey"] ?? string.Empty,
                    Rect = ReadFloats(so["rect"], [0f, 0f, 0f, 0f]),
                    Pivot = ReadFloats(so["pivot"], [0.5f, 0.5f]),
                    PixelsPerUnit = (float?)so["pixelsPerUnit"] ?? 100f,
                    Border = ReadFloats(so["border"], [0f, 0f, 0f, 0f]),
                    Folder = (string)so["folder"] ?? string.Empty,
                });
            }
            foreach (var s in res["scripts"] as JArray ?? []) {
                if (s is not JObject so) continue;
                manifest.Scripts.Add(new O5cpScriptEntry {
                    File = (string)so["file"] ?? string.Empty,
                    OriginalName = (string)so["originalName"] ?? string.Empty,
                    Sha256 = (string)so["sha256"] ?? string.Empty,
                    Bytes = (long?)so["bytes"] ?? 0,
                });
            }
        }
        if (obj["excluded"] is JObject exc) {
            foreach (var e in exc["fonts"] as JArray ?? []) {
                if (e is not JObject eo) continue;
                manifest.ExcludedFonts.Add(new O5cpExcludedEntry {
                    Key = (string)eo["key"] ?? string.Empty,
                    Sha256 = (string)eo["sha256"] ?? string.Empty,
                });
            }
            foreach (var e in exc["textures"] as JArray ?? []) {
                if (e is not JObject eo) continue;
                manifest.ExcludedTextures.Add(new O5cpExcludedEntry {
                    Key = (string)eo["key"] ?? string.Empty,
                    Sha256 = (string)eo["sha256"] ?? string.Empty,
                });
            }
            foreach (var e in exc["scripts"] as JArray ?? []) {
                if (e is not JObject eo) continue;
                manifest.ExcludedScripts.Add(new O5cpExcludedEntry {
                    Key = (string)eo["key"] ?? string.Empty,
                    Sha256 = (string)eo["sha256"] ?? string.Empty,
                });
            }
        }
        if (string.IsNullOrWhiteSpace(manifest.Package.Id)) {
            error = "manifest.package.id is missing";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static float[] ReadFloats(JToken token, float[] fallback) {
        if (token is not JArray arr || arr.Count != fallback.Length) {
            return (float[])fallback.Clone();
        }
        var result = new float[fallback.Length];
        for (int i = 0; i < result.Length; i++) {
            try {
                result[i] = arr[i].Value<float>();
            } catch {
                result[i] = fallback[i];
            }
        }
        return result;
    }
}
