using GTweens.Easings;
using Overlayer.Tag.Core;
using UnityEngine;

namespace Overlayer.TagImpl;

public static class Effects {
    private sealed class AnimationState {
        internal double Previous;
        internal double Target;
        internal double StartedAt;
    }

    private static readonly Dictionary<string, AnimationState> easedValues = [];
    private static readonly Dictionary<string, AnimationState> movingValues = [];

    [Tag(TagType = TagType.ProcessFormat)]
    public static double EasedValue(string tagName, int digits = -1, double speed = 500,
        Easing ease = Easing.Linear) {
        if(!TryReadNumber(tagName, out double value)) {
            return 0;
        }

        AnimationState state = GetState(easedValues, tagName, value);
        double now = UnityEngine.Time.realtimeSinceStartup * 1000d;
        double current = Interpolate(state, now, speed, ease);
        if(value != state.Target) {
            state.Previous = current;
            state.Target = value;
            state.StartedAt = now;
            current = state.Previous;
        }

        return digits < 0 ? current : Math.Round(current, digits);
    }

    [Tag]
    public static string ColorRange(string tagName, double minimum, double maximum, string minimumHex,
        string maximumHex, Easing ease = Easing.Linear, int maxLength = -1) {
        if(!TryColorRangeProgress(tagName, minimum, maximum, ease, out float progress)
            || !TryColor(minimumHex, out Color from, out bool fromAlpha)
            || !TryColor(maximumHex, out Color to, out bool toAlpha)) {
            return string.Empty;
        }

        Color color = Color.LerpUnclamped(from, to, progress);
        string result = fromAlpha || toAlpha
            ? ColorUtility.ToHtmlStringRGBA(color)
            : ColorUtility.ToHtmlStringRGB(color);
        return maxLength < 0 || result.Length <= maxLength ? result : result[..maxLength];
    }

    internal static bool TryColorRangeProgress(string tagName, double minimum, double maximum, Easing ease, out float progress) {
        progress = 0f;
        if(!TryReadNumber(tagName, out double value)) {
            return false;
        }

        if(maximum <= minimum) {
            return true;
        }

        progress = EaseValue(Mathf.Clamp01((float)((value - minimum) / (maximum - minimum))), ease);
        return true;
    }

    [Tag]
    public static double MovingMan(string tagName, double startSize, double endSize, double defaultSize,
        double speed, bool invert = false, Easing ease = Easing.OutExpo) {
        if(!TryReadNumber(tagName, out double value)) {
            return defaultSize;
        }

        AnimationState state = GetState(movingValues, tagName, value);
        double now = UnityEngine.Time.realtimeSinceStartup * 1000d;
        if(value != state.Target) {
            state.Target = value;
            state.StartedAt = now;
        }

        if(speed <= 0 || now - state.StartedAt >= speed) {
            return defaultSize;
        }

        float progress = Mathf.Clamp01((float)((now - state.StartedAt) / speed));
        float eased = EaseValue(invert ? 1f - progress : progress, ease);
        return startSize + ((endSize - startSize) * eased);
    }

    private static AnimationState GetState(Dictionary<string, AnimationState> states, string key, double value) {
        if(!states.TryGetValue(key, out AnimationState state)) {
            state = new AnimationState {
                Previous = value,
                Target = value,
                StartedAt = UnityEngine.Time.realtimeSinceStartup * 1000d
            };
            states[key] = state;
        }

        return state;
    }

    private static double Interpolate(AnimationState state, double now, double speed, Easing ease) {
        if(speed <= 0) {
            return state.Target;
        }

        float progress = Mathf.Clamp01((float)((now - state.StartedAt) / speed));
        return state.Previous + ((state.Target - state.Previous) * EaseValue(progress, ease));
    }

    private static float EaseValue(float progress, Easing ease)
        => PresetEasingDelegateFactory.GetEaseDelegate(ease)(0f, 1f, progress);

    private static bool TryReadNumber(string tagName, out double value) {
        value = 0;
        if(!TagManager.TryGet(tagName, out TagCore tag) || tag.RequiredParameterCount != 0) {
            return false;
        }

        try {
            object[] args = new object[tag.Parameters.Length];
            for(int i = 0; i < args.Length; i++) {
                args[i] = tag.Parameters[i].DefaultValue;
            }

            object result = tag.Invoke(args);
            return result != null && double.TryParse(result.ToString(), out value);
        } catch {
            return false;
        }
    }

    private static bool TryColor(string hex, out Color color, out bool hasAlpha) {
        color = default;
        hex = hex?.TrimStart('#');
        hasAlpha = hex?.Length is 4 or 8;
        return hex?.Length is 3 or 4 or 6 or 8
            && ColorUtility.TryParseHtmlString("#" + hex, out color);
    }
}
