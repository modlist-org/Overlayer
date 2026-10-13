using Overlayer.TextEngine.Parse;
using Xunit;

namespace Overlayer.Tests;

public sealed class ParserTests {
    [Fact]
    public void PlainText_ReturnsNoTags() {
        Assert.Empty(Parser.Parse("dropped the beat and died"));
    }

    [Fact]
    public void SimpleTag_ParsesExpression() {
        var tags = Parser.Parse("{Bpm}");
        var tag = Assert.Single(tags);
        Assert.Equal("Bpm", tag.Expression);
        Assert.Null(tag.Format);
    }

    [Fact]
    public void FormattedTag_ParsesExprAndFormat() {
        var tags = Parser.Parse("{CurHit:F2}");
        var tag = Assert.Single(tags);
        Assert.Equal("CurHit", tag.Expression);
        Assert.Equal("F2", tag.Format);
    }

    [Fact]
    public void MathExpression_WithFormat() {
        var tags = Parser.Parse("{Bpm * 1.5:F1}");
        var tag = Assert.Single(tags);
        Assert.Equal("Bpm * 1.5", tag.Expression);
        Assert.Equal("F1", tag.Format);
    }

    [Fact]
    public void TernaryOperator_InParens_PreservesFormat() {
        var tags = Parser.Parse("{(Combo > 100 ? 2 : 1):F0}");
        var tag = Assert.Single(tags);
        Assert.Equal("(Combo > 100 ? 2 : 1)", tag.Expression);
        Assert.Equal("F0", tag.Format);
    }

    [Fact]
    public void TimeFormat_PreservesColons() {
        var tags = Parser.Parse("{Time:HH:mm:ss}");
        var tag = Assert.Single(tags);
        Assert.Equal("Time", tag.Expression);
        Assert.Equal("HH:mm:ss", tag.Format);
    }

    [Fact]
    public void MultipleTags_ReturnsInOrder() {
        var tags = Parser.Parse("{FullCombo} nice {Miss:F0}");
        Assert.Equal(2, tags.Count);
        Assert.Equal("FullCombo", tags[0].Expression);
        Assert.Null(tags[0].Format);
        Assert.Equal("Miss", tags[1].Expression);
        Assert.Equal("F0", tags[1].Format);
    }

    [Fact]
    public void NativeScanner_DirectSpanScan_ReturnsExpectedSpans() {
        Assert.True(Overlayer.Native.O5Native.IsAvailable);
        Span<Overlayer.Native.OvTagSpan> spans = stackalloc Overlayer.Native.OvTagSpan[16];
        int count = Overlayer.Native.OvTagScanner.Scan("{Bpm:F2} text {2 + 3 * 4}", spans);
        Assert.Equal(2, count);

        // First span: {Bpm:F2}
        Assert.Equal(0u, spans[0].RawStart);
        Assert.Equal(8u, spans[0].RawLen);
        Assert.Equal(3u, spans[0].ExprLen);
        Assert.Equal(2u, spans[0].FmtLen);

        // Second span: {2 + 3 * 4}
        Assert.Equal(9u, spans[1].ExprLen);
        Assert.Equal(0u, spans[1].FmtLen);
    }

    [Fact]
    public void PureMathExpression_EvaluatesDirectly() {
        var tags = Parser.Parse("{2 + 3 * 4}");
        var tag = Assert.Single(tags);
        Assert.Equal("2 + 3 * 4", tag.Expression);
        Assert.Null(tag.Format);

        // Native O5Expr engine evaluates arithmetic to 14
        long h = Overlayer.Native.O5Expr.Compile(tag.Expression);
        Assert.True(h > 0);
        Assert.True(Overlayer.Native.O5Expr.TryEval(h, out double val));
        Assert.Equal(14.0, val);
        Overlayer.Native.O5Expr.Free(h);
    }

    [Fact]
    public void FastFormatter_NativeZig_FormatsCorrectly() {
        Assert.True(Overlayer.Native.O5Fmt.IsSupported);

        Assert.Equal("1234.57", Overlayer.Native.O5Fmt.FormatString(1234.567, "F2"));
        Assert.Equal("1,234,567.89", Overlayer.Native.O5Fmt.FormatString(1234567.89, "N2"));
        Assert.Equal("0042", Overlayer.Native.O5Fmt.FormatString(42.0, "D4"));
        Assert.Equal("85.60%", Overlayer.Native.O5Fmt.FormatString(0.856, "P2"));
        Assert.Equal("00FF", Overlayer.Native.O5Fmt.FormatString(255.0, "X4"));
        Assert.Equal("12.34", Overlayer.Native.O5Fmt.FormatString(12.34, "G"));
        Assert.Equal("1.23E+003", Overlayer.Native.O5Fmt.FormatString(1234.5, "E2"));

        // Custom formats: 0, #, ., ,, %
        Assert.Equal("5.00", Overlayer.Native.O5Fmt.FormatString(5.0, "0.00"));
        Assert.Equal("5.1", Overlayer.Native.O5Fmt.FormatString(5.1, "0.##"));
        Assert.Equal("1,234,567", Overlayer.Native.O5Fmt.FormatString(1234567.0, "#,##0"));
        Assert.Equal("007", Overlayer.Native.O5Fmt.FormatString(7.0, "000"));
        Assert.Equal("12.3%", Overlayer.Native.O5Fmt.FormatString(0.123, "0.0%"));
    }

    [Fact]
    public void UnifiedTagExecution_EvaluatesThroughExprCore() {
        // Pure math constant folding
        var tags = Parser.Parse("{2 + 3 * 4}");
        var compiled = Overlayer.Tag.Core.ExprCore.CompilePlaceholder(tags[0]);
        Assert.Equal("14", compiled.Get());

        // Math expression with format
        var tagsWithFmt = Parser.Parse("{2 + 3 * 4:F2}");
        var compiledWithFmt = Overlayer.Tag.Core.ExprCore.CompilePlaceholder(tagsWithFmt[0]);
        Assert.Equal("14.00", compiledWithFmt.Get());

        // Math expression with D4 format
        var padTags = Parser.Parse("{40 + 2:D4}");
        var padCompiled = Overlayer.Tag.Core.ExprCore.CompilePlaceholder(padTags[0]);
        Assert.Equal("0042", padCompiled.Get());
    }

    [Fact]
    public void LegacyJSExpr_EmitsWarning_AndEvaluatesSafely() {
        var tags = Parser.Parse("{JSExpr: 2 + 3 * 4}");
        var tag = Assert.Single(tags);

        var compiled = Overlayer.Tag.Core.ExprCore.CompilePlaceholder(tag);
        Assert.True(compiled.IsValid);
        Assert.Equal("14", compiled.Get());

        // Verify deprecation warning diagnostic was produced
        Assert.Contains(compiled.Diagnostics, d => d.Id == Overlayer.Tag.Diagnostics.DiagnosticId.LegacyJsExprDeprecated);
        Assert.Contains(compiled.Diagnostics, d => d.Severity == Overlayer.Tag.Diagnostics.CompileSeverity.Warning);
    }
}
