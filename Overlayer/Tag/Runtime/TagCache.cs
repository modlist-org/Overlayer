using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Runtime;

public static class TagCache {
    private static readonly Dictionary<string, CompiledPlaceholder> Cache = [];

    public static CompiledPlaceholder GetOrCompile(ParsedTag parsed) {
        if(!TagManager.TryGet(parsed.Name, out var tag)) {
            return new CompiledPlaceholder(
                () => parsed.Raw, [
                    new CompileDiagnostic(
                        DiagnosticId.TagNotFound,
                        CompileSeverity.Error,
                        new(parsed.Name, parsed.Index, parsed.Length),
                        [parsed.Name]
                    )
                ]
            );
        }

        string key = MakeKey(parsed);

        if(Cache.TryGetValue(key, out var compiled)) {
            return compiled;
        }

        if((tag.TagType & TagType.Advanced) != 0) {
            compiled = AdvancedCompiler.Compile(tag, parsed);
        } else {
            compiled = Compiler.Compile(tag, parsed);
        }

        if(compiled.IsValid) {
            Cache[key] = compiled;
        }

        return compiled;
    }

    public static void Clear() => Cache.Clear();

    private static string MakeKey(ParsedTag p) {
        if(p.Args == null || p.Args.Length == 0) {
            return p.Name;
        }

        return string.Concat(p.Name, ":", string.Join(",", p.Args));
    }
}