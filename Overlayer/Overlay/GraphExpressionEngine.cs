using Microsoft.ClearScript;
using Overlayer.Native;
using Overlayer.Tag.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Overlayer.Overlay;

public sealed class GraphExpressionEngine : IDisposable {
    public enum EngineKind {
        None,
        ZigNative,
        V8Script
    }

    public delegate bool TagLookupDelegate(string name, out TagCore tag);
    public delegate void TagTouchDelegate(string name);
    public delegate ScriptObject V8EvaluatorDelegate(string code);

    public static TagLookupDelegate DefaultTagLookup { get; set; }
    public static TagTouchDelegate DefaultTagTouch { get; set; }
    public static V8EvaluatorDelegate DefaultV8Evaluator { get; set; }

    private readonly TagLookupDelegate _tagLookup;
    private readonly TagTouchDelegate _tagTouch;
    private readonly V8EvaluatorDelegate _v8Evaluator;

    private static readonly HashSet<string> ReservedMathNames = new(StringComparer.OrdinalIgnoreCase) {
        "t", "math", "pi", "e", "true", "false",
        "sin", "cos", "tan", "asin", "acos", "atan", "atan2",
        "sqrt", "abs", "floor", "ceil", "ceiling", "trunc", "truncate",
        "round", "exp", "ln", "log", "log10", "max", "min", "pow", "sign", "ieeeremainder"
    };

    private static readonly Regex TagFuncRegex = new(@"\bTag\s*\(\s*[""'](?<name>[A-Za-z0-9_]+)[""']\s*\)", RegexOptions.Compiled);
    private static readonly Regex BracketTagRegex = new(@"\{(?<name>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex IdentRegex = new(@"\b(?<name>[A-Za-z0-9_]+)\b", RegexOptions.Compiled);

    public EngineKind Kind { get; private set; } = EngineKind.None;
    public string CurrentCode { get; private set; }
    public IReadOnlyList<TagCore> BoundTags => _tagList;

    private long _nativeHandle;
    private bool _nativeJs;
    private readonly List<TagCore> _tagList = [];
    private Func<double>[] _tagGetters = [];
    private ScriptObject _v8Func;

    public GraphExpressionEngine(
        TagLookupDelegate tagLookup = null,
        TagTouchDelegate tagTouch = null,
        V8EvaluatorDelegate v8Evaluator = null) {
        _tagLookup = tagLookup ?? DefaultTagLookup;
        _tagTouch = tagTouch ?? DefaultTagTouch;
        _v8Evaluator = v8Evaluator ?? DefaultV8Evaluator;
    }

    public void Compile(string code) {
        if (code == CurrentCode && Kind != EngineKind.None) {
            return;
        }
        Dispose();
        CurrentCode = code;
        if (string.IsNullOrWhiteSpace(code)) {
            return;
        }

        // 1. C# 태그 분석 및 Zig 네이티브 컴파일 시도
        if (TryCompileNativeWithTags(code)) {
            Kind = EngineKind.ZigNative;
            return;
        }

        // 2. JS 태그가 있거나 Zig가 지원하지 않는 구문일 때 -> V8 ClearScript로 폴백
        if (TryCompileV8(code)) {
            Kind = EngineKind.V8Script;
            return;
        }

        Kind = EngineKind.None;
    }

    private bool TryCompileNativeWithTags(string code) {
        if (!O5Expr.IsSupported && !OvJsExpr.IsSupported) {
            return false;
        }

        var tags = new List<TagCore>();
        var nameToVar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool hasJsTag = false;

        // A. Tag("Name") 형태 탐색 및 치환
        string transformed = TagFuncRegex.Replace(code, match => {
            string name = match.Groups["name"].Value;
            if (TryFindTag(name, out var tag)) {
                if (tag.IsJS) {
                    hasJsTag = true;
                } else {
                    return GetOrAssignVar(name, tag, tags, nameToVar);
                }
            }
            return match.Value;
        });

        if (hasJsTag) {
            return false;
        }

        // B. {Name} 형태 탐색 및 치환
        transformed = BracketTagRegex.Replace(transformed, match => {
            string name = match.Groups["name"].Value;
            if (TryFindTag(name, out var tag)) {
                if (tag.IsJS) {
                    hasJsTag = true;
                } else {
                    return GetOrAssignVar(name, tag, tags, nameToVar);
                }
            }
            return match.Value;
        });

        if (hasJsTag) {
            return false;
        }

        // C. 식별자 또는 Name() 형태 탐색 및 치환
        transformed = IdentRegex.Replace(transformed, match => {
            string name = match.Groups["name"].Value;
            int idx = match.Index;

            if (ReservedMathNames.Contains(name) || IsVVar(name)) {
                return match.Value;
            }

            // 앞 문자가 '.'이면 (예: Math.sin) 속성/메서드 접근이므로 스킵
            if (idx > 0 && transformed[idx - 1] == '.') {
                return match.Value;
            }

            if (TryFindTag(name, out var tag)) {
                if (tag.IsJS) {
                    hasJsTag = true;
                    return match.Value;
                }
                return GetOrAssignVar(name, tag, tags, nameToVar);
            }

            return match.Value;
        });

        if (hasJsTag) {
            return false;
        }

        // Name() -> v0() 로 치환된 경우 괄호 제거 (v0() -> v0)
        transformed = Regex.Replace(transformed, @"\b(?<v>v\d+)\s*\(\s*\)", "${v}");

        // Zig 바이트코드 컴파일: JS 의미론 엔진 우선, 레거시 O5 폴백
        long handle = OvJsExpr.Compile(transformed, allowT: true);
        bool js = handle != 0;
        if (!js) {
            handle = O5Expr.Compile(transformed);
        }
        if (handle == 0) {
            return false;
        }

        // 컴파일 성공: C# 태그 고속 게터 생성
        var getters = new Func<double>[tags.Count];
        for (int i = 0; i < tags.Count; i++) {
            var tag = tags[i];
            try {
                _tagTouch?.Invoke(tag.Name);
            } catch {
            }
            getters[i] = CreateFastGetter(tag);
        }

        FreeNative();
        _nativeHandle = handle;
        _nativeJs = js;
        _tagList.Clear();
        _tagList.AddRange(tags);
        _tagGetters = getters;
        return true;
    }

    private bool TryFindTag(string name, out TagCore tag) {
        if (_tagLookup != null) {
            return _tagLookup(name, out tag);
        }
        tag = null;
        return false;
    }

    private static string GetOrAssignVar(string name, TagCore tag, List<TagCore> list, Dictionary<string, string> map) {
        if (map.TryGetValue(name, out string v)) {
            return v;
        }
        if (list.Count >= 32) {
            return name;
        }
        string varName = $"v{list.Count}";
        list.Add(tag);
        map[name] = varName;
        return varName;
    }

    private static bool IsVVar(string s) {
        return (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            && s.Length > 1 && s.Length <= 4
            && int.TryParse(s[1..], out int n) && n >= 0 && n < 32;
    }

    private static Func<double> CreateFastGetter(TagCore tag) {
        if (tag == null) {
            return () => 0.0;
        }
        if (tag.Member != null) {
            try {
                Expression call;
                switch (tag.MemberType) {
                    case TagMemberType.Method:
                        var method = (MethodInfo)tag.Member;
                        var pExprs = new Expression[tag.Parameters.Length];
                        for (int i = 0; i < tag.Parameters.Length; i++) {
                            var p = tag.Parameters[i];
                            object def = p.HasDefaultValue ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
                            pExprs[i] = Expression.Constant(def, p.ParameterType);
                        }
                        call = Expression.Call(null, method, pExprs);
                        break;
                    case TagMemberType.Property:
                        call = Expression.Property(null, (PropertyInfo)tag.Member);
                        break;
                    case TagMemberType.Field:
                        call = Expression.Field(null, (FieldInfo)tag.Member);
                        break;
                    default:
                        call = null;
                        break;
                }
                if (call != null) {
                    Expression cast = Expression.Convert(call, typeof(double));
                    return Expression.Lambda<Func<double>>(cast).Compile();
                }
            } catch {
            }
        }

        // Fallback
        return () => {
            try {
                object obj = tag.Invoke();
                return obj != null ? Convert.ToDouble(obj) : 0.0;
            } catch {
                return 0.0;
            }
        };
    }

    private bool TryCompileV8(string code) {
        if (_v8Evaluator == null) {
            return false;
        }
        try {
            _v8Func = _v8Evaluator($"(function(t){{ return ({code}); }})");
            return _v8Func != null;
        } catch {
            _v8Func = null;
            return false;
        }
    }

    public bool Evaluate(float now, out float value) {
        value = 0f;
        if (Kind == EngineKind.ZigNative) {
            int count = _tagGetters.Length;
            if (_nativeJs) {
                Span<double> args = stackalloc double[count];
                for (int i = 0; i < count; i++) {
                    args[i] = _tagGetters[i]();
                }
                double result = OvJsExpr.Eval((nint)_nativeHandle, args, now);
                if (double.IsNaN(result) || double.IsInfinity(result)) {
                    return false;
                }
                value = (float)result;
                return true;
            }
            if (count == 0) {
                if (O5Expr.TryEval(_nativeHandle, now, out double res) && !double.IsNaN(res) && !double.IsInfinity(res)) {
                    value = (float)res;
                    return true;
                }
                return false;
            }

            Span<double> vars = stackalloc double[count];
            for (int i = 0; i < count; i++) {
                vars[i] = _tagGetters[i]();
            }

            if (O5Expr.TryEval(_nativeHandle, now, vars, out double val) && !double.IsNaN(val) && !double.IsInfinity(val)) {
                value = (float)val;
                return true;
            }
            return false;
        }

        if (Kind == EngineKind.V8Script && _v8Func != null) {
            try {
                object raw = _v8Func.Invoke(false, now);
                if (raw != null && raw != Undefined.Value && TryToFloat(raw, out value)) {
                    return true;
                }
            } catch {
                try {
                    (_v8Func as IDisposable)?.Dispose();
                } catch { }
                _v8Func = null;
                Kind = EngineKind.None;
            }
            return false;
        }

        return false;
    }

    private static bool TryToFloat(object raw, out float value) {
        try {
            value = Convert.ToSingle(raw);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        } catch {
            value = 0f;
            return false;
        }
    }

    private void FreeNative() {
        if (_nativeHandle == 0) {
            return;
        }
        if (_nativeJs) {
            OvJsExpr.Free((nint)_nativeHandle);
        } else {
            O5Expr.Free(_nativeHandle);
        }
        _nativeHandle = 0;
    }

    public void Dispose() {
        FreeNative();
        _tagList.Clear();
        _tagGetters = [];
        if (_v8Func != null) {
            try {
                (_v8Func as IDisposable)?.Dispose();
            } catch { }
            _v8Func = null;
        }
        Kind = EngineKind.None;
    }
}
