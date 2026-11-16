using O5Kit.Core;
using Overlayer.TagImpl;
using Overlayer.Utility;
using UnityEngine;

namespace Overlayer.UI.Easing;

// Evaluates an ease exactly like the effect tags: a preset name or an inline curve(...).
internal static class EaseMath {
    public static float Evaluate(string ease, float t) {
        t = Mathf.Clamp01(t);
        if (string.IsNullOrWhiteSpace(ease)) {
            return t;
        }
        if (Enum.TryParse(ease.Trim(), true, out O5Ease preset)) {
            return Effects.EaseValue(t, preset);
        }
        return CustomEase.TryParse(ease, out var custom) ? custom.Evaluate(t) : t;
    }
}
