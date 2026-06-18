using Overlayer.UI;
using UnityEngine;

namespace Overlayer.Utility.Math;

public static class MathVisuals {
    public static Color GetStateColor(EvalState state) => state switch {
        EvalState.OK                               => UIColors.ObjectActiveMathOk,
        EvalState.Same                             => UIColors.ObjectActive,
        EvalState.OverRange | EvalState.UnderRange => UIColors.ObjectActiveMathWarn,
        EvalState.Error                            => UIColors.ObjectActiveMathErr,
        _                                          => UIColors.ObjectActive
    };
}