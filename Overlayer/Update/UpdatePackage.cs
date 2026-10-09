using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Overlayer.Update;

public sealed class ReleaseInfo {
    public string Tag;
    public Version Version;
    public bool Prerelease;
    public string Url;
    public string AssetUrl;
    // Null when GitHub reported no digest for the asset.
    public string Sha256;
}

// The updater's pure logic — release picking and zip install — kept free of
// Unity and MelonLoader so Overlayer.Tests can link it directly.
public static class UpdatePackage {
    public const string OldSuffix = ".ovupdate-old";
    public const long MaxDownloadBytes = 256L * 1024 * 1024;
    private const long MaxExtractedBytes = 512L * 1024 * 1024;
    private static readonly string[] InstallPrefixes = ["Mods/", "UserLibs/", "UserData/Overlayer/"];
    // Maintainer-owned data: always overwritten. Anything else under UserData
    // (example scripts) is only added when missing, so user edits survive.
    private const string LangPrefix = "UserData/Overlayer/Lang/";
    private static readonly string[] SweepDirs = ["Mods", "UserLibs", "UserData/Overlayer/Lang"];

    // releasesJson is GitHub's /repos/{owner}/{repo}/releases response. The
    // highest numeric version above current wins; on a tie a stable release
    // beats a prerelease.
    public static ReleaseInfo PickRelease(string releasesJson, Version current, string assetName, bool includePrerelease) {
        ReleaseInfo best = null;
        foreach (JToken rel in JArray.Parse(releasesJson)) {
            if ((bool?)rel["draft"] == true) continue;
            bool pre = (bool?)rel["prerelease"] == true;
            if (pre && !includePrerelease) continue;
            string tag = (string)rel["tag_name"];
            if (!TryParseTag(tag, out Version version) || version <= current) continue;
            if (rel["assets"] is not JArray assets) continue;
            JToken asset = assets.FirstOrDefault(a => (string)a["name"] == assetName);
            string url = (string)asset?["browser_download_url"];
            if (!IsTrustedUrl(url)) continue;
            bool better = best == null
                || version > best.Version
                || (version == best.Version && best.Prerelease && !pre);
            if (!better) continue;
            best = new ReleaseInfo {
                Tag = tag,
                Version = version,
                Prerelease = pre,
                Url = (string)rel["html_url"],
                AssetUrl = url,
                Sha256 = ParseSha256((string)asset["digest"]),
            };
        }
        return best;
    }

    // "5.8.1", "v5.8.1" and "5.9.0-beta.1" all compare by their X.Y.Z part —
    // the build itself only ever carries the numeric version.
    public static bool TryParseTag(string tag, out Version version) {
        version = null;
        if (string.IsNullOrEmpty(tag)) return false;
        string numeric = tag.TrimStart('v', 'V');
        int cut = numeric.IndexOfAny(['-', '+']);
        if (cut >= 0) numeric = numeric[..cut];
        return Version.TryParse(numeric, out version);
    }

    private static bool IsTrustedUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);

    private static string ParseSha256(string digest) {
        const string prefix = "sha256:";
        if (string.IsNullOrEmpty(digest) || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        string hex = digest[prefix.Length..];
        return hex.Length == 64 ? hex.ToLowerInvariant() : null;
    }

    public static void VerifySha256(string path, string expected) {
        if (expected == null) return;
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (actual != expected) throw new InvalidDataException($"checksum mismatch: expected {expected}, got {actual}");
    }

    // Installs the release zip (Mods/ UserLibs/ UserData/) over gameRoot.
    // Everything is extracted to stageRoot first so a bad zip fails before the
    // install is touched. Each file then swaps in with the old copy renamed to
    // *.ovupdate-old — Windows lets a loaded DLL be renamed but not deleted —
    // and a failure mid-swap rolls every file back. The new build loads on the
    // next launch. Returns the number of files installed.
    public static int Install(string zipPath, string stageRoot, string gameRoot) {
        string root = WithSeparator(Path.GetFullPath(gameRoot));
        if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
        string stage = WithSeparator(Path.GetFullPath(stageRoot));
        List<(string Staged, string Dest)> files = [];
        bool hasPayload = false;
        long extracted = 0;
        using (ZipArchive zip = ZipFile.OpenRead(zipPath)) {
            foreach (ZipArchiveEntry entry in zip.Entries) {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string rel = entry.FullName.Replace('\\', '/');
                if (!InstallPrefixes.Any(p => rel.StartsWith(p, StringComparison.Ordinal))) continue;
                string dest = Contained(root, rel);
                if (rel.StartsWith("UserData/", StringComparison.Ordinal)
                    && !rel.StartsWith(LangPrefix, StringComparison.Ordinal)
                    && File.Exists(dest)) continue;
                extracted = checked(extracted + entry.Length);
                if (extracted > MaxExtractedBytes) throw new InvalidDataException("the update zip expands beyond the safety limit");
                string staged = Contained(stage, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(staged));
                entry.ExtractToFile(staged);
                files.Add((staged, dest));
                hasPayload |= rel == "Mods/Overlayer.dll";
            }
        }
        if (!hasPayload) throw new InvalidDataException("the update zip has no Mods/Overlayer.dll");

        List<(string Dest, string Old)> swapped = [];
        try {
            foreach ((string staged, string dest) in files) {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                string old = null;
                if (File.Exists(dest)) {
                    old = dest + OldSuffix;
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(dest, old);
                }
                swapped.Add((dest, old));
                File.Move(staged, dest);
            }
        } catch {
            for (int i = swapped.Count - 1; i >= 0; i--) {
                (string dest, string old) = swapped[i];
                try {
                    if (File.Exists(dest)) File.Delete(dest);
                    if (old != null) File.Move(old, dest);
                } catch {
                    // Best effort: an .ovupdate-old left behind still holds the
                    // previous file for a manual restore.
                }
            }
            throw;
        }
        foreach ((_, string old) in swapped) {
            if (old != null) TryDelete(old);
        }
        TryDeleteDirectory(stageRoot);
        return files.Count;
    }

    // Deletes the *.ovupdate-old files a previous install couldn't remove
    // because they were still loaded.
    public static void SweepOld(string gameRoot) {
        foreach (string dir in SweepDirs) {
            string path = Path.Combine(gameRoot, dir);
            if (!Directory.Exists(path)) continue;
            foreach (string file in Directory.GetFiles(path, "*" + OldSuffix)) TryDelete(file);
        }
    }

    private static string Contained(string rootWithSeparator, string relative) {
        string full = Path.GetFullPath(Path.Combine(rootWithSeparator, relative));
        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new InvalidDataException("the update zip contains an unsafe path: " + relative);
        return full;
    }

    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch {
            // Still loaded (Windows); SweepOld retries next launch.
        }
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        } catch {
            // Temp leftovers are harmless; the next install clears the stage.
        }
    }
}
