using Overlayer.Compat.Interface;
using Overlayer.Tag.Core;

namespace Overlayer.TagImpl;

public sealed class FpsTracker : IRuntimeTick {
    public static FpsTracker Instance { get; private set; }

    private const int MaxWindowMs = 10000;
    private const int MaxSamples = 4096;

    // Frame timestamps, oldest first from (head - count). Each window query
    // binary-searches its own start instead of pruning a shared ring, so
    // {Fps:500} no longer truncates {Fps:5000} in the same frame.
    private readonly double[] samples = new double[MaxSamples];
    private int head;
    private int count;
    private readonly Dictionary<int, (double value, double next)> fpsHeld = [];
    private readonly Dictionary<int, (double value, double next)> msHeld = [];

    public FpsTracker() {
        Instance = this;
    }

    public void Tick() {
        samples[head] = UnityEngine.Time.realtimeSinceStartup;
        head = (head + 1) & (MaxSamples - 1);
        if (count < MaxSamples) {
            count++;
        }
    }

    private double Sample(int i) => samples[(head - count + i) & (MaxSamples - 1)];

    // Samples with now - t <= window (same rule as the native ring prune).
    private bool Window(int windowMs, out long n, out double span) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        double window = windowMs / 1000d;
        int lo = 0, hi = count;
        while (lo < hi) {
            int mid = (lo + hi) >> 1;
            if (now - Sample(mid) > window) lo = mid + 1;
            else hi = mid;
        }
        n = count - lo;
        span = n < 2 ? 0d : Sample(count - 1) - Sample(lo);
        return n >= 2 && span >= 0.000001;
    }

    public double Fps(int windowMs) {
        if (windowMs <= 0) {
            float dt = UnityEngine.Time.unscaledDeltaTime;
            return dt > 0f ? 1d / dt : 0d;
        }
        double now = UnityEngine.Time.realtimeSinceStartup;
        int w = Math.Min(Math.Max(windowMs, 1), MaxWindowMs);
        if (!fpsHeld.TryGetValue(w, out var e) || now >= e.next) {
            if (fpsHeld.Count > 32) {
                fpsHeld.Clear();
            }
            double value = WindowedFps(w);
            fpsHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    public double FrameTime(int windowMs) {
        if (windowMs <= 0) {
            return UnityEngine.Time.unscaledDeltaTime * 1000d;
        }
        double now = UnityEngine.Time.realtimeSinceStartup;
        int w = Math.Min(Math.Max(windowMs, 1), MaxWindowMs);
        if (!msHeld.TryGetValue(w, out var e) || now >= e.next) {
            if (msHeld.Count > 32) {
                msHeld.Clear();
            }
            double value = WindowedMs(w);
            msHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    private double WindowedFps(int windowMs) =>
        Window(windowMs, out long n, out double span) ? (n - 1) / span : 0d;

    private double WindowedMs(int windowMs) =>
        Window(windowMs, out long n, out double span) ? span / (n - 1) * 1000d : 0d;
}

public static class Performance {
    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frame time in milliseconds (value refreshes every window)\nEx) {FrameTime} / {FrameTime:0.00} / {FrameTime:500,0.00}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double FrameTime(int windowMs = 0) => FpsTracker.Instance?.FrameTime(windowMs) ?? 0d;

    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frames per second (value refreshes every window)\nEx) {Fps} / {Fps:0} / {Fps:500,0}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double Fps(int windowMs = 0) => FpsTracker.Instance?.Fps(windowMs) ?? 0d;

    [Tag(Desc = "Total number of logical processor cores available on the system")]
    public static int ProcessorCount => System.Environment.ProcessorCount;
}
