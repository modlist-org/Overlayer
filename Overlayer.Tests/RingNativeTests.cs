using Overlayer.Native;
using Xunit;
using Xunit.Abstractions;

public sealed class RingNativeTests {
    private readonly ITestOutputHelper output;

    static RingNativeTests() {
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

    public RingNativeTests(ITestOutputHelper output) {
        this.output = output;
        output.WriteLine($"native available: {O5Native.IsAvailable}");
    }

    private sealed class Oracle {
        private readonly List<double> items = new();
        private readonly int cap;

        public Oracle(int cap) {
            this.cap = cap;
        }

        public void Push(double t) {
            if (items.Count >= cap) {
                items.RemoveAt(0);
            }
            items.Add(t);
        }

        public void Prune(double now, double window) {
            while (items.Count > 0 && now - items[0] > window) {
                items.RemoveAt(0);
            }
        }

        public double DecaySum(double now) {
            double s = 0;
            foreach (double t in items) {
                double age = now - t;
                if (age >= 0) {
                    s += Math.Exp(-age);
                }
            }
            return s;
        }

        public int Count() => items.Count;
        public double First() => items.Count == 0 ? double.NaN : items[0];
        public double Last() => items.Count == 0 ? double.NaN : items[^1];
    }

    [Fact]
    public void RingMatchesOracle() {
        var rng = new Random(42);
        foreach (int cap in new[] { 1, 3, 16, 1024 }) {
            long h = O5Ring.Create(cap);
            Assert.NotEqual(0, h);
            try {
                var oracle = new Oracle(cap);
                double now = 1000.0;
                for (int i = 0; i < 5000; i++) {
                    double op = rng.NextDouble();
                    if (op < 0.6) {
                        now += rng.NextDouble() * 0.05;
                        double t = rng.NextDouble() < 0.05 ? now + rng.NextDouble() : now;
                        O5Ring.Push(h, t);
                        oracle.Push(t);
                    } else if (op < 0.8) {
                        double w = rng.NextDouble() * 5.0;
                        O5Ring.Prune(h, now, w);
                        oracle.Prune(now, w);
                    } else {
                        Assert.Equal(oracle.Count(), O5Ring.Count(h));
                        Assert.Equal(oracle.DecaySum(now), O5Ring.DecaySum(h, now), 12);
                        double ef = oracle.First();
                        double af = O5Ring.First(h);
                        Assert.True((double.IsNaN(ef) && double.IsNaN(af)) || ef == af);
                        double el = oracle.Last();
                        double al = O5Ring.Last(h);
                        Assert.True((double.IsNaN(el) && double.IsNaN(al)) || el == al);
                    }
                }
            } finally {
                O5Ring.Destroy(h);
            }
        }
    }

    [Fact]
    public void RingBench() {
        long h = O5Ring.Create(1024);
        try {
            const int iters = 200000;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) {
                double now = 1000.0 + i * 0.001;
                O5Ring.Push(h, now);
                O5Ring.Prune(h, now, 10.0);
                O5Ring.DecaySum(h, now);
            }
            sw.Stop();
            output.WriteLine($"ring push+prune+decay x{iters}: {sw.Elapsed.TotalMilliseconds:F1}ms");

            var oracle = new Oracle(1024);
            sw.Restart();
            for (int i = 0; i < iters; i++) {
                double now = 1000.0 + i * 0.001;
                oracle.Push(now);
                oracle.Prune(now, 10.0);
                oracle.DecaySum(now);
            }
            sw.Stop();
            output.WriteLine($"list push+prune+decay x{iters}: {sw.Elapsed.TotalMilliseconds:F1}ms");
        } finally {
            O5Ring.Destroy(h);
        }
    }
}
