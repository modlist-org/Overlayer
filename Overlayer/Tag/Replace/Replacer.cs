namespace Overlayer.Tag.Replace;

public sealed class Replacer {
    private Placeholder placeholder;

    public CompiledPlaceholder Compiled { get; private set; } = new(static () => string.Empty);

    public Placeholder Placeholder {
        get => placeholder;
        set {
            if(Equals(placeholder, value)) {
                return;
            }

            placeholder = value;

            Compiled = ReplaseTagCache.CacheAndGet(value);
        }
    }

    public bool IsValid => Compiled.IsValid;

    public Exception Error => Compiled.Error;

    public string Get() => Compiled.Get();
}