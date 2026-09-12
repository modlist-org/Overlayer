namespace Overlayer.UI.Utility;

internal static class ColorUtils {
    public static string ToHtmlStringRGB(UnityEngine.Color color)
        => IO.Utility.ColorUtils.ToHtmlStringRGB(color);

    public static string ToHtmlStringRGBA(UnityEngine.Color color)
        => IO.Utility.ColorUtils.ToHtmlStringRGBA(color);
}