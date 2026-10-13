using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct OvPoint2D {
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct OvQuad2D {
    public OvPoint2D P0;
    public OvPoint2D P1;
    public OvPoint2D P2;
    public OvPoint2D P3;
}

[StructLayout(LayoutKind.Sequential)]
internal struct OvMapTransform {
    public float ScaleX;
    public float OffsetX;
    public float ScaleY;
    public float OffsetY;
    public float XMin;
}

internal static unsafe class OvVertexBuilder {
    private const string LibName = "overlayernative";

    public static bool IsSupported => OvNative.IsAvailable;

    public static int BuildGraphQuads(
        ReadOnlySpan<OvPoint2D> points,
        in OvMapTransform transform,
        float halfThickness,
        Span<OvQuad2D> outQuads
    ) {
        if (points.Length < 2 || outQuads.Length == 0) return 0;

        if (IsSupported) {
            try {
                fixed (OvPoint2D* pPtr = points)
                fixed (OvQuad2D* qPtr = outQuads) {
                    return (int)ov_graph_build_quads(
                        pPtr,
                        (nuint)points.Length,
                        transform,
                        halfThickness,
                        qPtr,
                        (nuint)outQuads.Length
                    );
                }
            } catch {
                // fallback to managed
            }
        }

        return BuildGraphQuadsManaged(points, transform, halfThickness, outQuads);
    }

    public static int BuildGraphQuadsManaged(
        ReadOnlySpan<OvPoint2D> points,
        in OvMapTransform transform,
        float halfThickness,
        Span<OvQuad2D> outQuads
    ) {
        if (points.Length < 2) return 0;
        int maxQuads = Math.Min(points.Length - 1, outQuads.Length);
        int written = 0;

        for (int i = 0; i < maxQuads; i++) {
            var rawA = points[i];
            var rawB = points[i + 1];

            float ax = rawA.X * transform.ScaleX + transform.OffsetX;
            float ay = rawA.Y * transform.ScaleY + transform.OffsetY;
            float bx = rawB.X * transform.ScaleX + transform.OffsetX;
            float by = rawB.Y * transform.ScaleY + transform.OffsetY;

            if (bx < transform.XMin) continue;

            if (ax < transform.XMin) {
                float dx = bx - ax;
                if (dx > 0.000001f) {
                    float t = (transform.XMin - ax) / dx;
                    ax = transform.XMin;
                    ay = ay + (by - ay) * t;
                } else {
                    ax = bx;
                    ay = by;
                }
            }

            float segDx = bx - ax;
            float segDy = by - ay;
            float sqLen = segDx * segDx + segDy * segDy;
            if (sqLen < 0.000001f) continue;

            float invLen = 1.0f / MathF.Sqrt(sqLen);
            float nx = -segDy * invLen * halfThickness;
            float ny = segDx * invLen * halfThickness;

            outQuads[written++] = new OvQuad2D {
                P0 = new OvPoint2D { X = ax - nx, Y = ay - ny },
                P1 = new OvPoint2D { X = ax + nx, Y = ay + ny },
                P2 = new OvPoint2D { X = bx + nx, Y = by + ny },
                P3 = new OvPoint2D { X = bx - nx, Y = by - ny },
            };
        }

        return written;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint ov_graph_build_quads(
        OvPoint2D* pointsPtr,
        nuint count,
        OvMapTransform transform,
        float halfThickness,
        OvQuad2D* outQuadsPtr,
        nuint maxQuads
    );
}
