using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Overlayer.Package;

public enum O5cpEntryKind {
    Manifest,
    Canvas,
    Font,
    Texture,
    Script,
    Thumbnail,
}

public static class O5cpPackage {
    public static string ComputeSha256(byte[] data) {
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }

    public static string ComputeSha256(Stream stream) {
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public static string SanitizeSegment(string name, string ext) {
        var builder = new System.Text.StringBuilder();
        foreach (char c in name ?? string.Empty) {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }
        string clean = builder.ToString().Trim('.', '_');
        if (clean.Length > 64) {
            clean = clean[..64];
        }
        if (string.IsNullOrEmpty(clean)) {
            clean = "res";
        }
        return clean + (ext ?? string.Empty).ToLowerInvariant();
    }

    public static string MakePackageId(string name, string seedHex) {
        var builder = new System.Text.StringBuilder();
        foreach (char c in name ?? string.Empty) {
            if (char.IsLetterOrDigit(c)) {
                builder.Append(c);
            } else if (builder.Length > 0 && builder[^1] != '_') {
                builder.Append('_');
            }
        }
        string slug = builder.ToString().Trim('_');
        if (slug.Length > 32) {
            slug = slug[..32];
        }
        if (string.IsNullOrEmpty(slug)) {
            slug = "Canvas";
        }
        string hash = (seedHex ?? string.Empty).ToLowerInvariant();
        hash = hash.Length >= 6 ? hash[..6] : hash.PadRight(6, '0');
        return $"{slug}_{hash}";
    }

    public static bool IsAllowedPath(string rel, out O5cpEntryKind kind) {
        kind = default;
        if (string.IsNullOrEmpty(rel)) {
            return false;
        }
        string path = rel.Replace('\\', '/');
        if (path == O5cpFormat.ManifestFile) {
            kind = O5cpEntryKind.Manifest;
            return true;
        }
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && !path.Contains('/')) {
            kind = O5cpEntryKind.Canvas;
            return true;
        }
        if (path.StartsWith(O5cpFormat.FontsPrefix, StringComparison.Ordinal)) {
            kind = O5cpEntryKind.Font;
            return O5cpFormat.FontExt.Contains(Path.GetExtension(path));
        }
        if (path.StartsWith(O5cpFormat.TexturesPrefix, StringComparison.Ordinal)) {
            kind = O5cpEntryKind.Texture;
            return O5cpFormat.TextureExt.Contains(Path.GetExtension(path));
        }
        if (path.StartsWith(O5cpFormat.ScriptsPrefix, StringComparison.Ordinal)) {
            kind = O5cpEntryKind.Script;
            return O5cpFormat.ScriptExt.Contains(Path.GetExtension(path));
        }
        if (path.StartsWith(O5cpFormat.AssetsPrefix, StringComparison.Ordinal)) {
            kind = O5cpEntryKind.Thumbnail;
            return O5cpFormat.ThumbnailExt.Contains(Path.GetExtension(path));
        }
        return false;
    }

    public static void WriteZip(string zipPath, IEnumerable<(string path, byte[] data)> files) {
        string dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        if (File.Exists(zipPath)) {
            File.Delete(zipPath);
        }
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, data) in files) {
            string rel = (path ?? string.Empty).Replace('\\', '/');
            if (!IsAllowedPath(rel, out _)) {
                throw new InvalidDataException($"refusing to pack disallowed path: {rel}");
            }
            var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(data ?? [], 0, (data ?? []).Length);
        }
    }

    public static List<string> ExtractZip(string zipPath, string stageDir) {
        string stage = WithSeparator(Path.GetFullPath(stageDir));
        if (Directory.Exists(stageDir)) {
            Directory.Delete(stageDir, true);
        }
        Directory.CreateDirectory(stageDir);
        var written = new List<string>();
        long extracted = 0;
        int count = 0;
        using (ZipArchive zip = ZipFile.OpenRead(zipPath)) {
            foreach (ZipArchiveEntry entry in zip.Entries) {
                if (string.IsNullOrEmpty(entry.Name)) {
                    continue;
                }
                string rel = entry.FullName.Replace('\\', '/');
                if (!IsAllowedPath(rel, out _)) {
                    throw new InvalidDataException($"package contains a disallowed path: {rel}");
                }
                extracted = checked(extracted + entry.Length);
                if (extracted > O5cpFormat.MaxExtractedBytes) {
                    throw new InvalidDataException("package expands beyond the safety limit");
                }
                if (++count > O5cpFormat.MaxEntries) {
                    throw new InvalidDataException("package has too many files");
                }
                string dest = Contained(stage, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                entry.ExtractToFile(dest);
                written.Add(rel);
            }
        }
        return written;
    }

    public static bool TryReadManifest(string stageDir, string rel, out O5cpManifest manifest, out string error) {
        manifest = null;
        string path = Contained(WithSeparator(Path.GetFullPath(stageDir)), rel.Replace('\\', '/'));
        JToken token;
        try {
            token = JToken.Parse(File.ReadAllText(path));
        } catch (Exception e) {
            error = $"bad manifest: {e.Message}";
            return false;
        }
        return O5cpManifest.TryParse(token, out manifest, out error);
    }

    private static string Contained(string rootWithSeparator, string relative) {
        string full = Path.GetFullPath(Path.Combine(rootWithSeparator, relative));
        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal)) {
            throw new InvalidDataException("package contains an unsafe path: " + relative);
        }
        return full;
    }

    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}
