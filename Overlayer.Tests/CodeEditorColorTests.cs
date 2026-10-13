using Overlayer.Tag.Core;
using Overlayer.TextEngine.Highlight;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class CodeEditorColorTests {
    private static class MockTags {
        public static double Combo => 350.0;
        public static double Score => 155700.0;
        public static double Acc => 0.985;
        public static string Title => "Galaxy Collapse";
        public static double GetCombo() => 350.0;
        public static double ComboMultiplier => 1.5;
    }

    private static readonly Dictionary<string, TagCore> Tags = new(StringComparer.OrdinalIgnoreCase) {
        ["Combo"] = new TagCore("Combo", typeof(MockTags).GetProperty(nameof(MockTags.Combo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Score"] = new TagCore("Score", typeof(MockTags).GetProperty(nameof(MockTags.Score), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Acc"] = new TagCore("Acc", typeof(MockTags).GetProperty(nameof(MockTags.Acc), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["Title"] = new TagCore("Title", typeof(MockTags).GetProperty(nameof(MockTags.Title), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["GetCombo"] = new TagCore("GetCombo", typeof(MockTags).GetMethod(nameof(MockTags.GetCombo), BindingFlags.Public | BindingFlags.Static)!, TagType.None),
        ["ComboMultiplier"] = new TagCore("ComboMultiplier", typeof(MockTags).GetProperty(nameof(MockTags.ComboMultiplier), BindingFlags.Public | BindingFlags.Static)!, TagType.None)
    };

    public CodeEditorColorTests() {
        ExprCore.TagLookupProvider = (string name, out TagCore tag) => Tags.TryGetValue(name, out tag);
    }

    [Fact]
    public void Palette_AllSyntaxKindsHaveDistinctNonZeroColors() {
        var kinds = Enum.GetValues<TagSyntaxKind>();
        var seenColors = new HashSet<uint>();

        foreach (var kind in kinds) {
            uint rgba = TagSyntaxHighlighter.GetColorRgba(kind);
            Assert.True(rgba != 0, $"Color for {kind} must not be zero");
            byte a = (byte)(rgba & 0xFF);
            Assert.Equal(255, a); // fully opaque alpha
            Assert.True(seenColors.Add(rgba), $"Color for {kind} (0x{rgba:X8}) must be unique in palette");
        }
    }

    [Fact]
    public void SimpleTag_WithFormat_ProducesPreciseColorKinds() {
        string text = "{Combo:N0}";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // '{' -> Delimiter (0xA6ACCDFF)
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[0]);
        Assert.Equal(0xA6ACCDFF, TagSyntaxHighlighter.GetColorRgba(kinds[0]!.Value));

        // 'Combo' -> Tag (0x96FF91FF)
        for (int i = 1; i <= 5; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kinds[i]);
            Assert.Equal(0x96FF91FF, TagSyntaxHighlighter.GetColorRgba(kinds[i]!.Value));
        }

        // ':' -> Separator (0x8990B3FF)
        Assert.Equal(TagSyntaxKind.Separator, kinds[6]);
        Assert.Equal(0x8990B3FF, TagSyntaxHighlighter.GetColorRgba(kinds[6]!.Value));

        // 'N0' -> Format (0x82D2CEFF)
        Assert.Equal(TagSyntaxKind.Format, kinds[7]);
        Assert.Equal(TagSyntaxKind.Format, kinds[8]);
        Assert.Equal(0x82D2CEFF, TagSyntaxHighlighter.GetColorRgba(kinds[7]!.Value));

        // '}' -> Delimiter
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[9]);
    }

    [Fact]
    public void MethodTag_WithAndWithoutParens_ProducesTagColor() {
        // Without parens: {GetCombo}
        string textNoParens = "{GetCombo}";
        var kindsNoParens = TagSyntaxHighlighter.BuildSyntaxKinds(textNoParens, TagSyntaxHighlighter.GetSpans(textNoParens));
        Assert.Equal(TagSyntaxKind.Delimiter, kindsNoParens[0]);
        for (int i = 1; i <= 8; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kindsNoParens[i]);
        }
        Assert.Equal(TagSyntaxKind.Delimiter, kindsNoParens[9]);

        // With parens: {GetCombo()}
        string textParens = "{GetCombo()}";
        var kindsParens = TagSyntaxHighlighter.BuildSyntaxKinds(textParens, TagSyntaxHighlighter.GetSpans(textParens));
        Assert.Equal(TagSyntaxKind.Delimiter, kindsParens[0]);
        for (int i = 1; i <= 10; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kindsParens[i]);
        }
        Assert.Equal(TagSyntaxKind.Delimiter, kindsParens[11]);
    }

    [Fact]
    public void ComplexMathExpression_NestedCallsAndFormatting_ProducesRichColors() {
        string text = "{Math.round(GetCombo() * 1.5 + 10):N0}";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // '{' -> Delimiter
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[0]);

        // 'Math' -> JsType
        int mathIdx = text.IndexOf("Math");
        for (int i = mathIdx; i < mathIdx + 4; i++) {
            Assert.Equal(TagSyntaxKind.JsType, kinds[i]);
        }

        // 'round' -> JsFunction
        int roundIdx = text.IndexOf("round");
        for (int i = roundIdx; i < roundIdx + 5; i++) {
            Assert.Equal(TagSyntaxKind.JsFunction, kinds[i]);
        }

        // 'GetCombo' -> Tag
        int comboIdx = text.IndexOf("GetCombo");
        for (int i = comboIdx; i < comboIdx + 8; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kinds[i]);
        }

        // '1.5' -> JsNumber
        int num1Idx = text.IndexOf("1.5");
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[num1Idx]);
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[num1Idx + 1]);
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[num1Idx + 2]);

        // '10' -> JsNumber
        int num2Idx = text.IndexOf("10");
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[num2Idx]);
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[num2Idx + 1]);

        // ':' -> Separator
        int colonIdx = text.LastIndexOf(':');
        Assert.Equal(TagSyntaxKind.Separator, kinds[colonIdx]);

        // 'N0' -> Format
        Assert.Equal(TagSyntaxKind.Format, kinds[colonIdx + 1]);
        Assert.Equal(TagSyntaxKind.Format, kinds[colonIdx + 2]);

        // '}' -> Delimiter
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[^1]);
    }

    [Fact]
    public void ComplexTernaryWithStringsAndNumbers_ProducesStringAndNumberColors() {
        string text = "{(Score >= 1000 ? 'S Rank' : 'A Rank')}";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // 'Score' -> Tag
        int scoreIdx = text.IndexOf("Score");
        for (int i = scoreIdx; i < scoreIdx + 5; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kinds[i]);
        }

        // '1000' -> JsNumber
        int numIdx = text.IndexOf("1000");
        for (int i = numIdx; i < numIdx + 4; i++) {
            Assert.Equal(TagSyntaxKind.JsNumber, kinds[i]);
        }

        // 'S Rank' -> JsString
        int sRankIdx = text.IndexOf("'S Rank'");
        for (int i = sRankIdx; i < sRankIdx + 8; i++) {
            Assert.Equal(TagSyntaxKind.JsString, kinds[i]);
        }

        // 'A Rank' -> JsString
        int aRankIdx = text.IndexOf("'A Rank'");
        for (int i = aRankIdx; i < aRankIdx + 8; i++) {
            Assert.Equal(TagSyntaxKind.JsString, kinds[i]);
        }
    }

    [Fact]
    public void ComplexCommentAndArithmetic_ProducesCommentColors() {
        string text = "{/* bonus calc */ Score + Combo * 2:F0}";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // '/* bonus calc */' -> JsComment
        int cmtIdx = text.IndexOf("/* bonus calc */");
        for (int i = cmtIdx; i < cmtIdx + "/* bonus calc */".Length; i++) {
            Assert.Equal(TagSyntaxKind.JsComment, kinds[i]);
        }

        // 'Score' -> Tag
        int scoreIdx = text.IndexOf("Score");
        Assert.Equal(TagSyntaxKind.Tag, kinds[scoreIdx]);

        // 'Combo' -> Tag
        int comboIdx = text.IndexOf("Combo");
        Assert.Equal(TagSyntaxKind.Tag, kinds[comboIdx]);

        // '2' -> JsNumber
        int numIdx = text.IndexOf("2");
        Assert.Equal(TagSyntaxKind.JsNumber, kinds[numIdx]);

        // 'F0' -> Format
        int fmtIdx = text.IndexOf("F0");
        Assert.Equal(TagSyntaxKind.Format, kinds[fmtIdx]);
    }

    [Fact]
    public void DeprecatedJSExpr_AllFormsProduceUnknownTagRedColor() {
        // Form 1: {JSExpr: 1 + 2}
        string textColon = "{JSExpr: 1 + 2}";
        var kindsColon = TagSyntaxHighlighter.BuildSyntaxKinds(textColon, TagSyntaxHighlighter.GetSpans(textColon));
        int js1 = textColon.IndexOf("JSExpr");
        for (int i = js1; i < js1 + 6; i++) {
            Assert.Equal(TagSyntaxKind.UnknownTag, kindsColon[i]);
            Assert.Equal(0xE2676DFF, TagSyntaxHighlighter.GetColorRgba(kindsColon[i]!.Value));
        }

        // Form 2: {JSExpr(1 + 2)}
        string textParens = "{JSExpr(1 + 2)}";
        var kindsParens = TagSyntaxHighlighter.BuildSyntaxKinds(textParens, TagSyntaxHighlighter.GetSpans(textParens));
        int js2 = textParens.IndexOf("JSExpr(1 + 2)");
        for (int i = js2; i < js2 + "JSExpr(1 + 2)".Length; i++) {
            Assert.Equal(TagSyntaxKind.UnknownTag, kindsParens[i]);
        }

        // Form 3: {JSExpr}
        string textBare = "{JSExpr}";
        var kindsBare = TagSyntaxHighlighter.BuildSyntaxKinds(textBare, TagSyntaxHighlighter.GetSpans(textBare));
        for (int i = 1; i <= 6; i++) {
            Assert.Equal(TagSyntaxKind.UnknownTag, kindsBare[i]);
        }
    }

    [Fact]
    public void UnknownTags_ProduceUnknownTagRedColor() {
        string text = "{NonExistentTag} and {NonExistentFunc()}";
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, TagSyntaxHighlighter.GetSpans(text));

        int tag1 = text.IndexOf("NonExistentTag");
        for (int i = tag1; i < tag1 + "NonExistentTag".Length; i++) {
            Assert.Equal(TagSyntaxKind.UnknownTag, kinds[i]);
        }

        int tag2 = text.IndexOf("NonExistentFunc()");
        for (int i = tag2; i < tag2 + "NonExistentFunc()".Length; i++) {
            Assert.Equal(TagSyntaxKind.UnknownTag, kinds[i]);
        }
    }

    [Fact]
    public void IncompleteTyping_ProvidesImmediateColorFeedback() {
        // While typing "{Com", 'Com' is unknown tag (red)
        string typing1 = "Status: {Com";
        var kinds1 = TagSyntaxHighlighter.BuildSyntaxKinds(typing1, TagSyntaxHighlighter.GetSpans(typing1));
        Assert.Equal(TagSyntaxKind.Delimiter, kinds1[typing1.IndexOf('{')]);
        int comIdx = typing1.IndexOf("Com");
        Assert.Equal(TagSyntaxKind.UnknownTag, kinds1[comIdx]);

        // Once completed to "{Combo", 'Combo' becomes known Tag (green)
        string typing2 = "Status: {Combo";
        var kinds2 = TagSyntaxHighlighter.BuildSyntaxKinds(typing2, TagSyntaxHighlighter.GetSpans(typing2));
        int comboIdx = typing2.IndexOf("Combo");
        for (int i = comboIdx; i < comboIdx + 5; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kinds2[i]);
        }

        // Deprecated tag typing "{JSEx" and "{JSExpr" are always red UnknownTag
        string typing3 = "{JSExpr";
        var kinds3 = TagSyntaxHighlighter.BuildSyntaxKinds(typing3, TagSyntaxHighlighter.GetSpans(typing3));
        Assert.Equal(TagSyntaxKind.UnknownTag, kinds3[1]);
    }

    [Fact]
    public void MixedTemplateText_WithMultipleTagsAndPlainText_PositionsAreAccurate() {
        string text = "Song: {Title} | Combo: {GetCombo()} (x{ComboMultiplier:F1}) | Status: {(Combo > 0 ? 'Alive' : 'Dead')}";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // Plain text outside tags has null kind (rendered in text component's default color)
        for (int i = 0; i < "Song: ".Length; i++) {
            Assert.Null(kinds[i]);
        }

        int midText = text.IndexOf(" | Combo: ");
        for (int i = midText; i < midText + " | Combo: ".Length; i++) {
            Assert.Null(kinds[i]);
        }

        // {Title}
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[text.IndexOf("{Title}")]);
        Assert.Equal(TagSyntaxKind.Tag, kinds[text.IndexOf("Title")]);

        // {GetCombo()}
        Assert.Equal(TagSyntaxKind.Tag, kinds[text.IndexOf("GetCombo")]);

        // {ComboMultiplier:F1}
        Assert.Equal(TagSyntaxKind.Tag, kinds[text.IndexOf("ComboMultiplier")]);
        int multColon = text.IndexOf(":F1");
        Assert.Equal(TagSyntaxKind.Separator, kinds[multColon]);
        Assert.Equal(TagSyntaxKind.Format, kinds[multColon + 1]);
        Assert.Equal(TagSyntaxKind.Format, kinds[multColon + 2]);

        // 'Alive' & 'Dead' strings
        Assert.Equal(TagSyntaxKind.JsString, kinds[text.IndexOf("'Alive'")]);
        Assert.Equal(TagSyntaxKind.JsString, kinds[text.IndexOf("'Dead'")]);
    }

    [Fact]
    public void WhitespaceTolerantTag_HighlightsExactTokenBoundsWithoutSpaces() {
        string text = "{   GetCombo   :   F2   }";
        var spans = TagSyntaxHighlighter.GetSpans(text);
        var kinds = TagSyntaxHighlighter.BuildSyntaxKinds(text, spans);

        // Opening and closing delimiters
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[0]);
        Assert.Equal(TagSyntaxKind.Delimiter, kinds[^1]);

        // Spaces before GetCombo must NOT be highlighted
        Assert.Null(kinds[1]);
        Assert.Null(kinds[2]);
        Assert.Null(kinds[3]);

        // 'GetCombo' is highlighted
        int comboIdx = text.IndexOf("GetCombo");
        for (int i = comboIdx; i < comboIdx + 8; i++) {
            Assert.Equal(TagSyntaxKind.Tag, kinds[i]);
        }

        // Spaces between GetCombo and ':' must NOT be highlighted
        int colonIdx = text.IndexOf(':');
        Assert.Null(kinds[comboIdx + 8]);
        Assert.Equal(TagSyntaxKind.Separator, kinds[colonIdx]);

        // Spaces between ':' and 'F2' must NOT be highlighted
        int f2Idx = text.IndexOf("F2");
        Assert.Null(kinds[colonIdx + 1]);

        // 'F2' is highlighted
        Assert.Equal(TagSyntaxKind.Format, kinds[f2Idx]);
        Assert.Equal(TagSyntaxKind.Format, kinds[f2Idx + 1]);

        // Spaces after 'F2' must NOT be highlighted
        Assert.Null(kinds[f2Idx + 2]);
    }
}
