using Overlayer.Native;
using Overlayer.Overlay;
using Overlayer.Tag.Core;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class GraphExpressionEngineTests {
    public static class FakeGraphTags {
        public static double MyKps() => 15.0;
        public static double MyFps() => 60.0;
    }

    private static readonly Dictionary<string, TagCore> TestTags = new(StringComparer.OrdinalIgnoreCase);

    static GraphExpressionEngineTests() {
        try {
            string baseDir = AppContext.BaseDirectory;
            string dir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "O5Kit", "native", "zig-out", "lib"));
            if (Directory.Exists(dir)) {
                string so = Directory.GetFiles(dir, "O5KitNative.*").FirstOrDefault();
                if (!string.IsNullOrEmpty(so)) {
                    Environment.SetEnvironmentVariable("O5NATIVE_PATH", so);
                }
            }
        } catch {
        }

        var kpsMethod = typeof(FakeGraphTags).GetMethod(nameof(FakeGraphTags.MyKps), BindingFlags.Public | BindingFlags.Static);
        var fpsMethod = typeof(FakeGraphTags).GetMethod(nameof(FakeGraphTags.MyFps), BindingFlags.Public | BindingFlags.Static);
        TestTags["MyKps"] = new TagCore("MyKps", kpsMethod, TagType.None);
        TestTags["MyFps"] = new TagCore("MyFps", fpsMethod, TagType.None);

        // JS Tag mockup: TagCore with jsInvoker (null for unit test)
        TestTags["MyJsTag"] = new TagCore("MyJsTag", null, [], TagType.None, "mock js tag", typeof(double));

        GraphExpressionEngine.DefaultTagLookup = (string name, out TagCore tag) => TestTags.TryGetValue(name, out tag);
    }

    [Fact]
    public void PureMathExpression_UsesZigNative() {
        if (!O5Expr.IsSupported) return;
        using var engine = new GraphExpressionEngine();
        engine.Compile("50 + 40 * Math.sin(t * 2)");

        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.Empty(engine.BoundTags);

        Assert.True(engine.Evaluate(0f, out float val0));
        Assert.Equal(50f, val0);

        Assert.True(engine.Evaluate((float)(Math.PI / 4.0), out float valPi4));
        Assert.True(Math.Abs(valPi4 - 90f) < 1e-4);
    }

    [Fact]
    public void JsOnlySyntax_RunsNativeWithJsSemantics() {
        Assert.True(OvJsExpr.IsSupported);
        using var engine = new GraphExpressionEngine();
        engine.Compile("(Math.max(t, Tag(\"MyKps\")) << 36) + Math.round(-2.5) + Math.hypot(3, 4)");
        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.True(engine.Evaluate(20f, out float val));
        Assert.Equal((20 << 36) - 2 + 5, val);
        engine.Compile("1 / (t - 1)");
        Assert.False(engine.Evaluate(1f, out float inf));
        Assert.Equal(0f, inf);
    }

    [Fact]
    public void TagFuncExpression_UsesZigNativeWithFastGetter() {
        if (!O5Expr.IsSupported) return;
        using var engine = new GraphExpressionEngine();
        engine.Compile("Tag(\"MyKps\") * 2");

        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.Single(engine.BoundTags);
        Assert.Equal("MyKps", engine.BoundTags[0].Name);

        Assert.True(engine.Evaluate(0f, out float val));
        // 15 * 2 = 30
        Assert.Equal(30f, val);
    }

    [Fact]
    public void BracketTagExpression_UsesZigNativeWithFastGetter() {
        if (!O5Expr.IsSupported) return;
        using var engine = new GraphExpressionEngine();
        engine.Compile("{MyFps} / 2 + 5");

        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.Single(engine.BoundTags);
        Assert.Equal("MyFps", engine.BoundTags[0].Name);

        Assert.True(engine.Evaluate(0f, out float val));
        // 60 / 2 + 5 = 35
        Assert.Equal(35f, val);
    }

    [Fact]
    public void BareIdentifierTagExpression_UsesZigNative() {
        if (!O5Expr.IsSupported) return;
        using var engine = new GraphExpressionEngine();
        engine.Compile("MyKps * 3");

        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.Single(engine.BoundTags);
        Assert.Equal("MyKps", engine.BoundTags[0].Name);

        Assert.True(engine.Evaluate(0f, out float val));
        // 15 * 3 = 45
        Assert.Equal(45f, val);
    }

    [Fact]
    public void MultiTagAndMathMixedExpression_UsesZigNative() {
        if (!O5Expr.IsSupported) return;
        using var engine = new GraphExpressionEngine();
        engine.Compile("MyFps + MyKps * 2");

        Assert.Equal(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
        Assert.Equal(2, engine.BoundTags.Count);

        Assert.True(engine.Evaluate(0f, out float val));
        // 60 + 15 * 2 = 90
        Assert.Equal(90f, val);
    }

    [Fact]
    public void JsTag_AvoidsZig_AndFallsBackToV8() {
        using var engine = new GraphExpressionEngine();
        // Since MyJsTag is a JS tag (tag.IsJS == true), it MUST NOT use ZigNative!
        engine.Compile("MyJsTag() * 2");

        Assert.NotEqual(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
    }

    [Fact]
    public void ComplexJsStatement_AvoidsZig_AndFallsBack() {
        using var engine = new GraphExpressionEngine();
        // Syntax with 'var' or multiple statements that Zig cannot parse
        engine.Compile("var a = 10; a * 2");

        Assert.NotEqual(GraphExpressionEngine.EngineKind.ZigNative, engine.Kind);
    }
}
