using UnityEngine;

namespace Overlayer.UI.Utility;

internal static class ColorUtils {
    public static string ToHtmlStringRGB(Color color) {
        Color32 c = color;

        return $"{c.r:X2}{c.g:X2}{c.b:X2}";
    }

    public static string ToHtmlStringRGBA(Color color) {
        Color32 c = color;

        return $"{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
    }
}