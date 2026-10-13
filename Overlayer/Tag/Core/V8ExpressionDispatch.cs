using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Tag.Core;

// Resolve reflection once, not once per expression per frame. Read the property
// on every call so initialization, shutdown and engine replacement still work.
internal static class V8ExpressionDispatch {
    internal delegate bool Evaluator(string code, out object result);

    internal static Evaluator Create(Type coreType) {
        var property = coreType?.GetProperty("V8", BindingFlags.Public | BindingFlags.Static);
        var method = property?.PropertyType.GetMethod("TryEvaluateFx", [typeof(string), typeof(object).MakeByRefType()]);
        if (method == null || method.ReturnType != typeof(bool)) return null;

        var code = Expression.Parameter(typeof(string), "code");
        var result = Expression.Parameter(typeof(object).MakeByRefType(), "result");
        var engine = Expression.Variable(property.PropertyType, "engine");
        var body = Expression.Block([engine],
            Expression.Assign(result, Expression.Constant(null, typeof(object))),
            Expression.Assign(engine, Expression.Property(null, property)),
            Expression.AndAlso(
                Expression.NotEqual(engine, Expression.Constant(null, property.PropertyType)),
                Expression.Call(engine, method, code, result)));
        return Expression.Lambda<Evaluator>(body, code, result).Compile();
    }
}
