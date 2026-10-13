using Esprima;
using Microsoft.ClearScript.V8;
using Overlayer.Tag.Core;
using Overlayer.V8;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public class ImplJsGeneratorTests {
    public static class SampleTags {
        public static double Score() => 100.0;
        public static string PlayerName() => "TestPlayer";
        public static double Add(double a, double b) => a + b;
        public static string FormatText(string text, int count = 1) => text;
    }

    private static List<TagCore> CreateSampleTags() {
        var scoreMethod = typeof(SampleTags).GetMethod(nameof(SampleTags.Score), BindingFlags.Public | BindingFlags.Static)!;
        var playerMethod = typeof(SampleTags).GetMethod(nameof(SampleTags.PlayerName), BindingFlags.Public | BindingFlags.Static)!;
        var addMethod = typeof(SampleTags).GetMethod(nameof(SampleTags.Add), BindingFlags.Public | BindingFlags.Static)!;
        var formatMethod = typeof(SampleTags).GetMethod(nameof(SampleTags.FormatText), BindingFlags.Public | BindingFlags.Static)!;

        return [
            new TagCore("Score", scoreMethod, TagType.None, "Current player score */ dangerous comment test"),
            new TagCore("PlayerName", playerMethod, TagType.None, "Player's name"),
            new TagCore("Add", addMethod, TagType.None, "Adds two numbers"),
            new TagCore("FormatText", formatMethod, TagType.None, "Formats text with count"),
            // Non-identifier tag should be skipped
            new TagCore("Invalid-Tag#Name", scoreMethod, TagType.None, "Should be skipped"),
            // Reserved global should be skipped
            new TagCore("Tag", scoreMethod, TagType.None, "Reserved global")
        ];
    }

    [Fact]
    public void BuildJs_GeneratesSyntacticallyValidJavaScript() {
        var tags = CreateSampleTags();
        string js = ImplJsGenerator.BuildJs(tags);

        // Verify with Esprima AST parser
        var parser = new JavaScriptParser();
        var program = parser.ParseScript(js);
        Assert.NotNull(program);

        // Verify with V8 engine directly
        using var engine = new V8ScriptEngine();
        // Provide mock TagAccessHelper so Tag proxy doesn't throw on invocation
        engine.Execute("globalThis.TagAccessHelper = { Get: () => 42 };");
        engine.Execute(js);

        // Verify zero-arg tags have valueOf and toString
        object scoreVal = engine.Evaluate("Score.valueOf()");
        Assert.Equal(42, Convert.ToInt32(scoreVal));

        // Verify functions exist
        Assert.True((bool)engine.Evaluate("typeof Score === 'function'"));
        Assert.True((bool)engine.Evaluate("typeof Add === 'function'"));
        Assert.True((bool)engine.Evaluate("typeof FormatText === 'function'"));

        // Verify invalid tag was skipped
        Assert.True((bool)engine.Evaluate("typeof globalThis['Invalid-Tag#Name'] === 'undefined'"));
    }

    [Fact]
    public void BuildDts_ContainsExpectedDeclarations() {
        var tags = CreateSampleTags();
        string dts = ImplJsGenerator.BuildDts(tags);

        Assert.Contains("declare function Score(): number;", dts);
        Assert.Contains("declare function PlayerName(): string;", dts);
        Assert.Contains("declare function Add(a: number, b: number): number;", dts);
        Assert.DoesNotContain("Invalid-Tag#Name", dts);
    }

    [Fact]
    public void ImplJs_AllowsDirectTagFunctionInvocation() {
        var tags = CreateSampleTags();
        string js = ImplJsGenerator.BuildJs(tags);

        using var engine = new V8ScriptEngine();
        engine.Execute("globalThis.TagAccessHelper = { Get: (prop) => prop === 'Score' ? 100 : 0 };");
        engine.Execute(js);

        // Calling Score() directly without Tag. prefix works
        object result = engine.Evaluate("Score()");
        Assert.Equal(100, Convert.ToInt32(result));
    }
}
