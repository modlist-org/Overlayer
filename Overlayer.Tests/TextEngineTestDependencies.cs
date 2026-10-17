// Only the Unity-facing dependencies are replaced. TextEngineCore and its
// segment/composition/cache implementation are the real linked sources.
namespace Overlayer.Core {
    public static class MainCore {
        private static readonly AsyncLocal<Overlayer.Tests.TestV8Host> host = new();
        public static Overlayer.Tests.TestV8Host V8 { get => host.Value; set => host.Value = value; }
        public static Translator Tr { get; } = new();
        public sealed class Translator {
            public string Get(string key, string fallback) => fallback;
        }
    }
}

namespace Overlayer.Tag.Runtime {
    public sealed class Replacer : IDisposable {
        // Registered tags compile like production (without the shared cache);
        // anything else renders raw unless a test supplies a Getter.
        public TextEngine.Parse.ParsedTag Parsed {
            get;
            set {
                field = value;
                Compiled = Tag.Core.TagManager.TryGet(value.Expression, out var tag)
                    ? Tag.Compile.Compiler.Compile(tag, value)
                    : EmptyDelegates.EmptyCompiled;
            }
        }
        public CompiledPlaceholder Compiled { get; private set; } = EmptyDelegates.EmptyCompiled;
        public Func<string> Getter { get; set; }
        public bool Disposed { get; private set; }
        public string Get() {
            if (Disposed) throw new ObjectDisposedException(nameof(Replacer));
            if (Getter != null) return Getter();
            return Compiled == EmptyDelegates.EmptyCompiled ? Parsed.Raw : Compiled.Get();
        }
        public void Dispose() => Disposed = true;
    }
}

namespace Overlayer.Tag.Core {
    // Registry discovery depends on Unity/module loading; compilation and the
    // cache are real production sources, with a controllable test registry.
    public static class TagManager {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TagCore> tags = new();
        public static bool TryGet(string name, out TagCore tag)
            => (LocalTagSet.Current != null && LocalTagSet.Current.TryGet(name, out tag)) || tags.TryGetValue(name, out tag);
        public static List<TagCore> GetAllTags() => [.. tags.Values];
        public static void Set(TagCore tag) => tags[tag.Name] = tag;
    }
}

namespace Overlayer.Patch.Lazy {
    public static class LazyPatchController {
        public static void Sync(string[] names) { }
    }
}
