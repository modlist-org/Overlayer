using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

[Collection("ExprCore.TagLookupProvider")]
public sealed class ExprCoreTests {
    private static class DummyExprTags {
        public static double GetCombo() => 100.0;
        public static double Score => 500.0;
        public static double Combo => 100.0;
        public static double Acc => 0.985;
        public static string Title => "Galaxy Collapse";
        public static double ComboMultiplier => 1.5;
        public static int Second => 40;
    }

    private static readonly Dictionary<string, TagCore> Tags = new(StringComparer.OrdinalIgnoreCase) {
        ["GetCombo"] = new TagCore("GetCombo", typeof(DummyExprTags).GetMethod(nameof(DummyExprTags.GetCombo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Score"] = new TagCore("Score", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Score), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Combo"] = new TagCore("Combo", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Combo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Acc"] = new TagCore("Acc", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Acc), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Title"] = new TagCore("Title", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Title), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["ComboMultiplier"] = new TagCore("ComboMultiplier", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.ComboMultiplier), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Second"] = new TagCore("Second", typeof(DummyExprTags).GetProperty(nameof(DummyExprTags.Second), BindingFlags.Public | BindingFlags.Static)!, TagType.None)
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

    [Theory]
    [InlineData("{round(Second / 60 * 360)}", "round(Second / 60 * 360)", null, "240")]
    [InlineData("{Round(Second / 60 * 360)}", "Round(Second / 60 * 360)", null, "240")]
    [InlineData("{round(1.234, 2)}", "round(1.234, 2)", null, "1.23")]
    [InlineData("{clamp(Score, 0, 144):0}", "clamp(Score, 0, 144)", "0", "144")]
    [InlineData("{lerp(0, 100, 0.25)}", "lerp(0, 100, 0.25)", null, "25")]
    [InlineData("{round(sin(deg2rad(90)) * 100)}", "round(sin(deg2rad(90)) * 100)", null, "100")]
    [InlineData("{round(rad2deg(pi))}", "round(rad2deg(pi))", null, "180")]
    [InlineData("{mod(-1, 4)}", "mod(-1, 4)", null, "3")]
    [InlineData("{fmod(-5, 3)}", "fmod(-5, 3)", null, "-2")]
    [InlineData("{step(10, 15)}", "step(10, 15)", null, "1")]
    [InlineData("{step(10, 5)}", "step(10, 5)", null, "0")]
    [InlineData("{smoothstep(0, 10, 5)}", "smoothstep(0, 10, 5)", null, "0.5")]
    [InlineData("{exp2(3)}", "exp2(3)", null, "8")]
    [InlineData("{round(ln(e))}", "round(ln(e))", null, "1")]
    [InlineData("{log(100, 10)}", "log(100, 10)", null, "2")]
    [InlineData("{1e2 + round(2.6)}", "1e2 + round(2.6)", null, "103")]
    public void BareMathFunctions_EvaluateCorrectly(string raw, string expr, string fmt, string expected) {
        var parsed = new ParsedTag(raw, expr, fmt, 0, raw.Length);
        var compiled = ExprCore.CompilePlaceholder(parsed);
        Assert.True(compiled.IsValid);
        Assert.Equal(expected, compiled.Get());
    }

    [Fact]
    public void NormalizeBareMathForJs_PreservesStringsMembersAndScientificNotation() {
        Assert.Equal("'round(1)' + Math.round(2.4)", ExprCore.NormalizeBareMathForJs("'round(1)' + round(2.4)", true));
        Assert.Equal("obj.round(2.4) + 1e2 + Math.PI", ExprCore.NormalizeBareMathForJs("obj.round(2.4) + 1e2 + pi", true));
        Assert.Equal("function round(x) { return Math.round(x); }", ExprCore.NormalizeBareMathForJs("function round(x) { return round(x); }", true));
    }
}
