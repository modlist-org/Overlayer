using Microsoft.ClearScript;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Tag.Compile;

public static class ExpressionBuilder {
    public static Expression Build(TagCore tag, ResolvedSignature sig, List<CompileDiagnostic> diag) {
        var parameters = tag.Parameters;

        var argsConst = Expression.Constant(sig.Args);
        var converted = Expression.Variable(typeof(object[]), "converted");

        var body = new List<Expression> {
            Expression.Assign(
                converted,
                Expression.NewArrayBounds(typeof(object), Expression.Constant(parameters.Length))
            )
        };

        for(int i = 0; i < parameters.Length; i++) {
            Expression value;

            if(i < sig.Args.Length) {
                value = Expression.Call(
                    typeof(ArgConverter),
                    nameof(ArgConverter.Convert),
                    Type.EmptyTypes,
                    Expression.ArrayIndex(argsConst, Expression.Constant(i)),
                    Expression.Constant(parameters[i].ParameterType)
                );

                value = Expression.Convert(value, typeof(object));
            } else {
                object defaultValue = DBNull.Value;
                try {
                    defaultValue = parameters[i].DefaultValue;
                } catch { }
                value = Expression.Constant(defaultValue, typeof(object));
            }

            body.Add(
                Expression.Assign(
                    Expression.ArrayAccess(converted, Expression.Constant(i)),
                    value
                )
            );
        }

        Expression call = tag.MemberType switch {
            TagMemberType.Method => Expression.Call(
                (MethodInfo)tag.Member,
                BuildCallArgs(tag.Parameters, converted)
            ),

            TagMemberType.Property => Expression.Property(null, (PropertyInfo)tag.Member),

            TagMemberType.Field => Expression.Field(null, (FieldInfo)tag.Member),

            TagMemberType.JS => Expression.Call(
                Expression.Property(Expression.Constant(tag), nameof(TagCore.JSFunction)),
                typeof(ScriptObject).GetMethod(nameof(ScriptObject.Invoke), [typeof(bool), typeof(object[])])!,
                Expression.Constant(false),
                converted
            ),

            _ => throw new NotSupportedException($"Unsupported member type: {tag.MemberType}")
        };

        Expression result;

        if(tag.ReturnType == typeof(string)) {
            result = Expression.Coalesce(call, Expression.Constant(""));
        } else if(sig.HasFormat) {
            var formattable = Expression.Convert(call, typeof(IFormattable));

            var method = typeof(IFormattable).GetMethod(
                nameof(IFormattable.ToString),
                [typeof(string), typeof(IFormatProvider)]
            );

            result = Expression.Call(
                formattable,
                method,
                Expression.Constant(sig.Format),
                Expression.Constant(null, typeof(IFormatProvider))
            );
        } else {
            var m = tag.ReturnType.GetMethod(
                nameof(ToString),
                Type.EmptyTypes
            );

            result = m != null ? Expression.Call(call, m) : Expression.Call(
                call,
                typeof(object).GetMethod(nameof(ToString))!
            );
        }

        body.Add(result);

        return Expression.Block([converted], body);
    }

    private static Expression[] BuildCallArgs(
    ParameterInfo[] parameters,
    Expression converted) {
        var list = new Expression[parameters.Length];

        for(int i = 0; i < parameters.Length; i++) {
            var index = Expression.Constant(i);
            var access = Expression.ArrayIndex(converted, index);

            list[i] = Expression.Convert(
                access,
                parameters[i].ParameterType
            );
        }

        return list;
    }
}