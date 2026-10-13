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
    private readonly Dictionary<string, CacheEntry> retired = [];
    private readonly Dictionary<string, int> activeTagCounts = [];
    private readonly object lockObject = new();
    private long generation;
    internal long Generation { get { lock (lockObject) return generation; } }

    public CompiledPlaceholder GetOrCompile(ParsedTag parsed) {
        return GetOrCompile(parsed, out _);
    }

    public CompiledPlaceholder GetOrCompile(ParsedTag parsed, out string key) {
        long epoch;
        lock (lockObject) {
            epoch = generation;
            key = MakeKey(parsed, epoch);
            if (cache.TryGetValue(key, out var entry)) {
                return WithContext(entry.Compiled, parsed);
            }
        }

        CompiledPlaceholder compiled;
        string expr = parsed.Expression;
        string lookupName = expr.EndsWith("()") ? expr[..^2].Trim() : expr;

        if (TagManager.TryGet(lookupName, out var tag) && (tag.RequiredParameterCount == 0 || expr.EndsWith("()"))) {
            compiled = (tag.TagType & TagType.Advanced) != 0
                ? AdvancedCompiler.Compile(tag, parsed)
                : Compiler.Compile(tag, parsed);
        } else {
            compiled = ExprCore.CompilePlaceholder(parsed);
            if (!compiled.IsValid) {
                string suggestion = FindSuggestion(parsed.Expression);
                compiled = new CompiledPlaceholder(() => parsed.Raw, [
                    new CompileDiagnostic(
                        DiagnosticId.TagNotFound,
                        CompileSeverity.Error,
                        new(parsed.Expression, parsed.Index, parsed.Length),
                        suggestion == null ? [parsed.Expression] : [parsed.Expression, suggestion]
                    )
                ]);
            }
        }

        lock (lockObject) {
            // Never publish an old V8 binding into a new cache generation.
            if (generation != epoch) return compiled;
            if (!cache.TryGetValue(key, out var entry)) {
                cache[key] = new CacheEntry(compiled);
            }
            return WithContext(cache[key].Compiled, parsed);
        }
    }

    public CompiledPlaceholder Acquire(ParsedTag parsed, out string key) {
        while (true) {
            var compiled = GetOrCompile(parsed, out key);
            string[] changed = null;
            lock (lockObject) {
                if (key != MakeKey(parsed, generation)) continue;
                if (!cache.TryGetValue(key, out var entry)) cache[key] = entry = new CacheEntry(compiled);
                entry.RefCount++;
                if (entry.RefCount == 1) changed = TrackActive(KeyToTagName(key), 1);
            }
            NotifyLazy(changed);
            return compiled;
        }
    }

    public void IncrementRef(string key) {
        string[] changed = null;
        lock (lockObject) {
            if (cache.TryGetValue(key, out var entry) || retired.TryGetValue(key, out entry)) {
                entry.RefCount++;
                if (entry.RefCount == 1) {
                    changed = TrackActive(KeyToTagName(key), 1);
                }
            }
        }
        NotifyLazy(changed);
    }

    public void DecrementRef(string key) {
        string[] changed = null;
        lock (lockObject) {
            if (cache.TryGetValue(key, out var entry) || retired.TryGetValue(key, out entry)) {
                entry.RefCount--;
                if (entry.RefCount <= 0) {
                    cache.Remove(key);
                    retired.Remove(key);
                    changed = TrackActive(KeyToTagName(key), -1);
                }
            }
        }
        NotifyLazy(changed);
    }

    public void Clear() {
        lock (lockObject) {
            // Invalidation prevents reuse, but live snapshots still own their
            // bindings and lazy-patch dependencies until their leases finish.
            foreach (var pair in cache) {
                if (pair.Value.RefCount > 0) retired.Add(pair.Key, pair.Value);
            }
            cache.Clear();
            generation++;
        }
    }

    public IReadOnlyCollection<string> GetActiveTagNames() {
        lock (lockObject) {
            return [.. activeTagCounts.Keys];
        }
    }

    private string[] TrackActive(string tagName, int delta) {
        if (string.IsNullOrEmpty(tagName)) {
            return null;
        }
        activeTagCounts.TryGetValue(tagName, out int count);
        count += delta;
        if (count <= 0) {
            activeTagCounts.Remove(tagName);
        } else {
            activeTagCounts[tagName] = count;
        }
        return [.. activeTagCounts.Keys];
    }

    private static void NotifyLazy(string[] activeTags) {
        if (activeTags == null) {
            return;
        }
        try {
            Overlayer.Patch.Lazy.LazyPatchController.Sync(activeTags);
        } catch {
        }
    }

    private static string KeyToTagName(string key) {
        if (string.IsNullOrEmpty(key)) {
            return null;
        }
        int start = key.IndexOf('\0') + 1;
        int end = key.IndexOf('\0', start);
        string name = end < 0 ? key[start..] : key.Substring(start, end - start);
        return name.EndsWith("()", StringComparison.Ordinal) ? name[..^2].Trim() : name;
    }

    public string GetKey(ParsedTag parsed) {
        lock (lockObject) return MakeKey(parsed, generation);
    }

    private static string MakeKey(ParsedTag p, long epoch) => string.Concat(epoch.ToString(), "\0", p.Expression, "\0", p.Format);

    private static string FindSuggestion(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        var (Name, Score) = TagManager.GetAllTags()
            .Where(tag => !tag.IsHidden)
            .Select(tag => (tag.Name, Score: Fuzz.WeightedRatio(name, tag.Name)))
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        return Score >= 70 ? Name : null;
    }

    private static CompiledPlaceholder WithContext(CompiledPlaceholder compiled, ParsedTag parsed) {
        if (compiled.Diagnostics.Length == 0) {
            return compiled;
        }

        var context = new DiagnosticContext(parsed.Name, parsed.Index, parsed.Length);
        return new CompiledPlaceholder(compiled.Delegate, [.. compiled.Diagnostics.Select(d =>
            new CompileDiagnostic(d.Id, d.Severity, context, d.Data)
        )], compiled.Js);
    }
}
