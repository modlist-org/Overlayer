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

    [Fact]
    public void ComputeReanchoredPosition_PreservesCornersAcrossPivotAndAnchorTransitions() {
        static (Pt[] Corners, Pt LocalPos) ComputeParentCorners(
            Pt anchoredPos, Pt pivot, Pt size, Pt scale, float rotDeg,
            Pt parentMin, Pt parentSize, Pt anchorMin, Pt anchorMax) {
            Pt anchorRef = new(
                parentMin.X + (anchorMin.X + anchorMax.X) * 0.5f * parentSize.X,
                parentMin.Y + (anchorMin.Y + anchorMax.Y) * 0.5f * parentSize.Y);
            Pt localPos = anchorRef + anchoredPos;
            float rad = rotDeg * (MathF.PI / 180f);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            Pt ToParent(float u, float v) {
                float lx = (u - pivot.X) * size.X * scale.X;
                float ly = (v - pivot.Y) * size.Y * scale.Y;
                return new Pt(localPos.X + lx * cos - ly * sin, localPos.Y + lx * sin + ly * cos);
            }
            return ([ToParent(0f, 0f), ToParent(0f, 1f), ToParent(1f, 1f), ToParent(1f, 0f)], localPos);
        }

        Pt parentMin = new(-960f, -540f);
        Pt parentSize = new(1920f, 1080f);
        Pt size = new(140f, 48f);

        (Pt AnchorMin, Pt AnchorMax, Pt Pivot, Pt Scale, float Rot)[] cases = [
            (new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(1f, 1f), 0f),
            (new(0.5f, 0.5f), new(0.5f, 0.5f), new(0f, 1f), new(1.25f, 0.8f), 37f),
            (new(0f, 0f), new(1f, 1f), new(0.25f, 0.75f), new(1.5f, 2f), -25f),
        ];

        Pt[] targets = [
            new(0f, 0f), new(0.5f, 0f), new(1f, 0f),
            new(0f, 0.5f), new(0.5f, 0.5f), new(1f, 0.5f),
            new(0f, 1f), new(0.5f, 1f), new(1f, 1f),
        ];

        foreach (var (aMin, aMax, oldPivot, scale, rot) in cases) {
            Pt startAnchored = new(215f, -130f);
            var (beforeCorners, oldLocalPos) = ComputeParentCorners(
                startAnchored, oldPivot, size, scale, rot, parentMin, parentSize, aMin, aMax);

            foreach (Pt target in targets) {
                Pt newAnchored = HudHandleMath.ComputeReanchoredPosition(
                    oldLocalPos, oldPivot, size, scale, rot, parentMin, parentSize, target);
                var (afterCorners, _) = ComputeParentCorners(
                    newAnchored, target, size, scale, rot, parentMin, parentSize, target, target);

                for (int c = 0; c < 4; c++) {
                    Assert.True(MathF.Abs(beforeCorners[c].X - afterCorners[c].X) < 1e-3f,
                        $"Corner {c}.X drifted: {beforeCorners[c].X} vs {afterCorners[c].X}");
                    Assert.True(MathF.Abs(beforeCorners[c].Y - afterCorners[c].Y) < 1e-3f,
                        $"Corner {c}.Y drifted: {beforeCorners[c].Y} vs {afterCorners[c].Y}");
                }
            }
        }
    }

    [Fact]
    public void ComputeReanchoredPosition_MidDragAnchorSwitchAndSnapTracksDragDeltaWithoutJump() {
        Pt parentMin = new(-960f, -540f);
        Pt parentSize = new(1920f, 1080f);
        Pt fittedSize = new(110f, 44f);
        Pt scale = new(1.2f, 0.9f);
        float rot = 18f;

        Pt anchor = new(0.5f, 0.5f);
        Pt pivot = new(0.5f, 0.5f);
        Pt dragBasePos = new(0f, 0f);
        Pt anchoredPos = dragBasePos;

        Pt CornerZero(Pt a, Pt p, Pt pos) {
            Pt aRef = new(parentMin.X + a.X * parentSize.X, parentMin.Y + a.Y * parentSize.Y);
            Pt localPos = aRef + pos;
            float rad = rot * (MathF.PI / 180f);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            float lx = (0f - p.X) * fittedSize.X * scale.X;
            float ly = (0f - p.Y) * fittedSize.Y * scale.Y;
            return new Pt(localPos.X + lx * cos - ly * sin, localPos.Y + lx * sin + ly * cos);
        }

        Pt initialCorner = CornerZero(anchor, pivot, anchoredPos);

        // Frame 1: drag moves +600, +300 (with a +12 snap on this frame) and AutoAnchor switches to top-right (1, 1).
        Pt screenDelta1 = new(600f, 300f);
        Pt snap1 = new(12f, 0f);
        anchoredPos = dragBasePos + screenDelta1 + snap1;

        Pt localPosBeforeSwitch = new Pt(parentMin.X + anchor.X * parentSize.X, parentMin.Y + anchor.Y * parentSize.Y) + anchoredPos;
        Pt target1 = new(1f, 1f);
        Pt posBefore = anchoredPos;
        anchoredPos = HudHandleMath.ComputeReanchoredPosition(
            localPosBeforeSwitch, pivot, fittedSize, scale, rot, parentMin, parentSize, target1);
        anchor = pivot = target1;
        dragBasePos += anchoredPos - posBefore;

        Pt afterFrame1Corner = CornerZero(anchor, pivot, anchoredPos);
        Assert.True(MathF.Abs((initialCorner.X + screenDelta1.X + snap1.X) - afterFrame1Corner.X) < 1e-3f);
        Assert.True(MathF.Abs((initialCorner.Y + screenDelta1.Y + snap1.Y) - afterFrame1Corner.Y) < 1e-3f);

        // Frame 2: snap is gone, pointer moved slightly more (+605, +305); must land cleanly at initial + screenDelta2.
        Pt screenDelta2 = new(605f, 305f);
        anchoredPos = dragBasePos + screenDelta2;
        Pt afterFrame2Corner = CornerZero(anchor, pivot, anchoredPos);
        Assert.True(MathF.Abs((initialCorner.X + screenDelta2.X) - afterFrame2Corner.X) < 1e-3f);
        Assert.True(MathF.Abs((initialCorner.Y + screenDelta2.Y) - afterFrame2Corner.Y) < 1e-3f);
    }
}
