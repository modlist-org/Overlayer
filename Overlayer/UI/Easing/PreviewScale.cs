using System;
using System.Globalization;

namespace Overlayer.UI.Easing;

/// <summary>
/// Maps curve preview values onto a horizontal track: [0, 1] spans end-to-end by default,
/// expanding only when the sampled curve undershoots below 0 or overshoots above 1.
/// </summary>
internal readonly struct PreviewScale {
    public const float OvershootEpsilon = 0.005f;
    public const float MinZeroOnePx = 28f;
    public const float MinSeparateLabelPx = 18f;

    public readonly float Min;
    public readonly float Max;
    public readonly bool HasUnder;
    public readonly bool HasOver;
    public readonly float X0;
    public readonly float X1;
    public readonly float ZeroLabelX;
    public readonly float OneLabelX;
    public readonly bool MergedZeroOne;
    public readonly float MergedLabelX;
    public readonly bool MergeLines;
    public readonly string UnderText;
    public readonly string OverText;

    private PreviewScale(
        float min, float max, bool hasUnder, bool hasOver,
        float x0, float x1, float zeroLabelX, float oneLabelX,
        bool mergedZeroOne, float mergedLabelX, bool mergeLines,
        string underText, string overText) {
        Min = min;
        Max = max;
        HasUnder = hasUnder;
        HasOver = hasOver;
        X0 = x0;
        X1 = x1;
        ZeroLabelX = zeroLabelX;
        OneLabelX = oneLabelX;
        MergedZeroOne = mergedZeroOne;
        MergedLabelX = mergedLabelX;
        MergeLines = mergeLines;
        UnderText = underText;
        OverText = overText;
    }

    public static PreviewScale Compute(float sampledMin, float sampledMax, float width) {
        width = Math.Max(width, 1f);
        bool hasUnder = sampledMin < -OvershootEpsilon;
        bool hasOver = sampledMax > 1f + OvershootEpsilon;
        float min = hasUnder ? sampledMin : 0f;
        float max = hasOver ? sampledMax : 1f;
        float span = Math.Max(max - min, 1e-4f);
        float x0 = (0f - min) / span * width;
        float x1 = (1f - min) / span * width;

        string underText = hasUnder ? FormatValue(min) : string.Empty;
        string overText = hasOver ? FormatValue(max) : string.Empty;
        float underW = underText.Length * 6.5f;
        float overW = overText.Length * 6.5f;

        float zeroLabelX = hasUnder ? Math.Max(x0, underW + 8f) : x0;
        float oneLabelX = hasOver ? Math.Min(x1, width - overW - 8f) : x1;
        bool mergedZeroOne = (x1 - x0) < MinZeroOnePx || (oneLabelX - zeroLabelX) < MinSeparateLabelPx;

        float midX = (x0 + x1) * 0.5f;
        float minCenter = hasUnder ? underW + 14f : 0f;
        float maxCenter = hasOver ? width - overW - 14f : width;
        float mergedLabelX = minCenter <= maxCenter ? Clamp(midX, minCenter, maxCenter) : midX;
        bool mergeLines = (x1 - x0) < 2f;

        return new PreviewScale(
            min, max, hasUnder, hasOver,
            x0, x1, zeroLabelX, oneLabelX,
            mergedZeroOne, mergedLabelX, mergeLines,
            underText, overText);
    }

    public float MapX(float value, float width) {
        float span = Math.Max(Max - Min, 1e-4f);
        float t = Clamp((value - Min) / span, 0f, 1f);
        return t * Math.Max(width, 0f);
    }

    public static string FormatValue(float v)
        => Math.Abs(v) >= 10f ? v.ToString("0.#", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
