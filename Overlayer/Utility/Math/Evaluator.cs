using NCalc;

namespace Overlayer.Utility.Math;

public enum EvalState {
    OK,
    Error,
    Same,
    OverRange,
    UnderRange
}

public static class Evaluator<T> where T : struct, IComparable, IConvertible {
    private static readonly Dictionary<string, double> Constants = new() {
        { "PI", System.Math.PI },
        { "E", System.Math.E }
    };

    public static (T result, EvalState state) Evaluate(string exprStr, T currentVal, T? min = null, T? max = null) {
        if(string.IsNullOrWhiteSpace(exprStr)) {
            return (currentVal, EvalState.Error);
        }

        try {
            var e = new Expression(exprStr, EvaluateOptions.IgnoreCase);
            foreach(var constant in Constants) {
                e.Parameters[constant.Key] = constant.Value;
            }

            object evalResult = e.Evaluate();
            T result = (T)Convert.ChangeType(evalResult, typeof(T));

            if(min.HasValue && max.HasValue) {
                if(result.CompareTo(min.Value) < 0) {
                    return (min.Value, EvalState.UnderRange);
                }

                if(result.CompareTo(max.Value) > 0) {
                    return (max.Value, EvalState.OverRange);
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
}
