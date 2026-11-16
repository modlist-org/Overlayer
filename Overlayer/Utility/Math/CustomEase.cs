using System.Globalization;
using System.Text;

// Not Overlayer.Utility.Math: that namespace would shadow System.Math across Overlayer.Utility.
namespace Overlayer.Utility;

public enum CustomEaseKind {
    Curve,
    Lines,
    Mono,
    Anim,
}

// Inline ease curve written in a tag argument, e.g. curve(0,0;0.3,0.8;1,1).
// curve: Catmull-Rom through the points, lines: straight segments, mono: monotone cubic (Fritsch-Carlson),
// anim: Unity AnimationCurve style, each key x,y,inTangent,outTangent (slopes; x,y,t sets both, x,y is automatic).
public sealed class CustomEase {
    private const int MaxCached = 128;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly object cacheLock = new();
    private static readonly Dictionary<string, CustomEase> cache = [];

    public readonly CustomEaseKind Kind;
    private readonly float[] xs;
    private readonly float[] ys;
    // Slopes arriving at / leaving each point; equal except for anim keys with broken tangents.
    private readonly float[] inTangents;
    private readonly float[] outTangents;

    public int Count => xs.Length;
    public (float x, float y) this[int index] => (xs[index], ys[index]);

    /// <summary>Slopes into and out of a point (0 for lines).</summary>
    public (float inTangent, float outTangent) TangentAt(int index)
        => inTangents == null ? (0f, 0f) : (inTangents[index], outTangents[index]);

    private CustomEase(CustomEaseKind kind, float[] xs, float[] ys, float[] inTangents = null, float[] outTangents = null) {
        Kind = kind;
        this.xs = xs;
        this.ys = ys;
        if (kind == CustomEaseKind.Anim) {
            this.inTangents = inTangents;
            this.outTangents = outTangents;
            return;
        }
        this.inTangents = kind switch {
            CustomEaseKind.Curve => CatmullRomTangents(xs, ys),
            CustomEaseKind.Mono => MonotoneTangents(xs, ys),
            _ => null
        };
        this.outTangents = this.inTangents;
    }

    public static bool TryParse(string text, out CustomEase ease) {
        ease = null;
        if (text == null) {
            return false;
        }
        lock (cacheLock) {
            if (cache.TryGetValue(text, out ease)) {
                return ease != null;
            }
        }

        ease = Parse(text);
        lock (cacheLock) {
            if (cache.Count >= MaxCached) {
                cache.Clear();
            }
            // Failures are cached too, so a bad string isn't reparsed every frame.
            if (cache.TryGetValue(text, out var existing)) {
                ease = existing;
            } else {
                cache[text] = ease;
            }
        }
        return ease != null;
    }

    private static CustomEase Parse(string text) {
        string s = text.Trim();
        int open = s.IndexOf('(');
        if (open <= 0 || s[s.Length - 1] != ')') {
            return null;
        }

        CustomEaseKind kind;
        string name = s.Substring(0, open).Trim();
        if (name.Equals("curve", StringComparison.OrdinalIgnoreCase)) {
            kind = CustomEaseKind.Curve;
        } else if (name.Equals("lines", StringComparison.OrdinalIgnoreCase)) {
            kind = CustomEaseKind.Lines;
        } else if (name.Equals("mono", StringComparison.OrdinalIgnoreCase)) {
            kind = CustomEaseKind.Mono;
        } else if (name.Equals("anim", StringComparison.OrdinalIgnoreCase)) {
            kind = CustomEaseKind.Anim;
        } else {
            return null;
        }

        string inner = s.Substring(open + 1, s.Length - open - 2);
        string[] entries = inner.Split(';');
        if (entries.Length < 2) {
            return null;
        }

        int n = entries.Length;
        var xs = new float[n];
        var ys = new float[n];
        var ins = kind == CustomEaseKind.Anim ? new float[n] : null;
        var outs = kind == CustomEaseKind.Anim ? new float[n] : null;
        var auto = kind == CustomEaseKind.Anim ? new bool[n] : null;
        for (int i = 0; i < n; i++) {
            string[] parts = entries[i].Split(',');
            int max = kind == CustomEaseKind.Anim ? 4 : 2;
            if (parts.Length < 2 || parts.Length > max
                || !TryNumber(parts[0], out float x)
                || !TryNumber(parts[1], out float y)
                || x < 0f || x > 1f
                || (i > 0 && x <= xs[i - 1])) {
                return null;
            }
            xs[i] = x;
            ys[i] = y;
            if (kind != CustomEaseKind.Anim) {
                continue;
            }
            if (parts.Length == 2) {
                auto[i] = true;
                continue;
            }
            if (!TryNumber(parts[2], out float inT)) {
                return null;
            }
            float outT = inT;
            if (parts.Length == 4 && !TryNumber(parts[3], out outT)) {
                return null;
            }
            ins[i] = inT;
            outs[i] = outT;
        }
        if (kind == CustomEaseKind.Anim) {
            // Keys without slopes get the smooth (Catmull-Rom) one.
            float[] smooth = CatmullRomTangents(xs, ys);
            for (int i = 0; i < n; i++) {
                if (auto[i]) {
                    ins[i] = outs[i] = smooth[i];
                }
            }
        }
        return new CustomEase(kind, xs, ys, ins, outs);
    }

    private static bool TryNumber(string text, out float value) {
        return float.TryParse(text.Trim(), NumberStyles.Float, Invariant, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public float Evaluate(float t) {
        if (float.IsNaN(t)) {
            t = 0f;
        }
        t = t < 0f ? 0f : t > 1f ? 1f : t;
        int last = xs.Length - 1;
        if (t <= xs[0]) {
            return ys[0];
        }
        if (t >= xs[last]) {
            return ys[last];
        }

        // Largest i with xs[i] <= t (i < last here).
        int lo = 0;
        int hi = last;
        while (hi - lo > 1) {
            int mid = (lo + hi) >> 1;
            if (xs[mid] <= t) {
                lo = mid;
            } else {
                hi = mid;
            }
        }

        float h = xs[lo + 1] - xs[lo];
        float u = (t - xs[lo]) / h;
        float y0 = ys[lo];
        float y1 = ys[lo + 1];
        if (inTangents == null) {
            return y0 + ((y1 - y0) * u);
        }

        // Cubic Hermite: leaves lo with its out-slope, arrives at lo+1 with its in-slope.
        float u2 = u * u;
        float u3 = u2 * u;
        float h00 = (2f * u3) - (3f * u2) + 1f;
        float h10 = u3 - (2f * u2) + u;
        float h01 = (-2f * u3) + (3f * u2);
        float h11 = u3 - u2;
        return (h00 * y0) + (h10 * h * outTangents[lo]) + (h01 * y1) + (h11 * h * inTangents[lo + 1]);
    }

    private static float[] Secants(float[] xs, float[] ys) {
        var d = new float[xs.Length - 1];
        for (int i = 0; i < d.Length; i++) {
            d[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
        }
        return d;
    }

    private static float[] CatmullRomTangents(float[] xs, float[] ys) {
        int n = xs.Length;
        var m = new float[n];
        m[0] = (ys[1] - ys[0]) / (xs[1] - xs[0]);
        m[n - 1] = (ys[n - 1] - ys[n - 2]) / (xs[n - 1] - xs[n - 2]);
        for (int i = 1; i < n - 1; i++) {
            m[i] = (ys[i + 1] - ys[i - 1]) / (xs[i + 1] - xs[i - 1]);
        }
        return m;
    }

    private static float[] MonotoneTangents(float[] xs, float[] ys) {
        int n = xs.Length;
        float[] d = Secants(xs, ys);
        var m = new float[n];
        m[0] = d[0];
        m[n - 1] = d[n - 2];
        for (int i = 1; i < n - 1; i++) {
            m[i] = d[i - 1] * d[i] <= 0f ? 0f : (d[i - 1] + d[i]) * 0.5f;
        }

        for (int i = 0; i < n - 1; i++) {
            if (d[i] == 0f) {
                m[i] = 0f;
                m[i + 1] = 0f;
                continue;
            }
            float a = m[i] / d[i];
            float b = m[i + 1] / d[i];
            if (a < 0f) {
                m[i] = a = 0f;
            }
            if (b < 0f) {
                m[i + 1] = b = 0f;
            }
            float s = (a * a) + (b * b);
            if (s > 9f) {
                float tau = 3f / (float)System.Math.Sqrt(s);
                m[i] = tau * a * d[i];
                m[i + 1] = tau * b * d[i];
            }
        }
        return m;
    }

    public override string ToString() {
        var points = new (float x, float y)[xs.Length];
        var slopes = Kind == CustomEaseKind.Anim ? new (float inTangent, float outTangent)[xs.Length] : null;
        for (int i = 0; i < points.Length; i++) {
            points[i] = (xs[i], ys[i]);
            if (slopes != null) {
                slopes[i] = TangentAt(i);
            }
        }
        return Format(Kind, points, slopes);
    }

    /// <summary>
    /// Canonical text for a curve. <paramref name="tangents"/> is used for <see cref="CustomEaseKind.Anim"/>
    /// only (one pair per point); a key whose slopes match is written with a single slope.
    /// </summary>
    public static string Format(CustomEaseKind kind, IReadOnlyList<(float x, float y)> points,
        IReadOnlyList<(float inTangent, float outTangent)> tangents = null) {
        var sb = new StringBuilder(kind switch {
            CustomEaseKind.Lines => "lines",
            CustomEaseKind.Mono => "mono",
            CustomEaseKind.Anim => "anim",
            _ => "curve"
        });
        sb.Append('(');
        for (int i = 0; i < points.Count; i++) {
            if (i > 0) {
                sb.Append(';');
            }
            sb.Append(FormatNumber(points[i].x)).Append(',').Append(FormatNumber(points[i].y));
            if (kind != CustomEaseKind.Anim || tangents == null || i >= tangents.Count) {
                continue;
            }
            string inT = FormatNumber(tangents[i].inTangent);
            string outT = FormatNumber(tangents[i].outTangent);
            sb.Append(',').Append(inT);
            if (outT != inT) {
                sb.Append(',').Append(outT);
            }
        }
        return sb.Append(')').ToString();
    }

    private static string FormatNumber(float value) {
        string text = System.Math.Round((double)value, 3).ToString("0.###", Invariant);
        return text == "-0" ? "0" : text;
    }
}
