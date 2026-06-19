using Overlayer.UI;
using UnityEngine;

namespace Overlayer.Utility.Math;

public static class MathVisuals {
    public static Color GetStateColor(EvalState state) => state switch {
        EvalState.OK =>         UIColors.ObjectActiveMathOk,
        EvalState.Error =>      UIColors.ObjectActiveMathErr,
        EvalState.Same =>       UIColors.ObjectActive,
        EvalState.OverRange =>  UIColors.ObjectActiveMathWarn,
        EvalState.UnderRange => UIColors.ObjectActiveMathWarn,
        _ =>                    UIColors.ObjectActive
    };
}