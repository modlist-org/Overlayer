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

    private long lastCleanupTicks = DateTimeOffset.Now.Ticks;
    private static readonly long OneSecondTicks = TimeSpan.FromSeconds(1).Ticks;

    public CompiledPlaceholder GetOrCompile(ParsedTag parsed) {
        if(!TagManager.TryGet(parsed.Name, out var tag)) {
            return new CompiledPlaceholder(
                () => parsed.Raw, [
                    new CompileDiagnostic(
                        DiagnosticId.TagNotFound,
                        CompileSeverity.Error,
                        new(parsed.Name, parsed.Index, parsed.Length),
                        [parsed.Name]
                    )
                ]
            );
        }

        string key = MakeKey(parsed);

        if(cache.TryGetValue(key, out var entry)) {
            return entry.Compiled;
        }

        CompiledPlaceholder compiled;
        if((tag.TagType & TagType.Advanced) != 0) {
            compiled = AdvancedCompiler.Compile(tag, parsed);
        } else {
            compiled = Compiler.Compile(tag, parsed);
        }

        if(compiled.IsValid) {
            cache[key] = new CacheEntry(compiled);
        }

        return compiled;
    }

    public void IncrementRef(string key) {
        if(cache.TryGetValue(key, out var entry)) {
            entry.RefCount++;
        }
    }

    public void DecrementRef(string key) {
        if(cache.TryGetValue(key, out var entry)) {
            entry.RefCount--;
            if(entry.RefCount <= 0) {
                entry.LastReferencedTicks = DateTimeOffset.Now.Ticks;
            }
        }
    }

    public void Clear() => cache.Clear();

    public void Tick() {
        if(cache.Count == 0) {
            return;
        }

        long nowTicks = DateTimeOffset.Now.Ticks;
        if(nowTicks - lastCleanupTicks < OneSecondTicks) {
            return;
        }

        lastCleanupTicks = nowTicks;

        int configSeconds = 5;
        if(configSeconds <= 0) {
            return;
        }

        long expirationTicks = TimeSpan.FromSeconds(configSeconds).Ticks;
        List<string> toRemove = null;

        foreach(var pair in cache) {
            if(pair.Value.RefCount <= 0 && (nowTicks - pair.Value.LastReferencedTicks > expirationTicks)) {
                toRemove ??= [];
                toRemove.Add(pair.Key);
            }
        }

        if(toRemove != null) {
            for(int i = 0; i < toRemove.Count; i++) {
                cache.Remove(toRemove[i]);
            }
        }
    }

    public string GetKey(ParsedTag parsed) => MakeKey(parsed);

    private static string MakeKey(ParsedTag p) {
        if(p.Args == null || p.Args.Length == 0) {
            return p.Name;
        }
        return string.Concat(p.Name, ":", string.Join(",", p.Args));
    }
}