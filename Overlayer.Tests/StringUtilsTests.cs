using Overlayer.Utility;
using Xunit;

namespace Overlayer.Tests;

public sealed class StringUtilsTests {
    [Fact]
    public void Chosung_CoversAllNineteenInitials() {
        // One syllable per initial consonant, in Unicode order (가 까 나 다 따 ... 하).
        var syllables = new System.Text.StringBuilder();
        for(int i = 0; i < 19; i++) {
            syllables.Append((char)(0xAC00 + i * 588));
        }
        Assert.Equal("ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ", StringUtils.NormalizeToHangulChosung(syllables.ToString()));
    }
}
