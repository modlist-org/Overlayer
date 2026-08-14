using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.TextEngine.Core;

namespace Overlayer.IO.Fx;

public sealed class FxValue<T> : ISettingsFile, ICopyable<FxValue<T>> {
    private const string FxKey = "Fx";
    private static readonly ConcurrentDictionary<Type, Func<string, object>> Converters = new();

    static FxValue() {
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
            _ => null
        };

        if (converter != null) {
            RegisterConverter(converter);
        }
    }

    public static void RegisterConverter(Func<string, T> converter) {
        Converters[typeof(T)] = s => converter(s)!;
    }

    private T staticValue;

    public T Value {
        get => Evaluate();
        set => staticValue = value;
    }

    public TextEngineCore Engine { get; set; }

    public bool UseFx { get; set; }

    public FxValue() { }

    public FxValue(T value, TextEngineCore engine = null, bool useFx = false) {
        staticValue = value;
        Engine = engine;
        UseFx = useFx;
    }

    public T Evaluate() {
        if (!UseFx || Engine == null) {
            return staticValue;
        }

        var rendered = Engine.Get();
        if (string.IsNullOrEmpty(rendered)) {
            return staticValue;
        }

        var targetType = typeof(T);

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
                staticValue = token.ToObject<T>()!;
            } catch {
                staticValue = default!;
            }
        }
    }

    public static implicit operator FxValue<T>(T value) => new FxValue<T>(value);

    public static implicit operator T(FxValue<T> fx) => fx == null ? default! : fx.Value;

    public override string ToString() {
        if (UseFx && Engine != null) {
            return Engine.Get() ?? staticValue?.ToString() ?? string.Empty;
        }

        return staticValue?.ToString() ?? string.Empty;
    }

    public static FxValue<T> Create(T value, TextEngineCore engine, bool useFx = true)
        => new(value, engine, useFx);
}