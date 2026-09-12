using System.Globalization;
using Newtonsoft.Json.Linq;
using Overlayer.IO;
using UnityEngine;

namespace Overlayer.IO.Fx;

public static class FxConverters {

    public static void RegisterDefaultConverters() {
        // "[x, y]" — each component is a numeric expression
        FxValue<Vector2>.RegisterConverter(ParseVector2);

        // "[x, y, z]"
        FxValue<Vector3>.RegisterConverter(ParseVector3);

        // "[x, y, z, w]"
        FxValue<Vector4>.RegisterConverter(ParseVector4);

        // "[x, y, width, height]"
        FxValue<Rect>.RegisterConverter(ParseRect);

        // "[x, y, z, w]" || "[x, y, z]" (euler)
        FxValue<Quaternion>.RegisterConverter(ParseQuaternion);

        // "[r, g, b, a]"
        FxValue<Color>.RegisterConverter(ParseColor);

        // "[r1,g1,b1,a1,...]" (16) || "[r,g,b,a]"
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
        if (TryJsArray(raw, out var a) && a.Length >= 2) {
            return new Vector2(a[0], a[1]);
        }

        var p = SplitComponents(raw, 2);
        return new Vector2(Eval(p[0]), Eval(p[1]));
    }

    public static Vector3 ParseVector3(string raw) {
        if (TryJsArray(raw, out var a) && a.Length >= 3) {
            return new Vector3(a[0], a[1], a[2]);
        }

        var p = SplitComponents(raw, 3);
        return new Vector3(Eval(p[0]), Eval(p[1]), Eval(p[2]));
    }

    public static Vector4 ParseVector4(string raw) {
        if (TryJsArray(raw, out var a) && a.Length >= 4) {
            return new Vector4(a[0], a[1], a[2], a[3]);
        }

        var p = SplitComponents(raw, 4);
        return new Vector4(Eval(p[0]), Eval(p[1]), Eval(p[2]), Eval(p[3]));
    }

    public static Rect ParseRect(string raw) {
        if (TryJsArray(raw, out var a) && a.Length >= 4) {
            return new Rect(a[0], a[1], a[2], a[3]);
        }

        var p = SplitComponents(raw, 4);
        return new Rect(Eval(p[0]), Eval(p[1]), Eval(p[2]), Eval(p[3]));
    }

    public static Quaternion ParseQuaternion(string raw) {
        if (TryJsArray(raw, out var a)) {
            if (a.Length >= 4) {
                return new Quaternion(a[0], a[1], a[2], a[3]);
            }

            if (a.Length == 3) {
                return Quaternion.Euler(a[0], a[1], a[2]);
            }
        }

        var p = SplitComponents(raw, 0);

        return p.Length switch {
            >= 4 => new Quaternion(Eval(p[0]), Eval(p[1]), Eval(p[2]), Eval(p[3])),
            3 => Quaternion.Euler(Eval(p[0]), Eval(p[1]), Eval(p[2])),
            _ => throw new FormatException("Expected [x, y, z, w] or [x, y, z].")
        };
    }

    public static Color ParseColor(string raw) {
        if (TryJsArray(raw, out var a) && a.Length >= 3) {
            return new Color(a[0], a[1], a[2], a.Length >= 4 ? a[3] : 1.0f);
        }

        var p = SplitComponents(raw, 0);
        if (p.Length < 3) {
            throw new FormatException("Expected [r, g, b, a].");
        }

        var alpha = p.Length >= 4 ? Eval(p[3]) : 1.0f;
        return new Color(Eval(p[0]), Eval(p[1]), Eval(p[2]), alpha);
    }

    public static GradientColor ParseGradientColor(string raw) {
        if (TryJsArray(raw, out var a)) {
            if (a.Length >= 16) {
                return new GradientColor(
                    new Color(a[0], a[1], a[2], a[3]),
                    new Color(a[4], a[5], a[6], a[7]),
                    new Color(a[8], a[9], a[10], a[11]),
                    new Color(a[12], a[13], a[14], a[15]));
            }

            if (a.Length >= 4) {
                return new GradientColor(new Color(a[0], a[1], a[2], a[3]), true);
            }
        }

        var p = SplitComponents(raw, 0);

        return p.Length switch {
            >= 16 => new GradientColor(
                new Color(Eval(p[0]), Eval(p[1]), Eval(p[2]), Eval(p[3])),
                new Color(Eval(p[4]), Eval(p[5]), Eval(p[6]), Eval(p[7])),
                new Color(Eval(p[8]), Eval(p[9]), Eval(p[10]), Eval(p[11])),
                new Color(Eval(p[12]), Eval(p[13]), Eval(p[14]), Eval(p[15]))),
            >= 4 => new GradientColor(new Color(Eval(p[0]), Eval(p[1]), Eval(p[2]), Eval(p[3])), true),
            _ => throw new FormatException("Expected [r, g, b, a] or 16 components.")
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

    private static float Eval(string expr) => FxValue.EvaluateNumericComponent(expr);

    private static bool TryJsArray(string raw, out float[] values) {
        values = null;
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return false;
        }

        try {
            if (!FxValue.TryEvaluateJs(FxValue.WrapJsBlock(text), out var result) || result == null) {
                return false;
            }

            if (result is System.Collections.IList list && list.Count > 0 && list.Count <= 64) {
                var arr = new float[list.Count];
                for (int i = 0; i < list.Count; i++) {
                    arr[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
                }

                values = arr;
                return true;
            }
        } catch {
        }

        return false;
    }

    private static string[] SplitComponents(string input, int expected) {
        if (string.IsNullOrWhiteSpace(input)) {
            throw new FormatException("Empty expression.");
        }

        var text = input.Trim();
        if (text.StartsWith('[') && text.EndsWith(']')) {
            text = text[1..^1];
        }

        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '(' || c == '[') {
                depth++;
            } else if (c == ')' || c == ']') {
                depth--;
            } else if ((c == ',' || c == ';') && depth == 0) {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }
        parts.Add(text[start..]);

        var result = parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        if (expected > 0 && result.Length < expected) {
            throw new FormatException($"Expected at least {expected} components.");
        }

        return result;
    }

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
