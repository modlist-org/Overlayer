using System.Diagnostics;
using Microsoft.ClearScript;
using Overlayer.Core;
using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Xunit;
using Xunit.Abstractions;

namespace Overlayer.Tests;

// Opt-in: OVERLAYER_BENCHMARK=1 ./test.sh -c Release --filter FrameCost
public sealed class FrameCostBenchmark(ITestOutputHelper output) {
    public static double Accuracy = 99.123456;
    public static double GetAccuracy() => Accuracy;

    [Fact]
    public void PerTagCost() {
        if (Environment.GetEnvironmentVariable("OVERLAYER_BENCHMARK") != "1") return;
        using var v8 = new TestV8Host();
        MainCore.V8 = v8;
        var csTag = new TagCore("Accuracy", typeof(FrameCostBenchmark).GetMethod(nameof(GetAccuracy)), TagType.None);
        var jsNum = new TagCore("JsNum", (ScriptObject)v8.Engine.Evaluate("(() => 99.123456)"), [], TagType.None);
        var jsStr = new TagCore("JsStr", (ScriptObject)v8.Engine.Evaluate("(() => 'abc')"), [], TagType.None);
        ExprCore.TagLookupProvider = (string n, out TagCore t) => (t = n == "Accuracy" ? csTag : null) != null;
        try {
            var cs = Compiler.Compile(csTag, Parser.Parse("{Accuracy:F2}")[0]).Get;
            var jsn = Compiler.Compile(jsNum, Parser.Parse("{JsNum:F2}")[0]).Get;
            var jss = Compiler.Compile(jsStr, Parser.Parse("{JsStr}")[0]).Get;
            var expr = ExprCore.CompilePlaceholder(Parser.Parse("{Accuracy * 2 + Math.round(Accuracy) :F2}")[0]).Get;
            var inlineJs = ExprCore.CompilePlaceholder(Parser.Parse("{'x' + 1}")[0]).Get;
            int frame = 0;
            Measure("C# tag unchanged", cs);
            Measure("C# tag changing", () => { Accuracy = ++frame; return cs(); });
            Measure("native expr unchanged", expr);
            Measure("native expr changing", () => { Accuracy = ++frame; return expr(); });
            Measure("JS tag number:F2", jsn);
            Measure("JS tag string", jss);
            Measure("inline JS (V8)", inlineJs);
        } finally {
            MainCore.V8 = null;
            ExprCore.TagLookupProvider = null;
        }
    }

    [Fact]
    public async Task OverlayFrame() {
        if (Environment.GetEnvironmentVariable("OVERLAYER_BENCHMARK") != "1") return;
        using var v8 = new TestV8Host();
        v8.Engine.Execute("globalThis.combo = 0;");
        TagManager.Set(new TagCore("BenchCombo", (ScriptObject)v8.Engine.Evaluate("(() => ++globalThis.combo)"), [], TagType.None));
        TagManager.Set(new TagCore("BenchAcc", (ScriptObject)v8.Engine.Evaluate("(() => 99.1234)"), [], TagType.None));
        TagManager.Set(new TagCore("BenchName", (ScriptObject)v8.Engine.Evaluate("(() => 'Song Title')"), [], TagType.None));
        var engines = new List<TextEngine.Core.TextEngineCore>();
        for (int i = 0; i < 10; i++) {
            var e = new TextEngine.Core.TextEngineCore { Text = $"#{i} {{BenchCombo}}x {{BenchAcc:F2}}% {{BenchName}}" };
            while (e.State == TextEngine.Core.TextEngineState.Compiling) await Task.Delay(1);
            engines.Add(e);
        }
        var results = new string[engines.Count];
        Measure("10 overlays x3 JS, Get each", () => { foreach (var e in engines) e.Get(); return null; });
        Measure("10 overlays x3 JS, GetAll", () => { TextEngine.Core.TextEngineCore.GetAll(engines, results); return null; });
        output.WriteLine(results[3]);
        foreach (var e in engines) e.Dispose();
    }

    private void Measure(string label, Func<string> f) {
        for (int i = 0; i < 2000; i++) f();
        const int n = 20000;
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        long t0 = Stopwatch.GetTimestamp();
        for (int i = 0; i < n; i++) f();
        long dt = Stopwatch.GetTimestamp() - t0;
        alloc = GC.GetAllocatedBytesForCurrentThread() - alloc;
        output.WriteLine($"{label,-24} {dt * 1e9 / Stopwatch.Frequency / n,8:F1} ns  {alloc / (double)n,6:F1} B");
    }
}
