using Microsoft.ClearScript;
using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class SignatureResolverTests {
    private static class DummyTags {
        public static double FullComboValue => 12345.0;
        public static string MissValue => "GaugeFail";
        public static double Stream(double a, double b) => a + b;
    }

    private static TagCore MethodTag(string name, string method, TagType type)
        => new(name, typeof(DummyTags).GetMethod(method, BindingFlags.Public | BindingFlags.Static)!, type);

    private static TagCore PropertyTag(string name, string property, TagType type)
        => new(name, typeof(DummyTags).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!, type);

    private static TagCore JsTag(string name, string[] paramNames, TagType type)
        => new(name, (ScriptObject)null, paramNames, type);

    private static ResolvedSignature Resolve(TagCore tag, string[] args, List<CompileDiagnostic> diag)
        => SignatureResolver.Resolve(tag, new Placeholder(tag.Name, args), diag, new DiagnosticContext(tag.Name, 0, 8));

    [Fact]
    public void ZeroParam_Numeric_FormatIsValid() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(PropertyTag("FullCombo", nameof(DummyTags.FullComboValue), TagType.None), ["N0"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("N0", sig.Format);
        Assert.Empty(diag);
    }

    [Fact]
    public void ZeroParam_String_FormatFails() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(PropertyTag("Miss", nameof(DummyTags.MissValue), TagType.None), ["N0"], diag);

        Assert.False(sig.IsExecutable);
        Assert.Contains(diag, d => d.Id == DiagnosticId.FormatFail);
    }

    [Fact]
    public void Method_WithFlag_SplitsLastArgAsFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Stream", nameof(DummyTags.Stream), TagType.ProcessFormat), ["200", "174", "N2"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("N2", sig.Format);
        Assert.Equal(["200", "174"], sig.Args);
    }

    [Fact]
    public void Method_WithoutFlag_KeepsAllArgsAsValues() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Stream", nameof(DummyTags.Stream), TagType.None), ["200", "174"], diag);

        Assert.True(sig.IsExecutable);
        Assert.False(sig.HasFormat);
        Assert.Equal(2, sig.Args.Length);
    }

    [Fact]
    public void Js_ZeroParam_FormatIsValid() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(JsTag("Offbeat", [], TagType.None), ["N2"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("N2", sig.Format);
    }

    [Fact]
    public void Js_WithParams_WithFlag_SplitsLastArgAsFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(JsTag("Knob", ["left", "right"], TagType.ProcessFormat), ["200", "174", "N2"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("N2", sig.Format);
        Assert.Equal(["200", "174"], sig.Args);
    }

    [Fact]
    public void Js_WithParams_WithoutFlag_KeepsAllArgsAsValues() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(JsTag("Knob", ["left", "right"], TagType.None), ["200", "174"], diag);

        Assert.True(sig.IsExecutable);
        Assert.False(sig.HasFormat);
    }

    [Fact]
    public void MissingRequiredArg_ReturnsError() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Stream", nameof(DummyTags.Stream), TagType.ProcessFormat), [], diag);

        Assert.False(sig.IsExecutable);
        Assert.Contains(diag, d => d.Id == DiagnosticId.ArgTooFew);
    }
}
