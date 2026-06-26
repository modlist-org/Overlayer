using Microsoft.ClearScript;
using Overlayer.Tag.Core;
namespace Overlayer.V8.Scripting.Tag;

public static class JSTagManager {
    private static readonly Dictionary<string, (ScriptObject Func, TagType Type, string Desc)> _jsTags = [];
    private static readonly object _lock = new();

    public static void Add(string name, ScriptObject func, TagType type, string desc) {
        lock(_lock) {
            _jsTags[name] = (func, type, desc);
        }
    }

    public static void Remove(string name) {
        lock(_lock) {
            _jsTags.Remove(name);
        }
    }

    public static bool TryGet(string name, out ScriptObject func) {
        lock(_lock) {
            if(_jsTags.TryGetValue(name, out var data)) {
                func = data.Func;
                return true;
            }
        }
        func = null;
        return false;
    }

    public static IEnumerable<string> GetAllNames() {
        lock(_lock) {
            return _jsTags.Keys.ToList();
        }
    }
}