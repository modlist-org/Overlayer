namespace Overlayer.Tag.Compile;

public static class FormatValidator {
    public static bool TryValidate(Type type, string format, out Exception exception) {
        exception = null;

        if (string.IsNullOrEmpty(format)) {
            return true;
        }

        try {
            if (type == typeof(DateTime)) {
                _ = DateTime.Now.ToString(format);
            } else if (type == typeof(float)) {
                _ = 1f.ToString(format);
            } else if (type == typeof(double)) {
                _ = 1d.ToString(format);
            } else if (type == typeof(decimal)) {
                _ = 1m.ToString(format);
            } else if (type == typeof(int)) {
                _ = 1.ToString(format);
            } else if (type == typeof(long)) {
                _ = 1L.ToString(format);
            } else {
                return false;
            }

            return true;
        } catch (Exception ex) {
            exception = ex;
            return false;
        }
    }

    public static bool TryValidateJs(string format, out Exception exception) {
        exception = null;

        if (string.IsNullOrEmpty(format)) {
            return true;
        }

        if (TryValidate(typeof(double), format, out var last)) {
            return true;
        }
        if (TryValidate(typeof(long), format, out last)) {
            return true;
        }
        if (TryValidate(typeof(decimal), format, out last)) {
            return true;
        }
        if (TryValidate(typeof(DateTime), format, out last)) {
            return true;
        }

        exception = last;
        return false;
    }

    public static string FormatObject(object value, string format) {
        if (value == null) {
            return string.Empty;
        }

        if (value is Microsoft.ClearScript.Undefined) {
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

    public static string StringifyObject(object value) {
        if (value == null) {
            return string.Empty;
        }

        if (value is Microsoft.ClearScript.Undefined) {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
