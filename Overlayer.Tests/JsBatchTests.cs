using System.Globalization;
using Microsoft.ClearScript;
using Overlayer.ModuleAPI;
using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Core;
using Xunit;

namespace Overlayer.Tests;

public sealed class JsBatchTests {
    private static string Register(TestV8Host v8, string name, string js, string[] parameters = null, TagType type = TagType.None) {
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate(js), parameters ?? [], type));
        return name;
    }

    private static async Task<TextEngineCore> Ready(string text) {
        var engine = new TextEngineCore { Text = text };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (engine.State == TextEngineState.Compiling) await Task.Delay(1, timeout.Token);
        Assert.Equal(TextEngineState.Ready, engine.State);
        return engine;
    }

    [Fact]
    public async Task BatchedResultsFormatExactlyLikeDirectCalls() {
        using var v8 = new TestV8Host();
        var cases = new (string js, string format)[] {
            ("(() => 5)", null), ("(() => 5)", "D3"), ("(() => 5.5)", null), ("(() => 0.1 + 0.2)", null),
            ("(() => 3000000000)", null), ("(() => 2**53)", null), ("(() => 1e21)", null), ("(() => -0)", null),
            ("(() => NaN)", null), ("(() => Infinity)", null), ("(() => 12.345)", "F2"), ("(() => 0.25)", "P0"),
            ("(() => 'a:1:b')", null), ("(() => '')", null), ("(() => '🎵' + 'x'.repeat(30))", null),
            ("(() => true)", null), ("(() => null)", null), ("(() => undefined)", null), ("(() => Symbol())", null),
            ("(() => 10n)", null), ("(() => [1, 2])", null), ("(() => ({}))", null), ("(() => { throw 1; })", null),
            ("((a) => a + ':' + 7)", "x")
        };
        var names = new List<string>();
        for (int i = 0; i < cases.Length; i++) {
            string[] parameters = cases[i].js.StartsWith("((a)") ? ["a"] : [];
            names.Add(Register(v8, "BatchFmt" + i, cases[i].js, parameters));
        }
        string template = string.Join("|", names.Select((n, i) =>
            "{" + n + (cases[i].format == null ? "" : ":" + cases[i].format) + "}"));
        using var engine = await Ready(template);
        var expected = string.Join("|", names.Select((n, i) => {
            var segment = engine.Segments[i];
            Assert.True(segment.Replacer.Compiled.Js != null, segment.Replacer.Parsed.Raw + " expr=" + segment.Replacer.Parsed.Expression + " diag=" + string.Join(",", segment.Replacer.Compiled.Diagnostics.Select(d => d.Id + ":" + string.Join(";", d.Data ?? []))));
            try { return segment.Replacer.Compiled.Delegate(); } catch { return segment.Replacer.Parsed.Raw; }
        }));
        Assert.Equal(expected, engine.Get());
        Assert.Contains("|005|", expected);
        Assert.EndsWith("|x:7", expected);
    }

    [Fact]
    public async Task StatefulCallsKeepCountAndOrderAcrossEngines() {
        using var v8 = new TestV8Host();
        v8.Engine.Execute("globalThis.seq = 0;");
        Register(v8, "BatchNext", "(() => ++globalThis.seq)");
        Register(v8, "BatchLabel", "((p) => p + (++globalThis.seq))", ["p"]);
        using var a = await Ready("{BatchNext}-{BatchNext}-{BatchLabel:a}");
        using var b = await Ready("{BatchLabel:b} {BatchNext}");
        using var single = await Ready("one {BatchNext}");
        var results = new string[3];
        TextEngineCore.GetAll([a, b, single], results);
        Assert.Equal(["1-2-a3", "b4 5", "one 6"], results);
        TextEngineCore.GetAll([a, b, single], results);
        Assert.Equal(["7-8-a9", "b10 11", "one 12"], results);
        Assert.Equal("13-14-a15", a.Get());
    }

    [Fact]
    public async Task ErrorsAndBlockedTagsStayIsolated() {
        using var v8 = new TestV8Host();
        v8.Engine.Execute("globalThis.calls = 0;");
        Register(v8, "BatchBoom", "(() => { ++globalThis.calls; throw new Error('x'); })");
        Register(v8, "BatchOk", "(() => 'ok' + (++globalThis.calls))");
        Register(v8, "BatchPlaying", "(() => 'p' + (++globalThis.calls))", type: TagType.BlockOnNotPlaying);
        using var engine = await Ready("[{BatchBoom}][{BatchOk}][{BatchPlaying}]");
        bool playing = true;
        using var _ = PlaybackState.Register(() => playing);
        Assert.Equal("[{BatchBoom}][ok2][p3]", engine.Get());
        playing = false;
        Assert.Equal("[{BatchBoom}][ok5][p3]", engine.Get());
        Assert.Equal(5, Convert.ToInt32(v8.Engine.Evaluate("globalThis.calls"), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void NarrowingMatchesClearScript() {
        using var v8 = new TestV8Host();
        var random = new Random(5);
        var echo = (ScriptObject)v8.Engine.Evaluate("(x => x)");
        var fromBits = (ScriptObject)v8.Engine.Evaluate("((hi, lo) => { const v = new DataView(new ArrayBuffer(8)); v.setUint32(0, hi); v.setUint32(4, lo); return v.getFloat64(0); })");
        for (int i = 0; i < 20000; i++) {
            double value = (i % 4) switch {
                0 => BitConverter.Int64BitsToDouble(random.NextInt64()),
                1 => Math.Round((random.NextDouble() - 0.5) * Math.Pow(2, random.Next(0, 64))),
                2 => (float)((random.NextDouble() - 0.5) * Math.Pow(2, random.Next(-30, 40))),
                _ => (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-5, 25))
            };
            long bits = BitConverter.DoubleToInt64Bits(value);
            object expected = fromBits.Invoke(false, (uint)(bits >>> 32), (uint)bits);
            object actual = JsBatch.Narrow(value);
            Assert.True(expected?.GetType() == actual.GetType(), $"{value:R} bits {bits:X}: CS {expected?.GetType()} {expected}, mine {actual.GetType()}");
            Assert.Equal(Convert.ToString(expected, CultureInfo.InvariantCulture), Convert.ToString(actual, CultureInfo.InvariantCulture));
        }
        GC.KeepAlive(echo);
    }
}
