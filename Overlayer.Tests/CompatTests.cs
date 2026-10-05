using Overlayer.Compat;
using Xunit;

namespace Overlayer.Tests;

public sealed class CompatTests {
    [Theory]
    [InlineData("NoWrap", 0)]
    [InlineData("Normal", 1)]
    [InlineData("PreserveWhitespace", 2)]
    [InlineData("PreserveWhitespaceNoWrap", 3)]
    [InlineData("nowrap", 0)]
    [InlineData("NORMAL", 1)]
    [InlineData("2", 2)]
    public void Tmp_FromName_Maps(string name, int expected) {
        Assert.Equal(expected, TmpCompat.FromName(name, -99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public void Tmp_FromName_FallsBack(string name) {
        Assert.Equal(1, TmpCompat.FromName(name, 1));
    }

    [Fact]
    public void Tmp_FromName_ClampsNumeric() {
        Assert.Equal(3, TmpCompat.FromName("99", 0));
        Assert.Equal(0, TmpCompat.FromName("-5", 1));
    }

    [Theory]
    [InlineData(0, "NoWrap")]
    [InlineData(1, "Normal")]
    [InlineData(2, "PreserveWhitespace")]
    [InlineData(3, "PreserveWhitespaceNoWrap")]
    [InlineData(-1, "Normal")]
    [InlineData(42, "Normal")]
    public void Tmp_ToName_RoundTrips(int mode, string expected) {
        Assert.Equal(expected, TmpCompat.ToName(mode));
    }

    [Fact]
    public void Tmp_BoolFallback_MapsNoWrap() {
        var tmp = new TMPro.TMP_Text();
        TmpCompat.SetWrappingMode(tmp, TmpCompat.NoWrap);
        Assert.False(tmp.enableWordWrapping);
        Assert.Equal(TmpCompat.NoWrap, TmpCompat.GetWrappingMode(tmp));
        TmpCompat.SetWrappingMode(tmp, TmpCompat.Normal);
        Assert.True(tmp.enableWordWrapping);
        Assert.Equal(TmpCompat.Normal, TmpCompat.GetWrappingMode(tmp));
        // PreserveWhitespaceNoWrap behaves as no-wrap on old TMP.
        TmpCompat.SetWrappingMode(tmp, TmpCompat.PreserveWhitespaceNoWrap);
        Assert.Equal(TmpCompat.NoWrap, TmpCompat.GetWrappingMode(tmp));
    }

    [Theory]
    [InlineData("None", 0)]
    [InlineData("Merge", 1)]
    [InlineData("Intersect", 2)]
    [InlineData("Difference", 3)]
    [InlineData("Flip", 4)]
    [InlineData("merge", 1)]
    public void Collider_FromName_Maps(string name, int expected) {
        Assert.Equal(expected, ColliderCompat.CompositeFromName(name, -99));
    }

    [Fact]
    public void Collider_MissingProperty_IsNoOp() {
        Assert.False(ColliderCompat.HasCompositeOperation);
        var col = new UnityEngine.Collider2D();
        ColliderCompat.SetCompositeOperation(col, ColliderCompat.CompositeFlip);
        Assert.Equal(ColliderCompat.CompositeNone, ColliderCompat.GetCompositeOperation(col));
        Assert.Equal("None", ColliderCompat.CompositeToName(99));
    }

    [Fact]
    public void Time_FallsBackToFloatClocks() {
        Assert.Equal(1.5, UnityTimeCompat.TimeAsDouble);
        Assert.Equal(2.5, UnityTimeCompat.UnscaledTimeAsDouble);
        Assert.Equal(3.5, UnityTimeCompat.RealtimeSinceStartupAsDouble);
        Assert.Equal(4.5, UnityTimeCompat.FixedTimeAsDouble);
        Assert.Equal(5.5, UnityTimeCompat.FixedUnscaledTimeAsDouble);
    }
}
