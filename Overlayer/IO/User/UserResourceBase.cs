namespace Overlayer.IO.User;

public abstract class UserResourceBase<T> {
    protected Dictionary<string, (string path, T value)> Cache { get; } = [];
    public IReadOnlyCollection<string> Keys => Cache.Keys;

    public bool TryGet(string key, out T value) {
        if(key == null) {
            value = default;
            return false;
        }

        if(Cache.TryGetValue(key, out var entry)) {
            value = entry.value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetPath(string key, out string path) {
        if(key != null && Cache.TryGetValue(key, out var entry)) {
            path = entry.path;
            return true;
        }
        path = string.Empty;
        return false;
    }

    public bool TryRenameKey(string oldKey, string newKey) {
        if(
            string.IsNullOrWhiteSpace(oldKey) ||
            string.IsNullOrWhiteSpace(newKey) ||
            string.Equals(oldKey, newKey, StringComparison.Ordinal) ||
            !Cache.TryGetValue(oldKey, out var entry) ||
            Cache.ContainsKey(newKey)
        ) {
            return false;
        }

        Cache.Remove(oldKey);
        Cache[newKey] = entry;
        return true;
    }

    public bool TryGetKey(
        Predicate<T> predicate,
        out string key
    ) {
        foreach(var (cacheKey, (_, value)) in Cache) {
            if(predicate(value)) {
                key = cacheKey;
                return true;
            }
        }

        key = string.Empty;
        return false;
    }

    public abstract void Dispose();
}
