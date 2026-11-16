using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;

namespace Overlayer.IO;

public sealed class SettingsFile<T>(string path) where T : class, ISettingsFile, new() {
    public T Data { get; } = new();

    public readonly string Path = path;

    private readonly object saveLock = new();

    private readonly object scheduleLock = new();

    private CancellationTokenSource saveCts;

    private Task saveTask;

    public bool Load() {
        try {
            if (!File.Exists(Path)) {
                return false;
            }

            string json = File.ReadAllText(Path);
            JToken token = JToken.Parse(json);
            Data.Deserialize(token);

            return true;
        } catch (Exception e) {
            MainCore.Log.Err(
                $"[{nameof(SettingsFile<>)}] Failed to load settings '{Path}': {e}"
            );

            return false;
        }
    }

    public bool Save() {
        lock (saveLock) {
            try {
                string dir = System.IO.Path.GetDirectoryName(Path);

                if (!string.IsNullOrEmpty(dir)) {
                    Directory.CreateDirectory(dir);
                }

                string json = Data.Serialize().ToString();
                File.WriteAllText(Path, json);

                return true;
            } catch (Exception e) {
                MainCore.Log.Err(
                    $"[{nameof(SettingsFile<>)}] Failed to save settings '{Path}': {e}"
                );

                return false;
            }
        }
    }

    // Debounced: every call restarts the delay and only the last one saves.
    // (Used to cancel the pending save and then bail out because one was
    // "already scheduled", so two calls within the delay saved nothing.)
    public void RequestSave(
        int delay = 500
    ) {
        lock (scheduleLock) {
            saveCts?.Cancel();

            saveCts = new CancellationTokenSource();

            CancellationToken token = saveCts.Token;

            saveTask = Task.Run((Func<Task>)(async () => {
                try {
                    await Task.Delay(delay, token);

                    Save();
                } catch (OperationCanceledException) {
                } catch (Exception e) {
                    MainCore.Log.Err(
                        $"[{nameof(SettingsFile<>)}] Failed to request save '{Path}': {e}"
                    );
                }
            }));
        }
    }

    public void Dispose() {
        Task pending;
        lock (scheduleLock) {
            saveCts?.Cancel();
            pending = saveTask;
            saveTask = null;
            saveCts = null;
        }
        try {
            pending?.Wait();
        } catch (AggregateException) {
        }

        if (Data is IDisposable disposable) {
            disposable.Dispose();
        }
    }
}
