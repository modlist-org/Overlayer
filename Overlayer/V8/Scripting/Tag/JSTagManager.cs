using Microsoft.ClearScript;
using Overlayer.Tag.Core;
namespace Overlayer.V8.Scripting.Tag;

public static class JSTagManager {
    private static readonly Dictionary<string, (ScriptObject Func, TagType Type, string Desc, Type ReturnType)> _jsTags = [];
    private static readonly object _lock = new();

    public static void Add(string name, ScriptObject func, TagType type, string desc, Type returnType = null) {
        lock (_lock) {
            _jsTags[name] = (func, type, desc, returnType ?? typeof(object));
        }
    }

    public static void Remove(string name) {
        lock (_lock) {
            _jsTags.Remove(name);
        }
    }

    public static bool TryGet(string name, out ScriptObject func) {
        lock (_lock) {
            if (_jsTags.TryGetValue(name, out var data)) {
                func = data.Func;
                return true;
            }
        }
        func = null;
        return false;
    }

    public static IEnumerable<string> GetAllNames() {
        lock (_lock) {
            return [.. _jsTags.Keys];
        }
    }

    /// <summary>Removes every entry, returning the removed tag names.</summary>
    public static List<string> Clear() {
        lock (_lock) {
            List<string> names = [.. _jsTags.Keys];
            _jsTags.Clear();
            return names;
        }
    }
}