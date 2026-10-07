using Microsoft.ClearScript;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Tag.Compile;

public static class ExpressionBuilder {
    public static Expression Build(TagCore tag, ResolvedSignature sig, List<CompileDiagnostic> diag) {
        var parameters = tag.Parameters;

        // Args are validated by SignatureResolver, so convert them once here
        // instead of re-parsing strings and allocating an object[] every frame.
        var values = new Expression[parameters.Length];

        for(int i = 0; i < parameters.Length; i++) {
            Expression value;

            if(i < sig.Args.Length) {
                value = Expression.Constant(
                    ArgConverter.Convert(sig.Args[i], parameters[i].ParameterType),
                    typeof(object)
                );
            } else {
                object defaultValue = null;
                try {
                    defaultValue = parameters[i].DefaultValue;
                } catch { }
                if(defaultValue == null || defaultValue == DBNull.Value || defaultValue is Missing) {
                    Type paramType = parameters[i].ParameterType;
                    object fallback = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                    value = Expression.Constant(fallback, typeof(object));
                } else {
                    value = Expression.Constant(defaultValue, typeof(object));
                }
            }

            values[i] = value;
        }

        Expression call = tag.MemberType switch {
            TagMemberType.Method => Expression.Call(
                (MethodInfo)tag.Member,
                BuildCallArgs(tag.Parameters, values)
            ),

            TagMemberType.Property => Expression.Property(null, (PropertyInfo)tag.Member),

            TagMemberType.Field => Expression.Field(null, (FieldInfo)tag.Member),

            TagMemberType.JS => Expression.Call(
                Expression.Property(Expression.Constant(tag), nameof(TagCore.JSFunction)),
                typeof(ScriptObject).GetMethod(nameof(ScriptObject.Invoke), [typeof(bool), typeof(object[])])!,
                Expression.Constant(false),
                Expression.NewArrayInit(typeof(object), values)
            ),

            _ => throw new NotSupportedException($"Unsupported member type: {tag.MemberType}")
        };

        Expression result;

        if(tag.IsJS) {
            // JS values are dynamic: JsResultFormatter resolves formatting
            // against the runtime value (null/undefined-safe).
            if(sig.HasFormat) {
                result = Expression.Call(
                    typeof(JsResultFormatter),
                    nameof(JsResultFormatter.ToFormattedString),
                    Type.EmptyTypes,
                    call,
                    Expression.Constant(sig.Format)
                );
            } else {
                result = Expression.Call(
                    typeof(JsResultFormatter),
                    nameof(JsResultFormatter.ToDisplayString),
                    Type.EmptyTypes,
                    call
                );
            }
        } else if(tag.ReturnType == typeof(string)) {
            result = Expression.Coalesce(call, Expression.Constant(""));
        } else if(MemoKeyType(tag.ReturnType) is Type keyType) {
            // Numeric tags often hold their value across many frames; reuse
            // the last string instead of re-formatting (and allocating) it.
            // ponytail: memo ignores CurrentCulture changes while the value is
            // unchanged; key the memo on culture too if that ever matters.
            var value = Expression.Variable(tag.ReturnType, "value");
            var key = Expression.Variable(keyType, "key");
            var memoType = typeof(StringMemo<>).MakeGenericType(keyType);
            var memo = Expression.Constant(Activator.CreateInstance(memoType));

            result = Expression.Block(
                [value, key],
                Expression.Assign(value, call),
                Expression.Assign(key, keyType == tag.ReturnType
                    ? value
                    // Bitwise key: 0.0 == -0.0 but they can format differently.
                    : Expression.Call(
                        typeof(BitConverter),
                        nameof(BitConverter.DoubleToInt64Bits),
                        Type.EmptyTypes,
                        Expression.Convert(value, typeof(double))
                    )),
                Expression.Condition(
                    Expression.Call(memo, memoType.GetMethod(nameof(StringMemo<int>.Matches))!, key),
                    Expression.Field(memo, memoType.GetField(nameof(StringMemo<int>.Text))!),
                    Expression.Call(memo, memoType.GetMethod(nameof(StringMemo<int>.Store))!, key,
                        FormatValue(tag.ReturnType, value, sig))
                )
            );
        } else {
            result = FormatValue(tag.ReturnType, call, sig);
        }

        return result;
    }

    private static Expression FormatValue(Type type, Expression value, ResolvedSignature sig) {
        if(sig.HasFormat && typeof(IFormattable).IsAssignableFrom(type)) {
            // Call the type's own ToString(string, IFormatProvider) when it has
            // one, so value types are not boxed to IFormattable every frame.
            var direct = type.GetMethod(
                nameof(IFormattable.ToString),
                [typeof(string), typeof(IFormatProvider)]
            );

            return Expression.Call(
                direct != null ? value : Expression.Convert(value, typeof(IFormattable)),
                direct ?? typeof(IFormattable).GetMethod(
                    nameof(IFormattable.ToString),
                    [typeof(string), typeof(IFormatProvider)]
                ),
                Expression.Constant(sig.Format),
                Expression.Constant(null, typeof(IFormatProvider))
            );
        }

        var m = type.GetMethod(
            nameof(ToString),
            Type.EmptyTypes
        );

        return m != null ? Expression.Call(value, m) : Expression.Call(
            value,
            typeof(object).GetMethod(nameof(ToString))!
        );
    }

    // Primitive numerics only: their Equals matches their formatted output
    // (decimal does not: 1.0m == 1.00m). Floating point keys on bits.
    private static Type MemoKeyType(Type type) {
        if(type == typeof(double) || type == typeof(float)) {
            return typeof(long);
        }
        return type == typeof(int) || type == typeof(long) || type == typeof(uint)
            || type == typeof(ulong) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(byte) || type == typeof(sbyte)
            ? type
            : null;
    }

    private static Expression[] BuildCallArgs(
    ParameterInfo[] parameters,
    Expression[] values) {
        var list = new Expression[parameters.Length];

        for(int i = 0; i < parameters.Length; i++) {
            list[i] = Expression.Convert(
                values[i],
                parameters[i].ParameterType
            );
        }

        return list;
    }
}

public sealed class StringMemo<TKey> where TKey : struct, IEquatable<TKey> {
    private bool has;
    private TKey key;
    public string Text;

    public bool Matches(TKey value) => has && key.Equals(value);

    public string Store(TKey value, string text) {
        Text = text;
        key = value;
        has = true;
        return text;
    }
}
