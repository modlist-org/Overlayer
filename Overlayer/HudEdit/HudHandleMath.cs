using System;

namespace Overlayer.HudEdit;

internal readonly struct Pt(float x, float y) {
    public readonly float X = x;
    public readonly float Y = y;

    public float SqrMagnitude => X * X + Y * Y;
    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    public Pt Normalized {
        get {
            float m = Magnitude;
            return m > 1e-6f ? new Pt(X / m, Y / m) : default;
        }
    }

    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public static Pt operator *(Pt a, float s) => new(a.X * s, a.Y * s);
    public static Pt operator *(float s, Pt a) => new(a.X * s, a.Y * s);
    public static float Dot(Pt a, Pt b) => a.X * b.X + a.Y * b.Y;
}

internal enum HudHandleKind { None, Corner, Edge, Rotate }

internal readonly struct HudHandleHit(HudHandleKind kind, int index, float angleDeg) {
    public readonly HudHandleKind Kind = kind;
    public readonly int Index = index;
    public readonly float AngleDeg = angleDeg;
}

/// <summary>
/// Pure screen-pixel hit-testing, resize/aspect ratios, and cursor geometry for Figma-style HUD handles.
/// Kept free of UnityEngine types so unit tests run directly against it.
/// </summary>
internal static class HudHandleMath {
    public const float HandleSize = 10f;
    public const float EdgeHandleSize = 8f;
    public const float EdgeHandleMinSpanPx = 28f;
    public const float CornerHitHalfPx = 7f;
    public const float EdgeHitPx = 6f;
    public const float RotateOuterPx = 22f;
    public const float RotateShoulderPx = 12f;

    public static HudHandleHit HitTest(
        Pt c0, Pt c1, Pt c2, Pt c3, Pt mouse,
        bool canCorner, bool canEdgeX, bool canEdgeY, bool canRotate) {
        Pt[] c = [c0, c1, c2, c3];
        Pt[] outX = new Pt[4];
        Pt[] outY = new Pt[4];
        float[] spanX = new float[4];
        float[] spanY = new float[4];

        for (int i = 0; i < 4; i++) {
            Pt dx = c[i] - c[i ^ 3];
            Pt dy = c[i] - c[i ^ 1];
            spanX[i] = dx.Magnitude;
            spanY[i] = dy.Magnitude;
            outX[i] = dx.Normalized;
            outY[i] = dy.Normalized;
        }

        if (canCorner) {
            int best = -1;
            float bestDist2 = float.MaxValue;
            for (int i = 0; i < 4; i++) {
                Pt d = mouse - c[i];
                float ox = Pt.Dot(d, outX[i]);
                float oy = Pt.Dot(d, outY[i]);
                float minInX = -MathF.Min(CornerHitHalfPx, spanX[i] * 0.25f);
                float minInY = -MathF.Min(CornerHitHalfPx, spanY[i] * 0.25f);
                if (ox >= minInX && ox <= CornerHitHalfPx && oy >= minInY && oy <= CornerHitHalfPx) {
                    float dist2 = d.SqrMagnitude;
                    if (dist2 < bestDist2) {
                        bestDist2 = dist2;
                        best = i;
                    }
                }
            }
            if (best >= 0) {
                return new HudHandleHit(HudHandleKind.Corner, best, AngleDeg(c[best] - c[(best + 2) & 3]));
            }
        }

        bool inside = InQuad(mouse, c0, c1, c2, c3);
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < 4; i++) {
                bool widthAxis = (i & 1) == 0;
                if (!(widthAxis ? canEdgeX : canEdgeY)) {
                    continue;
                }
                float perpSpan = widthAxis ? (c3 - c0).Magnitude : (c1 - c0).Magnitude;
                float threshold = inside ? MathF.Min(EdgeHitPx, perpSpan * 0.25f) : EdgeHitPx;
                float d = DistanceToSegment(mouse, c[i], c[(i + 1) & 3]);
                if (d <= threshold && d < bestDist) {
                    bestDist = d;
                    best = i;
                }
            }
            if (best >= 0) {
                Pt mid = (c[best] + c[(best + 1) & 3]) * 0.5f;
                int opp = (best + 2) & 3;
                Pt oppMid = (c[opp] + c[(opp + 1) & 3]) * 0.5f;
                Pt normal = mid - oppMid;
                float angle = normal.SqrMagnitude > 1e-6f ? AngleDeg(normal) : ((best & 1) == 0 ? 0f : 90f);
                return new HudHandleHit(HudHandleKind.Edge, best, angle);
            }
        }

        if (canRotate && !inside) {
            int best = -1;
            float bestDist2 = float.MaxValue;
            float maxR2 = RotateOuterPx * RotateOuterPx;
            for (int i = 0; i < 4; i++) {
                Pt d = mouse - c[i];
                float dist2 = d.SqrMagnitude;
                if (dist2 > maxR2) {
                    continue;
                }
                float ox = Pt.Dot(d, outX[i]);
                float oy = Pt.Dot(d, outY[i]);
                if (ox < 0f && oy < 0f) {
                    continue;
                }
                float shoulderX = MathF.Min(RotateShoulderPx, spanX[i] * 0.25f);
                float shoulderY = MathF.Min(RotateShoulderPx, spanY[i] * 0.25f);
                if (ox >= -shoulderX && ox <= RotateOuterPx && oy >= -shoulderY && oy <= RotateOuterPx) {
                    if (dist2 < bestDist2) {
                        bestDist2 = dist2;
                        best = i;
                    }
                }
            }
            if (best >= 0) {
                Pt outDiag = (outX[best] + outY[best]).Normalized;
                if (outDiag.SqrMagnitude < 1e-6f) {
                    Pt center = (c0 + c1 + c2 + c3) * 0.25f;
                    outDiag = (c[best] - center).Normalized;
                }
                return new HudHandleHit(HudHandleKind.Rotate, best, AngleDeg(outDiag));
            }
        }

        return default;
    }

    public static (float Rx, float Ry) ComputeCornerRatios(
        Pt fixedPt, Pt startCorner, Pt mouse, Pt axisX, Pt axisY, bool lockAspect, float minRatio = 0.01f) {
        Pt startVec = startCorner - fixedPt;
        Pt curVec = mouse - fixedPt;
        if (lockAspect) {
            float k = startVec.SqrMagnitude > 1e-3f ? Pt.Dot(curVec, startVec) / startVec.SqrMagnitude : 1f;
            k = MathF.Max(k, minRatio);
            return (k, k);
        }
        float startDx = Pt.Dot(startVec, axisX);
        float startDy = Pt.Dot(startVec, axisY);
        float curDx = Pt.Dot(curVec, axisX);
        float curDy = Pt.Dot(curVec, axisY);
        float rx = MathF.Abs(startDx) > 1e-3f ? curDx / startDx : 1f;
        float ry = MathF.Abs(startDy) > 1e-3f ? curDy / startDy : 1f;
        return (MathF.Max(rx, minRatio), MathF.Max(ry, minRatio));
    }

    public static (float Rx, float Ry) ComputeEdgeRatios(
        Pt fixedPt, Pt startEdgeMid, Pt mouse, bool widthAxis, bool lockAspect, float minRatio = 0.01f) {
        Pt outward = startEdgeMid - fixedPt;
        float startLen = outward.Magnitude;
        float r = startLen > 1e-3f ? Pt.Dot(mouse - fixedPt, outward * (1f / startLen)) / startLen : 1f;
        r = MathF.Max(r, minRatio);
        if (lockAspect) {
            return (r, r);
        }
        return widthAxis ? (r, 1f) : (1f, r);
    }

    public static int QuantizeResizeAngle(float angleDeg) {
        float a = angleDeg % 180f;
        if (a < 0f) {
            a += 180f;
        }
        return ((int)MathF.Floor((a + 22.5f) / 45f) % 4) * 45;
    }

    public static int QuantizeRotateAngle(float angleDeg) {
        float a = angleDeg % 360f;
        if (a < 0f) {
            a += 360f;
        }
        return ((int)MathF.Floor((a + 22.5f) / 45f) % 8) * 45;
    }

    public static bool InResizeArrow(float u, float v) {
        float au = MathF.Abs(u);
        float av = MathF.Abs(v);
        if (au <= 6.5f && av <= 1.6f) {
            return true;
        }
        return au > 6.5f && au <= 12f && av <= (12f - au) * 0.95f;
    }

    public static bool InRotateArc(float u, float v) {
        const float cx = -4.5f;
        const float r = 8.5f;
        const float spanRad = 50f * MathF.PI / 180f;
        float cosS = MathF.Cos(spanRad);
        float sinS = MathF.Sin(spanRad);

        float dx = u - cx;
        float dist = MathF.Sqrt(dx * dx + v * v);
        float phi = MathF.Atan2(v, dx);
        if (MathF.Abs(phi) <= spanRad && MathF.Abs(dist - r) <= 1.55f) {
            return true;
        }

        float av = MathF.Abs(v);
        float bx = cx + r * cosS;
        float by = r * sinS;
        float tx = -sinS;
        float ty = cosS;
        float nx = cosS;
        float ny = sinS;
        float pu = (u - bx) * tx + (av - by) * ty;
        float pv = (u - bx) * nx + (av - by) * ny;
        return pu >= -0.6f && pu <= 4.8f && MathF.Abs(pv) <= (4.8f - pu) * 0.85f;
    }

    public static bool InMoveCross(float u, float v) => InHalfMoveArrow(u, v) || InHalfMoveArrow(v, u);

    private static bool InHalfMoveArrow(float u, float v) {
        float au = MathF.Abs(u);
        float av = MathF.Abs(v);
        if (au <= 5.5f && av <= 1.4f) {
            return true;
        }
        return au > 5.5f && au <= 10.5f && av <= (10.5f - au) * 0.95f;
    }

    private static bool InQuad(Pt p, Pt c0, Pt c1, Pt c2, Pt c3) {
        Pt[] q = [c0, c1, c2, c3];
        bool? sign = null;
        for (int i = 0; i < 4; i++) {
            Pt a = q[i], b = q[(i + 1) & 3];
            float cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (MathF.Abs(cross) < 1e-4f) {
                continue;
            }
            bool s = cross > 0f;
            if (sign == null) {
                sign = s;
            } else if (sign != s) {
                return false;
            }
        }
        return sign != null;
    }

    private static float DistanceToSegment(Pt p, Pt a, Pt b) {
        Pt ab = b - a;
        float len2 = ab.SqrMagnitude;
        float t = len2 < 1e-6f ? 0f : Math.Clamp(Pt.Dot(p - a, ab) / len2, 0f, 1f);
        return (p - (a + ab * t)).Magnitude;
    }

    private static float AngleDeg(Pt dir) => MathF.Atan2(dir.Y, dir.X) * (180f / MathF.PI);
}
