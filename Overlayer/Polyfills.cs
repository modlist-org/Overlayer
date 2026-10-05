// Polyfills for runtimes without a modern framework surface (old Unity Mono).
//
// The C# compiler emits these marker attributes for modern language features
// (e.g. readonly structs). If the typeref points at the netstandard facade,
// ancient Mono runtimes that lack it die with TypeLoadException while scanning
// metadata (e.g. Harmony PatchAll). Defining them here redirects the emitted
// typeref at our own assembly, which is always loadable.
//
// Only defined where the framework lacks the type; on modern TFMs the
// framework definition is used and this file contributes nothing.
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices {
    // NOTE: intentionally NOT static. The compiler emits this as a real
    // attribute instance, so it needs an accessible parameterless ctor.
    // It is instantiated by reflection (e.g. Harmony scanning) on runtimes
    // that lack the framework type, which is exactly the point.
    internal sealed class IsReadOnlyAttribute : Attribute {
        public IsReadOnlyAttribute() {
        }
    }
}
#endif
