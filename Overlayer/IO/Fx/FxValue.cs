using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using NCalc;
using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.TextEngine.Core;

namespace Overlayer.IO.Fx;

public interface IFxValue {
    Type ValueType { get; }

    bool UseFx { get; set; }

    string Expression { get; set; }

    bool HasFx { get; }

    object EvaluateObject();

    IFxValue CopyFx();

    JToken SerializeFx();

    void DeserializeFx(JToken token);
}

public abstract class FxValue {
    private protected const string FxKey = "Fx";

    protected static readonly ConcurrentDictionary<Type, Func<string, object>> Converters = new();

    protected static readonly ConcurrentDictionary<Type, Func<JToken, object>> RawReaders = new();

    protected static readonly ConcurrentDictionary<Type, Func<object, JToken>> RawWriters = new();

    public static void ClearConverters() {
        Converters.Clear();
        RawReaders.Clear();
        RawWriters.Clear();
    }

    internal static string WrapJsBlock(string code) => "{\n" + (code ?? string.Empty) + "\n}";

    internal static int JsBlockPrefixLength => 2;

    internal static string SanitizeJsExpression(string code) {
        if (string.IsNullOrEmpty(code)) {
            return code;
        }

        bool dirty = false;
        foreach (char c in code) {
            if (c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\uFEFF') {
                dirty = true;
                break;
            }
        }

        if (!dirty) {
            return code;
        }

        var sb = new StringBuilder(code.Length);
        foreach (char c in code) {
            if (c != '\u200B' && c != '\u200C' && c != '\u200D' && c != '\uFEFF') {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    internal static bool TryEvaluateJs(string code, out object result) {
        result = null;
        try {
            var v8 = MainCore.V8;
            if (v8 == null) {
                return false;
            }

            return v8.TryEvaluateFx(SanitizeJsExpression(code), out result) && result != null;
        } catch {
            result = null;
            return false;
        }
    }

        internal static bool TryConvertScalar(Type targetType, string rendered) {
        try {
            var expression = new Expression(rendered, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
            expression.Parameters["PI"] = Math.PI;
            expression.Parameters["E"] = Math.E;
            var value = expression.Evaluate();
            if (value == null) {
                return false;
            }

            Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        } catch {
            return false;
        }
    }

    internal static bool TryConvertRegistered(Type targetType, string rendered) {
        try {
            if (!Converters.TryGetValue(targetType, out var converter)) {
                return false;
            }

            converter(rendered);
            return true;
        } catch {
            return false;
        }
    }

    internal static bool TryConvertEnum(Type enumType, string rendered) {
        var text = rendered?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return false;
        }

        if (int.TryParse(text, out _)) {
            return true;
        }

        return Enum.TryParse(enumType, text, true, out _);
    }

    internal static float EvaluateNumericComponent(string expr) {
        var text = SanitizeJsExpression(expr)?.Trim() ?? string.Empty;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct)) {
            return direct;
        }

        var expression = new Expression(text, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
        expression.Parameters["PI"] = Math.PI;
        expression.Parameters["E"] = Math.E;
        var value = expression.Evaluate();
        if (value == null) {
            throw new FormatException($"Cannot evaluate '{expr}'.");
        }

        return Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }
}

public sealed class FxValue<T> : FxValue, IFxValue, ISettingsFile, ICopyable<FxValue<T>>, IDisposable {
    static FxValue() {
        if (typeof(T).IsEnum) {
            RegisterConverter(raw => {
                if (string.IsNullOrWhiteSpace(raw)) return default!;
                if (int.TryParse(raw, out var intVal)) return (T)Enum.ToObject(typeof(T), intVal);
                return Enum.TryParse(typeof(T), raw, true, out var parsed) ? (T)parsed : default!;
            });
            return;
        }

        Func<string, T> converter = Type.GetTypeCode(typeof(T)) switch {
            TypeCode.Byte    => s => byte.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.SByte   => s => sbyte.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Int16   => s => short.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.UInt16  => s => ushort.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Int32   => s => int.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.UInt32  => s => uint.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Int64   => s => long.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.UInt64  => s => ulong.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Single  => s => float.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Double  => s => double.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Decimal => s => decimal.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.Boolean => s => bool.TryParse(s, out var v) ? (T)(object)v : default!,
            TypeCode.String  => s => (T)(object)s,
            _ => null!
        };

        if (converter != null) {
            RegisterConverter(converter);
        }
    }

    public static void RegisterConverter(Func<string, T> converter) {
        Converters[typeof(T)] = s => converter(s)!;
    }

    public static void RegisterRawReader(Func<JToken, T> reader) {
        RawReaders[typeof(T)] = t => reader(t)!;
    }

    public static void RegisterRawWriter(Func<T, JToken> writer) {
        RawWriters[typeof(T)] = o => writer((T)o);
    }

    private T staticValue = default!;

    public T Value {
        get => Evaluate();
        set => staticValue = value;
    }

    public T StaticValue {
        get => staticValue;
        set => staticValue = value;
    }

    public TextEngineCore Engine { get; set; }

    public bool UseFx { get; set; }

    public bool HasFx => UseFx && Engine != null;

    public string Expression {
        get => Engine?.Text ?? string.Empty;
        set {
            Engine ??= new TextEngineCore();
            Engine.Text = value ?? string.Empty;
        }
    }

    #region IFxValue
    Type IFxValue.ValueType => typeof(T);

    string IFxValue.Expression {
        get => Expression;
        set => Expression = value;
    }

    object IFxValue.EvaluateObject() => Evaluate()!;

    IFxValue IFxValue.CopyFx() => Copy();

    JToken IFxValue.SerializeFx() => Serialize();

    void IFxValue.DeserializeFx(JToken token) => Deserialize(token);
    #endregion

    #region Mode helpers
    public void SetStatic(T value) {
        staticValue = value;
        UseFx = false;
    }

    public void SetExpression(string expression) {
        Expression = expression ?? string.Empty;
        UseFx = true;
    }

    public void DisableFx() => UseFx = false;

    public void EnsureEngine() => Engine ??= new TextEngineCore();
    #endregion

    #region Constructors
    public FxValue() { }

    public FxValue(T value, TextEngineCore engine = null, bool useFx = false) {
        staticValue = value;
        Engine = engine;
        UseFx = useFx;
    }

    public FxValue(string expression) {
        UseFx = true;
        Engine = new TextEngineCore { Text = expression };
    }

    public FxValue(string expression, TextEngineCore engine, bool useFx = true) {
        Engine = engine ?? new TextEngineCore();
        Engine.Text = expression;
        UseFx = useFx;
    }
    #endregion

    public T Evaluate() {
        if (!UseFx || Engine == null) {
            return staticValue;
        }

        var rendered = Engine.Text;
        if (string.IsNullOrEmpty(rendered)) {
            return staticValue;
        }

        if (typeof(T) != typeof(string)) {
            rendered = SanitizeJsExpression(rendered);
            if (string.IsNullOrEmpty(rendered)) {
                return staticValue;
            }
        }

        var targetType = typeof(T);
        var typeCode = Type.GetTypeCode(targetType);
        if(targetType == typeof(string)) {
            try {
                if (TryEvaluateJs(WrapJsBlock(rendered), out var jsResult) && jsResult != null) {
                    var text = Convert.ToString(jsResult, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(text)) {  
                        return (T)(object)text;
                    }
                }
            } catch {
            }
            return (T)(object)(Engine.Get() ?? rendered);
        }
        if(!targetType.IsEnum && (typeCode == TypeCode.Boolean || (typeCode >= TypeCode.SByte && typeCode <= TypeCode.Decimal))) {
            if (TryEvaluateJs(WrapJsBlock(rendered), out var jsResult)) {
                try {
                    return (T)Convert.ChangeType(jsResult, targetType, CultureInfo.InvariantCulture);
                } catch {
                }
            }
            try {
                var expression = new Expression(rendered, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
                expression.Parameters["PI"] = Math.PI;
                expression.Parameters["E"] = Math.E;
                var value = expression.Evaluate();
                if(value == null) return staticValue;
                return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            } catch {
                return staticValue;
            }
        }

        if (!Converters.TryGetValue(targetType, out var converter))
            throw new InvalidOperationException(
                $"Type '{targetType.FullName}' requires a custom converter to be registered via FxValue<{targetType.Name}>.RegisterConverter().");
        try {
            var result = converter(rendered);
            return result is T typedResult ? typedResult : staticValue;
        } catch {
            return staticValue;
        }
    }

    public FxValue<T> Copy() {
        TextEngineCore newEngine = null;
        if (Engine != null) {
            newEngine = new TextEngineCore { Text = Engine.Text };
        }

        return new FxValue<T>(staticValue, newEngine, UseFx);
    }

    public JToken Serialize() {
        if (!UseFx) {
            if(typeof(T).IsEnum) return new JValue(staticValue.ToString());
            try {
                if (RawWriters.TryGetValue(typeof(T), out var writer)) {
                    return writer(staticValue);
                }
                if (staticValue is ISettingsFile file) {
                    return file.Serialize();
                }
            } catch {
                return JValue.CreateNull();
            }
            return staticValue != null ? JToken.FromObject(staticValue) : JValue.CreateNull();
        }

        return new JObject {
            [FxKey] = Engine?.Text ?? string.Empty
        };
    }

    public void Deserialize(JToken token) {
        if (token == null || token.Type == JTokenType.Null) {
            UseFx = false;
            staticValue = default!;
            return;
        }

        if (token is JObject obj && obj.ContainsKey(FxKey)) {
            UseFx = true;
            
            var engineText = obj[FxKey]?.Value<string>() ?? string.Empty;
            Engine ??= new TextEngineCore();
            Engine.Text = engineText;
        } 
        else {
            UseFx = false;
            try {
                if (RawReaders.TryGetValue(typeof(T), out var raw)) {
                    var r = raw(token);
                    if (r is T typed) {
                        staticValue = typed;
                        return;
                    }
                }
                staticValue = token.ToObject<T>()!;
            } catch {
                staticValue = default!;
            }
        }
    }

    public void Dispose() {
        Engine?.Dispose();
        Engine = null;
    }

    #region Operators
    public static implicit operator FxValue<T>(T value) => new(value);

    public static implicit operator T(FxValue<T> fx) => fx == null ? default! : fx.Value;
    #endregion

    public override string ToString() {
        if (UseFx && Engine != null) {
            return Engine.Get() ?? staticValue?.ToString() ?? string.Empty;
        }

        return staticValue?.ToString() ?? string.Empty;
    }

    #region Factory Methods
    public static FxValue<T> FromValue(T value) => new(value);

    public static FxValue<T> FromExpression(string expression) => new(expression);

    public static FxValue<T> Create(T value, TextEngineCore engine, bool useFx = true)
        => new(value, engine, useFx);

    public static FxValue<T> Create(string expression, TextEngineCore engine, bool useFx = true)
        => new(expression, engine, useFx);
    #endregion
}

public static class FxUtil {
    public static bool ApplyIfChanged<T>(ref T cache, T current, Action<T> apply) {
        if (EqualityComparer<T>.Default.Equals(cache, current)) {
            return false;
        }

        cache = current;
        apply(current);
        return true;
    }

    public static bool HasFx(IFxValue fx) => fx != null && fx.HasFx;

    public static bool HasAnyFx(IEnumerable<IFxValue> values) {
        if (values == null) {
            return false;
        }

        foreach (var v in values) {
            if (v != null && v.HasFx) {
                return true;
            }
        }

        return false;
    }
}
