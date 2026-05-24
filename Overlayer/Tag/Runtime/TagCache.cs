using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;

namespace Overlayer.Tag.Runtime;

public static class TagCache {
    private static readonly Dictionary<string, CompiledPlaceholder> Cache = [];

    public static CompiledPlaceholder GetOrCompile(Placeholder placeholder) {
        string key = MakeKey(placeholder);

        if(Cache.TryGetValue(key, out var compiled)) {
            return compiled;
        }

        if(!TagManager.TryGet(placeholder.Name, out var tag)) {
            return CompileResultFactory.Error(DiagnosticId.TagNotFound, placeholder.Name);
        }

        compiled = Compiler.Compile(tag, placeholder);

        if(compiled.IsValid) {
            Cache[key] = compiled;
        }

        return compiled;
    }

    public static void Clear() => Cache.Clear();

    private static string MakeKey(Placeholder p) {
        var args = p.Args;
        if(args == null || args.Length == 0) {
            return p.Name;
        }

        return string.Concat(p.Name, ":", string.Join(",", args));
    }
}