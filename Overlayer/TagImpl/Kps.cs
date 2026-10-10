using O5Kit.Input;
using Overlayer.Compat.Interface;
using Overlayer.Tag.Core;
using UnityEngine;

namespace Overlayer.TagImpl;

public sealed class KpsTracker : IRuntimeTick {
    public static KpsTracker Instance { get; private set; }

    private const int MaxWindowMs = 10000;
    private const int MaxSamples = 1024;

    private static readonly KeyCode[] KeyboardKeys = BuildKeyboardKeys();
    private readonly List<double> keyTimes = new();
    private readonly Dictionary<int, (double value, double next)> held = [];
    private readonly object gate = new();

    public KpsTracker() {
        Instance = this;
    }

    public bool SuppressUnityKeys { get; set; }

    public void ReportKeyPress() {
        lock (gate) {
            Enqueue(keyTimes, UnityEngine.Time.realtimeSinceStartup);
        }
    }

    public void Tick() {
        if (!SuppressUnityKeys && O5Input.AnyKeyboardKeyDown) {
            double now = UnityEngine.Time.realtimeSinceStartup;
            int keys = O5Input.GetDownKeyCount(KeyboardKeys);
            if (keys > 0) {
                lock (gate) {
                    for (int i = 0; i < keys; i++) {
                        Enqueue(keyTimes, now);
                    }
                }
            }
        }

        lock (gate) {
            Prune(keyTimes, MaxWindowMs);
        }
    }

    public double Rate(int windowMs) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        if (windowMs <= 0) {
            return DecaySum(now);
        }
        int w = Math.Min(Math.Max(windowMs, 1), MaxWindowMs);
        if (!held.TryGetValue(w, out var e) || now >= e.next) {
            if (held.Count > 32) {
                held.Clear();
            }
            double value = DecaySum(now);
            held[w] = (value, now + w / 1000d);
            return value;
        }
        return e.value;
    }

    private double DecaySum(double now) {
        lock (gate) {
            Prune(keyTimes, MaxWindowMs);
            double value = 0d;
            foreach (double pressed in keyTimes) {
                double age = now - pressed;
                if (age < 0d) {
                    continue;
                }
                value += Math.Exp(-age);
            }
            return value;
        }
    }

    private static void Enqueue(List<double> queue, double time) {
        if (queue.Count >= MaxSamples) {
            queue.RemoveAt(0);
        }
        queue.Add(time);
    }

    private static void Prune(List<double> queue, double windowMs) {
        double now = UnityEngine.Time.realtimeSinceStartup;
        double window = windowMs / 1000d;
        while (queue.Count > 0 && now - queue[0] > window) {
            queue.RemoveAt(0);
        }
    }

    private static KeyCode[] BuildKeyboardKeys() {
        var keys = new List<KeyCode>();
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (key == KeyCode.None) {
                continue;
            }
            if (key is >= KeyCode.Mouse0 and <= KeyCode.Mouse6) {
                continue;
            }
            if (key is >= KeyCode.JoystickButton0 and <= KeyCode.Joystick8Button19) {
                continue;
            }
            keys.Add(key);
        }
        return [.. keys];
    }
}

public static class KpsTags {
    [Tag(TagType = TagType.ProcessFormat, Desc = "Keyboard key presses (value refreshes every window)\nEx) {Kps} / {Kps:0.00} / {Kps:500,0.00}\nwindow: milliseconds (default 0 = every frame, max 10000)")]
    public static double Kps(int windowMs = 0) => KpsTracker.Instance?.Rate(windowMs) ?? 0d;
}
