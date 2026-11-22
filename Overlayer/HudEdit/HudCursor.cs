using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overlayer.HudEdit;

/// <summary>Procedural hardware cursor textures for HUD transform handles and active drags.</summary>
internal static class HudCursor {
    private const int Size = 32;
    private static readonly Vector2 Hotspot = new(Size * 0.5f, Size * 0.5f);
    private static readonly Dictionary<int, Texture2D> Cache = [];
    private static int currentKey;

    public static void SetDefault() {
        if (currentKey == 0) {
            return;
        }
        currentKey = 0;
        Apply(null, Vector2.zero);
    }

    public static void Reset() {
        currentKey = 0;
        Apply(null, Vector2.zero);
    }

    public static void SetResize(float angleDeg) {
        int angle = HudHandleMath.QuantizeResizeAngle(angleDeg);
        SetCached(100 + angle, angle, HudHandleMath.InResizeArrow);
    }

    public static void SetRotate(float angleDeg) {
        int angle = HudHandleMath.QuantizeRotateAngle(angleDeg);
        SetCached(200 + angle, angle, HudHandleMath.InRotateArc);
    }

    public static void SetMove() => SetCached(300, 0, HudHandleMath.InMoveCross);

    private static void SetCached(int key, int angleDeg, Func<float, float, bool> predicate) {
        if (currentKey == key) {
            return;
        }
        if (!Cache.TryGetValue(key, out var tex) || tex == null) {
            tex = Build(angleDeg, predicate);
            Cache[key] = tex;
        }
        currentKey = key;
        Apply(tex, Hotspot);
    }

    private static void Apply(Texture2D tex, Vector2 hotspot) {
        try {
            Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
        } catch {
        }
    }

    private static Texture2D Build(int angleDeg, Func<float, float, bool> predicate) {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        bool[] shape = new bool[Size * Size];
        float rad = angleDeg * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++) {
            float dy = y + 0.5f - half;
            for (int x = 0; x < Size; x++) {
                float dx = x + 0.5f - half;
                float u = dx * cos + dy * sin;
                float v = -dx * sin + dy * cos;
                shape[y * Size + x] = predicate(u, v);
            }
        }

        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++) {
            for (int x = 0; x < Size; x++) {
                int idx = y * Size + x;
                if (shape[idx]) {
                    pixels[idx] = new Color32(255, 255, 255, 255);
                    continue;
                }
                bool border = false;
                for (int oy = -1; oy <= 1 && !border; oy++) {
                    int ny = y + oy;
                    if (ny < 0 || ny >= Size) {
                        continue;
                    }
                    for (int ox = -1; ox <= 1; ox++) {
                        if (ox == 0 && oy == 0) {
                            continue;
                        }
                        int nx = x + ox;
                        if (nx >= 0 && nx < Size && shape[ny * Size + nx]) {
                            border = true;
                            break;
                        }
                    }
                }
                pixels[idx] = border ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }
}
