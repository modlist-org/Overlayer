using System.Diagnostics;
using Overlayer.Native;
using Overlayer.Tag.Core;
using Overlayer.Tag.Compile;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace Overlayer.Tests;

// Opt-in timings, no flaky speed assertions. Run Release with
// DOTNET_TieredCompilation=0 OVERLAYER_BENCHMARK=1 ./test.sh -c Release --filter EnginePerformance
public sealed class EnginePerformanceTests(ITestOutputHelper output) {
    public static class Core {
        public static V8ExpressionDispatchTests.Engine V8 { get; } = new();
    }

    [Fact]
    public void CompiledDispatchHasNoPerCallAllocation() {
        var evaluate = V8ExpressionDispatch.Create(typeof(Core));
        for (int i = 0; i < 1000; i++) evaluate("hello", out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) evaluate("hello", out _);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void UnchangedNumericExpressionHasNoFormattingAllocation() {
        var memo = new StringMemo<long>();
        var text = ExprCore.FormatMemoizedResult(144.125, null, memo);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) ExprCore.FormatMemoizedResult(144.125, null, memo);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Same(text, ExprCore.FormatMemoizedResult(144.125, null, memo));
        Assert.Equal("145", ExprCore.FormatMemoizedResult(145, null, memo));
        Assert.Equal("NaN", ExprCore.FormatMemoizedResult(double.NaN, null, memo));
        Assert.Equal("Infinity", ExprCore.FormatMemoizedResult(double.PositiveInfinity, null, memo));
        Assert.Equal("-Infinity", ExprCore.FormatMemoizedResult(double.NegativeInfinity, null, memo));
    }

    [Fact]
    public void CompareDispatchAndBatchedGeometry() {
        if (Environment.GetEnvironmentVariable("OVERLAYER_BENCHMARK") != "1") return;
        Assert.True(OvVertexBuilder.IsSupported);
        const int iterations = 10000;
        var points = new OvPoint2D[1024];
        for (int i = 0; i < points.Length; i++) points[i] = new() { X = i, Y = MathF.Sin(i * 0.01f) };
        var quads = new OvQuad2D[points.Length];
        var transform = new OvMapTransform { ScaleX = 2, ScaleY = 4, XMin = 100 };
        Measure("fill managed (1024 samples)", () => OvVertexBuilder.BuildGraphFillQuadsManaged(points, transform, -10, quads), iterations);
        Measure("fill Zig (1024 samples)", () => OvVertexBuilder.BuildGraphFillQuads(points, transform, -10, quads), iterations);
        Assert.True(OvVertexBuilder.IsFillSupported);

        var evaluate = V8ExpressionDispatch.Create(typeof(Core));
        Measure("dispatch compiled", () => evaluate("hello", out _), iterations);
        // Reproduce the former reflection/argument-array hot path.
        Measure("dispatch reflection", () => {
            var property = typeof(Core).GetProperty("V8");
            var engine = property.GetValue(null);
            var method = engine.GetType().GetMethod("TryEvaluateFx");
            method.Invoke(engine, ["hello", null]);
        }, iterations);
        var memo = new StringMemo<long>();
        Measure("unchanged number memo", () => ExprCore.FormatMemoizedResult(144.125, null, memo), iterations);
        Measure("unchanged number formatting", () => 144.125.ToString(System.Globalization.CultureInfo.InvariantCulture), iterations);

        foreach (int literalLength in new[] { 8, 64, 256, 4096, 16384 }) {
            string template = string.Concat(Enumerable.Repeat(new string('x', literalLength) + "{Tag}", 64));
            var plan = new TextRenderPlan(template, Parser.Parse(template));
            var values = Enumerable.Repeat("12345", 64).ToArray();
            var destination = new char[template.Length + 64];
            int frame = 0;
            Measure($"compose managed literals={literalLength}", () => {
                values[0] = (++frame & 1) == 0 ? "12345" : "54321";
                plan.Compose(values, destination, native: false);
            }, iterations);
            Measure($"compose Zig incl packing literals={literalLength}", () => {
                values[0] = (++frame & 1) == 0 ? "12345" : "54321";
                plan.Compose(values, destination, native: true);
            }, iterations);
            Assert.True(OvTextRenderer.IsSupported);
        }
    }

    private void Measure(string label, Action action, int iterations) {
        for (int i = 0; i < 2000; i++) action();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) action();
        long elapsed = Stopwatch.GetTimestamp() - started;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        output.WriteLine($"{label}: {elapsed * 1e9 / Stopwatch.Frequency / iterations:F1} ns/call, {allocated / (double)iterations:F1} B/call");
    }
}
