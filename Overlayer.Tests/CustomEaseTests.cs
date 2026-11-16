using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Overlayer.Utility;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class CustomEaseTests {
    private const float Eps = 1e-4f;

    private static CustomEase Parse(string text) {
        Assert.True(CustomEase.TryParse(text, out var ease), text);
        return ease;
    }

    [Theory]
    [InlineData("curve(0,0;1,1)", CustomEaseKind.Curve)]
    [InlineData("LINES(0,0;0.5,0.2;1,1)", CustomEaseKind.Lines)]
    [InlineData(" mono ( 0 , 0 ; 0.4 , 0.9 ; 1 , 1 ) ", CustomEaseKind.Mono)]
    [InlineData("curve(0,0;0.3,-0.25;1,1.5)", CustomEaseKind.Curve)]
    [InlineData("lines(0.2,0;0.8,1)", CustomEaseKind.Lines)]
    public void TryParse_Valid(string text, CustomEaseKind kind) {
        Assert.Equal(kind, Parse(text).Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OutExpo")]
    [InlineData("curve")]
    [InlineData("curve()")]
    [InlineData("curve(0,0)")]
    [InlineData("curve(0,0;1,1")]
    [InlineData("spline(0,0;1,1)")]
    [InlineData("(0,0;1,1)")]
    [InlineData("curve(0,0;0.5;1,1)")]
    [InlineData("curve(0,0,0;1,1)")]
    [InlineData("curve(0,0;0.5,a;1,1)")]
    [InlineData("curve(0,0;0.5,0.5;0.5,0.6;1,1)")]
    [InlineData("curve(0.6,0;0.4,1)")]
    [InlineData("curve(-0.1,0;1,1)")]
    [InlineData("curve(0,0;1.1,1)")]
    [InlineData("curve(0,0;1,NaN)")]
    [InlineData("curve(0,0;;1,1)")]
    [InlineData("curve(0,0;1,1)x")]
    public void TryParse_Invalid(string text) {
        Assert.False(CustomEase.TryParse(text, out var ease));
        Assert.Null(ease);
    }

    [Fact]
    public void Lines_ExactMidpoints() {
        var ease = Parse("lines(0,0;0.5,0.2;1,1)");
        Assert.Equal(0.1f, ease.Evaluate(0.25f), Eps);
        Assert.Equal(0.2f, ease.Evaluate(0.5f), Eps);
        Assert.Equal(0.6f, ease.Evaluate(0.75f), Eps);
    }

    [Theory]
    [InlineData("curve(0,0;0.3,0.8;0.6,1.15;1,1)")]
    [InlineData("mono(0,0;0.4,0.9;1,1)")]
    [InlineData("lines(0,0;0.1,0.7;0.65,0.3;1,1)")]
    public void PassesThroughEveryControlPoint(string text) {
        var ease = Parse(text);
        for (int i = 0; i < ease.Count; i++) {
            var (x, y) = ease[i];
            Assert.Equal(y, ease.Evaluate(x), Eps);
        }
    }

    [Fact]
    public void Endpoints_ClampInputAndHoldOutsidePoints() {
        var ease = Parse("curve(0,0;0.3,0.8;0.6,1.15;1,1)");
        Assert.Equal(0f, ease.Evaluate(0f), Eps);
        Assert.Equal(1f, ease.Evaluate(1f), Eps);
        Assert.Equal(0f, ease.Evaluate(-5f), Eps);
        Assert.Equal(1f, ease.Evaluate(5f), Eps);

        var partial = Parse("mono(0.2,0.1;0.8,0.9)");
        Assert.Equal(0.1f, partial.Evaluate(0f), Eps);
        Assert.Equal(0.1f, partial.Evaluate(0.2f), Eps);
        Assert.Equal(0.9f, partial.Evaluate(0.9f), Eps);
        Assert.Equal(0.9f, partial.Evaluate(1f), Eps);
    }

    [Fact]
    public void Curve_AllowsOvershoot() {
        // Control point above 1 passes through, so the output is not clamped.
        var ease = Parse("curve(0,0;0.3,0.8;0.6,1.15;1,1)");
        Assert.Equal(1.15f, ease.Evaluate(0.6f), Eps);

        // Plain Catmull-Rom overshoots a flat section next to a steep one; mono must not.
        var steep = Parse("curve(0,0;0.1,0.9;0.5,1;1,1)");
        float max = 0f;
        for (int i = 0; i <= 1000; i++) {
            max = Math.Max(max, steep.Evaluate(i / 1000f));
        }
        Assert.True(max > 1f + 1e-3f, $"max {max}");
    }

    [Theory]
    [InlineData("mono(0,0;0.1,0.9;0.5,1;1,1)")]
    [InlineData("mono(0,0;0.4,0.9;1,1)")]
    [InlineData("mono(0,0;0.05,0.6;0.5,0.62;0.55,1;1,1)")]
    [InlineData("mono(0,1;0.2,0.95;0.3,0.1;1,0)")]
    public void Mono_NeverOvershootsBetweenPoints(string text) {
        var ease = Parse(text);
        for (int seg = 0; seg < ease.Count - 1; seg++) {
            var (x0, y0) = ease[seg];
            var (x1, y1) = ease[seg + 1];
            float lo = Math.Min(y0, y1) - Eps;
            float hi = Math.Max(y0, y1) + Eps;
            float prev = y0;
            for (int i = 0; i <= 200; i++) {
                float y = ease.Evaluate(x0 + ((x1 - x0) * i / 200f));
                Assert.InRange(y, lo, hi);
                // Monotonic within the segment, in the direction of the data.
                Assert.True(y1 >= y0 ? y >= prev - Eps : y <= prev + Eps, $"{text} seg {seg} step {i}");
                prev = y;
            }
        }
    }

    [Fact]
    public void Format_RoundTrips() {
        (float, float)[] points = [(0f, 0f), (0.3f, 0.8f), (0.6f, 1.15f), (1f, 1f)];
        string text = CustomEase.Format(CustomEaseKind.Curve, points);
        Assert.Equal("curve(0,0;0.3,0.8;0.6,1.15;1,1)", text);

        var ease = Parse(text);
        Assert.Equal(points.Length, ease.Count);
        for (int i = 0; i < points.Length; i++) {
            Assert.Equal(points[i].Item1, ease[i].x, Eps);
            Assert.Equal(points[i].Item2, ease[i].y, Eps);
        }
        Assert.Equal(text, ease.ToString());

        Assert.Equal("mono(0,-0.123;0.5,0.5;1,1)", CustomEase.Format(CustomEaseKind.Mono, [(0f, -0.12345f), (0.5f, 0.50001f), (1f, 1f)]));
        Assert.Equal("lines(0,0;1,1)", CustomEase.Format(CustomEaseKind.Lines, [(0f, -0.0001f), (1f, 1f)]));
    }

    [Fact]
    public void TryParse_CachesInstances() {
        var first = Parse("curve(0,0;0.42,0.77;1,1)");
        var second = Parse("curve(0,0;0.42,0.77;1,1)");
        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("anim(0,0;1,1)")]
    [InlineData("anim(0,0,2;1,1,0)")]
    [InlineData("anim(0,0,0,3;0.5,1,-1;1,1,0,0)")]
    public void Anim_ParsesTwoThreeOrFourNumbersPerKey(string text) {
        Assert.Equal(CustomEaseKind.Anim, Parse(text).Kind);
    }

    [Theory]
    [InlineData("anim(0,0,1,2,3;1,1)")]
    [InlineData("anim(0,0,a;1,1)")]
    [InlineData("curve(0,0,1;1,1)")]
    public void Anim_RejectsBadKeys(string text) {
        Assert.False(CustomEase.TryParse(text, out _));
    }

    [Fact]
    public void Anim_UsesGivenSlopes() {
        // Flat at both ends: a smoothstep, 0.5 in the middle.
        var flat = Parse("anim(0,0,0;1,1,0)");
        Assert.Equal(0.5f, flat.Evaluate(0.5f), Eps);
        Assert.Equal(0.15625f, flat.Evaluate(0.25f), Eps);
        // Slope 1 at both ends is the straight line.
        var line = Parse("anim(0,0,1;1,1,1)");
        Assert.Equal(0.3f, line.Evaluate(0.3f), Eps);
    }

    [Fact]
    public void Anim_BrokenTangents_UseOutOnTheLeftAndInOnTheRight() {
        // Middle key arrives flat (in 0) and leaves steep (out 4).
        var ease = Parse("anim(0,0,0;0.5,0.5,0,4;1,1,0)");
        Assert.Equal((0f, 4f), ease.TangentAt(1));
        Assert.Equal(0.5f, ease.Evaluate(0.5f), Eps);
        // Just before the key the curve is nearly flat, just after it climbs fast.
        float before = ease.Evaluate(0.5f) - ease.Evaluate(0.49f);
        float after = ease.Evaluate(0.51f) - ease.Evaluate(0.5f);
        Assert.True(after > before * 5f, $"before {before}, after {after}");
    }

    [Fact]
    public void Anim_KeysWithoutSlopes_MatchCurve() {
        var anim = Parse("anim(0,0;0.3,0.8;0.6,1.15;1,1)");
        var curve = Parse("curve(0,0;0.3,0.8;0.6,1.15;1,1)");
        for (int i = 0; i <= 20; i++) {
            float t = i / 20f;
            Assert.Equal(curve.Evaluate(t), anim.Evaluate(t), Eps);
        }
    }

    [Fact]
    public void Anim_FormatRoundTrip() {
        var points = new List<(float x, float y)> { (0f, 0f), (0.5f, 0.75f), (1f, 1f) };
        var slopes = new List<(float inTangent, float outTangent)> { (0f, 2f), (1.5f, 1.5f), (0.25f, 0.25f) };
        string text = CustomEase.Format(CustomEaseKind.Anim, points, slopes);
        Assert.Equal("anim(0,0,0,2;0.5,0.75,1.5;1,1,0.25)", text);
        var ease = Parse(text);
        Assert.Equal(text, ease.ToString());
        Assert.Equal((0f, 2f), ease.TangentAt(0));
    }

    // Both tag syntaxes must hand the whole curve(...) text to the ease parameter.
    private static class ProbeTags {
        public static string Probe(string tagName, int digits = -1, double speed = 500, string ease = "Linear")
            => string.Join("|", tagName, digits, speed, ease);
    }

    private static string Render(string text) {
        TagManager.Set(new TagCore("CustomEaseProbe", typeof(ProbeTags).GetMethod(nameof(ProbeTags.Probe), BindingFlags.Public | BindingFlags.Static)!, TagType.ProcessFormat));
        var parsed = Parser.Parse(text);
        Assert.Single(parsed);
        return TagCache.Instance.GetOrCompile(parsed[0]).Get();
    }

    [Fact]
    public void CallForm_KeepsCurveArgumentWhole() {
        Assert.Equal("Combo|0|500|curve(0,0;0.3,0.8;1,1)", Render("{CustomEaseProbe(Combo,0,500,curve(0,0;0.3,0.8;1,1))}"));
        Assert.Equal("Combo|0|500|OutExpo", Render("{CustomEaseProbe(Combo, 0, 500, OutExpo)}"));
    }
}
