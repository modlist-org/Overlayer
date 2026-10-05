using Newtonsoft.Json.Linq;
using Overlayer.IO;
using Overlayer.IO.Interface;
using Overlayer.IO.Utility;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

public struct GradientColor : ISettingsFile, ICopyable<GradientColor>, IEquatable<GradientColor> {
    private bool solidColor;

    private VertexGradient data;

    public GradientColor(Color color, bool solid = false) {
        solidColor = solid;

        data = new VertexGradient(color, color, color, color);

        RebuildCache();
    }

    public GradientColor(Color TopLeft, Color TopRight, Color BottomLeft, Color BottomRight) {
        solidColor = false;

        data = new VertexGradient(TopLeft, TopRight, BottomLeft, BottomRight);

        TLHex = ColorUtils.ToHtmlStringRGBA(TopLeft);
        TRHex = ColorUtils.ToHtmlStringRGBA(TopRight);
        BLHex = ColorUtils.ToHtmlStringRGBA(BottomLeft);
        BRHex = ColorUtils.ToHtmlStringRGBA(BottomRight);
    }

    public bool SolidColor {
        readonly get => solidColor;
        set {
            solidColor = value;

            if(solidColor) {
                var c = data.topLeft;
                data = new VertexGradient(c, c, c, c);
            }

            RebuildCache();
        }
    }

    public Color TL {
        readonly get => data.topLeft;
        set {
            data = solidColor
                ? new VertexGradient(value, value, value, value)
                : new VertexGradient(value, data.topRight, data.bottomLeft, data.bottomRight);

            RebuildCache();
        }
    }

    public Color TR {
        readonly get => solidColor ? data.topLeft : data.topRight;
        set {
            data = !solidColor
                ? new VertexGradient(data.topLeft, value, data.bottomLeft, data.bottomRight)
                : new VertexGradient(value, value, value, value);

            RebuildCache();
        }
    }

    public Color BL { // ㅗㅜㅑ
        readonly get => solidColor ? data.topLeft : data.bottomLeft;
        set {
            data = !solidColor
                ? new VertexGradient(data.topLeft, data.topRight, value, data.bottomRight)
                : new VertexGradient(value, value, value, value);

            RebuildCache();
        }
    }

    public Color BR {
        readonly get => solidColor ? data.topLeft : data.bottomRight;
        set {
            data = !solidColor
                ? new VertexGradient(data.topLeft, data.topRight, data.bottomLeft, value)
                : new VertexGradient(value, value, value, value);

            RebuildCache();
        }
    }

    public string TLHex { get; private set; }

    public string TRHex { get; private set; }

    public string BLHex { get; private set; }

    public string BRHex { get; private set; }

    private void RebuildCache() {
        TLHex = ColorUtils.ToHtmlStringRGBA(data.topLeft);
        TRHex = ColorUtils.ToHtmlStringRGBA(data.topRight);
        BLHex = ColorUtils.ToHtmlStringRGBA(data.bottomLeft);
        BRHex = ColorUtils.ToHtmlStringRGBA(data.bottomRight);
    }

    public readonly JToken Serialize() {
        if(solidColor) {
            return IOUtils.Write(data.topLeft);
        }

        return new JArray {
            IOUtils.Write(data.topLeft),
            IOUtils.Write(data.topRight),
            IOUtils.Write(data.bottomLeft),
            IOUtils.Write(data.bottomRight)
        };
    }

    public void Deserialize(JToken token) {
        if(token == null) {
            return;
        }

        if(token is JArray arr) {
            if(arr.Count >= 4 && arr[0] is JArray) {
                data = new VertexGradient(
                    ReadColor(arr[0]),
                    ReadColor(arr[1]),
                    ReadColor(arr[2]),
                    ReadColor(arr[3])
                );
                solidColor = false;
                RebuildCache();
                return;
            }

            var color = ReadColor(arr);
            data = new VertexGradient(color, color, color, color);
            solidColor = true;
            RebuildCache();
            return;
        }

        var solid = token.Value<float>();
        var col = new Color(solid, solid, solid, solid);

        data = new VertexGradient(col, col, col, col);
        solidColor = true;

        RebuildCache();
    }

    private static Color ReadColor(JToken token) {
        if(token is not JArray arr || arr.Count < 4) {
            throw new FormatException("Invalid gradient color.");
        }

        return new Color(
            (float)arr[0],
            (float)arr[1],
            (float)arr[2],
            (float)arr[3]
        );
    }

    public GradientColor Copy() {
        return new GradientColor {
            solidColor = solidColor,
            data = data,
            TLHex = TLHex,
            TRHex = TRHex,
            BLHex = BLHex,
            BRHex = BRHex
        };
    }

    #region Equality
    public readonly bool Equals(GradientColor other) {
        return solidColor == other.solidColor
            && data.topLeft.Equals(other.data.topLeft)
            && data.topRight.Equals(other.data.topRight)
            && data.bottomLeft.Equals(other.data.bottomLeft)
            && data.bottomRight.Equals(other.data.bottomRight);
    }

    public override readonly bool Equals(object obj)
        => obj is GradientColor other && Equals(other);

    public override readonly int GetHashCode()
        => HashCode.Combine(solidColor, data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);

    public static bool operator ==(GradientColor a, GradientColor b) => a.Equals(b);

    public static bool operator !=(GradientColor a, GradientColor b) => !a.Equals(b);
    #endregion

    public static implicit operator Color(GradientColor color) => color.data.topLeft;

    public static implicit operator VertexGradient(GradientColor color) => color.data;

    public static implicit operator GradientColor(Color color) => new(color);

    public static implicit operator GradientColor(VertexGradient color) {
        return new GradientColor(
            color.topLeft,
            color.topRight,
            color.bottomLeft,
            color.bottomRight
        );
    }

    public static GradientColor operator +(GradientColor a, GradientColor b) {
        return new GradientColor(
            a.data.topLeft + b.data.topLeft,
            a.data.topRight + b.data.topRight,
            a.data.bottomLeft + b.data.bottomLeft,
            a.data.bottomRight + b.data.bottomRight);
    }

    public static GradientColor operator -(GradientColor a, GradientColor b) {
        return new GradientColor(
            a.data.topLeft - b.data.topLeft,
            a.data.topRight - b.data.topRight,
            a.data.bottomLeft - b.data.bottomLeft,
            a.data.bottomRight - b.data.bottomRight);
    }

    public static GradientColor operator *(GradientColor a, float b) {
        Color mul = new(b, b, b, b);

        return new GradientColor(
            a.data.topLeft * mul,
            a.data.topRight * mul,
            a.data.bottomLeft * mul,
            a.data.bottomRight * mul);
    }

    public static GradientColor operator /(GradientColor a, float b) {
        float inv = 1f / b;
        Color mul = new(inv, inv, inv, inv);

        return new GradientColor(
            a.data.topLeft * mul,
            a.data.topRight * mul,
            a.data.bottomLeft * mul,
            a.data.bottomRight * mul);
    }
}
