using Overlayer.Compat.Interface;
using Overlayer.Tag.Core;
using UnityEngine.Device;

namespace Overlayer.TagImpl;

// Frame rate tracker, sampled every frame on the mod tick (unscaled time).
public sealed class FpsTracker : IRuntimeTick {
    public static FpsTracker Instance { get; private set; }

    private const int MaxWindowMs = 10000;
    private const int MaxSamples = 4096;

    private readonly Queue<double> frameTimes = new();
    private readonly Dictionary<int, (double value, double next)> fpsHeld = new();
    private readonly Dictionary<int, (double value, double next)> msHeld = new();

    public FpsTracker() {
        Instance = this;
    }

    public void Tick() {
        double now = UnityEngine.Time.realtimeSinceStartup;
        if(frameTimes.Count >= MaxSamples) {
            frameTimes.Dequeue();
        }
        frameTimes.Enqueue(now);
        Prune(MaxWindowMs);
    }

    public double Fps(int windowMs) {
        if(windowMs <= 0) {
            float dt = UnityEngine.Time.unscaledDeltaTime;
            return dt > 0f ? 1d / dt : 0d;
        }
        double now = UnityEngine.Time.realtimeSinceStartup;
        int w = Math.Min(Math.Max(windowMs, 1), MaxWindowMs);
        if(!fpsHeld.TryGetValue(w, out var e) || now >= e.next) {
            if(fpsHeld.Count > 32) {
                fpsHeld.Clear();
            }
            double value = WindowedFps(w);
            fpsHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    public double FrameTime(int windowMs) {
        if(windowMs <= 0) {
            return UnityEngine.Time.unscaledDeltaTime * 1000d;
        }
        double now = UnityEngine.Time.realtimeSinceStartup;
        int w = Math.Min(Math.Max(windowMs, 1), MaxWindowMs);
        if(!msHeld.TryGetValue(w, out var e) || now >= e.next) {
            if(msHeld.Count > 32) {
                msHeld.Clear();
            }
            double value = WindowedMs(w);
            msHeld[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    private double WindowedFps(int windowMs) {
        Prune(windowMs);
        int n = frameTimes.Count;
        if(n < 2) {
            return 0d;
        }
        double span = frameTimes.Last() - frameTimes.Peek();
        if(span < 0.000001) {
            return 0d;
        }
        return (n - 1) / span;
    }

    private double WindowedMs(int windowMs) {
        Prune(windowMs);
        int n = frameTimes.Count;
        if(n < 2) {
            return 0d;
        }
        double span = frameTimes.Last() - frameTimes.Peek();
        if(span < 0.000001) {
            return 0d;
        }
        return span / (n - 1) * 1000d;
    }

    private void Prune(int windowMs) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        double window = windowMs / 1000d;
        while(frameTimes.Count > 0 && now - frameTimes.Peek() > window) {
            frameTimes.Dequeue();
        }
    }
}

public static class Performance {
    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frame time in milliseconds (value refreshes every window)\nEx) {FrameTime} / {FrameTime:0.00} / {FrameTime:500,0.00}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double FrameTime(int windowMs = 0) => FpsTracker.Instance?.FrameTime(windowMs) ?? 0d;

    [Tag(TagType = TagType.ProcessFormat, Desc = "Average unscaled frames per second (value refreshes every window)\nEx) {Fps} / {Fps:0} / {Fps:500,0}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double Fps(int windowMs = 0) => FpsTracker.Instance?.Fps(windowMs) ?? 0d;

    [Tag(Desc = "Total number of logical processor cores available on the system")]
    public static int ProcessorCount => SystemInfo.processorCount;
}
