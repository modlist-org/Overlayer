using Microsoft.ClearScript;
using Overlayer.Async;
using Overlayer.Core;
using System.IO.Compression;

namespace Overlayer.V8.Scripting.Net;

// Script-side network/file helpers. Work runs off the main thread; every
// callback is delivered on the Unity main thread.
//
//   Net.Get(url, (ok, status, body) => { ... });
//   Net.Download(url, path, (ok, error) => { ... }, fraction => { ... });
//   Net.Unzip(zipPath, dir, (ok, error) => { ... });
//   Net.FindFiles(dir, "*.adofai")   // recursive, returns string[]
//   Net.DataPath                     // Overlayer's UserData folder
public sealed class NetAccess {
    private const long MaxGetBytes = 8L * 1024 * 1024;
    private const long MaxDownloadBytes = 2L * 1024 * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient() {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Overlayer-JS/1.0");
        return client;
    }

    public string DataPath => MainCore.Paths.RootPath;

    public bool Get(string url, object callback) {
        if(!IsWebUrl(url) || callback is not ScriptObject cb) {
            return false;
        }
        Task.Run(async () => {
            try {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if(response.Content.Headers.ContentLength > MaxGetBytes) {
                    throw new InvalidDataException("Response too large.");
                }
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Deliver(cb, response.IsSuccessStatusCode, (int)response.StatusCode, body);
            } catch(Exception e) {
                Deliver(cb, false, 0, e.Message);
            }
        });
        return true;
    }

    public bool Download(string url, string path, object callback, object progress = null) {
        if(!IsWebUrl(url) || string.IsNullOrWhiteSpace(path)) {
            return false;
        }
        var cb = callback as ScriptObject;
        var onProgress = progress as ScriptObject;
        Task.Run(async () => {
            string part = path + ".part";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                long? length = response.Content.Headers.ContentLength;
                if(length > MaxDownloadBytes) {
                    throw new InvalidDataException("Download too large.");
                }
                using(var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using(var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true)) {
                    byte[] buffer = new byte[65536];
                    long total = 0;
                    int lastPercent = -1;
                    int read;
                    while((read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) {
                        total += read;
                        if(total > MaxDownloadBytes) {
                            throw new InvalidDataException("Download too large.");
                        }
                        await output.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        int percent = length > 0 ? (int)(total * 100 / length.Value) : -1;
                        if(onProgress != null && percent != lastPercent) {
                            lastPercent = percent;
                            Deliver(onProgress, percent < 0 ? -1.0 : percent / 100.0);
                        }
                    }
                }
                if(File.Exists(path)) {
                    File.Delete(path);
                }
                File.Move(part, path);
                Deliver(cb, true, null);
            } catch(Exception e) {
                TryDelete(part);
                Deliver(cb, false, e.Message);
            }
        });
        return true;
    }

    public bool Unzip(string zipPath, string dir, object callback) {
        var cb = callback as ScriptObject;
        Task.Run(() => {
            try {
                ExtractSafe(zipPath, dir);
                Deliver(cb, true, null);
            } catch(Exception e) {
                Deliver(cb, false, e.Message);
            }
        });
        return true;
    }

    public string[] FindFiles(string dir, string pattern) {
        try {
            return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern ?? "*", SearchOption.AllDirectories) : [];
        } catch {
            return [];
        }
    }

    private static void ExtractSafe(string zipPath, string dir) {
        string root = Path.GetFullPath(dir);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        using var archive = ZipFile.OpenRead(zipPath);
        foreach(var entry in archive.Entries) {
            string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
            if(!target.StartsWith(prefix, StringComparison.Ordinal)) {
                throw new InvalidDataException($"Archive entry escapes target folder: {entry.FullName}");
            }
            if(string.IsNullOrEmpty(entry.Name)) {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            entry.ExtractToFile(target, true);
        }
    }

    private static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static void Deliver(ScriptObject fn, params object[] args) {
        if(fn == null) {
            return;
        }
        MainThread.Enqueue(() => {
            try {
                MainCore.V8.InvokeCallback(fn, args);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[{nameof(NetAccess)}] callback threw: {e.Message}");
            }
        });
    }

    private static void TryDelete(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        } catch { }
    }
}
