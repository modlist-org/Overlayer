using Microsoft.ClearScript;

namespace Overlayer.Tag.Compile;

/// <summary>
/// Runtime formatting for JS-registered tags (<see cref="Core.TagMemberType.JS"/>).
/// The declared <c>ReturnType</c> option is only used for compile-time
/// <see cref="FormatValidator"/> checks; the actual JS value is dynamic,
/// so formatting is resolved against the runtime value here.
/// </summary>
public static class JsResultFormatter {
    public static string ToDisplayString(object value) {
        if(value == null || value == Undefined.Value) {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    public static string ToFormattedString(object value, string format) {
        if(value == null || value == Undefined.Value) {
            return string.Empty;
        }

        if(string.IsNullOrEmpty(format)) {
            return value.ToString() ?? string.Empty;
        }

        if(value is IFormattable formattable) {
            try {
                return formattable.ToString(format, null) ?? string.Empty;
            } catch {
                return value.ToString() ?? string.Empty;
            }
        }

        return value.ToString() ?? string.Empty;
    }
}
