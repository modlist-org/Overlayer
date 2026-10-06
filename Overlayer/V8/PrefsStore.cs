using Newtonsoft.Json.Linq;
using Overlayer.Core;

namespace Overlayer.V8;

// Like Store, but saved to disk so script settings survive restarts.
// Values should be JSON-friendly (bool, number, string). Namespace your
// keys per script, e.g. Prefs.Set("HideUI.judgements", true).
public sealed class PrefsStore(string path) {
    private readonly object _lock = new();
    private JObject _values;

    public object Get(string key, object fallback = null) {
        if(string.IsNullOrEmpty(key)) {
            return fallback;
        }
        lock(_lock) {
            return Values().TryGetValue(key, out var token) && token is JValue v ? v.Value : fallback;
        }
    }

    public object Set(string key, object value) {
        if(string.IsNullOrEmpty(key)) {
            return value;
        }
        lock(_lock) {
            Values()[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
            Save();
        }
        return value;
    }

    public bool Remove(string key) {
        lock(_lock) {
            if(!Values().Remove(key)) {
                return false;
            }
            Save();
            return true;
        }
    }

    private JObject Values() {
        if(_values != null) {
            return _values;
        }
        try {
            _values = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[{nameof(PrefsStore)}] Failed to read {path}: {e.Message}");
            _values = new JObject();
        }
        return _values;
    }

    private void Save() {
        try {
            File.WriteAllText(path, _values.ToString());
        } catch(Exception e) {
            MainCore.Log.Wrn($"[{nameof(PrefsStore)}] Failed to save {path}: {e.Message}");
        }
    }
}
