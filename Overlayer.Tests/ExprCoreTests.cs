using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class ExprCoreTests {
    private static class DummyExprTags {
        public static double GetCombo() => 100.0;
        public static double Score => 500.0;
        public static double Combo => 100.0;
        public static double Acc => 0.985;
        public static string Title => "Galaxy Collapse";
        public static double ComboMultiplier => 1.5;
    }

    private static readonly Dictionary<string, TagCore> Tags = new(StringComparer.OrdinalIgnoreCase) {
        ["GetCombo"] = new TagCore("GetCombo", typeof(DummyExprTags).GetMethod(nameof(DummyExprTags.GetCombo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Score"] = new TagCore("Score", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Score), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Combo"] = new TagCore("Combo", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Combo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Acc"] = new TagCore("Acc", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Acc), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Title"] = new TagCore("Title", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Title), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["ComboMultiplier"] = new TagCore("ComboMultiplier", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.ComboMultiplier), BindingFlags.Public | BindingFlags.Static)!, TagType.None)
    };

    public ExprCoreTests() {
        ExprCore.TagLookupProvider = (string name, out TagCore tag) => Tags.TryGetValue(name, out tag);
    }

    [Fact]
    public void FunctionTag_WithoutParens_InExpression_EvaluatesLikeVariable() {
        var parsed = new ParsedTag("{GetCombo + 20}", "GetCombo + 20", (string)null, 0, 15);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.True(compiled.IsValid);
        Assert.Equal("120", compiled.Get());
    }

    [Fact]
    public void FunctionTag_WithParens_InExpression_Evaluates() {
        var parsed = new ParsedTag("{GetCombo() + 20}", "GetCombo() + 20", (string)null, 0, 17);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.True(compiled.IsValid);
        Assert.Equal("120", compiled.Get());
    }

    [Fact]
    public void FunctionTag_InExpression_WithFormat_EvaluatesFormatted() {
        var parsed = new ParsedTag("{GetCombo + 20:F2}", "GetCombo + 20", "F2", 0, 18);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.True(compiled.IsValid);
        Assert.Equal("120.00", compiled.Get());
    }

    [Fact]
    public void ConstantFolding_EvaluatesInstantly() {
        var parsed = new ParsedTag("{1 + 2 * 3}", "1 + 2 * 3", (string)null, 0, 11);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.True(compiled.IsValid);
        Assert.Equal("7", compiled.Get());
    }

    [Fact]
    public void LegacyJsExpr_ProducesDeprecationWarning() {
        var parsed = new ParsedTag("{JSExpr: 1 + 2}", "JSExpr", "1 + 2", 0, 15);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.Contains(compiled.Diagnostics, d => d.Id == DiagnosticId.LegacyJsExprDeprecated);
        Assert.Equal("3", compiled.Get());
    }

    [Fact]
    public void LegacyJsExpr_WithParens_ProducesDeprecationWarning() {
        var parsed = new ParsedTag("{JSExpr(10 + 20)}", "JSExpr(10 + 20)", (string)null, 0, 17);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.Contains(compiled.Diagnostics, d => d.Id == DiagnosticId.LegacyJsExprDeprecated);
        Assert.Equal("30", compiled.Get());
    }
}
