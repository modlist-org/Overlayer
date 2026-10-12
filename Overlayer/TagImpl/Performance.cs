using Overlayer.Compat.Interface;
using Overlayer.Native;
using Overlayer.Tag.Core;

namespace Overlayer.TagImpl;

public sealed class FpsTracker : IRuntimeTick {
    public static FpsTracker Instance { get; private set; }

    private const int MaxWindowMs = 10000;
    private const int MaxSamples = 4096;

    private readonly long ring = O5Ring.Create(MaxSamples);
    private readonly Dictionary<int, (double value, double next)> fpsHeld = [];
    private readonly Dictionary<int, (double value, double next)> msHeld = [];

    public FpsTracker() {
        Instance = this;
    }

    public void Tick() {
        double now = UnityEngine.Time.realtimeSinceStartup;
        O5Ring.Push(ring, now);
        O5Ring.Prune(ring, now, MaxWindowMs / 1000d);
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

    private double WindowedFps(int windowMs) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        O5Ring.Prune(ring, now, windowMs / 1000d);
        long n = O5Ring.Count(ring);
        if (n < 2) {
            return 0d;
        }
        double span = O5Ring.Last(ring) - O5Ring.First(ring);
        if (span < 0.000001) {
            return 0d;
        }
        return (n - 1) / span;
    }

    private double WindowedMs(int windowMs) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        O5Ring.Prune(ring, now, windowMs / 1000d);
        long n = O5Ring.Count(ring);
        if (n < 2) {
            return 0d;
        }
        double span = O5Ring.Last(ring) - O5Ring.First(ring);
        if (span < 0.000001) {
            return 0d;
        }
        return span / (n - 1) * 1000d;
    }
}

public static class Performance {
    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frame time in milliseconds (value refreshes every window)\nEx) {FrameTime} / {FrameTime:0.00} / {FrameTime:500,0.00}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double FrameTime(int windowMs = 0) => FpsTracker.Instance?.FrameTime(windowMs) ?? 0d;

    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frames per second (value refreshes every window)\nEx) {Fps} / {Fps:0} / {Fps:500,0}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double Fps(int windowMs = 0) => FpsTracker.Instance?.Fps(windowMs) ?? 0d;

    [Tag(Desc = "Total number of logical processor cores available on the system")]
    public static int ProcessorCount => UnityEngine.SystemInfo.processorCount;
}
