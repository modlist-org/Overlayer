using UnityEngine;

namespace Overlayer.Tutorial;

/// <summary>
/// Generates zero-overshoot, human-like cursor trajectories from (start, end) using
/// lightweight 384-bin autoregressive state dynamics, Ornstein-Uhlenbeck noise,
/// Fitts's-law timing with random pace variation, and internal time-warping.
/// </summary>
internal sealed class HumanMouse {
    public const int Bins = 384;

    internal sealed class Trajectory {
        public readonly float Duration;
        private readonly Vector2[] points;

        internal Trajectory(float duration, Vector2[] points) {
            Duration = duration;
            this.points = points;
        }

        public Vector2 Evaluate(float elapsedSeconds) {
            float u = Duration > 0f ? Mathf.Clamp01(elapsedSeconds / Duration) : 1f;
            return Sample(u);
        }

        public Vector2 Sample(float normalizedT) {
            if (points.Length == 1) {
                return points[0];
            }
            float t = Mathf.Clamp01(normalizedT);
            float idxF = t * (points.Length - 1);
            int i0 = (int)idxF;
            int i1 = Mathf.Min(points.Length - 1, i0 + 1);
            return Vector2.LerpUnclamped(points[i0], points[i1], idxF - i0);
        }
    }

    public float Speed = 1.0f;      // 0.5x .. 2.0x
    // Fitts's law MT = a + b*log2(1 + D/24): ~0.45 s for short hops, ~0.8 s across a 1080p screen.
    public float BaseMs = 180f;
    public float SlopeMs = 120f;
    public float PaceVar = 0.26f;
    public float Warp = 0.22f;
    public float Noise = 0.35f;
    public float Arc = 0.08f;
    public float ArcStd = 0.04f;
    public float PosArcProb = 0.5f;
    public float JitterPx = 1.4f;

    // [bias, remAlong, vAlong, u, perp, vPerp]
    public float[] WAlong = [0.2f, 22.0f, -10.5f, 2.0f, 0.0f, 0.0f];
    public float[] WPerp = [0.0f, 0.0f, 0.0f, 0.0f, -18.0f, -9.0f];
    public float ResAlongStd = 4.5f;
    public float ResPerpStd = 1.6f;

    private int nextSeed;

    public Trajectory Move(Vector2 from, Vector2 to, int? seed = null, float? speed = null) {
        Vector2 d = to - from;
        float dist = d.magnitude;
        if (dist < 1f) {
            return new Trajectory(0.05f, [to]);
        }

        int s = seed ?? System.Threading.Interlocked.Increment(ref nextSeed);
        var rng = new System.Random(unchecked(s * 9973 + Mathf.RoundToInt(dist * 13f)));
        Vector2 dir = d / dist;
        Vector2 perp = new(-dir.y, dir.x);

        float spd = Mathf.Clamp(speed ?? Speed, 0.5f, 2.0f);
        float bits = (float)(Math.Log(1.0 + dist / 24.0) / Math.Log(2.0));
        float paceMul = Mathf.Exp(Mathf.Clamp(RandN(rng) * PaceVar, -0.55f, 0.55f));
        float fittsMs = (BaseMs + SlopeMs * bits) * paceMul;
        float duration = Mathf.Max(0.08f, (fittsMs / 1000f) / spd);

        float arcSign = rng.NextDouble() < PosArcProb ? 1f : -1f;
        float arcTarget = arcSign * Mathf.Max(0.005f, Arc + RandN(rng) * ArcStd * 0.65f);

        float warpAmp = ((float)rng.NextDouble() * 2f - 1f) * Warp;
        int warpFreq = 1 + rng.Next(2);
        float warpPhase = (float)rng.NextDouble() * (float)Math.PI * 2f;

        const int steps = Bins;
        const float dt = 1f / steps;
        float sqrtDt = Mathf.Sqrt(dt);
        var rawA = new float[steps + 1];
        var rawP = new float[steps + 1];
        float a = 0f, vA = 0f;
        float p = 0f, vP = 0f;
        float ouA = 0f, ouP = 0f;

        for (int i = 0; i <= steps; i++) {
            rawA[i] = a;
            rawP[i] = p;
            float u = (float)i / steps;

            float settle = u > 0.62f ? Mathf.Min(1f, (u - 0.62f) / 0.28f) : 0f;
            float sinPiU = Mathf.Sin(u * (float)Math.PI);
            float goalP = sinPiU * arcTarget * (1f - settle);

            float remA = 1f - a;
            float remP = goalP - p;
            float accA = WAlong[0] + WAlong[1] * remA + WAlong[2] * vA + WAlong[3] * u + WAlong[4] * p + WAlong[5] * vP;
            float accP = WPerp[0] + WPerp[1] * remA + WPerp[2] * vA + WPerp[3] * u + WPerp[4] * (-remP) + WPerp[5] * vP;

            if (u > 0.68f) {
                float k = (u - 0.68f) / 0.32f;
                accA += (40f * k) * (1f - a) - (16f * k) * vA;
                accP += (40f * k) * (0f - p) - (16f * k) * vP;
            }

            float noiseEnvA = sinPiU * Mathf.Max(0f, 1f - Mathf.Max(0f, a - 0.7f) / 0.3f) * Noise;
            float noiseEnvP = sinPiU * Noise;
            ouA += (-6.5f * ouA) * dt + RandN(rng) * ResAlongStd * sqrtDt * noiseEnvA;
            ouP += (-6.5f * ouP) * dt + RandN(rng) * ResPerpStd * sqrtDt * noiseEnvP;

            vA += (accA + ouA) * dt;
            vP += (accP + ouP) * dt;

            // Cushion approach near target and prevent crossing > 1 or receding back from target.
            if (a > 0.6f && vA > 0f) {
                float vCap = Mathf.Max(0.08f, (1f - a) * 7.0f + 0.08f);
                if (vA > vCap) {
                    vA += (vCap - vA) * 0.25f;
                }
            }
            if (a > 0.75f && vA < 0f) {
                vA *= 0.7f;
            }

            a += vA * dt;
            p += vP * dt;
            if (a >= 1f) {
                a = 1f;
                if (vA > 0f) {
                    vA = 0f;
                }
            }
            if (a <= 0f) {
                a = 0f;
                if (vA < 0f) {
                    vA = 0f;
                }
            }
        }

        float endGapA = 1f - rawA[steps];
        float endP = rawP[steps];
        var points = new Vector2[steps + 1];
        float phase1 = (float)rng.NextDouble() * (float)Math.PI * 2f;
        float phase2 = (float)rng.NextDouble() * (float)Math.PI * 2f;

        for (int i = 0; i <= steps; i++) {
            if (i == 0) {
                points[0] = from;
                continue;
            }
            if (i == steps) {
                points[steps] = to;
                continue;
            }

            float t = (float)i / steps;
            float env = Mathf.Sin(t * (float)Math.PI);
            float warped = Mathf.Clamp01(t + warpAmp * env * Mathf.Sin(t * warpFreq * (float)Math.PI + warpPhase));
            float idxF = warped * steps;
            int i0 = (int)idxF;
            int i1 = Mathf.Min(steps, i0 + 1);
            float f = idxF - i0;

            float aVal = Mathf.Clamp01((rawA[i0] + (rawA[i1] - rawA[i0]) * f) + endGapA * (warped * warped * warped));
            float pVal = (rawP[i0] + (rawP[i1] - rawP[i0]) * f) - endP * warped;

            float jx = (Mathf.Sin(t * 31f + phase1) * 0.55f + Mathf.Sin(t * 73f + phase2) * 0.45f) * JitterPx * env;
            float jy = (Mathf.Cos(t * 27f + phase2) * 0.55f + Mathf.Cos(t * 61f + phase1) * 0.45f) * JitterPx * env;

            points[i] = from + dir * (dist * aVal) + perp * (dist * pVal) + new Vector2(jx, jy);
        }

        return new Trajectory(duration, points);
    }

    private static float RandN(System.Random rng) {
        double u1 = Math.Max(1e-6, rng.NextDouble());
        double u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
