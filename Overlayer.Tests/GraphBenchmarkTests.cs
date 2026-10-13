using Microsoft.ClearScript.V8;
using Overlayer.Native;
using Overlayer.Overlay;
using Overlayer.Tag.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Overlayer.Tests;

public sealed class GraphBenchmarkTests {
    private readonly ITestOutputHelper output;

    public static class BenchTags {
        public static double Kps() => 14.5;
        public static double Fps() => 144.0;
    }

    public sealed class TagHelper {
        public object Get(string name) {
            if (name == "Kps") return BenchTags.Kps();
            if (name == "Fps") return BenchTags.Fps();
            return 0.0;
        }
    }

    public GraphBenchmarkTests(ITestOutputHelper output) {
        this.output = output;
    }

    static GraphBenchmarkTests() {
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
    }

    [Fact]
    public void RunAndExportBenchmark() {
        if (!O5Expr.IsSupported) {
            output.WriteLine("O5Expr is not supported, skipping benchmark.");
            return;
        }

        var testTags = new Dictionary<string, TagCore>(StringComparer.OrdinalIgnoreCase) {
            ["Kps"] = new TagCore("Kps", typeof(BenchTags).GetMethod(nameof(BenchTags.Kps), BindingFlags.Public | BindingFlags.Static), TagType.None),
            ["Fps"] = new TagCore("Fps", typeof(BenchTags).GetMethod(nameof(BenchTags.Fps), BindingFlags.Public | BindingFlags.Static), TagType.None)
        };

        using var v8 = new V8ScriptEngine();
        v8.AddHostObject("Tag", new Func<string, object>(name => {
            if (testTags.TryGetValue(name, out var t)) return t.Invoke();
            return 0.0;
        }));
        v8.Execute("Object.defineProperty(globalThis, 'Fps', { get: () => Tag('Fps') });");
        v8.Execute("Object.defineProperty(globalThis, 'Kps', { get: () => Tag('Kps') });");

        var results = new List<object>();

        const int iters = 200_000;

        // --- Benchmark 1: Pure Math ---
        {
            string code = "50 + 40 * Math.sin(t * 2)";
            using var v8Func = v8.Evaluate($"(function(t){{ return ({code}); }})") as Microsoft.ClearScript.ScriptObject;
            using var engine = new GraphExpressionEngine(testTags.TryGetValue, null, c => v8.Evaluate(c) as Microsoft.ClearScript.ScriptObject);
            engine.Compile(code);

            // Warmup
            for (int i = 0; i < 5000; i++) {
                v8Func.Invoke(false, (float)i * 0.016f);
                engine.Evaluate((float)i * 0.016f, out _);
            }

            // V8
            long allocBeforeV8 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) {
                object raw = v8Func.Invoke(false, (float)i * 0.016f);
                float val = Convert.ToSingle(raw);
            }
            sw.Stop();
            double v8Ms = sw.Elapsed.TotalMilliseconds;
            long allocV8 = GC.GetAllocatedBytesForCurrentThread() - allocBeforeV8;

            // Zig
            long allocBeforeZig = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            for (int i = 0; i < iters; i++) {
                engine.Evaluate((float)i * 0.016f, out float val);
            }
            sw.Stop();
            double zigMs = sw.Elapsed.TotalMilliseconds;
            long allocZig = GC.GetAllocatedBytesForCurrentThread() - allocBeforeZig;

            results.Add(new {
                category = "Pure Math: 50 + 40 * sin(t*2)",
                iterations = iters,
                v8_ms = Math.Round(v8Ms, 2),
                v8_ns_per_op = Math.Round((v8Ms / iters) * 1_000_000, 2),
                v8_alloc_bytes = allocV8,
                zig_ms = Math.Round(zigMs, 2),
                zig_ns_per_op = Math.Round((zigMs / iters) * 1_000_000, 2),
                zig_alloc_bytes = allocZig,
                speedup = Math.Round(v8Ms / Math.Max(zigMs, 0.001), 1)
            });
        }

        // --- Benchmark 2: Single C# Tag ---
        {
            string code = "Tag(\"Kps\") * 2";
            using var v8Func = v8.Evaluate($"(function(t){{ return ({code}); }})") as Microsoft.ClearScript.ScriptObject;
            using var engine = new GraphExpressionEngine(testTags.TryGetValue, null, c => v8.Evaluate(c) as Microsoft.ClearScript.ScriptObject);
            engine.Compile(code);

            // Warmup
            for (int i = 0; i < 5000; i++) {
                v8Func.Invoke(false, (float)i * 0.016f);
                engine.Evaluate((float)i * 0.016f, out _);
            }

            // V8
            long allocBeforeV8 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) {
                object raw = v8Func.Invoke(false, (float)i * 0.016f);
                float val = Convert.ToSingle(raw);
            }
            sw.Stop();
            double v8Ms = sw.Elapsed.TotalMilliseconds;
            long allocV8 = GC.GetAllocatedBytesForCurrentThread() - allocBeforeV8;

            // Zig + C# Direct
            long allocBeforeZig = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            for (int i = 0; i < iters; i++) {
                engine.Evaluate((float)i * 0.016f, out float val);
            }
            sw.Stop();
            double zigMs = sw.Elapsed.TotalMilliseconds;
            long allocZig = GC.GetAllocatedBytesForCurrentThread() - allocBeforeZig;

            results.Add(new {
                category = "Single Tag: Tag(\"Kps\") * 2",
                iterations = iters,
                v8_ms = Math.Round(v8Ms, 2),
                v8_ns_per_op = Math.Round((v8Ms / iters) * 1_000_000, 2),
                v8_alloc_bytes = allocV8,
                zig_ms = Math.Round(zigMs, 2),
                zig_ns_per_op = Math.Round((zigMs / iters) * 1_000_000, 2),
                zig_alloc_bytes = allocZig,
                speedup = Math.Round(v8Ms / Math.Max(zigMs, 0.001), 1)
            });
        }

        // --- Benchmark 3: Multi C# Tag ---
        {
            string code = "Fps + Kps * 2";
            using var v8Func = v8.Evaluate($"(function(t){{ return ({code}); }})") as Microsoft.ClearScript.ScriptObject;
            using var engine = new GraphExpressionEngine(testTags.TryGetValue, null, c => v8.Evaluate(c) as Microsoft.ClearScript.ScriptObject);
            engine.Compile(code);

            // Warmup
            for (int i = 0; i < 5000; i++) {
                v8Func.Invoke(false, (float)i * 0.016f);
                engine.Evaluate((float)i * 0.016f, out _);
            }

            // V8
            long allocBeforeV8 = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) {
                object raw = v8Func.Invoke(false, (float)i * 0.016f);
                float val = Convert.ToSingle(raw);
            }
            sw.Stop();
            double v8Ms = sw.Elapsed.TotalMilliseconds;
            long allocV8 = GC.GetAllocatedBytesForCurrentThread() - allocBeforeV8;

            // Zig + C# Direct
            long allocBeforeZig = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            for (int i = 0; i < iters; i++) {
                engine.Evaluate((float)i * 0.016f, out float val);
            }
            sw.Stop();
            double zigMs = sw.Elapsed.TotalMilliseconds;
            long allocZig = GC.GetAllocatedBytesForCurrentThread() - allocBeforeZig;

            results.Add(new {
                category = "Multi Tag: Fps + Kps * 2",
                iterations = iters,
                v8_ms = Math.Round(v8Ms, 2),
                v8_ns_per_op = Math.Round((v8Ms / iters) * 1_000_000, 2),
                v8_alloc_bytes = allocV8,
                zig_ms = Math.Round(zigMs, 2),
                zig_ns_per_op = Math.Round((zigMs / iters) * 1_000_000, 2),
                zig_alloc_bytes = allocZig,
                speedup = Math.Round(v8Ms / Math.Max(zigMs, 0.001), 1)
            });
        }

        // --- Benchmark 4: History Buffer Pruning ---
        {
            const int bufferIters = 50_000;
            const int sampleCapacity = 10_000;

            // Old List<Vector2> with RemoveRange
            var oldList = new List<System.Numerics.Vector2>(sampleCapacity);
            for (int i = 0; i < sampleCapacity; i++) oldList.Add(new System.Numerics.Vector2(i * 0.01f, 50f));

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < bufferIters; i++) {
                oldList.Add(new System.Numerics.Vector2((sampleCapacity + i) * 0.01f, 50f));
                oldList.RemoveRange(0, 1);
            }
            sw.Stop();
            double oldListMs = sw.Elapsed.TotalMilliseconds;

            // New SampleRing (O(1))
            var ring = new RingBenchBuffer(sampleCapacity);
            for (int i = 0; i < sampleCapacity; i++) ring.Push(new System.Numerics.Vector2(i * 0.01f, 50f));

            sw.Restart();
            for (int i = 0; i < bufferIters; i++) {
                ring.Push(new System.Numerics.Vector2((sampleCapacity + i) * 0.01f, 50f));
                ring.TrimOne();
            }
            sw.Stop();
            double ringMs = sw.Elapsed.TotalMilliseconds;

            results.Add(new {
                category = "Buffer Pruning (10k items shift)",
                iterations = bufferIters,
                v8_ms = Math.Round(oldListMs, 2),
                v8_ns_per_op = Math.Round((oldListMs / bufferIters) * 1_000_000, 2),
                v8_alloc_bytes = 0,
                zig_ms = Math.Round(ringMs, 2),
                zig_ns_per_op = Math.Round((ringMs / bufferIters) * 1_000_000, 2),
                zig_alloc_bytes = 0,
                speedup = Math.Round(oldListMs / Math.Max(ringMs, 0.001), 1)
            });
        }

        string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("/tmp/bench_results.json", json);
        output.WriteLine(json);
    }

    private sealed class RingBenchBuffer {
        private readonly System.Numerics.Vector2[] buf;
        private int head;
        private int count;

        public RingBenchBuffer(int cap) {
            buf = new System.Numerics.Vector2[cap];
        }

        public void Push(System.Numerics.Vector2 pt) {
            if (count < buf.Length) {
                buf[(head + count) % buf.Length] = pt;
                count++;
            } else {
                buf[head] = pt;
                head = (head + 1) % buf.Length;
            }
        }

        public void TrimOne() {
            if (count > 0) {
                head = (head + 1) % buf.Length;
                count--;
            }
        }
    }
}
