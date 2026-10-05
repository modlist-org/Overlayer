namespace Overlayer.Tag.Compile;

public static class ArgConverter {
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    public static object Convert(string value, Type type) {
        if(type == typeof(string)) {
            return value;
        }
        if(type == typeof(int)) {
            return int.Parse(value, Invariant);
        }
        if(type == typeof(long)) {
            return long.Parse(value, Invariant);
        }
        if(type == typeof(float)) {
            return float.Parse(value, Invariant);
        }
        if(type == typeof(double)) {
            return double.Parse(value, Invariant);
        }
        if(type == typeof(bool)) {
            return bool.Parse(value);
        }
        if(type == typeof(byte)) {
            return byte.Parse(value, Invariant);
        }
        if(type == typeof(short)) {
            return short.Parse(value, Invariant);
        }
        if(type == typeof(uint)) {
            return uint.Parse(value, Invariant);
        }
        if(type == typeof(ulong)) {
            return ulong.Parse(value, Invariant);
        }
        if(type == typeof(ushort)) {
            return ushort.Parse(value, Invariant);
        }
        if(type == typeof(decimal)) {
            return decimal.Parse(value, Invariant);
        }

        return type.IsEnum
            ? Enum.Parse(type, value, true)
            : System.Convert.ChangeType(value, type, Invariant);
    }
}