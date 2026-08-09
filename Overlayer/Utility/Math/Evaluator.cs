using NCalc;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Overlayer.Utility.Math;

public enum EvalState {
    Ok,
    Error,
    Same,
    OverRange,
    UnderRange
}

public static class EvaluatorConstants {
    public static readonly Dictionary<string, double> Constants = new() {
        { "PI", System.Math.PI },
        { "E", System.Math.E }
    };

    public const NumberStyles NumStyle = NumberStyles.Float | NumberStyles.AllowThousands;
    public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
}

public static class Evaluator<T> where T : struct, IComparable<T>, IConvertible {

    public static (T result, EvalState state) Evaluate(string exprStr, T currentVal, T? min = null, T? max = null) {
        if(string.IsNullOrWhiteSpace(exprStr)) {
            return (currentVal, EvalState.Error);
        }

        if(double.TryParse(exprStr, EvaluatorConstants.NumStyle, EvaluatorConstants.Culture, out double parsedDirect)) {
            T directResult = (T)Convert.ChangeType(parsedDirect, typeof(T), EvaluatorConstants.Culture);
            return ValidateAndReturn(directResult, currentVal, min, max);
        }

        try {
            var e = new Expression(exprStr, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);

            foreach(var constant in EvaluatorConstants.Constants) {
                e.Parameters[constant.Key] = constant.Value;
            }

            object evalResult = e.Evaluate();
            if(evalResult == null) {
                return (currentVal, EvalState.Error);
            }

            T result = (T)((IConvertible)evalResult).ToType(typeof(T), EvaluatorConstants.Culture);
            return ValidateAndReturn(result, currentVal, min, max);
        } catch {
            return (currentVal, EvalState.Error);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (T result, EvalState state) ValidateAndReturn(T result, T currentVal, T? min, T? max) {
        if(min.HasValue && result.CompareTo(min.Value) < 0) {
            return (min.Value, EvalState.UnderRange);
        }

        if(max.HasValue && result.CompareTo(max.Value) > 0) {
            return (max.Value, EvalState.OverRange);
        }

        if(EqualityComparer<T>.Default.Equals(result, currentVal)) {
            return (result, EvalState.Same);
        }

        return (result, EvalState.Ok);
    }
}