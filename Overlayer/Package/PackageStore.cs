using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.IO.User.Impl;
using Overlayer.Overlay;
using System.IO.Compression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Package;

public sealed class PackageEntry {
    public string Id;
    public string Dir;
    public O5cpManifest Manifest;
    public OvCanvas Canvas;
    public bool Enabled = true;
    public bool Loaded;
    public Texture2D ThumbnailTexture;
    public Sprite ThumbnailSprite;
    public List<string> Warnings = [];
}

public static class PackageStore {
    public const string StateFileName = "state.json";

    public static readonly List<PackageEntry> Packages = [];

    public static event Action OnChanged;

    public static string KeyPrefix(string packageId) => $"pkg:{packageId}/";

    private static void NotifyChanged() {
        try {
            OnChanged?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Change notification failed: {e.Message}");
        }
    }

    public static void Initialize() {
        Packages.Clear();
        string root = MainCore.Paths.PackagesPath;
        if(!Directory.Exists(root)) {
            Directory.CreateDirectory(root);
            return;
        }
        SweepOrphanScripts();
        foreach(string dir in Directory.GetDirectories(root).OrderBy(Path.GetFileName)) {
            try {
                RemoveLegacyDir(dir);
                if(Directory.Exists(dir)) {
                    LoadInstalled(dir);
                }
            } catch(Exception e) {
                MainCore.Log.Err($"[{nameof(PackageStore)}] Failed to load package {dir}: {e.Message}");
            }
        }
        SweepTempCache();
    }

    public static void Tick() {
        foreach(var entry in Packages) {
            try {
                entry.Canvas?.RefreshFx();
            } catch {
            }
        }
    }

    public static void UnloadAll() {
        foreach(var entry in Packages) {
            try {
                UninstallScripts(entry);
                UnloadEntryResources(entry);
                if(entry.Canvas != null) {
                    entry.Canvas.Dispose();
                    entry.Canvas = null;
                }
                if(entry.ThumbnailTexture) {
                    Object.Destroy(entry.ThumbnailTexture);
                    entry.ThumbnailTexture = null;
                    entry.ThumbnailSprite = null;
                }
                entry.Loaded = false;
            } catch {
            }
        }
        Packages.Clear();
    }

    public static string ZipPath(PackageEntry entry) =>
        Directory.GetFiles(entry.Dir, "*.o5cp").OrderBy(Path.GetFileName).FirstOrDefault();

    public static string ReadZipText(string zipPath, string rel) {
        using var zip = ZipFile.OpenRead(zipPath);
        var zipEntry = zip.GetEntry((rel ?? string.Empty).Replace('\\', '/'));
        if(zipEntry == null) {
            return null;
        }
        if(zipEntry.Length > O5cpFormat.MaxExtractedBytes) {
            throw new InvalidDataException("package entry expands beyond the safety limit");
        }
        using var stream = zipEntry.Open();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static byte[] GetPackageBytes(PackageEntry entry, string rel) {
        if(entry == null) {
            return null;
        }
        rel = (rel ?? string.Empty).Replace('\\', '/');
        if(!O5cpPackage.IsAllowedPath(rel, out _)) {
            return null;
        }
        using var zip = ZipFile.OpenRead(ZipPath(entry));
        var zipEntry = zip.GetEntry(rel);
        if(zipEntry == null) {
            return null;
        }
        if(zipEntry.Length > O5cpFormat.MaxExtractedBytes) {
            throw new InvalidDataException("package entry expands beyond the safety limit");
        }
        using var stream = zipEntry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static string TempCachePath(PackageEntry entry, string rel) =>
        Path.Combine(MainCore.Paths.TempPath, "o5cp", entry.Id,
            (rel ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));

    public static string GetPackageFile(PackageEntry entry, string rel) {
        if(entry == null) {
            return null;
        }
        rel = (rel ?? string.Empty).Replace('\\', '/');
        if(!O5cpPackage.IsAllowedPath(rel, out _)) {
            return null;
        }
        string temp = TempCachePath(entry, rel);
        if(!File.Exists(temp)) {
            byte[] data = GetPackageBytes(entry, rel);
            if(data == null) {
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(temp));
            File.WriteAllBytes(temp, data);
        }
        return temp;
    }

    public sealed class StagedPackage : IDisposable {
        public string SourcePath;
        public O5cpManifest Manifest;
        public string FileHash = string.Empty;
        public string Hash = string.Empty;
        public string FileName = string.Empty;
        public List<string> Warnings = [];

        public void Dispose() {
        }
    }

    public static PackageEntry Install(string o5cpPath, out List<string> warnings) {
        warnings = [];
        using var staged = Stage(o5cpPath, out warnings);
        if(staged == null) {
            return null;
        }
        var entry = CommitStaged(staged, out var commitWarnings);
        warnings.AddRange(commitWarnings);
        return entry;
    }

    public static PackageEntry InstallBytes(byte[] data, out List<string> warnings) {
        warnings = [];
        if(data == null || data.Length == 0) {
            return null;
        }
        string tmp = Path.Combine(MainCore.Paths.TempPath, $"preset_{Guid.NewGuid():N}.o5cp");
        try {
            Directory.CreateDirectory(MainCore.Paths.TempPath);
            File.WriteAllBytes(tmp, data);
            var entry = Install(tmp, out warnings);
            return entry;
        } catch(Exception e) {
            warnings.Add($"bad package: {e.Message}");
            MainCore.Log.Err($"[{nameof(PackageStore)}] InstallBytes failed: {e.Message}");
            return null;
        } finally {
            try {
                if(File.Exists(tmp)) {
                    File.Delete(tmp);
                }
            } catch {
            }
        }
    }

    public static StagedPackage Stage(string o5cpPath, out List<string> warnings) {
        warnings = [];
        if(string.IsNullOrWhiteSpace(o5cpPath) || !File.Exists(o5cpPath)) {
            return null;
        }
        string fileHash;
        try {
            using var stream = File.OpenRead(o5cpPath);
            fileHash = O5cpPackage.ComputeSha256(stream);
        } catch(Exception e) {
            warnings.Add($"bad package: {e.Message}");
            MainCore.Log.Err($"[{nameof(PackageStore)}] Hash failed: {e.Message}");
            return null;
        }
        string text;
        try {
            text = ReadZipText(o5cpPath, O5cpFormat.ManifestFile);
        } catch(Exception e) {
            warnings.Add($"bad package: {e.Message}");
            MainCore.Log.Err($"[{nameof(PackageStore)}] Extract failed: {e.Message}");
            return null;
        }
        if(text == null) {
            warnings.Add("package has no manifest.json");
            return null;
        }
        JToken token;
        try {
            token = JToken.Parse(text);
        } catch(Exception e) {
            warnings.Add($"bad manifest: {e.Message}");
            return null;
        }
        if(!O5cpManifest.TryParse(token, out var manifest, out string manifestError)) {
            warnings.Add(manifestError);
            return null;
        }
        string name = manifest.Package.Name;
        if(string.IsNullOrWhiteSpace(name)) {
            name = "Package";
        }
        var staged = new StagedPackage {
            SourcePath = o5cpPath,
            Manifest = manifest,
            FileHash = fileHash,
            Hash = fileHash.Substring(0, 7),
            FileName = O5cpPackage.SanitizeSegment(name, ".o5cp"),
        };
        staged.Warnings.AddRange(warnings);
        if((manifest.FormatVersion > O5cpFormat.FormatVersion)
            || IsNewerApp(manifest.Package.MinAppVersion)) {
            staged.Warnings.Add($"made for a newer Overlayer (pkg {manifest.Package.AppVersion}, min {manifest.Package.MinAppVersion}). Loading anyway.");
        }
        return staged;
    }

    public static PackageEntry FindByHash(string hash) =>
        Packages.FirstOrDefault(p => p.Id == hash);

    public static PackageEntry CommitStaged(StagedPackage staged, out List<string> warnings) {
        warnings = [];
        if(staged?.Manifest == null || !File.Exists(staged.SourcePath)) {
            return null;
        }
        var live = FindByHash(staged.Hash);
        if(live != null) {
            if(!live.Enabled) {
                warnings.AddRange(SetEnabled(live, true));
            }
            warnings.AddRange(staged.Warnings);
            return live;
        }
        string dir = Path.Combine(MainCore.Paths.PackagesPath, staged.Hash);
        try {
            Directory.CreateDirectory(dir);
            File.Copy(staged.SourcePath, Path.Combine(dir, staged.FileName), overwrite: true);
        } catch(Exception e) {
            warnings.Add($"install failed: {e.Message}");
            MainCore.Log.Err($"[{nameof(PackageStore)}] Copy failed: {e.Message}");
            return null;
        }
        WriteStateFile(dir, new PackageState {
            Enabled = true,
            FullSha = staged.FileHash,
        });
        PackageEntry entry;
        try {
            entry = LoadInstalled(dir);
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Package load failed: {e.Message}");
            warnings.Add($"package failed to load: {e.Message}");
            try {
                Directory.Delete(dir, true);
            } catch {
            }
            return null;
        }
        if(entry == null) {
            warnings.Add("package failed to load after install");
            try {
                Directory.Delete(dir, true);
            } catch {
            }
            return null;
        }
        warnings.AddRange(entry.Warnings);
        UserResourceManager.Config.Save();
        MainCore.Log.Msg($"[{nameof(PackageStore)}] Installed package '{staged.Manifest.Package.Name}' ({staged.Hash})");
        NotifyChanged();
        return entry;
    }

    public static bool Remove(string packageId) {
        var entry = Packages.FirstOrDefault(p => p.Id == packageId);
        if(entry == null) {
            string stale = Path.Combine(MainCore.Paths.PackagesPath, SanitizeId(packageId));
            if(Directory.Exists(stale)) {
                UninstallScriptsForDir(stale);
                Directory.Delete(stale, true);
                SweepTempDir(SanitizeId(packageId));
                return true;
            }
            return false;
        }
        Packages.Remove(entry);
        try {
            UninstallScripts(entry);
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Script uninstall failed: {e.Message}");
        }
        UnloadEntryResources(entry);
        try {
            if(entry.Canvas != null) {
                entry.Canvas.Dispose();
                entry.Canvas = null;
            }
            if(entry.ThumbnailTexture) {
                Object.Destroy(entry.ThumbnailTexture);
                entry.ThumbnailTexture = null;
                entry.ThumbnailSprite = null;
            }
        } catch {
        }
        entry.Loaded = false;
        SweepTempDir(entry.Id);
        try {
            if(Directory.Exists(entry.Dir)) {
                Directory.Delete(entry.Dir, true);
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Failed to delete {entry.Dir}: {e.Message}");
        }
        UserResourceManager.Config.Save();
        MainCore.Log.Msg($"[{nameof(PackageStore)}] Removed package {packageId}");
        NotifyChanged();
        return true;
    }

    public static List<string> SetEnabled(PackageEntry entry, bool enabled) {
        var warnings = new List<string>();
        if(entry == null) {
            return warnings;
        }
        entry.Enabled = enabled;
        if(enabled) {
            if(!EnsureLoaded(entry, out warnings)) {
                entry.Enabled = false;
                warnings.Insert(0, "package failed to load");
            }
        } else {
            UnloadHeavy(entry);
        }
        var state = ReadStateFile(entry.Dir);
        state.Enabled = entry.Enabled;
        WriteStateFile(entry.Dir, state);
        NotifyChanged();
        return warnings;
    }

    private static PackageEntry LoadInstalled(string dir) {
        string zipPath = Directory.GetFiles(dir, "*.o5cp").OrderBy(Path.GetFileName).FirstOrDefault();
        if(zipPath == null) {
            return null;
        }
        string fullSha;
        try {
            using var stream = File.OpenRead(zipPath);
            fullSha = O5cpPackage.ComputeSha256(stream);
        } catch {
            return null;
        }
        string text = ReadZipText(zipPath, O5cpFormat.ManifestFile);
        if(text == null) {
            return null;
        }
        if(!O5cpManifest.TryParse(JToken.Parse(text), out var manifest, out string error)) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] {error}: {zipPath}");
            return null;
        }
        var state = ReadStateFile(dir);
        if(string.IsNullOrEmpty(state.FullSha)) {
            state.FullSha = fullSha;
            WriteStateFile(dir, state);
        } else if(state.FullSha != fullSha) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Hash mismatch, refusing {dir}");
            return null;
        }
        var entry = new PackageEntry {
            Id = SanitizeId(Path.GetFileName(dir)),
            Dir = dir,
            Manifest = manifest,
            Enabled = state.Enabled,
        };
        LoadThumbnail(entry);
        CheckModules(entry);
        if(entry.Enabled) {
            if(!EnsureLoaded(entry, out var loadWarnings)) {
                entry.Enabled = false;
                entry.Warnings.AddRange(loadWarnings);
                entry.Warnings.Insert(0, "package failed to load (disabled)");
                state.Enabled = false;
                WriteStateFile(dir, state);
            } else {
                entry.Warnings.AddRange(loadWarnings);
            }
        }
        foreach(var warning in entry.Warnings) {
            MainCore.Log.Wrn($"[{nameof(PackageStore)}] [{entry.Id}] {warning}");
        }
        Packages.Add(entry);
        return entry;
    }

    public static bool EnsureLoaded(PackageEntry entry, out List<string> warnings) {
        warnings = [];
        if(entry == null) {
            return false;
        }
        if(entry.Loaded && entry.Canvas != null) {
            return true;
        }
        string prefix = KeyPrefix(entry.Id);
        try {
            LoadEntryResources(entry, prefix, warnings);
            InstallScripts(entry, warnings);
            byte[] canvasBytes = GetPackageBytes(entry, entry.Manifest.CanvasFile);
            if(canvasBytes == null) {
                warnings.Add($"canvas file missing: {entry.Manifest.CanvasFile}");
                UnloadHeavy(entry);
                return false;
            }
            JToken canvasJson = JToken.Parse(System.Text.Encoding.UTF8.GetString(canvasBytes));
            O5cpKeyMapper.RewriteStaticKeys(canvasJson, key => prefix + key);
            var packedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach(var font in entry.Manifest.Fonts) {
                packedKeys.Add(font.Key);
            }
            foreach(var sprite in entry.Manifest.Sprites) {
                packedKeys.Add(sprite.Key);
            }
            O5cpKeyMapper.RewriteKeyLiterals(canvasJson,
                literal => packedKeys.Contains(literal) ? prefix + literal : null);
            var canvas = new OvCanvas();
            canvas.Deserialize(canvasJson);
            canvas.IsPackage = true;
            canvas.PackageId = entry.Id;
            canvas.RectTransform.SetParent(OverlayCore.Transform, false);
            canvas.ApplyConfig();
            canvas.RefreshLayouts();
            entry.Canvas = canvas;
            ApplyEnabled(entry);
            var scan = O5cpKeyScanner.Scan(canvasJson);
            foreach(string key in scan.FontKeys) {
                string shortKey = key.StartsWith(prefix, StringComparison.Ordinal)
                    ? key[prefix.Length..]
                    : key;
                if(!UserResourceManager.Fnt.TryGet(key, out _)) {
                    warnings.Add(WasExcluded(entry.Manifest.ExcludedFonts, shortKey)
                        ? $"font '{shortKey}' was excluded at export (missing)"
                        : $"font '{shortKey}' is missing from the package");
                }
            }
            foreach(string key in scan.SpriteKeys) {
                if(!UserResourceManager.Spr.TryGet(key, out _)) {
                    string shortKey = key.StartsWith(prefix, StringComparison.Ordinal)
                        ? key[prefix.Length..]
                        : key;
                    warnings.Add($"sprite '{shortKey}' is missing from the package");
                }
            }
            entry.Loaded = true;
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] [{entry.Id}] Load failed: {e.Message}");
            warnings.Add($"load failed: {e.Message}");
            try {
                UnloadHeavy(entry);
            } catch {
            }
            return false;
        }
    }

    public static void UnloadHeavy(PackageEntry entry) {
        if(entry == null) {
            return;
        }
        try {
            UninstallScripts(entry);
        } catch {
        }
        UnloadEntryResources(entry);
        try {
            if(entry.Canvas != null) {
                entry.Canvas.Dispose();
                entry.Canvas = null;
            }
        } catch {
        }
        entry.Loaded = false;
    }

    private static void ApplyEnabled(PackageEntry entry) {
        try {
            if(entry.Canvas != null) {
                entry.Canvas.Config.Enabled.Value = entry.Enabled;
                entry.Canvas.ApplyConfig();
            }
        } catch {
        }
    }

    private static void LoadEntryResources(PackageEntry entry, string prefix, List<string> warnings) {
        var manifest = entry.Manifest;
        foreach(var font in manifest.Fonts) {
            string key = prefix + font.Key;
            string path = GetPackageFile(entry, font.File);
            if(path == null || !File.Exists(path)) {
                warnings.Add($"font file missing: {font.File}");
                continue;
            }
            UserResourceManager.Fnt.Remove(key);
            var result = UserResourceManager.Fnt.Load(key, path);
            if(result != UserFont.Result.Success) {
                warnings.Add($"font load failed ({result}): {font.Key}");
                continue;
            }
            VerifyHash(path, font.Sha256, $"font {font.Key}", warnings);
        }
        foreach(var texture in manifest.Textures) {
            string key = prefix + texture.Key;
            string path = GetPackageFile(entry, texture.File);
            if(path == null || !File.Exists(path)) {
                warnings.Add($"texture file missing: {texture.File}");
                continue;
            }
            byte[] data = File.ReadAllBytes(path);
            UserResourceManager.T2D.Remove(key);
            var result = UserResourceManager.T2D.LoadData(key, path, data, texture.MipChain, texture.Linear);
            if(result != UserTexture2D.Result.Success) {
                warnings.Add($"texture load failed ({result}): {texture.Key}");
                continue;
            }
            VerifyHash(path, texture.Sha256, $"texture {texture.Key}", warnings);
            if(!string.IsNullOrEmpty(texture.Folder)) {
                UserResourceManager.Config.Data.ImageFolders[key] = texture.Folder;
            }
        }
        foreach(var sprite in manifest.Sprites) {
            string key = prefix + sprite.Key;
            string textureKey = prefix + sprite.TextureKey;
            if(!UserResourceManager.T2D.TryGet(textureKey, out _)) {
                warnings.Add($"sprite skipped, texture missing: {sprite.Key}");
                continue;
            }
            UserResourceManager.Spr.Remove(key);
            var result = UserResourceManager.Spr.Load(
                key,
                textureKey,
                new Rect(sprite.Rect[0], sprite.Rect[1], sprite.Rect[2], sprite.Rect[3]),
                new Vector2(sprite.Pivot[0], sprite.Pivot[1]),
                sprite.PixelsPerUnit,
                new Vector4(sprite.Border[0], sprite.Border[1], sprite.Border[2], sprite.Border[3]),
                out _);
            if(result != UserSprite.Result.Success) {
                warnings.Add($"sprite load failed ({result}): {sprite.Key}");
                continue;
            }
            if(!string.IsNullOrEmpty(sprite.Folder)) {
                UserResourceManager.Config.Data.ImageFolders[key] = sprite.Folder;
            }
        }
    }

    private static void UnloadEntryResources(PackageEntry entry) {
        string prefix = KeyPrefix(entry.Id);
        foreach(string key in UserResourceManager.Fnt.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) {
            UserResourceManager.Fnt.Remove(key);
        }
        foreach(string key in UserResourceManager.Spr.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) {
            UserResourceManager.Spr.Remove(key);
        }
        foreach(string key in UserResourceManager.T2D.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) {
            UserResourceManager.T2D.Remove(key);
        }
        foreach(string key in UserResourceManager.Config.Data.ImageFolders.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) {
            UserResourceManager.Config.Data.ImageFolders.Remove(key);
        }
        foreach(string key in UserResourceManager.Config.Data.FontFolders.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) {
            UserResourceManager.Config.Data.FontFolders.Remove(key);
        }
    }

    private static void InstallScripts(PackageEntry entry, List<string> warnings) {
        if(entry.Manifest.Scripts.Count == 0 || MainCore.V8 == null) {
            return;
        }
        string scriptDir = MainCore.V8.ScriptFolderPath;
        if(string.IsNullOrEmpty(scriptDir)) {
            return;
        }
        Directory.CreateDirectory(scriptDir);
        var state = ReadStateFile(entry.Dir);
        foreach(var script in entry.Manifest.Scripts) {
            byte[] data = GetPackageBytes(entry, script.File);
            if(data == null) {
                warnings.Add($"script file missing: {script.File}");
                continue;
            }
            string destName = $"pkg_{SafeFile(entry.Id)}_{O5cpPackage.SanitizeSegment(
                Path.GetFileNameWithoutExtension(script.OriginalName), ".js")}";
            string dest = Path.Combine(scriptDir, destName);
            try {
                File.WriteAllBytes(dest, data);
            } catch(Exception e) {
                warnings.Add($"script install failed: {script.OriginalName} ({e.Message})");
                continue;
            }
            state.InstalledScripts.RemoveAll(s => s.File == destName);
            state.InstalledScripts.Add((destName, script.Sha256));
            try {
                MainCore.V8.ReloadScriptFile(dest);
            } catch {
            }
        }
        WriteStateFile(entry.Dir, state);
    }

    private static void UninstallScripts(PackageEntry entry) {
        var state = ReadStateFile(entry.Dir);
        UninstallScriptFiles(state.InstalledScripts);
        state.InstalledScripts.Clear();
        WriteStateFile(entry.Dir, state);
    }

    private static void UninstallScriptsForDir(string dir) {
        var state = ReadStateFile(dir);
        UninstallScriptFiles(state.InstalledScripts);
    }

    private static void UninstallScriptFiles(List<(string File, string Sha)> installed) {
        if(installed.Count == 0) {
            return;
        }
        string scriptDir = null;
        try {
            scriptDir = MainCore.V8?.ScriptFolderPath;
        } catch {
        }
        if(string.IsNullOrEmpty(scriptDir) || !Directory.Exists(scriptDir)) {
            return;
        }
        foreach(var (file, sha) in installed) {
            string dest = Path.Combine(scriptDir, file);
            try {
                if(File.Exists(dest)
                    && (string.IsNullOrEmpty(sha) || HashFile(dest) == sha)) {
                    File.Delete(dest);
                }
                try {
                    MainCore.V8?.UnloadScriptFile(dest);
                } catch {
                }
            } catch(Exception e) {
                MainCore.Log.Err($"[{nameof(PackageStore)}] Script uninstall failed for {file}: {e.Message}");
            }
        }
    }

    private static void LoadThumbnail(PackageEntry entry) {
        if(string.IsNullOrEmpty(entry.Manifest.ThumbnailFile)) {
            return;
        }
        byte[] bytes = GetPackageBytes(entry, entry.Manifest.ThumbnailFile);
        if(bytes == null || bytes.Length == 0) {
            return;
        }
        try {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if(!tex.LoadImage(bytes)) {
                Object.Destroy(tex);
                return;
            }
            tex.filterMode = FilterMode.Bilinear;
            entry.ThumbnailTexture = tex;
            entry.ThumbnailSprite = Sprite.Create(
                tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        } catch {
        }
    }

    private static void CheckModules(PackageEntry entry) {
        if(entry.Manifest.Modules.Count == 0) {
            return;
        }
        var loaded = MainCore.ModuleService?.LoadedModules;
        foreach(var module in entry.Manifest.Modules) {
            ModuleAPI.OverlayerModule match = null;
            if(loaded != null) {
                foreach(var local in loaded) {
                    if(local != null && local.Name == module.Name) {
                        match = local;
                        break;
                    }
                }
            }
            if(match == null) {
                entry.Warnings.Add($"needs module '{module.Name}' v{module.Version} (not installed)");
            } else if(!string.IsNullOrEmpty(module.Version)
                && !string.IsNullOrEmpty(match.Version)
                && module.Version != match.Version) {
                entry.Warnings.Add($"needs module '{module.Name}' v{module.Version} (have v{match.Version})");
            }
        }
    }

    private static void RemoveLegacyDir(string dir) {
        if(Directory.GetFiles(dir, "*.o5cp").Length > 0) {
            return;
        }
        if(!File.Exists(Path.Combine(dir, O5cpFormat.ManifestFile))) {
            return;
        }
        try {
            Directory.Delete(dir, true);
            MainCore.Log.Msg($"[{nameof(PackageStore)}] Removed legacy exploded package dir {dir}");
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PackageStore)}] Legacy cleanup failed for {dir}: {e.Message}");
        }
    }

    private static void SweepTempDir(string packageId) {
        try {
            string dir = Path.Combine(MainCore.Paths.TempPath, "o5cp", packageId);
            if(Directory.Exists(dir)) {
                Directory.Delete(dir, true);
            }
        } catch {
        }
    }

    private static void SweepTempCache() {
        string root;
        try {
            root = Path.Combine(MainCore.Paths.TempPath, "o5cp");
            if(!Directory.Exists(root)) {
                return;
            }
        } catch {
            return;
        }
        HashSet<string> live = new(Packages.Select(p => p.Id), StringComparer.Ordinal);
        foreach(string dir in Directory.GetDirectories(root)) {
            try {
                if(!live.Contains(Path.GetFileName(dir))) {
                    Directory.Delete(dir, true);
                }
            } catch {
            }
        }
    }

    private static void SweepOrphanScripts() {
        string scriptDir = null;
        try {
            scriptDir = MainCore.V8?.ScriptFolderPath;
            if(string.IsNullOrEmpty(scriptDir) || !Directory.Exists(scriptDir)) {
                return;
            }
        } catch {
            return;
        }
        foreach(string file in Directory.GetFiles(scriptDir, "pkg_*.js")) {
            try {
                if(!O5cpFormat.IsPackageScript(file)) {
                    continue;
                }
                File.Delete(file);
            } catch {
            }
        }
    }

    private sealed class PackageState {
        public bool Enabled = true;
        public string FullSha = string.Empty;
        public List<(string File, string Sha)> InstalledScripts = [];
    }

    private static PackageState ReadStateFile(string dir) {
        var state = new PackageState();
        try {
            string path = Path.Combine(dir, StateFileName);
            if(!File.Exists(path)) {
                return state;
            }
            if(JToken.Parse(File.ReadAllText(path)) is not JObject obj) {
                return state;
            }
            state.Enabled = (bool?)obj["enabled"] ?? true;
            state.FullSha = (string)obj["sha256"] ?? string.Empty;
            if(obj["scripts"] is JArray arr) {
                foreach(var item in arr) {
                    if(item is JObject so && (string)so["file"] is string file && !string.IsNullOrEmpty(file)) {
                        state.InstalledScripts.Add((file, (string)so["sha256"] ?? string.Empty));
                    }
                }
            }
        } catch {
        }
        return state;
    }

    private static void WriteStateFile(string dir, PackageState state) {
        try {
            var obj = new JObject {
                ["enabled"] = state.Enabled,
                ["sha256"] = state.FullSha ?? string.Empty,
                ["scripts"] = new JArray(state.InstalledScripts.Select(s => new JObject {
                    ["file"] = s.File,
                    ["sha256"] = s.Sha ?? string.Empty,
                })),
            };
            File.WriteAllText(Path.Combine(dir, StateFileName), obj.ToString());
        } catch {
        }
    }

    private static bool WasExcluded(List<O5cpExcludedEntry> excluded, string key) =>
        excluded.Any(e => e.Key == key);

    private static void VerifyHash(string path, string expected, string label, List<string> warnings) {
        if(string.IsNullOrEmpty(expected)) {
            return;
        }
        try {
            if(HashFile(path) != expected.ToLowerInvariant()) {
                warnings.Add($"{label} hash mismatch (file changed?)");
            }
        } catch {
        }
    }

    private static string HashFile(string path) {
        using var stream = File.OpenRead(path);
        return O5cpPackage.ComputeSha256(stream);
    }

    private static bool IsNewerApp(string minAppVersion) {
        try {
            if(string.IsNullOrWhiteSpace(minAppVersion)) {
                return false;
            }
            return new Version(minAppVersion) > new Version(Info.Version);
        } catch {
            return false;
        }
    }

    public static string SanitizeId(string id) {
        var builder = new System.Text.StringBuilder();
        foreach(char c in id ?? string.Empty) {
            builder.Append(char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }
        string clean = builder.ToString().Trim('_', '-');
        if(clean.Length > 48) {
            clean = clean.Substring(0, 48);
        }
        return string.IsNullOrEmpty(clean) ? "pkg" : clean;
    }

    private static string SafeFile(string value) {
        var builder = new System.Text.StringBuilder();
        foreach(char c in value ?? string.Empty) {
            builder.Append(char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }
        string clean = builder.ToString();
        return string.IsNullOrEmpty(clean) ? "pkg" : clean;
    }
}
