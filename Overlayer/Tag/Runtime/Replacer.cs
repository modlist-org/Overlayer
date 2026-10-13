using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public sealed class Replacer : IDisposable {
    private string cachedKey;

    public CompiledPlaceholder Compiled { get; private set; } = EmptyDelegates.EmptyCompiled;

    public ParsedTag Parsed {
        get;
        set {
            string newKey = TagCache.Instance.GetKey(value);
            if (newKey != null && newKey == cachedKey) {
                field = value;
                return;
            }

            string previousKey = cachedKey;
            var compiled = TagCache.Instance.Acquire(value, out newKey);
            field = value;
            Compiled = compiled;
            cachedKey = newKey;
            if (previousKey != null) TagCache.Instance.DecrementRef(previousKey);
        }
    }

    public void Dispose() {
        if (cachedKey != null) {
            TagCache.Instance.DecrementRef(cachedKey);
            cachedKey = null;
        }
    }

    public bool IsValid => Compiled.IsValid;

    public string Get() => Compiled.Get();
}
