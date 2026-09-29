using Overlayer.TextEngine.Parse;
using Xunit;

namespace Overlayer.Tests;

public sealed class ParserTests {
    [Fact]
    public void PlainText_ReturnsNoTags() {
        Assert.Empty(Parser.Parse("dropped the beat and died"));
    }

    [Fact]
    public void SimpleTag_ParsesName() {
        var tags = Parser.Parse("{Bpm}");
        var tag = Assert.Single(tags);
        Assert.Equal("Bpm", tag.Name);
        Assert.Empty(tag.Args);
    }

    [Fact]
    public void ColonTag_SplitsArgs() {
        var tags = Parser.Parse("{Stream:200, 174}");
        var tag = Assert.Single(tags);
        Assert.Equal("Stream", tag.Name);
        Assert.Equal(["200", "174"], tag.Args);
    }

    [Fact]
    public void FuncForm_ParsesArgs() {
        var tags = Parser.Parse("{Stream(200,174)}");
        var tag = Assert.Single(tags);
        Assert.Equal("Stream", tag.Name);
        Assert.Equal(["200", "174"], tag.Args);
    }

    [Fact]
    public void JsExpr_PreservesBracesInside() {
        var tags = Parser.Parse("{JSExpr:{gauge: 100}}");
        var tag = Assert.Single(tags);
        Assert.Equal("JSExpr", tag.Name);
    }

    [Fact]
    public void MultipleTags_ReturnsInOrder() {
        var tags = Parser.Parse("{FullCombo} nice {Miss:fail}");
        Assert.Equal(2, tags.Count);
        Assert.Equal("FullCombo", tags[0].Name);
        Assert.Equal("Miss", tags[1].Name);
        Assert.Equal(["fail"], tags[1].Args);
    }
}
