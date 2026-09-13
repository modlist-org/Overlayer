namespace Overlayer.V8;

public sealed class FxStore {
    private const int MaxEntries = 1024;

    private readonly object _lock = new();
    private readonly Dictionary<string, object> _values = new();

    public object Get(string key, object fallback = null) {
        if (string.IsNullOrEmpty(key)) {
            return fallback;
        }

        lock(_lock) {
            return _values.TryGetValue(key, out var value) ? value : fallback;
        }
    }

    public object Set(string key, object value) {
        if (string.IsNullOrEmpty(key)) {
            return value;
        }

        lock(_lock) {
            if (_values.Count >= MaxEntries && !_values.ContainsKey(key)) {
                _values.Clear();
            }

            _values[key] = value;
            return value;
        }
    }

    public bool Has(string key) {
        if (string.IsNullOrEmpty(key)) {
            return false;
        }

        lock(_lock) {
            return _values.ContainsKey(key);
        }
    }

    public bool Remove(string key) {
        if (string.IsNullOrEmpty(key)) {
            return false;
        }

        lock(_lock) {
            return _values.Remove(key);
        }
    }

    public void Clear() {
        lock(_lock) {
            _values.Clear();
        }
    }

    public string[] Keys() {
        lock(_lock) {
            var keys = new List<string>(_values.Keys);
            return keys.ToArray();
        }
    }

    public int Count {
        get {
            lock(_lock) {
                return _values.Count;
            }
        }
    }
}
