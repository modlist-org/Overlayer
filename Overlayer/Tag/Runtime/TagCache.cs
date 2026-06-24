using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public sealed class TagCache {
    public static TagCache Instance { get; } = new();

    private class CacheEntry(CompiledPlaceholder compiled) {
        public readonly CompiledPlaceholder Compiled = compiled;
        public int RefCount = 0;
    }

    private readonly Dictionary<string, CacheEntry> cache = [];
    private readonly object lockObject = new();

    public CompiledPlaceholder GetOrCompile(ParsedTag parsed) {
        if(!TagManager.TryGet(parsed.Name, out var tag)) {
            return new CompiledPlaceholder(() => parsed.Raw, [
                new CompileDiagnostic(DiagnosticId.TagNotFound, CompileSeverity.Error, new(parsed.Name, parsed.Index, parsed.Length), [parsed.Name])
            ]);
        }

        string key = MakeKey(parsed);
        lock(lockObject) {
            if(cache.TryGetValue(key, out var entry)) {
                return entry.Compiled;
            }
        }

        CompiledPlaceholder compiled = (tag.TagType & TagType.Advanced) != 0
            ? AdvancedCompiler.Compile(tag, parsed)
            : Compiler.Compile(tag, parsed);

        if(compiled.IsValid) {
            lock(lockObject) {
                if(!cache.TryGetValue(key, out var entry)) {
                    cache[key] = new CacheEntry(compiled);
                }
                return cache[key].Compiled;
            }
        }
        return compiled;
    }

    public void IncrementRef(string key) {
        lock(lockObject) {
            if(cache.TryGetValue(key, out var entry)) {
                entry.RefCount++;
            }
        }
    }

    public void DecrementRef(string key) {
        lock(lockObject) {
            if(cache.TryGetValue(key, out var entry)) {
                entry.RefCount--;
                if(entry.RefCount <= 0) {
                    cache.Remove(key);
                }
            }
        }
    }

    public void Clear() {
        lock(lockObject) {
            cache.Clear();
        }
    }

    public string GetKey(ParsedTag parsed) => MakeKey(parsed);

    private static string MakeKey(ParsedTag p) =>
        (p.Args == null || p.Args.Length == 0)
        ? p.Name
        : string.Concat(p.Name, ":", string.Join(",", p.Args));
}