using Overlayer.Native;
using Xunit;

namespace Overlayer.Tests;

public sealed class VertexBuilderTests {
    [Fact]
    public void Native_BuildGraphQuads_ProducesCorrectQuadVertices() {
        Assert.True(OvVertexBuilder.IsSupported);

        Span<OvPoint2D> points = stackalloc OvPoint2D[] {
            new() { X = 0f, Y = 0f },
            new() { X = 100f, Y = 0f },
        };

        var xform = new OvMapTransform {
            ScaleX = 1f,
            OffsetX = 0f,
            ScaleY = 1f,
            OffsetY = 0f,
            XMin = -1000f,
        };

        Span<OvQuad2D> quads = stackalloc OvQuad2D[1];
        int count = OvVertexBuilder.BuildGraphQuads(points, xform, 2.5f, quads);

        Assert.Equal(1, count);
        // p0 = a - n = (0, -2.5)
        // p1 = a + n = (0, 2.5)
        // p2 = b + n = (100, 2.5)
        // p3 = b - n = (100, -2.5)
        Assert.Equal(0f, quads[0].P0.X, 0.01f);
        Assert.Equal(-2.5f, quads[0].P0.Y, 0.01f);
        Assert.Equal(0f, quads[0].P1.X, 0.01f);
        Assert.Equal(2.5f, quads[0].P1.Y, 0.01f);
        Assert.Equal(100f, quads[0].P2.X, 0.01f);
        Assert.Equal(2.5f, quads[0].P2.Y, 0.01f);
        Assert.Equal(100f, quads[0].P3.X, 0.01f);
        Assert.Equal(-2.5f, quads[0].P3.Y, 0.01f);
    }

    [Fact]
    public void Native_BuildGraphQuads_ClipsLeftCorrectly() {
        Assert.True(OvVertexBuilder.IsSupported);

        Span<OvPoint2D> points = stackalloc OvPoint2D[] {
            new() { X = -50f, Y = 0f },
            new() { X = 50f, Y = 0f },
        };

        var xform = new OvMapTransform {
            ScaleX = 1f,
            OffsetX = 0f,
            ScaleY = 1f,
            OffsetY = 0f,
            XMin = 0f, // Clip left at 0
        };

        Span<OvQuad2D> quads = stackalloc OvQuad2D[1];
        int count = OvVertexBuilder.BuildGraphQuads(points, xform, 1f, quads);

        Assert.Equal(1, count);
        // Clipped a starts at x = 0
        Assert.Equal(0f, quads[0].P0.X, 0.01f);
        Assert.Equal(0f, quads[0].P1.X, 0.01f);
        Assert.Equal(50f, quads[0].P2.X, 0.01f);
        Assert.Equal(50f, quads[0].P3.X, 0.01f);
    }
}
