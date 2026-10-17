using Overlayer.Tag.Runtime;

namespace Overlayer.Tag.Core;

/// <summary>Immutable set of tags visible to one text only.</summary>
public sealed class LocalTagSet(Dictionary<string, TagCore> tags) {
    private static long nextId;
    public readonly long Id = Interlocked.Increment(ref nextId);
    public readonly Dictionary<string, TagCore> Tags = tags;

    /// <summary>Local tags of the text being compiled or rendered on this thread; they shadow global tags.</summary>
    [ThreadStatic] internal static LocalTagSet Current;

    public bool TryGet(string name, out TagCore tag) => Tags.TryGetValue(name, out tag);
}

/// <summary>
/// Collects <c>RegisterTag</c> calls made while a text's Fx code runs, so
/// those tags resolve only in that text. Fx code runs every frame: an
/// unchanged registration is a no-op and <see cref="Tags"/> keeps its
/// reference, so the text only recompiles when a local tag actually changes.
/// </summary>
public sealed class LocalTagScope {
    [ThreadStatic] internal static LocalTagScope Current;

    /// <summary>A template placeholder like <c>{RegisterTag("Name", () => ...)}</c>.</summary>
    internal static bool IsRegistration(string expression)
        => expression != null && expression.TrimStart().StartsWith("RegisterTag(", StringComparison.Ordinal);

    private readonly Dictionary<string, (string Source, long Generation, TagCore Tag)> entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private bool dirty;

    public LocalTagSet Tags { get; private set; }

    internal void Begin() {
        seen.Clear();
        Current = this;
    }

    // completed=false (eval failed / backed off) keeps the previous tags.
    internal void End(bool completed) {
        Current = null;
        if (completed) {
            foreach (var name in entries.Keys.Where(name => !seen.Contains(name)).ToList()) {
                entries.Remove(name);
                dirty = true;
            }
        }
        if (dirty) {
            dirty = false;
            Tags = entries.Count == 0 ? null : new LocalTagSet(entries.ToDictionary(e => e.Key, e => e.Value.Tag, StringComparer.Ordinal));
        }
    }

    // ponytail: keyed on function source only; changing just the options object keeps the old tag until the function text changes.
    internal bool IsUnchanged(string name, string source) {
        seen.Add(name);
        // A new cache generation means V8 was reloaded: the old function is dead.
        return entries.TryGetValue(name, out var e) && e.Source == source && e.Generation == TagCache.Instance.Generation;
    }

    internal void Set(string name, string source, TagCore tag) {
        seen.Add(name);
        entries[name] = (source, TagCache.Instance.Generation, tag);
        dirty = true;
    }
}
