using Overlayer.Tag.Compile;
using Xunit;

namespace Overlayer.Tests;

public sealed class FormatValidatorTests {
    [Theory]
    [InlineData("N0")]
    [InlineData("F2")]
    [InlineData("P1")]
    public void NumericTypes_AcceptStandardFormats(string format) {
        Assert.True(FormatValidator.TryValidate(typeof(double), format, out _));
        Assert.True(FormatValidator.TryValidate(typeof(int), format, out _));
        Assert.True(FormatValidator.TryValidate(typeof(long), format, out _));
    }

    [Fact]
    public void DateTime_AcceptsDatePattern() {
        Assert.True(FormatValidator.TryValidate(typeof(DateTime), "yyyy-MM-dd", out _));
    }

    [Fact]
    public void StringType_RejectsNumericFormat() {
        Assert.False(FormatValidator.TryValidate(typeof(string), "N0", out _));
    }

    [Fact]
    public void EmptyFormat_AcceptsAnyType() {
        Assert.True(FormatValidator.TryValidate(typeof(string), "", out _));
        Assert.True(FormatValidator.TryValidate(typeof(double), null, out _));
        Assert.True(FormatValidator.TryValidateJs(null, out _));
        Assert.True(FormatValidator.TryValidateJs("", out _));
    }

    [Theory]
    [InlineData("N0")]
    [InlineData("F2")]
    [InlineData("D5")]
    [InlineData("yyyy-MM-dd")]
    public void Js_AcceptsNumericAndDateFormats(string format) {
        Assert.True(FormatValidator.TryValidateJs(format, out _));
    }

    [Fact]
    public void FormatObject_Double_AppliesFormat() {
        Assert.Equal(12345.0d.ToString("F2"), FormatValidator.FormatObject(12345.0d, "F2"));
    }

    [Fact]
    public void FormatObject_NullAndUndefined_ReturnEmpty() {
        Assert.Equal("", FormatValidator.FormatObject(null, "N2"));
        Assert.Equal("", FormatValidator.FormatObject(Microsoft.ClearScript.Undefined.Value, "N2"));
    }

    [Fact]
    public void FormatObject_NonFormattable_FallsBackToToString() {
        Assert.Equal("FullCombo", FormatValidator.FormatObject("FullCombo", "N2"));
    }

    [Fact]
    public void FormatObject_NullFormat_ReturnsToString() {
        Assert.Equal("200", FormatValidator.FormatObject(200, null));
    }

    [Fact]
    public void StringifyObject_HandlesNullUndefinedAndValue() {
        Assert.Equal("", FormatValidator.StringifyObject(null));
        Assert.Equal("", FormatValidator.StringifyObject(Microsoft.ClearScript.Undefined.Value));
        Assert.Equal("200", FormatValidator.StringifyObject(200));
    }
}
