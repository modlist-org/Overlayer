using System.Globalization;
using Newtonsoft.Json.Linq;
using Overlayer.IO;
using UnityEngine;

namespace Overlayer.IO.Fx;

public static class FxConverters {

    public static void RegisterDefaultConverters() {
        // "x, y"
        FxValue<Vector2>.RegisterConverter(ParseVector2);

        // "x, y, z"
        FxValue<Vector3>.RegisterConverter(ParseVector3);

        // "x, y, z, w"
        FxValue<Vector4>.RegisterConverter(ParseVector4);

        // "x, y, width, height"
        FxValue<Rect>.RegisterConverter(ParseRect);

        // "x, y, z, w" || "x, y, z"
        FxValue<Quaternion>.RegisterConverter(ParseQuaternion);

        // "r, g, b, a"
        FxValue<Color>.RegisterConverter(ParseColor);

        // "r1,g1,b1,a1,r2,g2,b2,a2,r3,g3,b3,a3,r4,g4,b4,a4" || "r,g,b,a"
        FxValue<GradientColor>.RegisterConverter(ParseGradientColor);

        FxValue<Vector2>.RegisterRawReader(ReadVector2);
        FxValue<Vector3>.RegisterRawReader(ReadVector3);
        FxValue<Vector4>.RegisterRawReader(ReadVector4);
        FxValue<Rect>.RegisterRawReader(ReadRect);
        FxValue<Quaternion>.RegisterRawReader(ReadQuaternion);
        FxValue<Color>.RegisterRawReader(ReadColor);
        FxValue<GradientColor>.RegisterRawReader(ReadGradientColor);

        FxValue<Vector2>.RegisterRawWriter(v => new JArray(v.x, v.y));
        FxValue<Vector3>.RegisterRawWriter(v => new JArray(v.x, v.y, v.z));
        FxValue<Vector4>.RegisterRawWriter(v => new JArray(v.x, v.y, v.z, v.w));
        FxValue<Rect>.RegisterRawWriter(r => new JArray(r.x, r.y, r.width, r.height));
        FxValue<Quaternion>.RegisterRawWriter(q => new JArray(q.x, q.y, q.z, q.w));
        FxValue<Color>.RegisterRawWriter(c => new JArray(c.r, c.g, c.b, c.a));
        FxValue<GradientColor>.RegisterRawWriter(g => g.Serialize());
    }

    public static void RegisterEnumConverter<TEnum>() where TEnum : struct, Enum {
        FxValue<TEnum>.RegisterConverter(raw => ParseEnum<TEnum>(raw));
    }

    #region Parsers

    public static Vector2 ParseVector2(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Vector2.zero;
        var p = Split(raw);
        return p.Length >= 2 && TryFloat(p[0], out var x) && TryFloat(p[1], out var y)
            ? new Vector2(x, y)
            : Vector2.zero;
    }

    public static Vector3 ParseVector3(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Vector3.zero;
        var p = Split(raw);
        return p.Length >= 3 && TryFloat(p[0], out var x) && TryFloat(p[1], out var y) && TryFloat(p[2], out var z)
            ? new Vector3(x, y, z)
            : Vector3.zero;
    }

    public static Vector4 ParseVector4(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Vector4.zero;
        var p = Split(raw);
        return p.Length >= 4 && TryFloat(p[0], out var x) && TryFloat(p[1], out var y) && TryFloat(p[2], out var z) && TryFloat(p[3], out var w)
            ? new Vector4(x, y, z, w)
            : Vector4.zero;
    }

    public static Rect ParseRect(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Rect.zero;
        var p = Split(raw);
        return p.Length >= 4 && TryFloat(p[0], out var x) && TryFloat(p[1], out var y) && TryFloat(p[2], out var w) && TryFloat(p[3], out var h)
            ? new Rect(x, y, w, h)
            : Rect.zero;
    }

    public static Quaternion ParseQuaternion(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Quaternion.identity;
        var p = Split(raw);

        return p.Length switch {
            >= 4 when TryFloat(p[0], out var x) && TryFloat(p[1], out var y) && TryFloat(p[2], out var z) &&
                      TryFloat(p[3], out var w) => new Quaternion(x, y, z, w),
            
            3 when TryFloat(p[0], out var ex) && TryFloat(p[1], out var ey) && TryFloat(p[2], out var ez) => Quaternion
                .Euler(ex, ey, ez),
            
            _ => Quaternion.identity
        };
    }

    public static Color ParseColor(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return Color.white;

        var p = Split(raw);
        if (p.Length < 3 || !TryFloat(p[0], out var r) || !TryFloat(p[1], out var g) || !TryFloat(p[2], out var b)) {
            return Color.white;
        }

        var a = p.Length >= 4 && TryFloat(p[3], out var parsedA) ? parsedA : 1.0f;
        return new Color(r, g, b, a);

    }

    public static GradientColor ParseGradientColor(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) return new GradientColor(Color.white, true);

        var p = Split(raw);

        return p.Length switch {
            >= 16 when TryFloat(p[0], out var r1) && TryFloat(p[1], out var g1) && TryFloat(p[2], out var b1) &&
                       TryFloat(p[3], out var a1) && TryFloat(p[4], out var r2) && TryFloat(p[5], out var g2) &&
                       TryFloat(p[6], out var b2) && TryFloat(p[7], out var a2) && TryFloat(p[8], out var r3) &&
                       TryFloat(p[9], out var g3) && TryFloat(p[10], out var b3) && TryFloat(p[11], out var a3) &&
                       TryFloat(p[12], out var r4) && TryFloat(p[13], out var g4) && TryFloat(p[14], out var b4) &&
                       TryFloat(p[15], out var a4) => new GradientColor(new Color(r1, g1, b1, a1),
                new Color(r2, g2, b2, a2), new Color(r3, g3, b3, a3), new Color(r4, g4, b4, a4)),
            
            >= 4 when TryFloat(p[0], out var sr) && TryFloat(p[1], out var sg) && TryFloat(p[2], out var sb) &&
                      TryFloat(p[3], out var sa) => new GradientColor(new Color(sr, sg, sb, sa), true),
            
            _ => new GradientColor(Color.white, true)
        };
    }

    public static TEnum ParseEnum<TEnum>(string raw, TEnum fallback = default) where TEnum : struct, Enum {
        if (string.IsNullOrWhiteSpace(raw)) {
            return fallback;
        }

        if (int.TryParse(raw, out var intVal)) {
            return (TEnum)Enum.ToObject(typeof(TEnum), intVal);
        }

        return Enum.TryParse<TEnum>(raw, true, out var result) ? result : fallback;
    }

    #endregion

    #region Helpers

    private static string[] Split(string input) {
        return input.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryFloat(string s, out float result) {
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static float Num(JToken t, float fallback = 0f) {
        try {
            return t == null ? fallback : t.Value<float>();
        } catch {
            return fallback;
        }
    }

    public static Vector2 ReadVector2(JToken token) {
        if (token is JArray arr && arr.Count >= 2) {
            return new Vector2(Num(arr[0]), Num(arr[1]));
        }

        return Vector2.zero;
    }

    public static Vector3 ReadVector3(JToken token) {
        if (token is JArray arr && arr.Count >= 3) {
            return new Vector3(Num(arr[0]), Num(arr[1]), Num(arr[2]));
        }

        return Vector3.zero;
    }

    public static Vector4 ReadVector4(JToken token) {
        if (token is JArray arr && arr.Count >= 4) {
            return new Vector4(Num(arr[0]), Num(arr[1]), Num(arr[2]), Num(arr[3]));
        }

        return Vector4.zero;
    }

    public static Rect ReadRect(JToken token) {
        if (token is JArray arr && arr.Count >= 4) {
            return new Rect(Num(arr[0]), Num(arr[1]), Num(arr[2]), Num(arr[3]));
        }

        return Rect.zero;
    }

    public static Quaternion ReadQuaternion(JToken token) {
        if (token is JArray arr && arr.Count >= 4) {
            return new Quaternion(Num(arr[0]), Num(arr[1]), Num(arr[2]), Num(arr[3]));
        }

        return Quaternion.identity;
    }

    public static Color ReadColor(JToken token) {
        if (token is JArray arr && arr.Count >= 3) {
            return new Color(Num(arr[0]), Num(arr[1]), Num(arr[2]), arr.Count >= 4 ? Num(arr[3], 1f) : 1f);
        }

        return Color.white;
    }

    public static GradientColor ReadGradientColor(JToken token) {
        var g = new GradientColor();
        g.Deserialize(token);

        return g;
    }

    #endregion
}