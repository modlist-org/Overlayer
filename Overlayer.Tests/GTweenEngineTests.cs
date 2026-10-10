using GTweens.Contexts;
using GTweens.Easings;
using GTweens.Extensions;
using Xunit;

namespace Overlayer.Tests;

// Embedded GTweens engine checks (sources linked from O5Kit ThirdParty).
// O5Kit's GTweenRunner drives exactly this path: context.Play + Tick(delta).
public sealed class GTweenEngineTests {
    [Fact]
    public void FloatTween_ReachesTarget_WithLinearEasing() {
        var ctx = new GTweensContext();
        float v = 0f;
        bool done = false;
        var tween = GTweenExtensions.Tween(() => v, x => v = x, 10f, 1f);
        tween.SetEasing(Easing.Linear);
        tween.OnComplete(() => done = true);
        ctx.Play(tween);

        ctx.Tick(0.5f);
        Assert.Equal(5f, v, precision: 4);
        Assert.False(done);

        ctx.Tick(0.5f);
        Assert.Equal(10f, v, precision: 4);
        Assert.True(done);
        Assert.True(tween.IsCompleted);
    }

    [Fact]
    public void KilledTween_Freezes_WithoutCompletion() {
        var ctx = new GTweensContext();
        float v = 0f;
        bool done = false;
        var tween = GTweenExtensions.Tween(() => v, x => v = x, 10f, 1f);
        tween.SetEasing(Easing.Linear);
        tween.OnComplete(() => done = true);
        ctx.Play(tween);

        ctx.Tick(0.5f);
        tween.Kill();

        ctx.Tick(1f);
        Assert.Equal(5f, v, precision: 4);
        Assert.False(done);
        Assert.True(tween.IsKilled);
    }

    [Fact]
    public void CompletedTween_Snaps_AndFiresCompletion() {
        var ctx = new GTweensContext();
        float v = 0f;
        bool done = false;
        var tween = GTweenExtensions.Tween(() => v, x => v = x, 10f, 10f);
        tween.SetEasing(Easing.Linear);
        tween.OnComplete(() => done = true);
        ctx.Play(tween);

        ctx.Tick(1f);
        tween.Complete();

        Assert.Equal(10f, v, precision: 4);
        Assert.True(done);
    }
}
