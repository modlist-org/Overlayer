using Overlayer.Core;
using System.Reflection;

namespace Overlayer.Tag.Core;

public static class TagManager {
    private static readonly object _lock = new();

    private static Dictionary<string, TagCore> _tags = [];
    public static int Count => _tags.Count;

    private static Task _initTask;

    public static Task InitializeAsync(Assembly asm) {
        bool isFirstInit;
        Task task;

        lock(_lock) {
            if(_initTask != null) {
                isFirstInit = false;
            } else {
                isFirstInit = true;

                _initTask = Task.Run(() => InitializeInternal(asm));
            }

            task = _initTask;
        }

        MainCore.Log.Msg(isFirstInit
            ? $"[{nameof(TagManager)}] Initialization started"
            : $"[{nameof(TagManager)}] Already initialized / returning existing task");

        return task;
    }

    private static void InitializeInternal(Assembly asm) {
        try {
            var list = TagLoader.LoadAsync(asm).GetAwaiter().GetResult();

            MainCore.Log.Msg($"[{nameof(TagManager)}] Loaded tags: {list.Count}");

            var dict = new Dictionary<string, TagCore>(list.Count);

            foreach(var tag in list) {
                dict[tag.Name] = tag;
            }

            _tags = dict;

            MainCore.Log.Msg($"[{nameof(TagManager)}] Initialization completed. Total: {_tags.Count}");
        } catch(Exception ex) {
            MainCore.Log.Msg($"[{nameof(TagManager)}] Initialization failed: {ex}");
            throw;
        }
    }

    public static bool TryGet(string name, out TagCore tag)
        => _tags.TryGetValue(name, out tag);

    public static void Set(TagCore tag) {
        lock(_lock) {
            Dictionary<string, TagCore> newMap = new(_tags) {
                [tag.Name] = tag
            };

            _tags = newMap;
        }
        MainCore.Log.Msg($"[{nameof(TagManager)}] Tag updated: {tag.Name}");
    }

    public static void Dispose() {
        lock(_lock) {
            _tags.Clear();
        }

        _initTask = null;

        MainCore.Log.Msg($"[{nameof(TagManager)}] Disposed");
    }
}