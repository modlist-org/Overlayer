using Microsoft.ClearScript;

namespace Overlayer.Tag.Compile;

public static class JsResultFormatter {
    public static string ToDisplayString(object value) {
        if (value == null || value == Undefined.Value) {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    public static string ToFormattedString(object value, string format) {
        if (value == null || value == Undefined.Value) {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(format)) {
            return value.ToString() ?? string.Empty;
        }

        if (value is IFormattable formattable) {
            try {
                return formattable.ToString(format, null) ?? string.Empty;
            } catch {
                return value.ToString() ?? string.Empty;
            }
        }

        return value.ToString() ?? string.Empty;
    }
}
