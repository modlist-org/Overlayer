using Newgrounds;
using Overlayer.Core;
using Overlayer.Tag.Core;

namespace Overlayer.Tag.Runtime;

public sealed class Replacer {
    public CompiledPlaceholder Compiled { get; private set; } = EmptyDelegates.EmptyCompiled;

    public Placeholder Placeholder {
        get;
        set {
            if(field.Equals(value)) {
                return;
            }

            field = value;
            Compiled = TagCache.GetOrCompile(value);
        }
    }

    public bool IsValid => Compiled.IsValid;

    public string Get() => Compiled.Get();
}