using System.Diagnostics;
using System.Globalization;
using Overlayer.Core;
using Overlayer.Native;
using Overlayer.Tag.Core;
using Overlayer.TextEngine.Parse;
using Xunit;
using Xunit.Abstractions;

namespace Overlayer.Tests;

// The native engine must be indistinguishable from V8 for every expression it accepts.
public sealed class JsExprParityTests(ITestOutputHelper output) {
    private static readonly double[] Vars = [3, -2.5, 0.1, 1e10, -0.0];

    private static readonly string[] Atoms = [
        "0", "1", "2", "3", "7", "0.5", "1.5", "2.5", "(-0.5)", "1e21", "4294967296", "2147483648", "0x7fffffff",
        ".25", "1e-7", "NaN", "Infinity", "true", "false", "Math.PI", "Math.E", "v0", "v1", "v2", "v3", "v4"
    ];

    private static readonly string[] Binary = [
        "+", "-", "*", "/", "%", "**", "<", "<=", ">", ">=", "==", "!=", "===", "!==",
        "&&", "||", "&", "|", "^", "<<", ">>", ">>>"
    ];

    private static readonly string[] Unary = ["-", "+", "!", "~"];

    private static readonly string[] Exact = ["abs", "ceil", "floor", "round", "sign", "trunc", "sqrt", "fround", "clz32"];
    private static readonly string[] Transcendental = ["sin", "cos", "tan", "atan", "exp", "log", "log2", "log10", "cbrt", "tanh", "asinh"];

    private static string Gen(Random r, int depth, bool exactOnly) {
        if (depth == 0 || r.Next(4) == 0) return Atoms[r.Next(Atoms.Length)];
        string a = Gen(r, depth - 1, exactOnly), b = Gen(r, depth - 1, exactOnly);
        switch (r.Next(exactOnly ? 6 : 7)) {
            case 0: case 1: {
                string op = Binary[r.Next(Binary.Length)];
                if (exactOnly && op == "**") op = "*";
                return $"({a} {op} {b})";
            }
            case 2: return $"({Unary[r.Next(Unary.Length)]}{a})";
            case 3: return $"({a} ? {b} : {Gen(r, depth - 1, exactOnly)})";
            case 4: return $"Math.{Exact[r.Next(Exact.Length)]}({a})";
            case 5: return r.Next(3) switch {
                0 => $"Math.max({a}, {b})",
                1 => $"Math.min({a}, {b}, {Gen(r, depth - 1, exactOnly)})",
                _ => $"Math.imul({a}, {b})"
            };
            default: return r.Next(3) switch {
                0 => $"Math.{Transcendental[r.Next(Transcendental.Length)]}({a})",
                1 => $"Math.hypot({a}, {b})",
                _ => $"Math.pow({a}, {b})"
            };
        }
    }

    // Rejection is always safe (V8 runs it); only accepted code must match.
    private static bool Native(string code, out double value) {
        value = 0;
        nint program = OvJsExpr.Compile(code);
        if (program == 0) return false;
        try { value = OvJsExpr.Eval(program, Vars); }
        finally { OvJsExpr.Free(program); }
        return true;
    }

    private static double V8(TestV8Host v8, string code) => Convert.ToDouble(v8.Engine.Evaluate(code), CultureInfo.InvariantCulture);

    private static TestV8Host NewHost() {
        var v8 = new TestV8Host();
        for (int i = 0; i < Vars.Length; i++) v8.Engine.Execute($"globalThis.v{i} = {(i == 4 ? "-0" : Vars[i].ToString("R", CultureInfo.InvariantCulture))};");
        return v8;
    }

    [Fact]
    public void ExactOperatorsMatchV8BitForBit() {
        Assert.True(OvJsExpr.IsSupported);
        using var v8 = NewHost();
        var random = new Random(20261014);
        int accepted = 0;
        for (int i = 0; i < 20000; i++) {
            // Unary plus forces a numeric result so boolean-typed trees are still exercised.
            string code = "+" + Gen(random, 5, exactOnly: true);
            if (!Native(code, out double actual)) continue;
            accepted++;
            double expected = V8(v8, code);
            // ClearScript marshals integral results as int, which drops the sign of -0.
            if (expected == 0 && actual == 0) continue;
            Assert.True(BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual) ||
                double.IsNaN(expected) && double.IsNaN(actual), $"{code}: V8 {expected:R}, native {actual:R}");
        }
        output.WriteLine($"{accepted}/20000 random expressions ran natively");
        Assert.True(accepted > 16000);
    }

    // Platform libm and V8's fdlibm port may disagree in the last bits; nothing more.
    [Fact]
    public void TranscendentalsMatchV8WithinFourUlps() {
        using var v8 = NewHost();
        var random = new Random(7);
        int inexact = 0, total = 0;
        string[] binary = ["atan2", "pow", "hypot"];
        foreach (string f in Transcendental.Concat(["acos", "asin", "sinh", "cosh", "atanh", "acosh", "expm1", "log1p"]).Concat(binary)) {
            for (int i = 0; i < 300; i++) {
                double x = (random.NextDouble() - 0.3) * Math.Pow(10, random.Next(-3, 4));
                double y = (random.NextDouble() - 0.3) * 8;
                string code = binary.Contains(f)
                    ? $"Math.{f}({x.ToString("R", CultureInfo.InvariantCulture)}, {y.ToString("R", CultureInfo.InvariantCulture)})"
                    : $"Math.{f}({x.ToString("R", CultureInfo.InvariantCulture)})";
                Assert.True(Native(code, out double actual), code);
                double expected = V8(v8, code);
                total++;
                if (expected == actual || double.IsNaN(expected) && double.IsNaN(actual)) continue;
                inexact++;
                long ulps = Math.Abs(BitConverter.DoubleToInt64Bits(expected) - BitConverter.DoubleToInt64Bits(actual));
                Assert.True(Math.Sign(expected) == Math.Sign(actual) && ulps <= 4, $"{code}: V8 {expected:R}, native {actual:R}");
            }
        }
        output.WriteLine($"{inexact}/{total} transcendental calls differ from V8 in the last bit");
    }

    [Theory]
    [InlineData("1 < 2")]
    [InlineData("'a' + 1")]
    [InlineData("Math.random()")]
    [InlineData("[1, 2].length")]
    [InlineData("(() => 4)()")]
    [InlineData("v0 > 1 ? 'big' : 0")]
    public void NonNumericJsIsLeftToV8(string code) => Assert.Equal(0, OvJsExpr.Compile(code));

    [Fact]
    public void PlaceholdersGoNativeButKeepV8FallbackAndOutput() {
        using var v8 = NewHost();
        MainCore.V8 = v8;
        try {
            foreach (string code in new[] {
                "Math.round(2.5) + (1 << 40)", "Math.max(1, 2, 3) ** 2 / 7", "~~-3.7 >>> 0", "0.1 + 0.2",
                "(1 > 2) + 0.5", "Math.hypot(3, 4, 12)", "1 / 0", "-1 / 0", "0 / 0", "Math.imul(0xffffffff, 5)"
            }) {
                string viaV8 = Convert.ToDouble(v8.Engine.Evaluate(code), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                if (viaV8 == "∞") viaV8 = "Infinity";
                if (viaV8 == "-∞") viaV8 = "-Infinity";
                Assert.NotEqual(0, OvJsExpr.Compile(code));
                Assert.Equal(viaV8, ExprCore.CompilePlaceholder(Parser.Parse("{" + code + "}")[0]).Get());
            }
            Assert.Equal("ab", ExprCore.CompilePlaceholder(Parser.Parse("{'a' + 'b'}")[0]).Get());
            Assert.Equal("4.00", ExprCore.CompilePlaceholder(Parser.Parse("{[1, 2, 3].reduce((a, b) => a + b) - 2:F2}")[0]).Get());
        } finally { MainCore.V8 = null; }
    }

    [Fact]
    public void NativeIsFasterThanV8() {
        using var v8 = NewHost();
        const string code = "Math.max(v0 * 2, Math.round(v1 / 3)) + (v2 > 0 ? Math.sqrt(v3) : 0) | 0";
        nint program = OvJsExpr.Compile(code);
        Assert.NotEqual(0, program);
        Assert.True(v8.TryEvaluateFx(code, out _));
        const int n = 200_000;
        double sink = 0;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++) sink += OvJsExpr.Eval(program, Vars);
        double nativeNs = sw.Elapsed.TotalNanoseconds / n;
        sw.Restart();
        for (int i = 0; i < n / 20; i++) { v8.TryEvaluateFx(code, out var r); sink += Convert.ToDouble(r); }
        double v8Ns = sw.Elapsed.TotalNanoseconds / (n / 20);
        OvJsExpr.Free(program);
        output.WriteLine($"native {nativeNs:F1} ns/eval, V8 {v8Ns:F1} ns/eval, {v8Ns / nativeNs:F0}x (sink {sink})");
        Assert.True(nativeNs * 10 < v8Ns);
    }
}
