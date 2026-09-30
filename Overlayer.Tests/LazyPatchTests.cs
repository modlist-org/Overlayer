using Overlayer.Patch.Lazy;
using Overlayer.Tag.Core;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class LazyPatchTests {
    private sealed class FakePatchA { }
    private sealed class FakePatchB { }

    private static class DummyTags {
        [NeedsPatch(typeof(FakePatchA), typeof(FakePatchB))]
        public static double Metered => 1.0;

        [NeedsPatch(typeof(FakePatchA))]
        public static double Plain => 2.0;
    }

    private static TagCore PropertyTag(string name, string property)
        => new(name, typeof(DummyTags).GetProperty(property, BindingFlags.Public | BindingFlags.Static)!,
            TagType.None);

    [Fact]
    public void PropertyTag_ResolvesPatchesThroughGetter() {
        var tag = PropertyTag("Metered", nameof(DummyTags.Metered));
        var types = NeedsPatchResolver.GetRequiredPatchTypes(tag);

        Assert.Equal(2, types.Count);
        Assert.Contains(typeof(FakePatchA), types);
        Assert.Contains(typeof(FakePatchB), types);
    }

    [Fact]
    public void SinglePatch_Resolves() {
        var tag = PropertyTag("Plain", nameof(DummyTags.Plain));

        Assert.Equal([typeof(FakePatchA)], NeedsPatchResolver.GetRequiredPatchTypes(tag));
    }

    [Fact]
    public void DuplicateTypes_AreDistinct() {
        var tag = PropertyTag("Metered", nameof(DummyTags.Metered));
        var types = NeedsPatchResolver.GetRequiredPatchTypes(tag);

        Assert.Equal(types.Count, new HashSet<Type>(types).Count);
    }
}
