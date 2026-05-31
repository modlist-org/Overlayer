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

    public abstract void Dispose();
}
