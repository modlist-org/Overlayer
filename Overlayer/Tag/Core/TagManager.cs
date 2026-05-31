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
                task = _initTask;
            } else {
                isFirstInit = true;

                _initTask = Task.Run(() => InitializeInternal(asm));
                task = _initTask;
            }
        }

        if(isFirstInit) {
            MainCore.Logger.Msg($"[{nameof(TagManager)}] Initialization started");
        } else {
            MainCore.Logger.Msg($"[{nameof(TagManager)}] Already initialized / returning existing task");
        }

        return task;
    }

    private static void InitializeInternal(Assembly asm) {
        try {
            var list = TagLoader.LoadAsync(asm).GetAwaiter().GetResult();

            MainCore.Logger.Msg($"[{nameof(TagManager)}] Loaded tags: {list.Count}");

            var dict = new Dictionary<string, TagCore>(list.Count);

            foreach(var tag in list) {
                dict[tag.Name] = tag;
            }

            _tags = dict;

            MainCore.Logger.Msg($"[{nameof(TagManager)}] Initialization completed. Total: {_tags.Count}");
        } catch(Exception ex) {
            MainCore.Logger.Msg($"[{nameof(TagManager)}] Initialization failed: {ex}");
            throw;
        }
    }

    public static bool TryGet(string name, out TagCore tag)
        => _tags.TryGetValue(name, out tag);

    public static void Set(TagCore tag) {
        Dictionary<string, TagCore> newMap;
        lock(_lock) {
            newMap = new(_tags) {
                [tag.Name] = tag
            };

            _tags = newMap;
        }
        MainCore.Logger.Msg($"[{nameof(TagManager)}] Tag updated: {tag.Name}");
    }

    public static void Dispose() {
        lock(_lock) {
            _tags.Clear();
        }

        _initTask = null;

        MainCore.Logger.Msg($"[{nameof(TagManager)}] Disposed");
    }
}