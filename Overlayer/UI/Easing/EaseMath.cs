using Overlayer.TagImpl;

namespace Overlayer.UI.Easing;

// Evaluates an ease exactly like the effect tags: a preset name, saved curve name, or inline curve(...).
internal static class EaseMath {
    public static float Evaluate(string ease, float t) => Effects.EvaluateEase(ease, t);
}
