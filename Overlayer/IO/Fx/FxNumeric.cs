using System.Collections.Concurrent;
using System.Globalization;
using Overlayer.Native;

namespace Overlayer.IO.Fx;

internal static class FxNumeric {
    private const int MaxCache = 512;

    private sealed class Entry {
        public static readonly Entry Fail = new();
        public readonly object Gate = new();
        public long Zig;
    }

    private static readonly ConcurrentDictionary<string, Entry> cache = new();
    private static readonly object evictGate = new();

    public static bool TryEvaluate(string rendered, Type targetType, out object value) {
        value = null;
        if (string.IsNullOrEmpty(rendered)) {
            return false;
        }
        if (double.TryParse(rendered, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct)
            && !double.IsNaN(direct) && !double.IsInfinity(direct)) {
            try {
                value = Convert.ChangeType(direct, targetType, CultureInfo.InvariantCulture);
                return true;
            } catch {
                return false;
            }
        }
        var entry = cache.GetOrAdd(rendered, Compile);
        if (ReferenceEquals(entry, Entry.Fail)) {
            return false;
        }
        lock (entry.Gate) {
            try {
                if (entry.Zig == 0 || !O5Expr.TryEval(entry.Zig, out var d)) {
                    return false;
                }
                value = Convert.ChangeType(d, targetType, CultureInfo.InvariantCulture);
                return true;
            } catch {
                return false;
            }
        }
    }

    private static Entry Compile(string rendered) {
        EvictIfFull();
        if (O5Expr.IsSupported) {
            long handle = O5Expr.Compile(rendered);
            if (handle != 0) {
                return new Entry { Zig = handle };
            }
        }
        return Entry.Fail;
    }

    private static void EvictIfFull() {
        if (cache.Count < MaxCache) {
            return;
        }
        lock (evictGate) {
            if (cache.Count < MaxCache) {
                return;
            }
            foreach (var kv in cache) {
                if (kv.Value != null && !ReferenceEquals(kv.Value, Entry.Fail) && kv.Value.Zig != 0) {
                    lock (kv.Value.Gate) {
                        O5Expr.Free(kv.Value.Zig);
                        kv.Value.Zig = 0;
                    }
                }
            }
            cache.Clear();
        }
    }
}
