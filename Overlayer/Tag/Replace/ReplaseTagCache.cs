namespace Overlayer.Tag.Replace;

public static class ReplaseTagCache {
    private static readonly Dictionary<Placeholder, CompiledPlaceholder> Cache = [];

    public static CompiledPlaceholder CacheAndGet(Placeholder placeholder) {
        if(Cache.TryGetValue(placeholder, out var compiled)) {
            return compiled;
        }

        compiled = Wrapper.Wrap(placeholder);

        Cache[placeholder] = compiled;

        return compiled;
    }

    public static void Clear() => Cache.Clear();
}