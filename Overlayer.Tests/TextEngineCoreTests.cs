using Overlayer.TextEngine.Core;
using Xunit;

namespace Overlayer.Tests;

public sealed class TextEngineCoreTests {
    [Fact]
    public async Task EqualContentReusesComposedStringWithoutSkippingEvaluation() {
        var engine = new TextEngineCore();
        try {
            engine.Text = "Value: {Test}";
            await Ready(engine);
            int calls = 0;
            engine.Segments[0].Replacer.Getter = () => { calls++; return new string('x', 1); };
            string first = engine.Get();
            Assert.Equal("Value: x", first);
            Assert.Same(first, engine.Get());
            Assert.Equal(2, calls);
            engine.Segments[0].Replacer.Getter = () => "changed";
            Assert.Equal("Value: changed", engine.Get());
        } finally { engine.Dispose(); }
    }

    [Fact]
    public async Task RenderToPreservesNullEmptyAndTruncatedReplacements() {
        var engine = new TextEngineCore();
        try {
            engine.Text = "A{One}B{Two}C";
            await Ready(engine);
            engine.Segments[0].Replacer.Getter = () => throw new InvalidOperationException();
            engine.Segments[1].Replacer.Getter = () => "";
            var buffer = new char[64];
            int written = engine.RenderTo(buffer);
            Assert.Equal("A{One}BC", new string(buffer, 0, written));
            written = engine.RenderTo(buffer.AsSpan(0, 3));
            Assert.Equal("A{O", new string(buffer, 0, written));
            Assert.Equal(0, engine.RenderTo(Span<char>.Empty));
        } finally { engine.Dispose(); }
    }

    [Fact]
    public async Task RenderToHasNoSteadyStateArrayAllocationAndSurvivesRecompile() {
        var engine = new TextEngineCore();
        try {
            engine.Text = "{One}";
            await Ready(engine);
            engine.Segments[0].Replacer.Getter = () => "1";
            var buffer = new char[64];
            for (int i = 0; i < 1000; i++) engine.RenderTo(buffer);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) engine.RenderTo(buffer);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.Equal("1", engine.Get());

            engine.Text = "{One}:{Two}";
            await Ready(engine);
            engine.Segments[0].Replacer.Getter = () => "2";
            engine.Segments[1].Replacer.Getter = () => "3";
            Assert.Equal("2:3", engine.Get());
            Assert.Equal("2:3", engine.Get());
            Assert.Equal(3, engine.RenderTo(buffer));
            Assert.Equal("2:3", new string(buffer, 0, 3));
        } finally { engine.Dispose(); }
    }

    private static async Task Ready(TextEngineCore engine) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (engine.State == TextEngineState.Compiling) await Task.Delay(1, timeout.Token);
        Assert.Equal(TextEngineState.Ready, engine.State);
    }

    [Fact]
    public async Task RecompileCannotDisposeOrMixAnInFlightGeneration() {
        using var engine = new TextEngineCore { Text = "old:{One}" };
        await Ready(engine);
        var old = engine.Segments[0].Replacer;
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        old.Getter = () => {
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            Assert.False(old.Disposed);
            return "value";
        };
        var render = Task.Run(engine.Get);
        try {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            engine.Text = "new:{One}/{Two}";
            await Ready(engine);
            Assert.False(old.Disposed);
            engine.Segments[0].Replacer.Getter = () => "1";
            engine.Segments[1].Replacer.Getter = () => "2";
        } finally { resume.Set(); }
        Assert.Equal("old:value", await render);
        Assert.True(old.Disposed);
        Assert.Equal("new:1/2", engine.Get());
    }

    [Fact]
    public async Task LatestGenerationWinsAndDisposePreventsNewCompilation() {
        using var engine = new TextEngineCore();
        for (int i = 0; i < 50; i++) engine.Text = i + ":{One}";
        await Ready(engine);
        engine.Segments[0].Replacer.Getter = () => "latest";
        Assert.Equal("49:latest", engine.Get());
        engine.Dispose();
        engine.ForceRecompile();
        engine.Text = "ignored";
        Assert.Equal(TextEngineState.Idle, engine.State);
        Assert.Empty(engine.Segments);
    }
}
