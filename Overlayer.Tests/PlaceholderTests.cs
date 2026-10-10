using Overlayer.Tag.Core;
using Xunit;

namespace Overlayer.Tests;

public sealed class PlaceholderTests {
    [Fact]
    public void Equals_SameNameAndArgs_ReturnsTrue() {
        var p1 = new Placeholder("Test", ["a", "b"]);
        var p2 = new Placeholder("Test", ["a", "b"]);
        Assert.True(p1 == p2);
        Assert.True(p1.Equals(p2));
        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentArgs_ReturnsFalse() {
        var p1 = new Placeholder("Test", ["a", "b"]);
        var p2 = new Placeholder("Test", ["a", "c"]);
        Assert.False(p1 == p2);
        Assert.True(p1 != p2);
        Assert.NotEqual(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void NullArgs_HandledAsEmptyArray() {
        var p1 = new Placeholder("Test", null);
        var p2 = new Placeholder("Test", []);
        Assert.True(p1 == p2);
        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void NullName_HandledSafely() {
        var p1 = new Placeholder(null, null);
        var p2 = new Placeholder(null, []);
        Assert.True(p1 == p2);
        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }
}
