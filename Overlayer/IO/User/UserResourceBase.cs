using UnityEngine;

namespace Overlayer.IO.User;

public abstract class UserResourceBase<T> {
    protected Dictionary<string, (string path, T value)> Cache { get; } = [];

    public bool TryGet(string key, out T value) {
        if(Cache.TryGetValue(key, out var entry)) {
            value = entry.value;
            return true;
        }

        value = default;
        return false;
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
