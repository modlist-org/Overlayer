using Overlayer.UI.Easing;
using Xunit;

namespace Overlayer.Tests;

public sealed class PreviewScaleTests {
    [Fact]
    public void NormalRange_SpansZeroToOneEndToEnd() {
        var s = PreviewScale.Compute(0f, 1f, 400f);

        Assert.False(s.HasUnder);
        Assert.False(s.HasOver);
        Assert.False(s.MergedZeroOne);
        Assert.False(s.MergeLines);
        Assert.Equal(0f, s.X0, 3);
        Assert.Equal(400f, s.X1, 3);
        Assert.Equal(0f, s.MapX(0f, 400f), 3);
        Assert.Equal(200f, s.MapX(0.5f, 400f), 3);
        Assert.Equal(400f, s.MapX(1f, 400f), 3);
    }

    [Fact]
    public void TinyFloatingNoise_DoesNotTriggerOvershoot() {
        var s = PreviewScale.Compute(-0.001f, 1.001f, 400f);

        Assert.False(s.HasUnder);
        Assert.False(s.HasOver);
        Assert.Equal(0f, s.Min);
        Assert.Equal(1f, s.Max);
        Assert.Equal(0f, s.X0, 3);
        Assert.Equal(400f, s.X1, 3);
    }

    [Fact]
    public void PositiveOvershoot_ExpandsOnlyUpperEnd() {
        var s = PreviewScale.Compute(0f, 1.25f, 400f);

        Assert.False(s.HasUnder);
        Assert.True(s.HasOver);
        Assert.False(s.MergedZeroOne);
        Assert.Equal(0f, s.X0, 3);
        Assert.Equal(320f, s.X1, 3);
        Assert.Equal("1.25", s.OverText);
        Assert.Equal(string.Empty, s.UnderText);
        Assert.Equal(400f, s.MapX(1.25f, 400f), 3);
    }

    [Fact]
    public void BidirectionalOvershoot_ShiftsZeroAndOneInward() {
        var s = PreviewScale.Compute(-0.5f, 1.5f, 400f);

        Assert.True(s.HasUnder);
        Assert.True(s.HasOver);
        Assert.False(s.MergedZeroOne);
        Assert.Equal(100f, s.X0, 3);
        Assert.Equal(300f, s.X1, 3);
        Assert.Equal("-0.5", s.UnderText);
        Assert.Equal("1.5", s.OverText);
    }

    [Fact]
    public void NarrowZeroOneSpan_MergesIntoZeroOneLabel() {
        var s = PreviewScale.Compute(-10f, 15f, 400f);

        Assert.True(s.HasUnder);
        Assert.True(s.HasOver);
        Assert.True(s.MergedZeroOne);
        Assert.False(s.MergeLines);
        Assert.Equal(160f, s.X0, 3);
        Assert.Equal(176f, s.X1, 3);
        Assert.Equal(168f, s.MergedLabelX, 3);
        Assert.Equal(160f, s.MapX(0f, 400f), 3);
        Assert.Equal(176f, s.MapX(1f, 400f), 3);
    }

    [Fact]
    public void UltraExtremeOvershoot_MergesVerticalLinesToo() {
        var s = PreviewScale.Compute(-500f, 500f, 400f);

        Assert.True(s.MergedZeroOne);
        Assert.True(s.MergeLines);
    }
}
