using Overlayer.Tag.Core;
using Xunit;

public sealed class TagArgParserTests {
    [Theory]
    [InlineData("Combo", "Combo")]
    [InlineData("  Combo  ", "Combo")]
    [InlineData("Combo()", "Combo")]
    [InlineData("Combo( )", "Combo")]
    public void BareNameHasNoArgs(string expr, string name) {
        Assert.True(TagArgParser.TrySplit(expr, out string got, out string[] args));
        Assert.Equal(name, got);
        Assert.Empty(args);
    }

    [Theory]
    [InlineData("Combo(1,2)", "Combo", new[] { "1", "2" })]
    [InlineData("Combo( 1 , 2.5 )", "Combo", new[] { "1", "2.5" })]
    [InlineData("F(a,{B},(1+2))", "F", new[] { "a", "{B}", "(1+2)" })]
    [InlineData("F('a,b',\"c,d\")", "F", new[] { "'a,b'", "\"c,d\"" })]
    [InlineData("F(  )", "F")]
    public void ArgsSplit(string expr, string name, string[] want = null) {
        Assert.True(TagArgParser.TrySplit(expr, out string got, out string[] args));
        Assert.Equal(name, got);
        Assert.Equal(want ?? [], args);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Combo(")]
    [InlineData("Combo)")]
    [InlineData("(1,2)")]
    [InlineData("F(a,b")]
    [InlineData("F((a)")]
    [InlineData("F('a)")]
    public void MalformedFails(string expr) {
        Assert.False(TagArgParser.TrySplit(expr, out _, out _));
    }

    [Fact]
    public void NullFails() {
        Assert.False(TagArgParser.TrySplit(null, out _, out _));
    }
}
