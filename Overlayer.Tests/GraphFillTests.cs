using Overlayer.Native;
using Xunit;

namespace Overlayer.Tests;

public sealed class GraphFillTests {
    private static readonly OvMapTransform Identity = new() { ScaleX = 1, ScaleY = 1, XMin = 0 };

    [Fact]
    public void FillClipsPrecedingSampleAndPreservesBaseline() {
        OvPoint2D[] points = [new() { X = -10, Y = 2 }, new() { X = 10, Y = 6 }];
        var quads = new OvQuad2D[1];
        Assert.Equal(1, OvVertexBuilder.BuildGraphFillQuadsManaged(points, Identity, -3, quads));
        Assert.Equal(0, quads[0].P0.X);
        Assert.Equal(-3, quads[0].P0.Y);
        Assert.Equal(10, quads[0].P2.X);
        Assert.Equal(6, quads[0].P2.Y);
        Assert.Equal(4, quads[0].P3.Y);
    }

    [Fact]
    public void SmallOutputStillScansPastInvisibleAndDegenerateLineSegments() {
        OvPoint2D[] points = [new() { X = -20 }, new() { X = -10 }, new(), new() { X = 10 }];
        var quads = new OvQuad2D[1];
        Assert.Equal(1, OvVertexBuilder.BuildGraphQuadsManaged(points, Identity, 1, quads));
        Assert.Equal(10, quads[0].P2.X);
        Assert.Equal(1, OvVertexBuilder.BuildGraphFillQuadsManaged(points, Identity, 0, quads));
        Assert.Equal(0, OvVertexBuilder.BuildGraphFillQuadsManaged(points, Identity, 0, []));
        Assert.Equal(0, OvVertexBuilder.BuildGraphFillQuadsManaged(points.AsSpan(0, 1), Identity, 0, quads));
    }

    [Fact]
    public void NativeFillMatchesManagedAcrossRandomTransformsAndCapacities() {
        Assert.True(OvVertexBuilder.IsSupported);
        var random = new Random(42);
        var points = new OvPoint2D[257];
        for (int i = 0; i < points.Length; i++)
            points[i] = new OvPoint2D { X = i - 100, Y = (float)random.NextDouble() * 40 - 20 };
        foreach (int capacity in new[] { 0, 1, 7, 256 }) {
            var native = new OvQuad2D[capacity];
            var managed = new OvQuad2D[capacity];
            var transform = new OvMapTransform { ScaleX = 2, ScaleY = 3, OffsetX = 4, OffsetY = -5, XMin = 0 };
            int count = OvVertexBuilder.BuildGraphFillQuads(points, transform, -10, native);
            if (capacity > 0) Assert.True(OvVertexBuilder.IsFillSupported);
            Assert.Equal(OvVertexBuilder.BuildGraphFillQuadsManaged(points, transform, -10, managed), count);
            for (int i = 0; i < count; i++) {
                AssertPoint(managed[i].P0, native[i].P0);
                AssertPoint(managed[i].P1, native[i].P1);
                AssertPoint(managed[i].P2, native[i].P2);
                AssertPoint(managed[i].P3, native[i].P3);
            }
        }
    }

    private static void AssertPoint(OvPoint2D expected, OvPoint2D actual) {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, 0.001f);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, 0.001f);
    }
}
