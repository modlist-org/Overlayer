namespace Overlayer.Tag.Compile;

public static class FormatValidator {
    public static bool TryValidate(Type type, string format, out Exception exception) {
        exception = null;

        if(string.IsNullOrEmpty(format)) {
            return true;
        }

        try {
            if(type == typeof(DateTime)) {
                _ = DateTime.Now.ToString(format);
            } else if(type == typeof(float)) {
                _ = 1f.ToString(format);
            } else if(type == typeof(double)) {
                _ = 1d.ToString(format);
            } else if(type == typeof(decimal)) {
                _ = 1m.ToString(format);
            } else if(type == typeof(int)) {
                _ = 1.ToString(format);
            } else if(type == typeof(long)) {
                _ = 1L.ToString(format);
            } else {
                return false;
            }

            return true;
        } catch(Exception ex) {
            exception = ex;
            return false;
        }
    }
}