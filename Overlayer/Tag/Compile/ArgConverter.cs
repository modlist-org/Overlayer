namespace Overlayer.Tag.Compile;

public static class ArgConverter {
    public static object Convert(string value, Type type) {
        if(type == typeof(string)) {
            return value;
        }
        if(type == typeof(int)) {
            return int.Parse(value);
        }
        if(type == typeof(long)) {
            return long.Parse(value);
        }
        if(type == typeof(float)) {
            return float.Parse(value);
        }
        if(type == typeof(double)) {
            return double.Parse(value);
        }
        if(type == typeof(bool)) {
            return bool.Parse(value);
        }
        if(type == typeof(byte)) {
            return byte.Parse(value);
        }
        if(type == typeof(short)) {
            return short.Parse(value);
        }
        if(type == typeof(uint)) {
            return uint.Parse(value);
        }
        if(type == typeof(ulong)) {
            return ulong.Parse(value);
        }
        if(type == typeof(ushort)) {
            return ushort.Parse(value);
        }
        if(type == typeof(decimal)) {
            return decimal.Parse(value);
        }

        if(type.IsEnum) {
            return Enum.Parse(type, value, true);
        }

        return System.Convert.ChangeType(value, type);
    }
}