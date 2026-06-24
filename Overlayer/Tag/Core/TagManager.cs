using Overlayer.Async;
using Overlayer.Core;
using System.Reflection;
using static Overlayer.Overlay.OvObject;

namespace Overlayer.Tag.Core;

public static class TagManager {
    private static readonly object _lock = new();

    private static readonly HashSet<Assembly> _registeredAssemblies = [];
    private static Dictionary<string, TagCore> _tags = [];

    public static int Count => _tags.Count;

    public static Task RegisterAsync(Assembly asm) {
        lock(_lock) {
            if(_registeredAssemblies.Contains(asm)) {
                MainCore.Log.Msg($"[{nameof(TagManager)}] Assembly '{asm.GetName().Name}' is already registered.");
                return Task.CompletedTask;
            }
            _registeredAssemblies.Add(asm);
        }

        MainCore.Log.Msg($"[{nameof(TagManager)}] Registration started for: {asm.GetName().Name}");

        return Task.Run(() => RegisterInternal(asm));
    }

    private static void RegisterInternal(Assembly asm) {
        try {
            var list = TagLoader.LoadAsync(asm).GetAwaiter().GetResult();
            MainCore.Log.Msg($"[{nameof(TagManager)}] Found tags in '{asm.GetName().Name}': {list.Count}");

            if(list.Count == 0) {
                return;
            }

            lock(_lock) {
                var newDict = new Dictionary<string, TagCore>(_tags);
                foreach(var tag in list) {
                    newDict[tag.Name] = tag;
                }
                _tags = newDict;
            }

            MainCore.Log.Msg($"[{nameof(TagManager)}] Registration completed for '{asm.GetName().Name}'. Total tags: {_tags.Count}");
            MainCore.V8.GenerateImplJs();
            MainCore.V8.LoadImplJs();
            MainThread.Enqueue(TextEngineUpdater.RecompileAll);
        } catch(Exception e) {
            MainCore.Log.Msg($"[{nameof(TagManager)}] Registration failed for '{asm.GetName().Name}': {e}");

            lock(_lock) {
                _registeredAssemblies.Remove(asm);
            }
        }
    }

    public static bool TryGet(string name, out TagCore tag)
        => _tags.TryGetValue(name, out tag);

    public static List<TagCore> GetAllTags() {
        lock(_lock) {
            return [.. _tags.Values];
        }
    }

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
            _registeredAssemblies.Clear();
        }
        MainCore.Log.Msg($"[{nameof(TagManager)}] Disposed");
    }
}