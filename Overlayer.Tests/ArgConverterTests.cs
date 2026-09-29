using Overlayer.Tag.Compile;
using Xunit;

namespace Overlayer.Tests;

public sealed class ArgConverterTests {
    private enum Judgement {
        Perfect,
        Good,
        Miss
    }

    [Fact]
    public void String_PassesThrough() {
        Assert.Equal("FullCombo", ArgConverter.Convert("FullCombo", typeof(string)));
    }

    [Theory]
    [InlineData("200", 200)]
    [InlineData("174", 174)]
    public void Int_Parses(string raw, int expected) {
        Assert.Equal(expected, ArgConverter.Convert(raw, typeof(int)));
    }

    [Fact]
    public void Double_Parses() {
        Assert.Equal(1.5d, ArgConverter.Convert("1.5", typeof(double)));
    }

    [Fact]
    public void Bool_Parses() {
        Assert.Equal(true, ArgConverter.Convert("True", typeof(bool)));
    }

    [Fact]
    public void Enum_ParsesIgnoreCase() {
        Assert.Equal(Judgement.Miss, ArgConverter.Convert("MiSs", typeof(Judgement)));
    }

    [Fact]
    public void InvalidInt_Throws() {
        Assert.Throws<FormatException>(() => ArgConverter.Convert("FullCombo", typeof(int)));
    }
}
