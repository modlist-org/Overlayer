using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public sealed class Replacer : IDisposable {
    private string cachedKey;

    public CompiledPlaceholder Compiled { get; private set; } = EmptyDelegates.EmptyCompiled;

    public ParsedTag Parsed {
        get;
        set {
            if(field.Equals(value)) {
                return;
            }

            if(cachedKey != null) {
                TagCache.Instance.DecrementRef(cachedKey);
            }

            field = value;
            Compiled = TagCache.Instance.GetOrCompile(field);

            cachedKey = TagCache.Instance.GetKey(field);
            if(cachedKey != null) {
                TagCache.Instance.IncrementRef(cachedKey);
            }
        }
    }

    public void Dispose() {
        if(cachedKey != null) {
            TagCache.Instance.DecrementRef(cachedKey);
            cachedKey = null;
        }
    }

    public bool IsValid => Compiled.IsValid;

    public string Get() => Compiled.Get();
}