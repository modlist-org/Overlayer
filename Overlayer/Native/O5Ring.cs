using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

internal static class O5Ring {
    private const string LibName = "o5native";

    private sealed class ManagedRing {
        private readonly double[] buf;
        private int head;
        private int len;

        public ManagedRing(int cap) {
            buf = new double[cap];
        }

        public void Push(double t) {
            if (len < buf.Length) {
                buf[(head + len) % buf.Length] = t;
                len++;
            } else {
                buf[head] = t;
                head = (head + 1) % buf.Length;
            }
        }

        public void Prune(double now, double window) {
            while (len > 0 && now - buf[head] > window) {
                head = (head + 1) % buf.Length;
                len--;
            }
        }

        public double DecaySum(double now) {
            double s = 0;
            for (int k = 0; k < len; k++) {
                double age = now - buf[(head + k) % buf.Length];
                if (age >= 0) {
                    s += Math.Exp(-age);
                }
            }
            return s;
        }

        public double First() => len == 0 ? double.NaN : buf[head];

        public double Last() => len == 0 ? double.NaN : buf[(head + len - 1) % buf.Length];

        public long Count() => len;
    }

    private static readonly Dictionary<long, ManagedRing> fallback = new();
    private static long nextFallbackId = 0;
    private static readonly object fallbackGate = new();

    private static bool Native => O5Native.IsAvailable;

    public static long Create(long cap) {
        if (cap <= 0) {
            return 0;
        }
        if (Native) {
            try {
                long h = o5_ring_create((nuint)cap);
                if (h != 0) {
                    return h;
                }
            } catch {
            }
        }
        lock (fallbackGate) {
            long id = --nextFallbackId;
            fallback[id] = new ManagedRing((int)Math.Min(cap, int.MaxValue));
            return id;
        }
    }

    public static void Destroy(long handle) {
        if (handle == 0) {
            return;
        }
        if (handle < 0) {
            lock (fallbackGate) {
                fallback.Remove(handle);
            }
            return;
        }
        if (!Native) {
            return;
        }
        try {
            o5_ring_destroy(handle);
        } catch {
        }
    }

    private static bool TryFallback(long handle, out ManagedRing ring) {
        lock (fallbackGate) {
            return fallback.TryGetValue(handle, out ring);
        }
    }

    public static void Push(long handle, double t) {
        if (handle == 0) {
            return;
        }
        if (handle < 0) {
            if (TryFallback(handle, out var ring)) {
                ring.Push(t);
            }
            return;
        }
        if (!Native) {
            return;
        }
        try {
            o5_ring_push(handle, t);
        } catch {
        }
    }

    public static void Prune(long handle, double now, double window) {
        if (handle == 0) {
            return;
        }
        if (handle < 0) {
            if (TryFallback(handle, out var ring)) {
                ring.Prune(now, window);
            }
            return;
        }
        if (!Native) {
            return;
        }
        try {
            o5_ring_prune(handle, now, window);
        } catch {
        }
    }

    public static double DecaySum(long handle, double now) {
        if (handle == 0) {
            return 0;
        }
        if (handle < 0) {
            return TryFallback(handle, out var ring) ? ring.DecaySum(now) : 0;
        }
        if (!Native) {
            return 0;
        }
        try {
            return o5_ring_decay_sum(handle, now);
        } catch {
            return 0;
        }
    }

    public static double First(long handle) {
        if (handle == 0) {
            return double.NaN;
        }
        if (handle < 0) {
            return TryFallback(handle, out var ring) ? ring.First() : double.NaN;
        }
        if (!Native) {
            return double.NaN;
        }
        try {
            return o5_ring_first(handle);
        } catch {
            return double.NaN;
        }
    }

    public static double Last(long handle) {
        if (handle == 0) {
            return double.NaN;
        }
        if (handle < 0) {
            return TryFallback(handle, out var ring) ? ring.Last() : double.NaN;
        }
        if (!Native) {
            return double.NaN;
        }
        try {
            return o5_ring_last(handle);
        } catch {
            return double.NaN;
        }
    }

    public static long Count(long handle) {
        if (handle == 0) {
            return 0;
        }
        if (handle < 0) {
            return TryFallback(handle, out var ring) ? ring.Count() : 0;
        }
        if (!Native) {
            return 0;
        }
        try {
            return (long)o5_ring_len(handle);
        } catch {
            return 0;
        }
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern long o5_ring_create(nuint cap);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void o5_ring_destroy(long handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern void o5_ring_push(long handle, double t);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern void o5_ring_prune(long handle, double now, double window);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_ring_decay_sum(long handle, double now);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_ring_first(long handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_ring_last(long handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint o5_ring_len(long handle);
}
