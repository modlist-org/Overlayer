using Overlayer.Patch.Safe;
using Xunit;

namespace Overlayer.Tests;

public sealed class SafePatchTests {
    public class DummyPatchTarget {
        public static int Value = 42;
        public static void SetValue(int v) => Value = v;
        public int InstanceValue;
        public void SetInstance(int v) => InstanceValue = v;
    }

    [Fact]
    public void GetMethodSafe_FindsExistingMethods() {
        var staticMethod = SafePatch.GetMethodSafe(nameof(DummyPatchTarget), nameof(DummyPatchTarget.SetValue), allowStatic: true);
        Assert.NotNull(staticMethod);
        Assert.Equal(nameof(DummyPatchTarget.SetValue), staticMethod.Name);

        var instanceMethod = SafePatch.GetMethodSafe(nameof(DummyPatchTarget), nameof(DummyPatchTarget.SetInstance));
        Assert.NotNull(instanceMethod);
        Assert.Equal(nameof(DummyPatchTarget.SetInstance), instanceMethod.Name);
    }

    [Fact]
    public void GetMethodSafe_CachesTypeAndResolvesFast() {
        var first = SafePatch.GetMethodSafe(nameof(DummyPatchTarget), nameof(DummyPatchTarget.SetValue), allowStatic: true);
        var second = SafePatch.GetMethodSafe(nameof(DummyPatchTarget), nameof(DummyPatchTarget.SetValue), allowStatic: true);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetMethodSafe_ThrowsOnMissingType() {
        Assert.Throws<Exception>(() => SafePatch.GetMethodSafe("NoSuchType_XYZ", "Whatever"));
    }

    [Fact]
    public void GetMethodSafe_ThrowsOnMissingMethod() {
        Assert.Throws<Exception>(() => SafePatch.GetMethodSafe(nameof(DummyPatchTarget), "NoSuchMethod_XYZ"));
    }

    [Fact]
    public void GetConstructorSafe_FindsDefaultConstructor() {
        var ctor = SafePatch.GetConstructorSafe(nameof(DummyPatchTarget));
        Assert.NotNull(ctor);
    }
}
