using System.Text.RegularExpressions;

namespace Overlayer.V8.Scripting.Tag;

internal static class JSScriptPreprocessor {
    private static readonly Regex ImplImportPattern = new(
        "^[\\t ]*import[\\t ]+[\"']\\.\\.?/impl(?:\\.js)?[\"'][\\t ]*;?[\\t ]*(?://[^\\r\\n]*)?(?:\\r?\\n|$)",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    public static string RemoveImplImports(string source) {
        if(string.IsNullOrEmpty(source)) {
            return source;
        }

        return ImplImportPattern.Replace(source, string.Empty);
    }
}
