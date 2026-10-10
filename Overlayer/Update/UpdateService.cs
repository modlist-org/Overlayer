using MelonLoader.Utils;
using Overlayer.Async;
using Overlayer.Core;
using System.IO;
using System.Net;
using System.Text;

namespace Overlayer.Update;

public enum UpdateStatus {
    Idle,
    Checking,
    UpToDate,
    Available,
    Installing,
    Installed,
    Failed,
}

// Checks GitHub releases at launch and, with AutoUpdate on, installs the
// newest one in the background. The running build stays loaded; the update
// takes effect on the next launch.
public static class UpdateService {
    private const string RepoOwner = "modlist-org";
    private const string RepoName = "Overlayer";

    public static UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    public static ReleaseInfo Available { get; private set; }
    public static string Error { get; private set; } = "";
    public static event Action OnChanged;

    public static bool IsHttpAvailable {
        get {
            if (_httpAvailable.HasValue) return _httpAvailable.Value;
            bool ok = UpdatePackage.ProbeHttpCapabilities(out string err);
            if (!ok && !string.IsNullOrEmpty(err)) {
                MainCore.Log?.Wrn($"[Update] HTTP capability check failed: {err}");
            }
            _httpAvailable = ok;
            return ok;
        }
    }

    private static bool? _httpAvailable;

#if IL2CPP
    // Releases only ship the Mono build; installing it over IL2CPP would break the mod.
    public static bool Supported => false;
#else
    public static bool Supported => IsHttpAvailable;
#endif

    private static string AssetName =>
        (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsPlayer || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor) ? "Overlayer_ML_win.zip"
        : (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor) ? "Overlayer_ML_mac.zip"
        : "Overlayer_ML_linux.zip";

    private static HttpWebRequest CreateRequest(string url, string accept) {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch {
            // Already negotiated by the runtime.
        }
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.Timeout = 20000;
        req.ReadWriteTimeout = 20000;
        req.UserAgent = "Overlayer-Updater/" + Info.Version;
        req.Accept = accept;
        req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        return req;
    }

    private static string GetString(string url) {
        HttpWebRequest req = CreateRequest(url, "application/vnd.github+json");
        using HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        using Stream stream = resp.GetResponseStream();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static void Initialize() {
        if (!Supported) {
            if (MainCore.Conf.AutoUpdate) {
                MainCore.Log?.Wrn("[Update] AutoUpdate is enabled, but HTTP networking / update service is not supported on this platform.");
            }
            return;
        }
        UpdatePackage.SweepOld(MelonEnvironment.GameRootDirectory);
        if (MainCore.Conf.AutoUpdate) {
            if (!IsHttpAvailable) {
                MainCore.Log?.Wrn("[Update] AutoUpdate is enabled, but HTTP networking is not available.");
                return;
            }
            Check(install: true);
        }
    }

    public static async void Check(bool install = false) {
        if (!Supported || !IsHttpAvailable || Status is UpdateStatus.Checking or UpdateStatus.Installing or UpdateStatus.Installed) return;
        bool beta = MainCore.Conf.UpdateBeta;
        Version current = MainCore.Version;
        Set(UpdateStatus.Checking);
        try {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=30";
            string json = await Task.Run(() => GetString(url));
            Available = UpdatePackage.PickRelease(json, current, AssetName, beta);
        } catch (Exception e) {
            Fail("check", e);
            return;
        }
        Set(Available == null ? UpdateStatus.UpToDate : UpdateStatus.Available);
        if (install && Available != null) Install();
    }

    public static async void Install() {
        ReleaseInfo info = Available;
        if (!Supported || !IsHttpAvailable || info == null || Status == UpdateStatus.Installing) return;
        string zip = Path.Combine(MainCore.Paths.TempPath, "Update.zip");
        string stage = Path.Combine(MainCore.Paths.TempPath, "Update");
        string root = MelonEnvironment.GameRootDirectory;
        Set(UpdateStatus.Installing);
        try {
            int count = await Task.Run(() => {
                Download(info.AssetUrl, zip);
                UpdatePackage.VerifySha256(zip, info.Sha256);
                return UpdatePackage.Install(zip, stage, root);
            });
            if (info.Sha256 == null) MainCore.Log.Wrn($"[Update] {info.Tag} had no checksum — integrity not verified");
            MainCore.Log.Msg($"[Update] installed {info.Tag} ({count} files) — restart the game to run it");
            Set(UpdateStatus.Installed);
        } catch (Exception e) {
            Fail("install", e);
        } finally {
            try {
                File.Delete(zip);
            } catch {
                // Overwritten by the next download.
            }
        }
    }

    private static void Download(string url, string path) {
        HttpWebRequest req = CreateRequest(url, "application/octet-stream");
        using HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        using Stream src = resp.GetResponseStream();
        using FileStream dst = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = src.Read(buffer, 0, buffer.Length)) > 0) {
            total += read;
            if (total > UpdatePackage.MaxDownloadBytes) throw new InvalidDataException("the update download is too large");
            dst.Write(buffer, 0, read);
        }
    }

    private static void Fail(string stage, Exception e) {
        Error = e.Message;
        MainCore.Log.Wrn($"[Update] {stage} failed: {e.Message}");
        Set(UpdateStatus.Failed);
    }

    private static void Set(UpdateStatus status) {
        Status = status;
        MainThread.Enqueue(() => OnChanged?.Invoke());
    }
}
