using Overlayer.Compat.Interface;
using Overlayer.Core;
using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public sealed class TagCache : IRuntimeTick {
    public static TagCache Instance { get; } = new();

    private class CacheEntry(CompiledPlaceholder compiled) {
        public readonly CompiledPlaceholder Compiled = compiled;
        public int RefCount = 0;
        public long LastReferencedTicks = DateTimeOffset.Now.Ticks;
    }

    private readonly Dictionary<string, CacheEntry> cache = [];
    private readonly object lockObject = new();

    private long lastCleanupTicks = DateTimeOffset.Now.Ticks;
    private static readonly long OneSecondTicks = TimeSpan.FromSeconds(1).Ticks;

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
                cache[key] = new CacheEntry(compiled);
            }
        }
        return compiled;
    }

    public void IncrementRef(string key) { lock(lockObject) { if(cache.TryGetValue(key, out var entry)) entry.RefCount++; } }

    public void DecrementRef(string key) {
        lock(lockObject) {
            if(cache.TryGetValue(key, out var entry)) {
                entry.RefCount--;
                if(entry.RefCount <= 0) {
                    entry.LastReferencedTicks = DateTimeOffset.Now.Ticks;
                }
            }
        }
    }

    public void Clear() { lock(lockObject) { cache.Clear(); } }

    public void Tick() {
        long nowTicks = DateTimeOffset.Now.Ticks;
        if(nowTicks - lastCleanupTicks < OneSecondTicks) {
            return;
        }

        lastCleanupTicks = nowTicks;

        int configSeconds = MainCore.Conf.TagCacheExpirationSeconds;
        if(configSeconds <= 0) {
            return;
        }

        long expirationTicks = TimeSpan.FromSeconds(configSeconds).Ticks;
        List<string> toRemove = [];

        lock(lockObject) {
            foreach(var pair in cache) {
                if(pair.Value.RefCount <= 0 && (nowTicks - pair.Value.LastReferencedTicks > expirationTicks)) {
                    toRemove.Add(pair.Key);
                }
            }

            foreach(var key in toRemove) {
                cache.Remove(key);
            }
        }
    }

    public string GetKey(ParsedTag parsed) => MakeKey(parsed);

    private static string MakeKey(ParsedTag p) =>
        (p.Args == null || p.Args.Length == 0) ? p.Name : string.Concat(p.Name, ":", string.Join(",", p.Args));
}