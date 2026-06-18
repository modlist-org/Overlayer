using NCalc;

namespace Overlayer.Utility.Math;

public enum EvalState {
    OK,
    Error,
    Same,
    OutRange
}

public static class Evaluator<T> where T : struct, IComparable, IConvertible {
    private static readonly Dictionary<string, double> Constants = new() {
        { "PI", System.Math.PI }, { "pi", System.Math.PI },
        { "E", System.Math.E },   { "e", System.Math.E }
    };

    public static (T result, EvalState state) Evaluate(string exprStr, T currentVal, T? min = null, T? max = null) {
        if(string.IsNullOrWhiteSpace(exprStr)) {
            return (currentVal, EvalState.Error);
        }

        try {
            var e = new Expression(exprStr);
            foreach(var constant in Constants) {
                e.Parameters[constant.Key] = constant.Value;
            }

            object evalResult = e.Evaluate();
            T result = (T)Convert.ChangeType(evalResult, typeof(T));

            if(min.HasValue && max.HasValue) {
                if(result.CompareTo(min.Value) < 0 || result.CompareTo(max.Value) > 0) {
                    return (Clamp(result, min.Value, max.Value), EvalState.OutRange);
                }
            }

            if(result.CompareTo(currentVal) == 0) {
                return (result, EvalState.Same);
            }

            return (result, EvalState.OK);
        } catch {
            return (currentVal, EvalState.Error);
        }
    }

    private static T Clamp(T val, T min, T max) {
        if(val.CompareTo(min) < 0) {
            return min;
        }

        return val.CompareTo(max) > 0 ? max : val;
    }
}
