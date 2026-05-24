using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public sealed class Replacer {
    public CompiledPlaceholder Compiled { get; private set; } = EmptyDelegates.EmptyCompiled;

    public ParsedTag Parsed {
        get;
        set {
            if(field.Equals(value)) {
                return;
            }

            field = value;
            Compiled = TagCache.GetOrCompile(field);
        }
    }

    public bool IsValid => Compiled.IsValid;

    public string Get() => Compiled.Get();
}