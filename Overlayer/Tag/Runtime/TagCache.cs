using FuzzySharp;
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
            string suggestion = FindSuggestion(parsed.Name);
            return new CompiledPlaceholder(() => parsed.Raw, [
                new CompileDiagnostic(
                    DiagnosticId.TagNotFound,
                    CompileSeverity.Error,
                    new(parsed.Name, parsed.Index, parsed.Length),
                    suggestion == null ? [parsed.Name] : [parsed.Name, suggestion]
                )
            ]);
        }

        string key = MakeKey(parsed);
        lock(lockObject) {
            if(cache.TryGetValue(key, out var entry)) {
                return WithContext(entry.Compiled, parsed);
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

    private static string FindSuggestion(string name) {
        if(string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        var (Name, Score) = TagManager.GetAllTags()
            .Select(tag => (tag.Name, Score: Fuzz.WeightedRatio(name, tag.Name)))
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        return Score >= 70 ? Name : null;
    }

    private static CompiledPlaceholder WithContext(CompiledPlaceholder compiled, ParsedTag parsed) {
        if(compiled.Diagnostics.Length == 0) {
            return compiled;
        }

        var context = new DiagnosticContext(parsed.Name, parsed.Index, parsed.Length);
        return new CompiledPlaceholder(compiled.Delegate, [.. compiled.Diagnostics.Select(d =>
            new CompileDiagnostic(d.Id, d.Severity, context, d.Data)
        )]);
    }
}
