using Newtonsoft.Json.Linq;

namespace Overlayer.IO;

public class Settings {
    public bool Active = true;
    public string Language = "en-US";
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;

    public bool ShowAutoplayJudgment = false;

    public JToken Serialize() {
        JObject obj = new() {
            [nameof(Active)] = Active,
            [nameof(Language)] = Language,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,

            [nameof(ShowAutoplayJudgment)] = ShowAutoplayJudgment
        };

        return obj;
    }

    public void Deserialize(JToken token) {
        var defaults = new Settings();

        Active = token.Value<bool?>(nameof(Active)) ?? defaults.Active;
        Language = token.Value<string>(nameof(Language)) ?? defaults.Language;
        ShowOnStartup = token.Value<bool?>(nameof(ShowOnStartup)) ?? defaults.ShowOnStartup;
        Tooltip = token.Value<bool?>(nameof(Tooltip)) ?? defaults.Tooltip;
        MiddleClickToDefault = token.Value<bool?>(nameof(MiddleClickToDefault)) ?? defaults.MiddleClickToDefault;

        ShowAutoplayJudgment = token.Value<bool?>(nameof(ShowAutoplayJudgment)) ?? defaults.ShowAutoplayJudgment;
    }

    public static readonly string Path = System.IO.Path.Combine(Core.OverlayerPath, $"{nameof(Settings)}.json");

    public void Load() {
        if(File.Exists(Path)) {
            try {
                string json = File.ReadAllText(Path);
                JToken token = JToken.Parse(json);
                Deserialize(token);
            } catch(Exception e) {
                Core.Logger.Error($"Failed to load settings: {e}");
            }
        }
    }

    private static readonly object saveLock = new();
    private CancellationTokenSource saveCts;
    private bool saveScheduled;

    public void Save() {
        lock(saveLock) {
            string json = Serialize().ToString();
            File.WriteAllText(Path, json);
        }
    }

    public void RequestSave() {
        if(saveScheduled) {
            return;
        }

        saveScheduled = true;

        saveCts?.Cancel();
        saveCts = new CancellationTokenSource();
        var token = saveCts.Token;

        _ = Task.Run(async () => {
            try {
                await Task.Delay(500, token);

                if(token.IsCancellationRequested) {
                    return;
                }

                Save();
            } catch(Exception e) {
                Core.Logger.Error(e.ToString());
            } finally {
                saveScheduled = false;
            }
        });
    }
}
