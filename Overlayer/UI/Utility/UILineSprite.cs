using UnityEngine;

namespace Overlayer.UI.Utility;

internal static class UILineSprite {
    private static Sprite? antialiasedLine;

    public static Sprite AntialiasedLine {
        get {
            if (antialiasedLine != null) {
                return antialiasedLine;
            }
            const int width = 1;
            const int height = 16;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) {
                float dist = Mathf.Abs(((y + 0.5f) / height) - 0.5f) * 2f;
                float t = Mathf.InverseLerp(0.35f, 1f, dist);
                float alpha = 1f - (t * t * (3f - 2f * t));
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                pixels[y] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            antialiasedLine = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            antialiasedLine.hideFlags = HideFlags.HideAndDontSave;
            return antialiasedLine;
        }
    }
}
