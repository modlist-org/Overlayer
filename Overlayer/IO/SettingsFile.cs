using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;

namespace Overlayer.IO;

public sealed class SettingsFile<T>(string path) where T : class, ISettingsFile, new() {
    public T Data { get; } = new();

    public readonly string Path = path;

    private readonly object saveLock = new();

    private CancellationTokenSource saveCts;

    private bool saveScheduled;

    public void Load() {
        try {
            string json = File.ReadAllText(Path);

            JToken token = JToken.Parse(json);

            Data.Deserialize(token);
        } catch(Exception e) {
            MainCore.Logger.Err(
                $"[{nameof(SettingsFile<>)}] Failed to load settings '{Path}': {e}"
            );
        }
    }

    public void Save() {
        lock(saveLock) {
            try {
                string dir = System.IO.Path.GetDirectoryName(Path);

                if(!string.IsNullOrEmpty(dir)) {
                    Directory.CreateDirectory(dir);
                }

                string json = Data.Serialize().ToString();

                File.WriteAllText(Path, json);
            } catch(Exception e) {
                MainCore.Logger.Err(
                    $"[{nameof(SettingsFile<T>)}] Failed to save settings '{Path}': {e}"
                );
            }
        }
    }

    public void RequestSave(
        int delay = 500
    ) {
        saveCts?.Cancel();

        saveCts = new CancellationTokenSource();

        CancellationToken token = saveCts.Token;

        if(saveScheduled) {
            return;
        }

        saveScheduled = true;

        _ = Task.Run(async () => {
            try {
                await Task.Delay(delay, token);

                if(token.IsCancellationRequested) {
                    return;
                }

                Save();
            } catch(OperationCanceledException) {
            } catch(Exception e) {
                MainCore.Logger.Err(
                    $"[{nameof(SettingsFile<T>)}] Failed to request save '{Path}': {e}"
                );
            } finally {
                saveScheduled = false;
            }
        });
    }
}