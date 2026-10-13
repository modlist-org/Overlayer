using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Overlayer.Native;
using Overlayer.Tag.Compile;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Core;

/// <summary>
/// Core execution and compilation engine for native math expressions and JavaScript fallback.
/// Features compile-time constant folding, O5Expr stack machine with tag variables, and O5Fmt zero-allocation formatting.
/// </summary>
public static class ExprCore {
    private static readonly object NativeCacheLock = new();
    private static readonly Dictionary<string, NativeExpression> NativeHandleCache = new(StringComparer.Ordinal);
    private static readonly Queue<string> NativeCacheOrder = new();

    private static NativeExpression GetOrCompileNative(string transformed) {
        lock (NativeCacheLock) {
            if (NativeHandleCache.TryGetValue(transformed, out var expression)) return expression;
            nint js = OvJsExpr.Compile(transformed);
            if (js != 0) {
                expression = new NativeExpression(js, js: true);
            } else {
                long handle = O5Expr.Compile(transformed);
                if (handle == 0) return null;
                expression = new NativeExpression(handle);
            }
            // The shared O5 runtime has 1024 slots; leave room for numeric Fx,
            // graphs and live delegates whose programs have left this cache.
            if (NativeHandleCache.Count >= 128) {
                NativeHandleCache.Remove(NativeCacheOrder.Dequeue());
            }
            NativeHandleCache.Add(transformed, expression);
            NativeCacheOrder.Enqueue(transformed);
            return expression;
        }
    }

    private static readonly HashSet<string> ReservedMathNames = new(StringComparer.OrdinalIgnoreCase) {
        "sin", "cos", "tan", "asin", "acos", "atan", "atan2",
        "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
        "sqrt", "cbrt", "exp", "exp2", "ln", "log", "log2", "log10",
        "abs", "sign", "floor", "ceil", "round", "trunc",
        "min", "max", "clamp", "pow", "mod", "fmod", "hypot",
        "lerp", "step", "smoothstep", "deg2rad", "rad2deg",
        "pi", "e", "t"
    };

    private static readonly Regex IdentRegex = new(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);

    public delegate bool TagLookupDelegate(string name, out TagCore tag);
    public static TagLookupDelegate TagLookupProvider { get; set; }

    public static CompiledPlaceholder CompilePlaceholder(ParsedTag parsed) {
        var diags = new List<CompileDiagnostic>();
        var ctx = new DiagnosticContext(parsed.Expression, parsed.Index, parsed.Length);
        var func = Compile(parsed.Expression, parsed.Format, parsed, ctx, diags);
        return new CompiledPlaceholder(func, [.. diags]);
    }

    public static Func<string> Compile(
        string rawCode,
        string format = null,
        ParsedTag parsed = default,
        DiagnosticContext ctx = default,
        List<CompileDiagnostic> diags = null
    ) {
        string trimmed = rawCode?.Trim() ?? string.Empty;

        // Legacy JSExpr migration warning
        bool isLegacyJs = !string.IsNullOrEmpty(parsed.Expression) &&
            (parsed.Expression.Equals("JSExpr", StringComparison.OrdinalIgnoreCase) ||
             parsed.Expression.StartsWith("JSExpr(", StringComparison.OrdinalIgnoreCase));
        if (isLegacyJs) {
            diags?.Add(new CompileDiagnostic(
                DiagnosticId.LegacyJsExprDeprecated,
                CompileSeverity.Warning,
                ctx,
                []
            ));
            if (!string.IsNullOrEmpty(format)) {
                trimmed = format.Trim();
                format = null;
            } else if (parsed.Expression.StartsWith("JSExpr(", StringComparison.OrdinalIgnoreCase) && parsed.Expression.EndsWith(")")) {
                trimmed = parsed.Expression[7..^1].Trim();
            }
        }

        if (string.IsNullOrEmpty(trimmed)) {
            if (diags != null) {
                diags.Add(new CompileDiagnostic(
                    DiagnosticId.AdvancedTagException,
                    CompileSeverity.Error,
                    ctx,
                    ["Missing expression"]
                ));
            }
            return () => parsed.Raw ?? string.Empty;
        }

        // 1. Tag Variable Substitution (Supports both {Func + 20} and {Func() + 20})
        var tags = new List<TagCore>();
        var nameToVar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string transformed = IdentRegex.Replace(trimmed, match => {
            string name = match.Groups["name"].Value;
            int idx = match.Index;

            if (ReservedMathNames.Contains(name) || (name.StartsWith("v") && name.Length > 1 && char.IsDigit(name[1]))) {
                return match.Value;
            }

            if (idx > 0 && trimmed[idx - 1] == '.') {
                return match.Value;
            }

            if (TagLookupProvider != null && TagLookupProvider(name, out var tag) && tag.RequiredParameterCount == 0 && IsNumeric(tag.ReturnType)) {
                if (!nameToVar.TryGetValue(name, out var varName)) {
                    varName = $"v{tags.Count}";
                    nameToVar[name] = varName;
                    tags.Add(tag);
                }
                return varName;
            }

            return match.Value;
        });

        // Strip empty parens for functions: v0() -> v0
        transformed = Regex.Replace(transformed, @"\b(?<v>v\d+)\s*\(\s*\)", "${v}");

        // 2. Native Path: OvJsExpr (exact JS number semantics), then legacy O5Expr
        if (OvJsExpr.IsSupported || O5Expr.IsSupported) {
            if (tags.Count == 0 && !HasDynamicVariable(transformed)) {
                // Constant Folding
                nint jsHandle = OvJsExpr.Compile(transformed);
                if (jsHandle != 0) {
                    double folded = OvJsExpr.Eval(jsHandle, []);
                    OvJsExpr.Free(jsHandle);
                    string foldedJs = FormatResult(folded, format);
                    return () => foldedJs;
                }
                long tempHandle = O5Expr.Compile(transformed);
                if (tempHandle != 0) {
                    if (O5Expr.TryEval(tempHandle, 0.0, out double constVal)) {
                        O5Expr.Free(tempHandle);
                        string foldedString = FormatResult(constVal, format);
                        return () => foldedString;
                    }
                    O5Expr.Free(tempHandle);
                }
            }

            var nativeExpression = GetOrCompileNative(transformed);
            if (nativeExpression != null) {
                var resultMemo = new StringMemo<long>();
                // Compile tag getters
                var getters = new Func<double>[tags.Count];
                for (int i = 0; i < tags.Count; i++) {
                    getters[i] = CreateGetter(tags[i]);
                }

                return () => {
                    if (getters.Length == 0) {
                        if (nativeExpression.Evaluate(out double val)) {
                            return FormatMemoizedResult(val, format, resultMemo);
                        }
                    } else {
                        Span<double> varBuf = stackalloc double[getters.Length];
                        for (int i = 0; i < getters.Length; i++) {
                            varBuf[i] = getters[i]();
                        }
                        if (nativeExpression.Evaluate(varBuf, out double val)) {
                            return FormatMemoizedResult(val, format, resultMemo);
                        }
                    }
                    return parsed.Raw ?? string.Empty;
                };
            }
        }

        // 3. Fallback: ClearScript V8 JavaScript Engine (via Reflection)
        string wrappedJsCode = isLegacyJs ? "{\n" + trimmed + "\n}" : WrapJsBlock(rawCode);
        var jsResultMemo = new StringMemo<long>();
        // Warn once per failure streak, not every frame (logging uses reflection and disk).
        bool warned = false;

        return () => {
            try {
                if (TryEvalV8(wrappedJsCode, out var result, out string err)) {
                    warned = false;
                    if (result is double d) return FormatMemoizedResult(d, format, jsResultMemo);
                    if (result is float f) return FormatMemoizedResult(f, format, jsResultMemo);
                    if (result is int i) return FormatMemoizedResult(i, format, jsResultMemo);
                    if (result is long l) return FormatMemoizedResult(l, format, jsResultMemo);
                    return result?.ToString() ?? string.Empty;
                }

                if (err != null && !warned) {
                    warned = true;
                    LogWarning($"[ExprCore] Runtime error, Tag '{parsed.Raw}': {err}");
                }

                return string.Empty;
            } catch {
                return parsed.Raw ?? string.Empty;
            }
        };
    }

    private static Func<double> CreateGetter(TagCore tag) {
        try {
            if (tag.MemberType == TagMemberType.Method) {
                var method = (MethodInfo)tag.Member;
                var p = method.GetParameters();
                Expression expr = p.Length == 0
                    ? Expression.Call(null, method)
                    : Expression.Call(null, method, p.Select(param => Expression.Constant(
                        param.DefaultValue ?? (param.ParameterType.IsValueType ? Activator.CreateInstance(param.ParameterType) : null),
                        param.ParameterType
                    )));
                var converted = Expression.Convert(expr, typeof(double));
                return Expression.Lambda<Func<double>>(converted).Compile();
            }

            if (tag.MemberType == TagMemberType.Property) {
                var converted = Expression.Convert(Expression.Property(null, (PropertyInfo)tag.Member), typeof(double));
                return Expression.Lambda<Func<double>>(converted).Compile();
            }

            if (tag.MemberType == TagMemberType.Field) {
                var converted = Expression.Convert(Expression.Field(null, (FieldInfo)tag.Member), typeof(double));
                return Expression.Lambda<Func<double>>(converted).Compile();
            }

            if (tag.MemberType == TagMemberType.JS) {
                return () => {
                    try {
                        var res = tag.JSFunction?.Invoke(false, []);
                        return Convert.ToDouble(res);
                    } catch {
                        return 0.0;
                    }
                };
            }

            return () => 0.0;
        } catch {
            return () => 0.0;
        }
    }

    private static bool IsNumeric(Type type) =>
        type == typeof(double) || type == typeof(float) ||
        type == typeof(int) || type == typeof(long) ||
        type == typeof(uint) || type == typeof(ulong) ||
        type == typeof(short) || type == typeof(ushort) ||
        type == typeof(byte) || type == typeof(sbyte);

    private static V8ExpressionDispatch.Evaluator v8Evaluator;

    private static bool TryEvalV8(string code, out object result, out string err) {
        result = null;
        err = null;
        try {
            var evaluate = v8Evaluator;
            if (evaluate == null) {
                evaluate = V8ExpressionDispatch.Create(typeof(ExprCore).Assembly.GetType("Overlayer.Core.MainCore"));
                if (evaluate != null) Interlocked.CompareExchange(ref v8Evaluator, evaluate, null);
            }
            if (evaluate == null) {
                err = "Engine null";
                return false;
            }

            return evaluate(code, out result);
        } catch (Exception ex) {
            err = ex.Message;
        }
        return false;
    }

    private static string WrapJsBlock(string code) {
        if (string.IsNullOrEmpty(code)) return "()";
        string trimmed = code.Trim();
        if (trimmed.EndsWith(";") || trimmed.Contains("\n") || trimmed.StartsWith("{")) {
            return $"(function(){{ {trimmed} }})()";
        }
        return $"({trimmed})";
    }

    private static bool HasDynamicVariable(string code) {
        for (int i = 0; i < code.Length; i++) {
            char c = code[i];
            if (c == 't' || c == 'T') {
                bool leftOk = i == 0 || !char.IsLetterOrDigit(code[i - 1]) && code[i - 1] != '_';
                bool rightOk = i == code.Length - 1 || !char.IsLetterOrDigit(code[i + 1]) && code[i + 1] != '_';
                if (leftOk && rightOk) {
                    return true;
                }
            }
        }
        return false;
    }

    private static string FormatResult(double val, string format) {
        if (double.IsNaN(val)) return "NaN";
        if (double.IsPositiveInfinity(val)) return "Infinity";
        if (double.IsNegativeInfinity(val)) return "-Infinity";
        if (string.IsNullOrEmpty(format)) {
            return val.ToString(CultureInfo.InvariantCulture);
        }
        return O5Fmt.FormatString(val, format);
    }

    internal static string FormatMemoizedResult(double value, string format, StringMemo<long> memo) {
        // Bit keys distinguish signed zero and do not repeatedly miss for NaN.
        long key = BitConverter.DoubleToInt64Bits(value);
        return memo.Matches(key) ? memo.Text : memo.Store(key, FormatResult(value, format));
    }

    private static void LogWarning(string msg) {
        try {
            var mainCoreType = Type.GetType("Overlayer.Core.MainCore, Overlayer");
            var logProp = mainCoreType?.GetProperty("Log", BindingFlags.Public | BindingFlags.Static);
            var log = logProp?.GetValue(null);
            var wrnMethod = log?.GetType().GetMethod("Wrn", [typeof(string)]);
            wrnMethod?.Invoke(log, [msg]);
        } catch {
        }
    }
}
