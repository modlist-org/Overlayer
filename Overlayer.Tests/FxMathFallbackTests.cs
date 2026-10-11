using Xunit;

namespace Overlayer.Tests;

public sealed class FxMathFallbackTests {
    [Theory]
    [InlineData("1+2*3", 7d)]
    [InlineData("(1+2)*3", 9d)]
    [InlineData("2^3", 8d)]
    [InlineData("10%3", 1d)]
    [InlineData("-4+10/2", 1d)]
    [InlineData("sin(0)+cos(0)", 1d)]
    [InlineData("sqrt(16)+abs(-3)", 7d)]
    [InlineData("min(3,7,2)+max(3,7,2)", 9d)]
    [InlineData("PI*2", 6.283185307179586d)]
    [InlineData("1>2", 0d)]
    [InlineData("2>=2", 1d)]
    [InlineData("1<2&&3<4", 1d)]
    public void Evaluates(string expr, double expected) {
        Assert.True(IO.Fx.FxMathFallback.TryEvaluate(expr, out double actual), expr);
        Assert.Equal(expected, actual, precision: 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1+")]
    [InlineData("(1+2")]
    [InlineData("foo(1)")]
    [InlineData("1/0")]
    public void Rejects(string expr) {
        Assert.False(IO.Fx.FxMathFallback.TryEvaluate(expr, out _));
    }
}
