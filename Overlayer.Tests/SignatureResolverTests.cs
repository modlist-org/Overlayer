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
        public static double Meter(int windowMs = 0) => windowMs;
        public static int Margin(string margin) => margin.Length;
        public static double Smooth(string tag, int digits = -1, int speed = 500, DummyEase ease = DummyEase.Linear) => speed;
    }

    private enum DummyEase { Linear, OutExpo }

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
    public void JsOnlyTag_IsBlockedInText() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(PropertyTag("JsThing", nameof(DummyTags.FullComboValue), TagType.JsOnly), [], diag);

        Assert.False(sig.IsExecutable);
        Assert.Contains(diag, d => d.Id == DiagnosticId.JsOnlyBlocked);
    }

    [Fact]
    public void MissingRequiredArg_ReturnsError() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Stream", nameof(DummyTags.Stream), TagType.ProcessFormat), [], diag);

        Assert.False(sig.IsExecutable);
        Assert.Contains(diag, d => d.Id == DiagnosticId.ArgTooFew);
    }

    [Fact]
    public void Method_AllArgsBindAsValues_NoFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Smooth", nameof(DummyTags.Smooth), TagType.ProcessFormat), ["Combo", "0", "500", "OutExpo"], diag);

        Assert.True(sig.IsExecutable);
        Assert.False(sig.HasFormat);
        Assert.Equal(["Combo", "0", "500", "OutExpo"], sig.Args);
        Assert.Empty(diag);
    }

    [Fact]
    public void Method_FullArgsPlusFormat_SplitsFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Smooth", nameof(DummyTags.Smooth), TagType.ProcessFormat), ["Combo", "0", "500", "OutExpo", "F1"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("F1", sig.Format);
        Assert.Equal(["Combo", "0", "500", "OutExpo"], sig.Args);
    }

    [Fact]
    public void Method_PartialArgsBindAsValues_NoFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Smooth", nameof(DummyTags.Smooth), TagType.ProcessFormat), ["Combo", "0"], diag);

        Assert.True(sig.IsExecutable);
        Assert.False(sig.HasFormat);
        Assert.Equal(["Combo", "0"], sig.Args);
    }

    [Fact]
    public void Method_LoneArgOnOptionalParams_StaysFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Meter", nameof(DummyTags.Meter), TagType.ProcessFormat), ["0"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("0", sig.Format);
        Assert.Empty(sig.Args);
    }

    [Fact]
    public void Method_LoneArgOnRequiredParam_BindsAsValue() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Margin", nameof(DummyTags.Margin), TagType.ProcessFormat), ["Perfect"], diag);

        Assert.True(sig.IsExecutable);
        Assert.False(sig.HasFormat);
        Assert.Equal(["Perfect"], sig.Args);
    }

    [Fact]
    public void Method_BadValueArg_FallsBackToFormat() {
        var diag = new List<CompileDiagnostic>();
        var sig = Resolve(MethodTag("Smooth", nameof(DummyTags.Smooth), TagType.ProcessFormat), ["Combo", "xyz"], diag);

        Assert.True(sig.IsExecutable);
        Assert.True(sig.HasFormat);
        Assert.Equal("xyz", sig.Format);
        Assert.Equal(["Combo"], sig.Args);
    }
}
