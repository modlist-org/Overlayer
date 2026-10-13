using System.Globalization;
using Overlayer.Native;

namespace Overlayer.IO.Fx;

internal static class FxNumeric {
    private const int MaxCache = 256;
    private static readonly Dictionary<string, long> cache = new(StringComparer.Ordinal);
    private static readonly Queue<string> order = new();
    private static readonly object gate = new();

    public static bool TryEvaluate(string rendered, Type targetType, out object value) {
        value = null;
        if (string.IsNullOrEmpty(rendered)) return false;
        if (double.TryParse(rendered, NumberStyles.Float, CultureInfo.InvariantCulture, out double direct)
            && !double.IsNaN(direct) && !double.IsInfinity(direct)) {
            return TryConvert(direct, targetType, out value);
        }
        if (!O5Expr.IsSupported) return false;

        // Compilation, evaluation and eviction share ownership protection.
        // Previously a caller could obtain an Entry just before another thread
        // freed and zeroed its program during a whole-cache eviction.
        lock (gate) {
            if (!cache.TryGetValue(rendered, out long handle)) {
                if (cache.Count >= MaxCache) {
                    string oldest = order.Dequeue();
                    O5Expr.Free(cache[oldest]);
                    cache.Remove(oldest);
                }
                handle = O5Expr.Compile(rendered);
                cache.Add(rendered, handle);
                order.Enqueue(rendered);
            }
            if (handle == 0 || !O5Expr.TryEval(handle, out double number)) return false;
            return TryConvert(number, targetType, out value);
        }
    }

    private static bool TryConvert(double number, Type targetType, out object value) {
        try {
            value = Convert.ChangeType(number, targetType, CultureInfo.InvariantCulture);
            return true;
        } catch {
            value = null;
            return false;
        }
    }
}
