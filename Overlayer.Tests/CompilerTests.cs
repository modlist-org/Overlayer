using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.TextEngine.Parse;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class CompilerTests {
    private static class DummyTags {
        public static double FullComboValue => 1557.0;
        public static string MissValue => "FullCombo";
        public static double GetCombo() => 1557.0;
    }

    private static TagCore PropertyTag(string name, string property, TagType type)
        => new(name, typeof(DummyTags).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!, type);

    [Fact]
    public void NumericProperty_WithFormat_RendersFormatted() {
        var tag = PropertyTag("FullCombo", nameof(DummyTags.FullComboValue), TagType.None);
        var compiled = Compiler.Compile(tag, new ParsedTag("{FullCombo:F1}", "FullCombo", ["F1"], 0, 14));

        Assert.True(compiled.IsValid);
        Assert.Equal(1557.0d.ToString("F1"), compiled.Get());
    }

    [Fact]
    public void NumericProperty_WithoutFormat_RendersToString() {
        var tag = PropertyTag("FullCombo", nameof(DummyTags.FullComboValue), TagType.None);
        var compiled = Compiler.Compile(tag, new ParsedTag("{FullCombo}", "FullCombo", [], 0, 11));

        Assert.True(compiled.IsValid);
        Assert.Equal(1557.0d.ToString(), compiled.Get());
    }

    [Fact]
    public void BlockedTag_RendersRawWhenNotPlaying() {
        var prop = typeof(DummyTags).GetProperty(nameof(DummyTags.FullComboValue), BindingFlags.Public | BindingFlags.Static)!;
        var tag = new TagCore("FullCombo", prop, TagType.BlockOnNotPlaying);
        var compiled = Compiler.Compile(tag, new ParsedTag("{FullCombo:F1}", "FullCombo", ["F1"], 0, 14));

        Assert.True(compiled.IsValid);
        Assert.Equal("{FullCombo:F1}", compiled.Get());
    }

    [Fact]
    public void StringProperty_WithFormat_IsInvalidAndFallsBackToRaw() {
        var tag = PropertyTag("Miss", nameof(DummyTags.MissValue), TagType.None);
        var compiled = Compiler.Compile(tag, new ParsedTag("{Miss:N0}", "Miss", ["N0"], 0, 9));

        Assert.False(compiled.IsValid);
        Assert.Equal("{Miss:N0}", compiled.Get());
    }

    private static TagCore MethodTag(string name, string method, TagType type)
        => new(name, typeof(DummyTags).GetMethod(method, BindingFlags.Public | BindingFlags.Static)!, type);

    [Fact]
    public void MethodTag_WithoutParens_InvokesLikeVariable() {
        var tag = MethodTag("GetCombo", nameof(DummyTags.GetCombo), TagType.None);
        var compiled = Compiler.Compile(tag, new ParsedTag("{GetCombo}", "GetCombo", (string)null, 0, 10));

        Assert.True(compiled.IsValid);
        Assert.Equal("1557", compiled.Get());
    }

    [Fact]
    public void MethodTag_WithParens_InvokesNormally() {
        var tag = MethodTag("GetCombo", nameof(DummyTags.GetCombo), TagType.None);
        var compiled = Compiler.Compile(tag, new ParsedTag("{GetCombo()}", "GetCombo()", (string)null, 0, 12));

        Assert.True(compiled.IsValid);
        Assert.Equal("1557", compiled.Get());
    }
}
