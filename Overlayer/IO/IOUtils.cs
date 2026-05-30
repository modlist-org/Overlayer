using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Overlayer.IO;

public static class IOUtils {
    #region Generic
    public static T Read<T>(JToken token, string key, T fallback) {
        var value = token[key];

        if(value == null) {
            return fallback;
        }

        try {
            return value.Value<T>();
        } catch {
            return fallback;
        }
    }
    #endregion
    #region Enum
    public static TEnum ReadEnum<TEnum>(JToken token, string key, TEnum fallback)
        where TEnum : struct, Enum {
        var value = token[key];

        if(value == null) {
            return fallback;
        }

        try {
            if(value.Type == JTokenType.Integer) {
                return (TEnum)Enum.ToObject(typeof(TEnum), value.Value<int>());
            }

            if(value.Type == JTokenType.String) {
                var str = value.Value<string>();

                if(Enum.TryParse<TEnum>(str, true, out var result)) {
                    return result;
                }
            }
        } catch {
            return fallback;
        }

        return fallback;
    }

    public static JValue WriteEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum => new(value.ToString());
    #endregion
    #region Vector2
    public static Vector2 Read(JToken token, string key, Vector2 fallback) {
        var value = token[key];

        if(value == null || value is not JArray arr || arr.Count < 2) {
            return fallback;
        }

        try {
            return new Vector2(
                (float)arr[0],
                (float)arr[1]
            );
        } catch {
            return fallback;
        }
    }

    public static JArray Write(Vector2 v)
        => new(v.x, v.y);
    #endregion
    #region Vector3
    public static Vector3 Read(JToken token, string key, Vector3 fallback) {
        var value = token[key];

        if(value == null || value is not JArray arr || arr.Count < 3) {
            return fallback;
        }

        try {
            return new Vector3(
                (float)arr[0],
                (float)arr[1],
                (float)arr[2]
            );
        } catch {
            return fallback;
        }
    }

    public static JArray Write(Vector3 v)
        => new(v.x, v.y, v.z);
    #endregion
    #region Vector4
    public static Vector4 Read(JToken token, string key, Vector4 fallback) {
        var value = token[key];

        if(value == null || value is not JArray arr || arr.Count < 4) {
            return fallback;
        }

        try {
            return new Vector4(
                (float)arr[0],
                (float)arr[1],
                (float)arr[2],
                (float)arr[3]
            );
        } catch {
            return fallback;
        }
    }

    public static JArray Write(Vector4 v) => new(v.x, v.y, v.z, v.w);
    #endregion
    #region Quaternion
    public static Quaternion Read(JToken token, string key, Quaternion fallback) {
        var value = token[key];

        if(value == null || value is not JArray arr || arr.Count < 4) {
            return fallback;
        }

        try {
            return new Quaternion(
                (float)arr[0],
                (float)arr[1],
                (float)arr[2],
                (float)arr[3]
            );
        } catch {
            return fallback;
        }
    }

    public static JArray Write(Quaternion q) => new(q.x, q.y, q.z, q.w);
    #endregion
    #region Color
    public static Color Read(JToken token, string key, Color fallback) {
        var value = token[key];

        if(value == null || value is not JArray arr || arr.Count < 4) {
            return fallback;
        }

        try {
            return new Color(
                (float)arr[0],
                (float)arr[1],
                (float)arr[2],
                (float)arr[3]
            );
        } catch {
            return fallback;
        }
    }

    public static JArray Write(Color c)
        => new(c.r, c.g, c.b, c.a);
    #endregion
    #region GradientColor
    public static GradientColor Read(JToken token, string key, GradientColor fallback) {
        var value = token[key];

        if(value == null) {
            return fallback;
        }

        try {
            var result = value.ToObject<GradientColor>();
            return result;
        } catch {
            return fallback;
        }
    }

    public static JToken Write(GradientColor value) => JToken.FromObject(value);
    #endregion
}