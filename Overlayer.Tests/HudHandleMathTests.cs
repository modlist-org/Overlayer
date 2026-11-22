using Overlayer.HudEdit;
using Xunit;

namespace Overlayer.Tests;

public sealed class HudHandleMathTests {
    // 200x100 axis-aligned box: BL=(100,100), TL=(100,200), TR=(300,200), BR=(300,100)
    private static readonly Pt C0 = new(100f, 100f);
    private static readonly Pt C1 = new(100f, 200f);
    private static readonly Pt C2 = new(300f, 200f);
    private static readonly Pt C3 = new(300f, 100f);

    [Fact]
    public void CornerHitTest_AllFourCornersCenterAndTinyBox() {
        Pt[] corners = [C0, C1, C2, C3];
        for (int i = 0; i < 4; i++) {
            var hit = HudHandleMath.HitTest(C0, C1, C2, C3, corners[i], true, true, true, true);
            Assert.Equal(HudHandleKind.Corner, hit.Kind);
            Assert.Equal(i, hit.Index);
        }

        // Center of box should not hit any handle so dragging moves it.
        var center = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(200f, 150f), true, true, true, true);
        Assert.Equal(HudHandleKind.None, center.Kind);

        // Tiny 12x12 box: interior center (6,6) must remain movable (not captured by corner/edge).
        var tiny = HudHandleMath.HitTest(
            new Pt(0f, 0f), new Pt(0f, 12f), new Pt(12f, 12f), new Pt(12f, 0f),
            new Pt(6f, 6f), true, true, true, true);
        Assert.Equal(HudHandleKind.None, tiny.Kind);
    }

    [Fact]
    public void EdgeHitTest_AlongAllFourEdges() {
        var left = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(100f, 150f), true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, left.Kind);
        Assert.Equal(0, left.Index);

        var topMid = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(200f, 200f), true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, topMid.Kind);
        Assert.Equal(1, topMid.Index);

        var topOffCenter = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(140f, 203f), true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, topOffCenter.Kind);
        Assert.Equal(1, topOffCenter.Index);

        var right = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(304f, 150f), true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, right.Kind);
        Assert.Equal(2, right.Index);

        var bottom = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(200f, 96f), true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, bottom.Kind);
        Assert.Equal(3, bottom.Index);
    }

    [Fact]
    public void OuterCornerRotationZones_TopLeftQuadrantsAndExclusions() {
        // TL is (100, 200).
        // Q2 (left & up from TL)
        var q2 = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(88f, 212f), true, true, true, true);
        Assert.Equal(HudHandleKind.Rotate, q2.Kind);
        Assert.Equal(1, q2.Index);

        // Q3 outer (left of left edge, just below TL, outside EdgeHitPx=6)
        var q3Outer = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(88f, 192f), true, true, true, true);
        Assert.Equal(HudHandleKind.Rotate, q3Outer.Kind);
        Assert.Equal(1, q3Outer.Index);

        // Q1 outer (above top edge, just right of TL, outside EdgeHitPx=6)
        var q1Outer = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(108f, 212f), true, true, true, true);
        Assert.Equal(HudHandleKind.Rotate, q1Outer.Kind);
        Assert.Equal(1, q1Outer.Index);

        // Q4 interior of box (right & down from TL, outside corner/edge hit) -> None (move)
        var q4Interior = HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(115f, 185f), true, true, true, true);
        Assert.Equal(HudHandleKind.None, q4Interior.Kind);

        // Outside middle of each edge -> None (must not rotate)
        Assert.Equal(HudHandleKind.None, HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(200f, 212f), true, true, true, true).Kind);
        Assert.Equal(HudHandleKind.None, HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(200f, 88f), true, true, true, true).Kind);
        Assert.Equal(HudHandleKind.None, HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(88f, 150f), true, true, true, true).Kind);
        Assert.Equal(HudHandleKind.None, HudHandleMath.HitTest(C0, C1, C2, C3, new Pt(312f, 150f), true, true, true, true).Kind);
    }

    [Fact]
    public void Rotated45DegBox_HitsHandlesAndRotatesAngles() {
        float s = MathF.Sqrt(0.5f);
        Pt Rot(float x, float y) => new(200f + x * s - y * s, 200f + x * s + y * s);
        Pt r0 = Rot(-60f, -40f);
        Pt r1 = Rot(-60f, 40f);
        Pt r2 = Rot(60f, 40f);
        Pt r3 = Rot(60f, -40f);

        // Grabbing rotated TR corner (index 2)
        var cHit = HudHandleMath.HitTest(r0, r1, r2, r3, r2, true, true, true, true);
        Assert.Equal(HudHandleKind.Corner, cHit.Kind);
        Assert.Equal(2, cHit.Index);

        // Rotated top edge midpoint (normal points along +Y rotated by 45 deg => 135 deg)
        Pt topMidOut = Rot(0f, 42f);
        var eHit = HudHandleMath.HitTest(r0, r1, r2, r3, topMidOut, true, true, true, true);
        Assert.Equal(HudHandleKind.Edge, eHit.Kind);
        Assert.Equal(1, eHit.Index);
        Assert.Equal(135, HudHandleMath.QuantizeResizeAngle(eHit.AngleDeg));

        // Outer corner rotation at rotated TL (r1): rotated (-60, 40) + outward (-12, +12) -> rotated (-72, 52)
        Pt rotZone = Rot(-72f, 52f);
        var rotHit = HudHandleMath.HitTest(r0, r1, r2, r3, rotZone, true, true, true, true);
        Assert.Equal(HudHandleKind.Rotate, rotHit.Kind);
        Assert.Equal(1, rotHit.Index);
        // Unrotated TL outDiag is 135 deg; rotated +45 deg => 180 deg
        Assert.Equal(180, HudHandleMath.QuantizeRotateAngle(rotHit.AngleDeg));
    }

    [Fact]
    public void ComputeCornerRatios_FreeVsShiftAspectLock() {
        Pt fixedPt = new(0f, 0f);
        Pt startCorner = new(100f, 50f);
        Pt mouse = new(200f, 25f);
        Pt ax = new(1f, 0f);
        Pt ay = new(0f, 1f);

        var (rxFree, ryFree) = HudHandleMath.ComputeCornerRatios(fixedPt, startCorner, mouse, ax, ay, lockAspect: false);
        Assert.Equal(2f, rxFree, 4);
        Assert.Equal(0.5f, ryFree, 4);

        var (rxLock, ryLock) = HudHandleMath.ComputeCornerRatios(fixedPt, startCorner, mouse, ax, ay, lockAspect: true);
        Assert.Equal(rxLock, ryLock, 5);
        Assert.Equal(1.7f, rxLock, 4);
    }

    [Fact]
    public void ComputeEdgeRatios_FreeVsShiftAspectLock() {
        Pt fixedPt = new(0f, 50f);
        Pt startEdge = new(100f, 50f);
        Pt mouse = new(150f, 80f);

        var (rxFree, ryFree) = HudHandleMath.ComputeEdgeRatios(fixedPt, startEdge, mouse, widthAxis: true, lockAspect: false);
        Assert.Equal(1.5f, rxFree, 4);
        Assert.Equal(1f, ryFree, 4);

        var (rxLock, ryLock) = HudHandleMath.ComputeEdgeRatios(fixedPt, startEdge, mouse, widthAxis: true, lockAspect: true);
        Assert.Equal(1.5f, rxLock, 4);
        Assert.Equal(1.5f, ryLock, 4);
    }

    [Fact]
    public void CursorQuantizationAndShapeBounds_StayInside32x32() {
        Assert.Equal(0, HudHandleMath.QuantizeResizeAngle(180f));
        Assert.Equal(135, HudHandleMath.QuantizeResizeAngle(-45f));
        Assert.Equal(315, HudHandleMath.QuantizeRotateAngle(-45f));
        Assert.Equal(0, HudHandleMath.QuantizeRotateAngle(355f));

        CheckMask(HudHandleMath.InResizeArrow);
        CheckMask(HudHandleMath.InRotateArc);
        CheckMask(HudHandleMath.InMoveCross);

        static void CheckMask(Func<float, float, bool> pred) {
            int count = 0;
            for (int y = 0; y < 32; y++) {
                float dy = y + 0.5f - 16f;
                for (int x = 0; x < 32; x++) {
                    float dx = x + 0.5f - 16f;
                    if (pred(dx, dy)) {
                        count++;
                        // 1px outline needs at least 1 pixel of margin from texture edge.
                        Assert.InRange(x, 1, 30);
                        Assert.InRange(y, 1, 30);
                    }
                }
            }
            Assert.True(count > 0);
        }
    }
}
