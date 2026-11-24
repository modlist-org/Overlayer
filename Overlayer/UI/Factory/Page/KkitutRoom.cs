using O5Kit.Core;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

/// <summary>Shift+click Credits: the leader's room. Overlays the Credits page; assets load by manifest path only, so they never show up as internal resources.</summary>
internal static class KkitutRoom {
    private const string LogoPath = "Image.Secret.KkitutLogo512.png";

    private static RectTransform root;
    private static CanvasGroup group;
    private static RectTransform glow, logo, ringA, ringB;
    private static Image backImg, glowImg, ringBImg;

    private static TextMeshProUGUI leader, name;
    private static Sprite logoSprite;
    private static ITweenHandle fade;
    private static Vector2 laidOut;
    private static bool subscribed;

    public static bool Active { get; private set; }

    public static Sprite LogoSprite => logoSprite != null ? logoSprite : logoSprite = PageCredits.LoadMipSprite(LogoPath);

    public static void Build(RectTransform page) {
        if (!subscribed) {
            subscribed = true;
            UIColors.OnPaletteChanged += ApplyPalette;
            MenuFactory.OnStateChanged += state => {
                if (state != (int)OriginalMenuState.Credits) {
                    Exit(true);
                }
            };
        }

        root = Rect("KkitutRoom", page);
        Stretch(root);
        group = root.gameObject.AddComponent<CanvasGroup>();

        // Backdrop: kkitut.org's deep radial falloff, pooled at the top-left.
        // Solid base plus a square glow sized from the page height (Layout),
        // so the falloff stays circular at any aspect ratio.
        backImg = root.gameObject.AddComponent<Image>();
        backImg.raycastTarget = true;

        glow = Rect("Glow", root);
        glow.anchorMin = glow.anchorMax = new Vector2(0f, 1f);
        glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.sprite = Make(Glow());
        glowImg.raycastTarget = false;

        // Spread to the edges for an open, quiet room: arcs sweep in from the
        // bottom-left, the logo sits top-right, the name rests bottom-right.
        // Sizes follow the page (see Layout).
        ringB = Rect("RingOuter", root);
        ringB.anchorMin = ringB.anchorMax = Vector2.zero;
        ringBImg = ringB.gameObject.AddComponent<Image>();
        ringBImg.sprite = Make(Ring(1024, 1.4f, 2, 38f));
        ringBImg.raycastTarget = false;

        ringA = Rect("RingInner", root);
        ringA.anchorMin = ringA.anchorMax = Vector2.zero;
        var ringAImg = ringA.gameObject.AddComponent<Image>();
        ringAImg.sprite = Make(Ring(1024, 1.8f, 3, 10f));
        ringAImg.color = new Color(1f, 1f, 1f, 0.12f);
        ringAImg.raycastTarget = false;

        logo = Rect("Logo", root);
        logo.anchorMin = logo.anchorMax = logo.pivot = Vector2.one;
        var logoImg = logo.gameObject.AddComponent<Image>();
        logoImg.sprite = LogoSprite;
        logoImg.preserveAspect = true;
        logoImg.raycastTarget = false;

        name = CornerText("Name", root, "Kkitut");
        name.font = O5KitAdapters.Ctx.Fonts.Medium;
        name.color = new Color(1f, 1f, 1f, 0.92f);

        // Tracking via plain spaces: TMP characterSpacing on this font left
        // some glyph pairs unspaced ("L E A DER", "K kitut").
        leader = CornerText("Leader", root, "L E A D E R");

        laidOut = Vector2.zero;
        ApplyPalette();
        root.gameObject.SetActive(false);
    }

    /// <summary>Opens the room. <paramref name="instant"/> skips the fade (entering from another tab: the page transition carries it in, so Credits never flashes).</summary>
    public static void Enter(bool instant = false) {
        if (Active || root == null) {
            return;
        }
        Active = true;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        if (instant) {
            fade?.Kill();
            group.alpha = 1f;
        } else {
            FadeTo(1f, 0.45f, null);
        }
        MenuFactory.SetCreditsSecretIcon(LogoSprite, true);
    }

    public static void Exit(bool instant) {
        if (!Active || root == null) {
            return;
        }
        Active = false;
        MenuFactory.SetCreditsSecretIcon(null, false);
        if (instant) {
            fade?.Kill();
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            return;
        }
        FadeTo(0f, 0.3f, () => {
            if (!Active && root != null) {
                root.gameObject.SetActive(false);
            }
        });
    }

    private static void FadeTo(float target, float duration, Action done) {
        fade?.Kill();
        if (target > 0f) {
            group.alpha = 0f;
        }
        var g = group;
        fade = O5KitAdapters.Ctx.Tween.TweenFloat(() => g.alpha, v => g.alpha = v, target, duration, done, O5Ease.OutSine);
    }

    // Room colors derive from the UI palette: a backdrop deeper than the
    // panel, a glow nudged toward the base color, accent ring and label.
    private static void ApplyPalette() {
        if (root == null) {
            return;
        }
        Color accent = UIColors.BaseColor;
        Color panel = UIColors.PanelBG;
        Color back = Color.Lerp(new Color(panel.r, panel.g, panel.b, 1f), Color.black, 0.55f);
        backImg.color = back;
        Color glowColor = Color.Lerp(back, accent, 0.09f);
        glowImg.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
        ringBImg.color = new Color(accent.r, accent.g, accent.b, 0.18f);
        leader.color = new Color(accent.r, accent.g, accent.b, 0.7f);
    }

    /// <summary>Per-frame motion; idle unless the room is on screen.</summary>
    public static void Tick() {
        if (root == null || !root.gameObject.activeInHierarchy) {
            return;
        }
        // Angles come from the wall clock, not accumulated frame time: the rings
        // keep turning while the room is closed and never lag behind a hitch.
        double seconds = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
        ringA.localRotation = Quaternion.Euler(0f, 0f, (float)(seconds * 4.0 % 360.0));
        ringB.localRotation = Quaternion.Euler(0f, 0f, (float)(-seconds * 2.5 % 360.0));

        Vector2 size = root.rect.size;
        if (size != laidOut && size.x > 0f && size.y > 0f) {
            laidOut = size;
            Layout(size.x, size.y);
        }
    }

    // Everything scales with the page so the room keeps its proportions
    // at any window size.
    private static void Layout(float w, float h) {
        glow.sizeDelta = Vector2.one * h * 1.9f;
        glow.anchoredPosition = new Vector2(h * 0.26f, -h * 0.16f);

        Vector2 pivot = new(w * 0.05f, h * 0.07f);
        ringA.anchoredPosition = ringB.anchoredPosition = pivot;
        ringA.sizeDelta = Vector2.one * h * 1.24f;
        ringB.sizeDelta = Vector2.one * h * 1.68f;

        float logoSize = h * 0.17f;
        logo.sizeDelta = new Vector2(logoSize, logoSize);
        logo.anchoredPosition = new Vector2(-w * 0.07f, -h * 0.11f);

        float nameSize = Mathf.Max(24f, h * 0.046f);
        float leaderSize = Mathf.Max(11f, h * 0.016f);
        Vector2 corner = new(-w * 0.07f, h * 0.09f);
        name.fontSize = nameSize;
        name.rectTransform.sizeDelta = new Vector2(w * 0.5f, nameSize * 1.3f);
        name.rectTransform.anchoredPosition = corner;
        leader.fontSize = leaderSize;
        leader.rectTransform.sizeDelta = new Vector2(w * 0.5f, leaderSize * 1.6f);
        leader.rectTransform.anchoredPosition = corner + new Vector2(0f, nameSize * 1.3f + leaderSize * 0.4f);
    }

    private static RectTransform Rect(string objName, Transform parent) {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return r;
    }

    private static void Stretch(RectTransform r) {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CornerText(string objName, Transform parent, string text) {
        var r = Rect(objName, parent);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
        var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = O5KitAdapters.Ctx.Fonts.Regular;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Sprite Make(Texture2D tex) {
        var s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    private static Texture2D NewTex(int w, int h, bool mips) => new(w, h, TextureFormat.RGBA32, mips, false) {
        wrapMode = TextureWrapMode.Clamp,
        filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
        hideFlags = HideFlags.HideAndDontSave,
    };

    // Linear radial falloff (alpha 1 at the center, 0 at the edge).
    private static Texture2D Glow() {
        const int n = 128;
        var tex = NewTex(n, n, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) {
            for (int x = 0; x < n; x++) {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float a = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // Anti-aliased ring split into `arcs` segments separated by `gapDeg` gaps.
    private static Texture2D Ring(int n, float stroke, int arcs, float gapDeg) {
        var tex = NewTex(n, n, true);
        var px = new Color32[n * n];
        float c = n * 0.5f;
        float radius = c - stroke - 2f;
        float segment = 360f / arcs;
        for (int y = 0; y < n; y++) {
            for (int x = 0; x < n; x++) {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float dist = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius);
                float a = Mathf.Clamp01(stroke * 0.5f - dist + 0.5f);
                if (a > 0f) {
                    float ang = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % segment;
                    float edge = Mathf.Min(ang, segment - gapDeg - ang);
                    a *= Mathf.Clamp01(edge * radius * Mathf.Deg2Rad + 0.5f);
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }
}
