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

    [Fact]
    public void NativeScanner_DirectSpanScan_ReturnsExpectedSpans() {
        Assert.True(Overlayer.Native.O5Native.IsAvailable);
        Span<Overlayer.Native.OvTagSpan> spans = stackalloc Overlayer.Native.OvTagSpan[16];
        int count = Overlayer.Native.OvTagScanner.Scan("{Tag:1,2} text {Func(a)}", spans);
        Assert.Equal(2, count);
        Assert.Equal(Overlayer.Native.OvTagType.Colon, spans[0].Type);
        Assert.Equal(Overlayer.Native.OvTagType.Func, spans[1].Type);
    }
}
