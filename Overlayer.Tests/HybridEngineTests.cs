using Microsoft.ClearScript;
using Overlayer.Core;
using Overlayer.Native;
using Overlayer.Tag.Compile;
using Overlayer.Tag.Core;
using Overlayer.TextEngine.Core;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;
using Overlayer.V8;
using Xunit;

namespace Overlayer.Tests;

public sealed class HybridEngineTests {
    public sealed class TagBridge(Dictionary<string, TagCore> tags) {
        public object Get(string name, params object[] args) => tags[name].Invoke(args);
    }

    [Fact]
    public void ParameterizedJsTagsWorkThroughGeneratedTagProxyAndClrBridge() {
        using var v8 = new TestV8Host();
        var tags = new Dictionary<string, TagCore> {
            ["RequiredText"] = new("RequiredText", (ScriptObject)v8.Engine.Evaluate("((number, text) => text + ':' + number * 2)"),
                ["number", "text"], TagType.None),
            ["RequiredNumber"] = new("RequiredNumber", (ScriptObject)v8.Engine.Evaluate("((number) => number * 2 + 0.5)"),
                ["number"], TagType.None)
        };
        v8.Engine.AddHostObject("TagAccessHelper", new TagBridge(tags));
        v8.Engine.Execute(ImplJsGenerator.BuildJs(tags.Values));
        MainCore.V8 = v8;
        try {
            Assert.Equal("hello:10", ExprCore.CompilePlaceholder(Parser.Parse("{RequiredText(5, 'hello')}")[0]).Get());
            Assert.Equal("world:14", ExprCore.CompilePlaceholder(Parser.Parse("{Tag.RequiredText(7, 'world')}")[0]).Get());
            Assert.Equal("12.50", ExprCore.CompilePlaceholder(Parser.Parse("{RequiredNumber(6):F2}")[0]).Get());
        } finally { MainCore.V8 = null; }
    }

    [Fact]
    public async Task ThrowingJsTagPreservesRawPlaceholderWithoutBreakingOtherTags() {
        using var v8 = new TestV8Host();
        var bad = new TagCore("Throws", (ScriptObject)v8.Engine.Evaluate("(() => { throw new Error('expected'); })"), [], TagType.None);
        using var engine = new TextEngineCore { NativeRendering = true, Text = "A{Throws}B{Good}C" };
        await Ready(engine);
        engine.Segments[0].Replacer.Getter = Compiler.Compile(bad, Parser.Parse("{Throws}")[0]).Get;
        engine.Segments[1].Replacer.Getter = () => "ok";
        Assert.Equal("A{Throws}BokC", engine.Get());
    }
    [Fact]
    public async Task StatefulJsTagsStayOrderedAndRichTextSurvivesNativeComposition() {
        using var v8 = new TestV8Host();
        v8.Engine.Execute("globalThis.n = 0;");
        var function = (ScriptObject)v8.Engine.Evaluate("(() => '<color=#ff00ff>' + (++globalThis.n) + '</color>')");
        var tag = new TagCore("Pulse", function, [], TagType.None);
        string prefix = new('x', 4096);
        using var engine = new TextEngineCore { NativeRendering = true, Text = prefix + "{Pulse}|{Pulse}" };
        await Ready(engine);
        foreach (var segment in engine.Segments) {
            var parsed = Parser.Parse(segment.Replacer.Parsed.Raw)[0];
            segment.Replacer.Getter = Compiler.Compile(tag, parsed).Get;
        }
        Assert.Equal(prefix + "<color=#ff00ff>1</color>|<color=#ff00ff>2</color>", engine.Get());
        Assert.Equal(prefix + "<color=#ff00ff>3</color>|<color=#ff00ff>4</color>", engine.Get());
        var output = new char[5000];
        int written = engine.RenderTo(output);
        Assert.Equal(prefix + "<color=#ff00ff>5</color>|<color=#ff00ff>6</color>", new string(output, 0, written));
        Assert.True(OvTextRenderer.IsSupported);
    }

    [Fact]
    public async Task OptionalJsParametersNumericFormattingAndUndefinedRemainCompatible() {
        using var v8 = new TestV8Host();
        var optional = (ScriptObject)v8.Engine.Evaluate("((value = 7) => value + 0.25)");
        var tag = new TagCore("Optional", optional, ["value?"], TagType.None);
        var parsed = Parser.Parse("{Optional}")[0];
        Assert.Equal("7.25", Compiler.Compile(tag, parsed).Get());
        var numeric = new TagCore("Number", (ScriptObject)v8.Engine.Evaluate("(() => 12.345)"), [], TagType.None);
        Assert.Equal("12.35", Compiler.Compile(numeric, Parser.Parse("{Number:F2}")[0]).Get());
        var empty = new TagCore("Empty", (ScriptObject)v8.Engine.Evaluate("(() => undefined)"), [], TagType.None);
        using var engine = new TextEngineCore { Text = "A{Empty}B" };
        await Ready(engine);
        engine.Segments[0].Replacer.Getter = Compiler.Compile(empty, Parser.Parse("{Empty}")[0]).Get;
        Assert.Equal("AB", engine.Get());
    }

    [Fact]
    public void LegacyAndModernInlineJsKeepStateAndFollowEngineReplacement() {
        using var first = new TestV8Host();
        MainCore.V8 = first;
        try {
            var legacy = ExprCore.CompilePlaceholder(Parser.Parse("{JSExpr: globalThis.count ??= 0; ++globalThis.count;}")[0]);
            Assert.Equal("1", legacy.Get());
            Assert.Equal("2", legacy.Get());
            var modern = ExprCore.CompilePlaceholder(Parser.Parse("{(() => '<size=110%>' + globalThis.count + '</size>')()}")[0]);
            Assert.Equal("<size=110%>2</size>", modern.Get());
            using var second = new TestV8Host();
            MainCore.V8 = second;
            Assert.Equal("1", legacy.Get());
            Assert.Equal("<size=110%>1</size>", modern.Get());
        } finally { MainCore.V8 = null; }
    }

    [Fact]
    public void EvictionDoesNotInvalidateLiveNativeExpressions() {
        Assert.True(O5Expr.IsSupported);
        var original = ExprCore.Compile("t + 12345");
        Assert.Equal("12345", original());
        for (int i = 0; i < 300; i++) {
            Assert.Equal(i.ToString(), ExprCore.Compile("t + " + i)());
            if (i % 100 == 0) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.Equal("12345", original());
    }

    [Fact]
    public void NativeAndManagedCompositionAgreeForAllOutputCapacities() {
        const string source = "A{One}B{Two}C{Three}D";
        var plan = new TextRenderPlan(source, Parser.Parse(source));
        string[] values = [null, "", "🎵<color=#ffffff>42</color>"];
        Assert.Equal("A{One}BC🎵<color=#ffffff>42</color>D".Length, plan.OutputLength(values));
        for (int capacity = 0; capacity < 64; capacity++) {
            var native = new char[capacity];
            var managed = new char[capacity];
            int n = plan.Compose(values, native, native: true);
            int m = plan.Compose(values, managed, native: false);
            Assert.True(OvTextRenderer.IsSupported);
            Assert.Equal(m, n);
            Assert.Equal(new string(managed, 0, m), new string(native, 0, n));
        }
        values[0] = "changed";
        values[1] = "longer value";
        values[2] = null;
        var buffer = new char[128];
        int written = plan.Compose(values, buffer, native: true);
        Assert.Equal("AchangedBlonger valueC{Three}D", new string(buffer, 0, written));
    }

    [Fact]
    public void NativeScannerMatchesManagedOnJsAndMalformedTemplates() {
        string[] corpus = [
            "🎵{Pulse} {Time:HH:mm:ss}", "{{escaped}} {Pulse}",
            "{JSExpr: (() => { return `x:${1}`; })()}",
            "{JSExpr: /* } */ ('}' + \"{\")} {Number:F2}",
            "{ // }\n return [1, {x: 2}];\n}", "{unterminated", "{'\\'}still string'}",
            string.Concat(Enumerable.Repeat("{Pulse}", 80))
        ];
        foreach (string source in corpus) {
            var native = new OvTagSpan[100];
            var managed = new OvTagSpan[100];
            int count = OvTagScanner.Scan(source, native);
            Assert.True(OvTagScanner.IsSupported);
            Assert.Equal(OvTagScanner.ScanManaged(source, managed), count);
            for (int i = 0; i < count; i++) Assert.Equal(managed[i], native[i]);
        }
    }

    private static async Task Ready(TextEngineCore engine) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (engine.State == TextEngineState.Compiling) await Task.Delay(1, timeout.Token);
        Assert.Equal(TextEngineState.Ready, engine.State);
    }

    [Fact]
    public void ScannerAndRendererParityFuzz() {
        var random = new Random(591);
        const string alphabet = "ab{}()[]:'\"`\\/\n *?012";
        for (int trial = 0; trial < 1000; trial++) {
            var chars = new char[random.Next(1, 200)];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[random.Next(alphabet.Length)];
            string source = new(chars);
            var nativeSpans = new OvTagSpan[64];
            var managedSpans = new OvTagSpan[64];
            int count = OvTagScanner.Scan(source, nativeSpans);
            Assert.Equal(OvTagScanner.ScanManaged(source, managedSpans), count);
            for (int i = 0; i < Math.Min(count, nativeSpans.Length); i++) Assert.Equal(managedSpans[i], nativeSpans[i]);
        }

        var template = string.Concat(Enumerable.Range(0, 20).Select(i => "literal" + i + "{Tag}"));
        var plan = new TextRenderPlan(template, Parser.Parse(template));
        var values = new string[20];
        for (int frame = 0; frame < 1000; frame++) {
            int slot = random.Next(values.Length);
            values[slot] = random.Next(4) switch { 0 => null, 1 => "", 2 => "🎵", _ => new string('x', random.Next(1, 32)) };
            int capacity = random.Next(512);
            var native = new char[capacity];
            var managed = new char[capacity];
            int n = plan.Compose(values, native, native: true);
            int m = plan.Compose(values, managed, native: false);
            Assert.Equal(m, n);
            Assert.Equal(new string(managed, 0, m), new string(native, 0, n));
        }
        Assert.True(OvTagScanner.IsSupported);
        Assert.True(OvTextRenderer.IsSupported);
    }

    [Fact]
    public void UnprefixedJsBlocksAndTernariesMatchLegacyJsExpr() {
        using var v8 = new TestV8Host();
        v8.Engine.Execute("""
            globalThis.CurTile = () => 40; globalThis.StartTile = () => 0; globalThis.LeftTile = () => 60;
            globalThis.XAccuracy = () => 0.5; globalThis.StartActualProgressPercent = () => 12.345;
            """);
        MainCore.V8 = v8;
        try {
            const string maxXAcc = """
                {
                  let done = CurTile() - StartTile();
                  let left = LeftTile();
                  let max = done + left > 0 ? (XAccuracy() * done + left) / (done + left) : 1;
                  let t = Math.min(Math.max(max, 0), 1);
                  let g = Math.round(178 * (1 - t)).toString(16).padStart(2, "0");
                  "<color=#FF" + g + g + ">" + (max * 100).toFixed(2) + "%</color>"
                }
                """;
            var block = Parser.Parse(maxXAcc)[0];
            Assert.Null(block.Format);
            Assert.Equal("<color=#FF2424>80.00%</color>", ExprCore.CompilePlaceholder(block).Get());

            var ternary = Parser.Parse("{StartTile() > 0 ? StartActualProgressPercent().toFixed(2) + '%<color=#FFFFFF> - ' : ''}")[0];
            Assert.Null(ternary.Format);
            Assert.Equal("", ExprCore.CompilePlaceholder(ternary).Get());
            v8.Engine.Execute("globalThis.StartTile = () => 3;");
            Assert.Equal("12.35%<color=#FFFFFF> - ", ExprCore.CompilePlaceholder(ternary).Get());

            Assert.Equal("7", ExprCore.CompilePlaceholder(Parser.Parse("{ let x = 3; return x + 4; }")[0]).Get());
        } finally { MainCore.V8 = null; }
    }
}
