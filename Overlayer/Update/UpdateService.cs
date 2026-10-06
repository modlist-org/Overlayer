using MelonLoader.Utils;
using Overlayer.Async;
using Overlayer.Core;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

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

#if IL2CPP
    // Releases only ship the Mono build; installing it over IL2CPP would break the mod.
    public static bool Supported => false;
#else
    public static bool Supported => true;
#endif

    private static readonly HttpClient Http = CreateClient();

    private static string AssetName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Overlayer_ML_win.zip"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Overlayer_ML_mac.zip"
        : "Overlayer_ML_linux.zip";

    private static HttpClient CreateClient() {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch {
            // Already negotiated by the runtime.
        }
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Overlayer-Updater/" + Info.Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static void Initialize() {
        if(!Supported) return;
        UpdatePackage.SweepOld(MelonEnvironment.GameRootDirectory);
        if(MainCore.Conf.AutoUpdate) Check(install: true);
    }

    public static async void Check(bool install = false) {
        if(!Supported || Status is UpdateStatus.Checking or UpdateStatus.Installing or UpdateStatus.Installed) return;
        bool beta = MainCore.Conf.UpdateBeta;
        Version current = MainCore.Version;
        Set(UpdateStatus.Checking);
        try {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=30";
            string json = await Task.Run(() => Http.GetStringAsync(url));
            Available = UpdatePackage.PickRelease(json, current, AssetName, beta);
        } catch(Exception e) {
            Fail("check", e);
            return;
        }
        Set(Available == null ? UpdateStatus.UpToDate : UpdateStatus.Available);
        if(install && Available != null) Install();
    }

    public static async void Install() {
        ReleaseInfo info = Available;
        if(!Supported || info == null || Status == UpdateStatus.Installing) return;
        string zip = Path.Combine(MainCore.Paths.TempPath, "Update.zip");
        string stage = Path.Combine(MainCore.Paths.TempPath, "Update");
        string root = MelonEnvironment.GameRootDirectory;
        Set(UpdateStatus.Installing);
        try {
            int count = await Task.Run(async () => {
                await Download(info.AssetUrl, zip);
                UpdatePackage.VerifySha256(zip, info.Sha256);
                return UpdatePackage.Install(zip, stage, root);
            });
            if(info.Sha256 == null) MainCore.Log.Wrn($"[Update] {info.Tag} had no checksum — integrity not verified");
            MainCore.Log.Msg($"[Update] installed {info.Tag} ({count} files) — restart the game to run it");
            Set(UpdateStatus.Installed);
        } catch(Exception e) {
            Fail("install", e);
        } finally {
            try {
                File.Delete(zip);
            } catch {
                // Overwritten by the next download.
            }
        }
    }

    private static async Task Download(string url, string path) {
        using HttpResponseMessage resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        using Stream src = await resp.Content.ReadAsStreamAsync();
        using FileStream dst = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while((read = await src.ReadAsync(buffer, 0, buffer.Length)) > 0) {
            total += read;
            if(total > UpdatePackage.MaxDownloadBytes) throw new InvalidDataException("the update download is too large");
            await dst.WriteAsync(buffer, 0, read);
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
