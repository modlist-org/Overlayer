using Overlayer.Core;
using System.Reflection;

namespace Overlayer.Tag.Core;

public static class TagManager {
    private static readonly object _lock = new();

    private static Dictionary<string, TagCore> _tags = [];
    public static int Count => _tags.Count;

    private static Task _initTask;

    public static Task InitializeAsync(Assembly asm) {
        lock(_lock) {
            if(_initTask != null) {
                MainCore.Logger.Msg($"[{nameof(TagManager)}] Already initialized / returning existing task");
                return _initTask;
            }

            MainCore.Logger.Msg($"[{nameof(TagManager)}] Initialization started");

            _initTask = Task.Run(async () => {
                try {
                    var list = await TagLoader.LoadAsync(asm);

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
            });

            return _initTask;
        }
    }

    public static bool TryGet(string name, out TagCore tag)
        => _tags.TryGetValue(name, out tag);

    public static void Set(TagCore tag) {
        lock(_lock) {
            _tags[tag.Name] = tag;
        }
    }
}