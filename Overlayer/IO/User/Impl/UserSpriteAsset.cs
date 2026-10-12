using Overlayer.Core;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.IO.User.Impl;

// Builds one runtime TMP_SpriteAsset out of every user-registered sprite,
// so <sprite index=N> can use UserData images. Rebuilt when the key set changes.
internal static class UserSpriteAsset {
    internal const string AssetKey = "User Sprites";

    private static TMP_SpriteAsset _asset;
    private static Texture2D _atlas;
    private static Material _material;
    private static string _builtFor;

    internal static TMP_SpriteAsset Current => _asset;

    internal static bool HasSprites() {
        try {
            return UserResourceManager.Spr.Keys.Count > 0;
        } catch {
            return false;
        }
    }

    internal static TMP_SpriteAsset GetOrBuild() {
        try {
            var keys = UserResourceManager.Spr.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (keys.Length == 0) {
                return null;
            }
            string sig = string.Join("\n", keys);
            if (_asset != null && sig == _builtFor) {
                return _asset;
            }
            DestroyBuilt();
            var built = Build(keys);
            if (built == null) {
                return _asset;
            }
            _asset = built;
            _builtFor = sig;
            return _asset;
        } catch (Exception e) {
            try {
                MainCore.Log.Err($"[UserSpriteAsset] GetOrBuild failed: {e.GetType().Name}: {e.Message}");
            } catch {
            }
            return _asset;
        }
    }

    private static TMP_SpriteAsset Build(string[] keys) {
        var items = new List<(string key, Sprite sprite, int w, int h)>();
        foreach (var key in keys) {
            try {
                if (!UserResourceManager.Spr.TryGet(key, out var entry)) {
                    continue;
                }
                var sprite = entry.sprite;
                var tex = sprite?.texture;
                if (sprite == null || tex == null) {
                    continue;
                }
                try {
                    tex.GetPixel(0, 0);
                } catch {
                    continue; // unreadable (compressed/packed) texture
                }
                var r = sprite.rect;
                int w = Mathf.Clamp(Mathf.CeilToInt(r.width), 1, tex.width);
                int h = Mathf.Clamp(Mathf.CeilToInt(r.height), 1, tex.height);
                items.Add((key, sprite, w, h));
            } catch {
            }
        }
        if (items.Count == 0) {
            return null;
        }

        int cell = 1;
        foreach (var item in items) {
            cell = Math.Max(cell, Math.Max(item.w, item.h));
        }
        int cols = Mathf.CeilToInt(Mathf.Sqrt(items.Count));
        int rows = Mathf.CeilToInt(items.Count / (float)cols);

        var atlas = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        var glyphs = new List<TMP_SpriteGlyph>(items.Count);
        var chars = new List<TMP_SpriteCharacter>(items.Count);
        for (int i = 0; i < items.Count; i++) {
            var (key, sprite, w, h) = items[i];
            int px = (i % cols) * cell;
            int py = (i / cols) * cell;
            try {
                var tex = sprite.texture;
                int sx = Mathf.Clamp(Mathf.FloorToInt(sprite.rect.x), 0, Math.Max(0, tex.width - 1));
                int sy = Mathf.Clamp(Mathf.FloorToInt(sprite.rect.y), 0, Math.Max(0, tex.height - 1));
                int sw = Math.Min(w, tex.width - sx);
                int sh = Math.Min(h, tex.height - sy);
                atlas.SetPixels(px, py, sw, sh, tex.GetPixels(sx, sy, sw, sh));
            } catch {
                continue;
            }
            // Metrics in pixels; renders at pixels * fontSize / pointSize.
            TMP_SpriteGlyph glyph;
            try {
                glyph = MakeGlyph((uint)i, w, h, px, py, sprite);
            } catch {
                continue;
            }
            glyphs.Add(glyph);
            TMP_SpriteCharacter ch;
            try {
                ch = MakeCharacter((uint)i, key, glyph);
            } catch {
                continue;
            }
            chars.Add(ch);
            try {
                MainCore.Log.Msg($"[UserSpriteAsset] #{i}: {key} ({w}x{h})");
            } catch {
            }
        }
        if (glyphs.Count == 0) {
            Object.Destroy(atlas);
            return null;
        }
        atlas.Apply(false, false);

        Shader shader;
        try {
            shader = Shader.Find("TextMeshPro/Sprite");
        } catch {
            shader = null;
        }
        if (shader == null) {
            Object.Destroy(atlas);
            return null;
        }

        var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        asset.hideFlags = HideFlags.DontSave;
        asset.name = AssetKey;
        asset.spriteSheet = atlas;
        var mat = new Material(shader) {
            hideFlags = HideFlags.DontSave
        };
        mat.mainTexture = atlas;
        asset.material = mat;
        if (!FillTables(asset, chars, glyphs)) {
            Object.Destroy(mat);
            Object.Destroy(atlas);
            Object.Destroy(asset);
            return null;
        }
        try {
            SetPointSize(asset, 100f);
        } catch {
        }
        try {
            asset.UpdateLookupTables();
        } catch {
        }

        _atlas = atlas;
        _material = mat;
        return asset;
    }

    // GlyphMetrics/GlyphRect live in different assemblies per TMP version
    // (or are absent from old interop), so resolve everything off the
    // glyph constructor's own parameter types.
    private static ConstructorInfo _glyphCtor;
    private static ConstructorInfo _charCtor;

    private static bool EnsureCtors(Sprite probe) {
        if (_glyphCtor != null && _charCtor != null) {
            return true;
        }
        try {
            var glyphType = typeof(TMP_SpriteGlyph);
            foreach (var c in glyphType.GetConstructors()) {
                var ps = c.GetParameters();
                if (ps.Length == 6 && ps[5].ParameterType.IsInstanceOfType(probe)) {
                    _glyphCtor = c;
                    break;
                }
            }
            if (_glyphCtor == null) {
                return false;
            }
            foreach (var c in typeof(TMP_SpriteCharacter).GetConstructors()) {
                var ps = c.GetParameters();
                if (ps.Length == 2 && ps[1].ParameterType == glyphType) {
                    _charCtor = c;
                    break;
                }
            }
        } catch {
        }
        return _glyphCtor != null && _charCtor != null;
    }

    private static TMP_SpriteGlyph MakeGlyph(uint index, float w, float h, int px, int py, Sprite sprite) {
        if (!EnsureCtors(sprite)) {
            throw new MissingMethodException("TMP_SpriteGlyph(Sprite) ctor");
        }
        var ps = _glyphCtor.GetParameters();
        object metrics = Activator.CreateInstance(ps[1].ParameterType, w, h, 0f, h, w);
        object rect = Activator.CreateInstance(ps[2].ParameterType, px, py, (int)w, (int)h);
        return (TMP_SpriteGlyph)_glyphCtor.Invoke([index, metrics, rect, 1f, 0, sprite]);
    }

    private static TMP_SpriteCharacter MakeCharacter(uint index, string key, TMP_SpriteGlyph glyph) {
        var ch = (TMP_SpriteCharacter)_charCtor.Invoke([0u, glyph]);
        ch.name = key;
        try {
            ch.scale = 1f;
        } catch {
        }
        try {
            ch.glyphIndex = index;
        } catch {
        }
        return ch;
    }

    private static void SetPointSize(TMP_SpriteAsset asset, float pointSize) {
        var fiProp = asset.GetType().GetProperty("faceInfo");
        object fi = fiProp.GetValue(asset);
        fi.GetType().GetProperty("pointSize").SetValue(fi, pointSize);
        fiProp.SetValue(asset, fi);
    }

    // Tables are getter-only (internal setter): fill the live lists,
    // falling back to the private backing fields by name.
    private static bool FillTables(TMP_SpriteAsset asset, List<TMP_SpriteCharacter> chars, List<TMP_SpriteGlyph> glyphs) {
        try {
            object charTable = asset.spriteCharacterTable;
            object glyphTable = asset.spriteGlyphTable;
            if (charTable != null && glyphTable != null) {
                // Interop lists don't accept System AddRange: add one by one.
                var addC = charTable.GetType().GetMethod("Add");
                var addG = glyphTable.GetType().GetMethod("Add");
                foreach (var c in chars) addC.Invoke(charTable, [c]);
                foreach (var g in glyphs) addG.Invoke(glyphTable, [g]);
                return true;
            }
        } catch {
        }
        try {
            var t = asset.GetType();
            var charField = t.GetField("m_SpriteCharacterTable", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? t.GetField("m_CharacterTable", BindingFlags.NonPublic | BindingFlags.Instance);
            var glyphField = t.GetField("m_SpriteGlyphTable", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? t.GetField("m_GlyphTable", BindingFlags.NonPublic | BindingFlags.Instance);
            if (charField == null || glyphField == null) {
                return false;
            }
            charField.SetValue(asset, chars);
            glyphField.SetValue(asset, glyphs);
            return true;
        } catch {
            return false;
        }
    }

    private static void DestroyBuilt() {
        try {
            if (_material != null) Object.Destroy(_material);
            if (_atlas != null) Object.Destroy(_atlas);
            if (_asset != null) Object.Destroy(_asset);
        } catch {
        } finally {
            _material = null;
            _atlas = null;
            _asset = null;
            _builtFor = null;
        }
    }
}
