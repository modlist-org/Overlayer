using System.Text.RegularExpressions;
using Overlayer.Tutorial;
using Xunit;

namespace Overlayer.Tests;

public sealed class TutorialWrapTests {
    private static readonly Regex RichTag = new(@"</?(?:color|noparse)(?:=[^>]*)?>");

    private static float Measure(string s) {
        if (string.IsNullOrEmpty(s)) {
            return 0f;
        }
        string visible = RichTag.Replace(s, string.Empty);
        float w = 0f;
        foreach (char c in visible) {
            if (c == ' ') {
                w += 4.2f;
            } else if (c >= 0x1100) {
                w += 13.8f;
            } else {
                w += 8.5f;
            }
        }
        return w;
    }

    [Fact]
    public void Format_PrefersSentenceBoundaryOverGreedyFill_Korean() {
        string text = "알겠어요. 메뉴의 <b>튜토리얼</b> 페이지에서 언제든 다시 시작할 수 있어요.";
        string formatted = TutorialWrap.Format(text, 380f, Measure);
        string[] lines = formatted.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("알겠어요.", lines[0]);
        Assert.DoesNotContain("수\n있어요.", formatted);
    }

    [Fact]
    public void Format_PrefersSentenceBoundaryOverGreedyFill_English() {
        string text = "Hi, I'm Olly. Let me walk you through how Overlayer works.";
        string formatted = TutorialWrap.Format(text, 380f, Measure);
        string[] lines = formatted.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Hi, I'm Olly.", lines[0]);
        Assert.Equal("Let me walk you through how Overlayer works.", lines[1]);
    }

    [Fact]
    public void Format_KeepsSuIsseoyoTogether_NoSingleWordOrphan() {
        string text = "나머지 장은 메뉴의 <b>튜토리얼</b> 페이지에서 언제든 볼 수 있어요.";
        string formatted = TutorialWrap.Format(text, 360f, Measure);
        string[] lines = formatted.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.DoesNotMatch(@"\n있어요\.$", formatted);
        Assert.EndsWith("수 있어요.", lines[1]);
    }

    [Fact]
    public void Format_KeepsCodeAndBoldSpansAtomic() {
        string text = "화면 밖으로 나갔다면 «Alt + `»를 <b>길게 눌러</b> 위치와 크기를 초기화할 수 있습니다.";
        string formatted = TutorialWrap.Format(text, 300f, Measure);
        string[] lines = formatted.Split('\n');
        Assert.All(lines, line => {
            Assert.DoesNotContain("<noparse>Alt +\n", line);
            Assert.DoesNotContain("길게\n눌러", line);
        });
        Assert.Contains("<noparse>Alt + `</noparse></color>를", formatted);
        Assert.Contains("<color=#FFFFFF>길게 눌러</color>", formatted);
    }

    [Fact]
    public void Format_PreservesDoubleSpacesInNoteLines() {
        string singleLine = "«0.55 + 0.45 * Math.sin(Time() * 4)»  부드러운 깜빡임";
        string formatted = TutorialWrap.Format(singleLine, 500f, Measure);
        Assert.Equal("<color=#AEB3FF><noparse>0.55 + 0.45 * Math.sin(Time() * 4)</noparse></color>  부드러운 깜빡임", formatted);

        string wrappedWithInternalDoubleSpace = "«{Fps:0}»  =  60  (59.6은 60으로 아주 길게 반올림하여 표시됩니다)";
        string wrapped = TutorialWrap.Format(wrappedWithInternalDoubleSpace, 360f, Measure);
        Assert.Contains("</noparse></color>  =  60", wrapped);
    }

    [Fact]
    public void Format_BypassesCjkProseAndStripsFaceMarks() {
        string zh = "[[happy]]可以在这里注册自己的字体和图片：点击<b>浏览</b>（或粘贴路径）、取好名字后，点击<b>添加图片 / 字体</b>即可添加";
        string formatted = TutorialWrap.Format(zh, 300f, Measure);
        Assert.DoesNotContain("\n", formatted);
        Assert.DoesNotContain("[[happy]]", formatted);
        Assert.Contains("<color=#FFFFFF>浏览</color>", formatted);
    }

    [Fact]
    public void NeededWidth_MeasuresLongCodeSpanButIgnoresChineseProse() {
        string zhProseOnly = "可以在这里注册自己的字体和图片，没有任何代码标签的长句子示例文本测试";
        Assert.Equal(0f, TutorialWrap.NeededWidth(zhProseOnly, Measure));

        string longCode = "«<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second}</color>»\n인자 순서: 태그, 최솟값";
        float needed = TutorialWrap.NeededWidth(longCode, Measure);
        Assert.Equal(Measure("<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second}</color>"), needed);
    }
}
