using Newtonsoft.Json.Linq;

namespace Overlayer.IO;

public class Settings {
    public bool Active = true;
    public string Language = "en-US";
    public bool ShowOnStartup = false;
    public bool Tooltip = true;
    public bool MiddleClickToDefault = true;
    public float UIScale = 1.0f;

    public bool ShowAutoplayJudgment = false;

    public JToken Serialize() {
        JObject obj = new() {
            [nameof(Active)] = Active,
            [nameof(Language)] = Language,
            [nameof(ShowOnStartup)] = ShowOnStartup,
            [nameof(Tooltip)] = Tooltip,
            [nameof(MiddleClickToDefault)] = MiddleClickToDefault,
            [nameof(UIScale)] = UIScale,

            [nameof(ShowAutoplayJudgment)] = ShowAutoplayJudgment
        };

        return obj;
    }

    private static T Read<T>(JToken token, string key, T fallback) {
        var value = token[key];
        if (value == null) {
            return fallback;
        }

        try {
            return value.Value<T>();
        }
        catch {
            return fallback;
        }
    }

    public void Deserialize(JToken token) {
        var defaults = new Settings();

        Active = Read(token, nameof(Active), defaults.Active);
        Language = Read(token, nameof(Language), defaults.Language);
        ShowOnStartup = Read(token, nameof(ShowOnStartup), defaults.ShowOnStartup);
        Tooltip = Read(token, nameof(Tooltip), defaults.Tooltip);
        MiddleClickToDefault = Read(token, nameof(MiddleClickToDefault), defaults.MiddleClickToDefault);
        UIScale = Read(token, nameof(UIScale), defaults.UIScale);

        ShowAutoplayJudgment = Read(token, nameof(ShowAutoplayJudgment), defaults.ShowAutoplayJudgment);
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
