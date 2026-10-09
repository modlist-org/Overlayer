using Overlayer.IO.User;

using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.Overlay;

/// <summary>
/// KeyViewer-style rain. While <see cref="Active"/> is true a trail grows out of the object's edge;
/// when it turns false the trail detaches and flies off. Drawn by a child <see cref="OvRainGraphic"/>
/// so it can sit on an object that already has its own Image/Text.
/// </summary>
#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class OvRainComponent
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public bool Active = true;
    public float Speed = 400f;
    public float Length = 400f;
    public float Width;
    public Vector2 Offset;
    public float FadeIn;
    public float FadeOut = 100f;
    public GradientColor Color = new(UnityEngine.Color.white, true);
    public string SpriteKey;
    public int MaxTrails = 32;

    private OvRainGraphic graphic;
    private RainTrail current;
    private bool meshHasTrails;

    private void EnsureGraphic() {
        if (graphic != null) {
            return;
        }
        var go = new GameObject("Rain", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();
        graphic = go.AddComponent<OvRainGraphic>();
        graphic.raycastTarget = false;
        graphic.Owner = this;
    }

    private void OnEnable() {
        EnsureGraphic();
        graphic.gameObject.SetActive(true);
    }

    private void OnDisable() {
        current = null;
        meshHasTrails = false;
        if (graphic != null) {
            graphic.Trails.Clear();
            graphic.gameObject.SetActive(false);
        }
    }

    private void OnDestroy() {
        if (graphic != null) {
            Destroy(graphic.gameObject);
        }
    }

    private void LateUpdate() {
        EnsureGraphic();
        Layout();

        var trails = graphic.Trails;
        if (Active && current == null) {
            while (trails.Count > 0 && trails.Count >= Mathf.Max(1, MaxTrails)) {
                trails.RemoveAt(0);
            }
            current = new RainTrail();
            trails.Add(current);
        } else if (!Active && current != null) {
            current.Held = false;
            current = null;
        }

        float step = Mathf.Max(0f, Speed) * Time.unscaledDeltaTime;
        float length = Mathf.Max(0f, Length);
        for (int i = trails.Count - 1; i >= 0; i--) {
            var trail = trails[i];
            // Head stops at the far edge so a long hold doesn't grow without bound.
            trail.Head = Mathf.Min(trail.Head + step, length);
            if (!trail.Held) {
                trail.Tail += step;
                if (trail.Tail >= length) {
                    trails.RemoveAt(i);
                }
            }
        }

        var sprite = !string.IsNullOrEmpty(SpriteKey) && UserResourceManager.Spr.TryGet(SpriteKey, out var res) ? res.sprite : null;
        if (sprite != graphic.Sprite) {
            graphic.Sprite = sprite;
            graphic.SetMaterialDirty();
        }
        // Idle rain (no trails now or last frame) keeps an empty mesh; skip the rebuild.
        bool hasTrails = trails.Count > 0;
        if (hasTrails || meshHasTrails) {
            graphic.SetVerticesDirty();
        }
        meshHasTrails = hasTrails;
    }

    // Rain area sits on the object's top edge: Length tall, Width wide (0 = object width).
    private void Layout() {
        var rt = graphic.rectTransform;
        Vector2 size = ((RectTransform)transform).rect.size;
        float length = Mathf.Max(0f, Length);
        float width = Width > 0f ? Width : size.x;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(width, length);
        rt.anchoredPosition = Offset;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }
}

public sealed class RainTrail {
    // Distances from the source edge, going up.
    public float Head;
    public float Tail;
    public bool Held = true;
}

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class OvRainGraphic
#if ML && IL2CPP
    (IntPtr ptr) : MaskableGraphic(ptr)
#else
    : MaskableGraphic
#endif
{
    public OvRainComponent Owner;
    public readonly List<RainTrail> Trails = [];
    public Sprite Sprite;

    private readonly List<float> cuts = [];

    public override Texture mainTexture => Sprite != null ? Sprite.texture : s_WhiteTexture;

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if (Owner == null || Trails.Count == 0) {
            return;
        }
        Rect rect = rectTransform.rect;
        float length = Mathf.Max(0f, Owner.Length);
        if (length <= 0f || rect.width <= 0f || rect.height <= 0f) {
            return;
        }
        float fadeIn = Mathf.Clamp(Owner.FadeIn, 0f, length);
        float fadeOut = Mathf.Clamp(Owner.FadeOut, 0f, length);
        Vector4 uv = Sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(Sprite) : new Vector4(0f, 0f, 1f, 1f);

        foreach (var trail in Trails) {
            float a0 = Mathf.Max(0f, trail.Tail);
            float a1 = Mathf.Min(length, trail.Head);
            if (a1 - a0 <= 0.01f) {
                continue;
            }
            // Split at the fade edges so the alpha ramps stay exact instead of being linearly smeared.
            cuts.Clear();
            cuts.Add(a0);
            if (fadeIn > a0 && fadeIn < a1) cuts.Add(fadeIn);
            if (length - fadeOut > a0 && length - fadeOut < a1) cuts.Add(length - fadeOut);
            cuts.Add(a1);
            cuts.Sort();
            float span = Mathf.Max(0.01f, trail.Head - trail.Tail);
            for (int i = 0; i < cuts.Count - 1; i++) {
                float s0 = cuts[i], s1 = cuts[i + 1];
                if (s1 - s0 <= 0.001f) {
                    continue;
                }
                AddQuad(vh, rect, s0, s1, (s0 - trail.Tail) / span, (s1 - trail.Tail) / span, length, fadeIn, fadeOut, uv);
            }
        }
    }

    private void AddQuad(VertexHelper vh, Rect rect, float s0, float s1, float t0, float t1, float length, float fadeIn, float fadeOut, Vector4 uv) {
        int start = vh.currentVertCount;
        // (along, cross) for the 4 corners; cross 0 = left/bottom side.
        for (int k = 0; k < 4; k++) {
            float s = k < 2 ? s0 : s1;
            float t = k < 2 ? t0 : t1;
            float c = k is 0 or 3 ? 0f : 1f;
            Vector2 pos = new(Mathf.Lerp(rect.xMin, rect.xMax, c), rect.yMin + s);
            float nx = (pos.x - rect.xMin) / rect.width;
            float ny = (pos.y - rect.yMin) / rect.height;
            var g = Owner.Color;
            Color col = UnityEngine.Color.Lerp(
                UnityEngine.Color.Lerp(g.BL, g.BR, nx),
                UnityEngine.Color.Lerp(g.TL, g.TR, nx),
                ny) * color;
            float alpha = 1f;
            if (fadeIn > 0f) alpha = Mathf.Min(alpha, s / fadeIn);
            if (fadeOut > 0f) alpha = Mathf.Min(alpha, (length - s) / fadeOut);
            col.a *= Mathf.Clamp01(alpha);
            // Sprite stretches over the whole trail: along the trail = sprite height.
            Vector2 tex = new(Mathf.Lerp(uv.x, uv.z, c), Mathf.Lerp(uv.y, uv.w, t));
            vh.AddVert(pos, col, tex);
        }
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
