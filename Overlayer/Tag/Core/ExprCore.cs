using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Overlayer.Native;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;

namespace Overlayer.Tag.Core;

/// <summary>
/// Core execution and compilation engine for native math expressions and JavaScript fallback.
/// Features compile-time constant folding, O5Expr stack machine with tag variables, and O5Fmt zero-allocation formatting.
/// </summary>
public static class ExprCore {
    private static readonly ConcurrentDictionary<string, long> NativeHandleCache = new(StringComparer.Ordinal);

    private static long GetOrCompileNative(string transformed) {
        if (NativeHandleCache.TryGetValue(transformed, out long handle)) {
            return handle;
        }
        EvictIfFull();
        handle = O5Expr.Compile(transformed);
        if (handle != 0) {
            NativeHandleCache[transformed] = handle;
        }
        return handle;
    }

    private static void EvictIfFull() {
        if (NativeHandleCache.Count < 800) {
            return;
        }
        foreach (var kv in NativeHandleCache) {
            if (kv.Value != 0) {
                O5Expr.Free(kv.Value);
            }
        }
        NativeHandleCache.Clear();
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

        // 2. Native O5Expr Path (with Constant Folding, Dynamic Vars, & O5Fmt)
        if (O5Expr.IsSupported) {
            if (tags.Count == 0 && !HasDynamicVariable(transformed)) {
                // Constant Folding
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

            long nativeHandle = GetOrCompileNative(transformed);
            if (nativeHandle != 0) {
                // Compile tag getters
                var getters = new Func<double>[tags.Count];
                for (int i = 0; i < tags.Count; i++) {
                    getters[i] = CreateGetter(tags[i]);
                }

                return () => {
                    if (getters.Length == 0) {
                        if (O5Expr.TryEval(nativeHandle, out double val)) {
                            return FormatResult(val, format);
                        }
                    } else {
                        Span<double> varBuf = stackalloc double[getters.Length];
                        for (int i = 0; i < getters.Length; i++) {
                            varBuf[i] = getters[i]();
                        }
                        if (O5Expr.TryEval(nativeHandle, 0.0, varBuf, out double val)) {
                            return FormatResult(val, format);
                        }
                    }
                    return parsed.Raw ?? string.Empty;
                };
            }
        }

        // 3. Fallback: ClearScript V8 JavaScript Engine (via Reflection)
        string wrappedJsCode = WrapJsBlock(rawCode);

        return () => {
            try {
                if (TryEvalV8(wrappedJsCode, out var result, out string err)) {
                    if (result is double d) return FormatResult(d, format);
                    if (result is float f) return FormatResult(f, format);
                    if (result is int i) return FormatResult(i, format);
                    if (result is long l) return FormatResult(l, format);
                    return result?.ToString() ?? string.Empty;
                }

                if (err != null) {
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

    private static bool TryEvalV8(string code, out object result, out string err) {
        result = null;
        err = null;
        try {
            var mainCoreType = Type.GetType("Overlayer.Core.MainCore, Overlayer");
            var v8Prop = mainCoreType?.GetProperty("V8", BindingFlags.Public | BindingFlags.Static);
            var v8 = v8Prop?.GetValue(null);
            if (v8 == null) {
                err = "Engine null";
                return false;
            }

            var tryEvalMethod = v8.GetType().GetMethod("TryEvaluateFx", [typeof(string), typeof(object).MakeByRefType()]);
            if (tryEvalMethod != null) {
                object[] args = [code, null];
                bool ok = (bool)tryEvalMethod.Invoke(v8, args);
                result = args[1];
                return ok;
            }
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
