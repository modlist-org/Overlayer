using Overlayer.Tag.Core;
using Xunit;

namespace Overlayer.Tests;

public sealed class V8ExpressionDispatchTests {
    public sealed class Engine {
        public bool TryEvaluateFx(string code, out object result) {
            result = code;
            return code != "failure";
        }
    }

    public static class Core {
        public static Engine V8 { get; set; }
    }

    [Fact]
    public void DispatchTracksEngineLifecycleAndClearsOutput() {
        var evaluate = V8ExpressionDispatch.Create(typeof(Core));
        Core.V8 = null;
        Assert.False(evaluate("hello", out var result));
        Assert.Null(result);
        Core.V8 = new Engine();
        Assert.True(evaluate("hello", out result));
        Assert.Equal("hello", result);
        Assert.False(evaluate("failure", out result));
        Core.V8 = null;
        Assert.False(evaluate("hello", out result));
        Assert.Null(result);
        Core.V8 = new Engine();
        Assert.True(evaluate("replacement", out result));
        Assert.Equal("replacement", result);
    }

    [Fact]
    public void UnsupportedCoreDoesNotProduceDispatch() {
        Assert.Null(V8ExpressionDispatch.Create(null));
        Assert.Null(V8ExpressionDispatch.Create(typeof(string)));
    }
}
