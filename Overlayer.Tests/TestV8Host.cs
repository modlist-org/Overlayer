using Microsoft.ClearScript.V8;

namespace Overlayer.Tests;

// Actual V8 execution, with only the Unity/runtime service boundary replaced.
public sealed class TestV8Host : IDisposable {
    public V8ScriptEngine Engine { get; } = new();
    private readonly Dictionary<string, V8Script> scripts = [];
    public bool TryEvaluateFx(string code, out object result) {
        result = null;
        try {
            if (!scripts.TryGetValue(code, out var script)) scripts.Add(code, script = Engine.Compile(code));
            result = Engine.Evaluate(script);
            return true;
        } catch { return false; }
    }
    public void Dispose() {
        foreach (var script in scripts.Values) script.Dispose();
        Engine.Dispose();
    }
}
