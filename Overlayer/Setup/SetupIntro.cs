using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.UI;
using Overlayer.UI.Factory.Page;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Setup;

// First-run intro, played over the whole window (top bar included, so the close button and drag
// area stay hidden) before the setup wizard:
//   1. a short arc of the logo's outline circles it, speeding up, and stretches into the full
//      outline at top speed,
//   2. the finished outline flashes white,
//   3. the logo pops in (no growth) with a white flash and an outline shockwave,
//   4. "Welcome to Overlayer" rises in under it,
//   5. the wizard opens (hidden) underneath, the logo flies into the top-bar logo and the title
//      into the wizard's title slot; then the backdrop is swiped away diagonally (top-left to
//      bottom-right) over the now opaque wizard, and the real logo and title take over.
// Art is loaded by manifest path only (never an internal resource). Easing uses only the
// presets used across Overlayer (OutExpo, OutSine, InOutSine, InCubic, Linear) via EaseMath.
internal static class SetupIntro {
    private const float TraceEnd = 1.8f;
    private const float TraceTurns = 2.5f;
    private const float ArcStart = 0.14f;
    private const float ArcGrowAt = 1.1f;
    private const float FlashEnd = 2.1f;
    private const float PopAt = 2.1f;
    private const float TextAt = 2.85f;
    private const float HandoffAt = 4.15f;
    private const float FlySeconds = 0.8f;
    private const float RevealSeconds = 0.9f;
    private const float LeadIn = 0.5f;
    private const float LogoSize = 250f;
    private const float WelcomeSize = 52f;

    private static RectTransform overlay, logoRoot, welcomeRect, welcomeClip, traceMask;
    private static GameObject inputBlocker;
    private static CanvasGroup logoGroup;
    private static Image backdrop, outline, traceArc, wave, logo, flash;
    private static TextMeshProUGUI welcome;
    private static Sprite logoSprite, outlineSprite, silhouetteSprite, whiteSprite;
    private static float time;
    private static bool handedOff;
    private static bool openingForIntro;
    private static Vector3 flyFrom, logoFrom;
    private static Vector2 welcomeSize, swipedSize;
    // Visible share of the padded intro art's width (512 of 576 px); the top-bar icon fills its rect.
    private const float LogoArtWidth = 512f / 576f;
    private static Color backdropColor;
    private static RectTransform wipe;
    private static readonly Vector2 Diagonal = new(0.70710678f, -0.70710678f);

    public static bool IsPlaying => overlay != null;

    /// <summary>True on a fresh install while waiting for the first-run intro to begin.</summary>
    public static bool IsPendingFirstRun
        => MainCore.Conf != null && MainCore.Conf.SetupPending.Value && !IsPlaying && !SetupWizard.IsOpen;

    /// <summary>True before the first-run intro starts or while the intro is actively playing.</summary>
    public static bool IsBlocking => IsPlaying || IsPendingFirstRun;

    internal static bool IsOpeningForIntro => openingForIntro;

    public static void Play() {
        if (IsPlaying || UICore.CanvasObj == null || UICore.Panel == null || HudEditor.IsOpen || SetupWizard.IsOpen) {
            return;
        }
        KeyCapture.Cancel();
        UIKeyboardNav.ClearFocus();
        O5KitAdapters.Ctx?.Tooltip?.Hide();
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(null);
        }
        if (!UICore.IsOpen) {
            try {
                openingForIntro = true;
                UICore.Open();
            } finally {
                openingForIntro = false;
            }
        }
        if (UICore.IsMenuOpen) {
            UICore.CloseMenu();
        }
        // Played full screen; the reveal eases the window back to its default size.
        UICore.FillScreenNow();
        Build();
        SetTopBarLogoVisible(false);
        time = 0f;
        handedOff = false;
        Tick();
    }

    public static void Tick() {
        if (overlay == null) {
            return;
        }
        if (!UICore.IsOpen || !overlay.gameObject.activeInHierarchy) {
            Abort();
            return;
        }
        if (overlay.GetSiblingIndex() != overlay.parent.childCount - 1) {
            overlay.SetAsLastSibling();
        }
        if (inputBlocker != null && inputBlocker.transform.GetSiblingIndex() != inputBlocker.transform.parent.childCount - 1) {
            inputBlocker.transform.SetAsLastSibling();
        }
        if (!handedOff && overlay.rect.size != swipedSize) {
            Swipe(0f); // before the reveal: keeps the backdrop fitted if the window resizes
        }
        // A loading hitch counts as one frame so it doesn't skip the show; ordinary low frame
        // rates still run in real time instead of slow motion.
        float dt = Time.unscaledDeltaTime;
        time += dt > 0.1f ? 1f / 60f : dt;
        // A blank beat before anything moves.
        float t = time - LeadIn;
        Color accent = UIColors.BaseColor;

        // 1. An arc circles the outline, accelerating, then stretches to the full outline.
        // The wedge mask turns; the outline inside counter-turns so it stays put.
        float angle = 360f * TraceTurns * E("InCubic", t / TraceEnd);
        traceMask.localRotation = Quaternion.Euler(0f, 0f, -angle);
        outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        // The arc grows out of nothing first, then stretches to the whole outline.
        float head = ArcStart * E("OutSine", Step01(t, 0f, 0.45f));
        traceArc.fillAmount = Mathf.Lerp(head, 1f, E("InCubic", Step01(t, ArcGrowAt, TraceEnd - ArcGrowAt)));
        // 2. Flash it white.
        float lit = E("OutSine", Step01(t, TraceEnd, FlashEnd - TraceEnd));
        Color outlineColor = Color.Lerp(new Color(accent.r, accent.g, accent.b, 0.9f), Color.white, lit);

        // 3. Pop.
        if (t < PopAt) {
            outline.color = outlineColor;
            logo.color = Fade(logo.color, 0f);
            flash.color = Fade(flash.color, 0f);
            wave.color = Fade(wave.color, 0f);
        } else {
            outline.color = Fade(outlineColor, 1f - E("OutSine", Step01(t, PopAt, 0.25f)));
            logo.color = Fade(Color.white, Step01(t, PopAt, 0.06f));
            flash.color = Fade(Color.white, 1f - E("OutExpo", Step01(t, PopAt, 0.6f)));
            float w = E("OutExpo", Step01(t, PopAt, 0.9f));
            wave.rectTransform.localScale = Vector3.one * (1f + 0.55f * w);
            wave.color = Fade(Color.white, 0.8f * (1f - w));
        }

        // 4. Welcome line: centered under the logo, rising out of a clip at its own baseline.
        float txt = E("OutExpo", Step01(t, TextAt, 0.9f));
        if (!handedOff && t >= TextAt - 0.1f) {
            if (welcomeSize == Vector2.zero) {
                // Measured once: the text never changes, and re-measuring re-lays it out every frame.
                welcomeSize = new(welcome.preferredWidth, welcome.preferredHeight);
                welcomeClip.sizeDelta = welcomeSize;
                welcomeClip.anchoredPosition = new Vector2(-welcomeSize.x * 0.5f, TextTop);
                welcomeRect.sizeDelta = welcomeSize;
            }
            welcomeRect.anchoredPosition = new Vector2(0f, -welcomeSize.y * (1f - txt));
            welcome.alpha = txt;
        }

        // 5. Hand over to the wizard.
        if (!handedOff && t >= HandoffAt) {
            handedOff = true;
            // Leave the reveal clip so the title can fly anywhere.
            welcomeRect.SetParent(overlay, true);
            welcomeClip.gameObject.SetActive(false);
            flyFrom = welcomeRect.position;
            logoFrom = logoRoot.position;
            // The wizard opens hidden now (the title needs its layout) and shows once everything lands.
            SetupWizard.HoldReveal = true;
            SetupWizard.Open();
            SetupWizard.ForceLayout();
            // The window shrinks back alongside the logo's flight (same start, ease and length);
            // the logo and title re-aim at their moving targets every frame.
            UICore.RestoreDefaultBoundsIfFull(FlySeconds, O5Kit.Core.O5Ease.OutExpo);
        }
        if (handedOff) {
            float h = Step01(t, HandoffAt, FlySeconds);
            float e = E("OutExpo", h);
            // Everything has landed: reveal the wizard and the window under the backdrop.
            // Keep input blocked until the wipe animation completely finishes.
            float revealAt = HandoffAt + FlySeconds;
            if (t >= revealAt && SetupWizard.HoldReveal) {
                SetupWizard.RevealNow();
            }
            Swipe(E("InOutSine", Step01(t, revealAt, RevealSeconds)));
            FlyLogo(e);
            if (SetupWizard.TryGetTitleRest(out TextMeshProUGUI title, out Vector3 rest)) {
                // Measured at the wizard's resting scale (it may still be at its opening scale).
                float titleScale = title.rectTransform.lossyScale.x / Mathf.Max(0.0001f, SetupWizard.ContentScale);
                float target = title.fontSize / WelcomeSize * (titleScale / welcomeRect.parent.lossyScale.x);
                welcomeRect.position = Vector3.LerpUnclamped(flyFrom, rest, e);
                welcomeRect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, target, e);
                title.alpha = 0f;
            }
            // The flying title covers the real one until the wizard has faded in.
            if (t >= revealAt + RevealSeconds) {
                Finish();
            }
        }
    }

    private const float LogoY = 50f;

    // 0: backdrop fully shown; 1: swiped off. The mask's edge travels top-left to bottom-right,
    // keeping what lies ahead of it; the backdrop is pinned to the window each frame.
    private static void Swipe(float p) {
        Vector2 size = overlay.rect.size;
        swipedSize = size;
        float side = (size.x + size.y) * 2f;
        float half = (size.x + size.y) * 0.35355339f;
        wipe.sizeDelta = new Vector2(side, side);
        wipe.anchoredPosition = Diagonal * Mathf.Lerp(-half, half, p);
        RectTransform back = backdrop.rectTransform;
        back.sizeDelta = size;
        back.position = overlay.TransformPoint(overlay.rect.center);
        back.rotation = overlay.rotation;
    }

    // Moves and shrinks the logo onto the top-bar logo (never grows it).
    private static void FlyLogo(float e) {
        RectTransform target = UICore.TopBarLogo;
        if (target == null) {
            logoGroup.alpha = 1f - e;
            return;
        }
        Rect r = target.rect;
        Vector3 to = target.TransformPoint(r.center);
        float targetWidth = r.width * target.lossyScale.x;
        float ownWidth = LogoSize * LogoArtWidth * logoRoot.parent.lossyScale.x;
        float scale = Mathf.Min(1f, targetWidth / Mathf.Max(0.0001f, ownWidth));
        logoRoot.position = Vector3.LerpUnclamped(logoFrom, to, e);
        logoRoot.localScale = Vector3.one * Mathf.LerpUnclamped(1f, scale, e);
    }

    // The real top-bar logo stays hidden until the intro's logo lands on it.
    private static void SetTopBarLogoVisible(bool visible) {
        if (UICore.TopBarLogo != null) {
            var img = UICore.TopBarLogo.GetComponent<Image>();
            if (img != null) {
                img.canvasRenderer.SetAlpha(visible ? 1f : 0f);
            }
        }
    }

    private const float TextTop = LogoY - LogoSize * 0.5f - 34f;

    private static void Build() {
        Color panel = UIColors.PanelBG;
        backdropColor = Color.Lerp(new Color(panel.r, panel.g, panel.b, 1f), Color.black, 0.35f);

        overlay = NewRect("OverlayerIntro", UICore.Panel);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        // Own canvas: the per-frame spin, fills and fades rebatch only the intro, not the whole window.
        var introCanvas = overlay.gameObject.AddComponent<Canvas>();
        O5Kit.Core.O5ColorSpace.Track(introCanvas);
        overlay.gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen raycast shield above panel + resize handles until Finish() ends the intro.
        inputBlocker = new GameObject("OverlayerIntroInputBlocker");
        inputBlocker.transform.SetParent(UICore.CanvasObj.transform, false);
        var blockerRect = inputBlocker.AddComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        var blockerCanvas = inputBlocker.AddComponent<Canvas>();
        blockerCanvas.overrideSorting = true;
        blockerCanvas.sortingOrder = 32766;
        O5Kit.Core.O5ColorSpace.Track(blockerCanvas);
        inputBlocker.AddComponent<GraphicRaycaster>();
        var blockerImg = inputBlocker.AddComponent<Image>();
        blockerImg.color = Color.clear;
        blockerImg.raycastTarget = true;
        welcomeSize = Vector2.zero;
        // Backdrop sits under a 45-degree stencil mask that swipes it away at the end.
        wipe = NewRect("Wipe", overlay);
        wipe.pivot = new Vector2(0f, 0.5f);
        wipe.localRotation = Quaternion.Euler(0f, 0f, -45f);
        var wipeImg = wipe.gameObject.AddComponent<Image>();
        wipeImg.raycastTarget = false;
        wipe.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        backdrop = NewRect("Backdrop", wipe).gameObject.AddComponent<Image>();
        backdrop.sprite = O5KitAdapters.Ctx.Sprites.RoundedPanel;
        backdrop.type = Image.Type.Sliced;
        backdrop.color = backdropColor;
        backdrop.raycastTarget = true; // hides and blocks the top bar (close, drag) until the hand-off
        Swipe(0f);

        logoRoot = NewRect("Logo", overlay);
        logoRoot.sizeDelta = new Vector2(LogoSize, LogoSize);
        logoRoot.anchoredPosition = new Vector2(0f, LogoY);
        logoGroup = logoRoot.gameObject.AddComponent<CanvasGroup>();

        outlineSprite ??= PageCredits.LoadMipSprite("Image.Intro.IntroLogoOutline.png");
        silhouetteSprite ??= PageCredits.LoadMipSprite("Image.Intro.IntroLogoSilhouette.png");
        logoSprite ??= PageCredits.LoadMipSprite("Image.Intro.IntroLogo.png");

        wave = Layer("Wave", outlineSprite, LogoSize);
        // Rotating wedge (a filled radial mask) that reveals an arc of the outline.
        // Filled images need a sprite (without one Unity draws the full rect).
        whiteSprite ??= MakeWhite();
        traceArc = Layer("Trace", whiteSprite, LogoSize * 1.5f);
        traceMask = traceArc.rectTransform;
        traceArc.preserveAspect = false;
        traceArc.color = Color.white;
        traceArc.type = Image.Type.Filled;
        traceArc.fillMethod = Image.FillMethod.Radial360;
        traceArc.fillOrigin = (int)Image.Origin360.Top;
        traceArc.fillClockwise = true;
        traceArc.fillAmount = 0f;
        traceMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var outlineRect = NewRect("Outline", traceMask);
        outlineRect.sizeDelta = new Vector2(LogoSize, LogoSize);
        outline = outlineRect.gameObject.AddComponent<Image>();
        outline.sprite = outlineSprite;
        outline.preserveAspect = true;
        outline.raycastTarget = false;
        outline.color = Color.clear;
        logo = Layer("Fill", logoSprite, LogoSize);
        flash = Layer("Flash", silhouetteSprite, LogoSize);

        // Reveal clip: its top-left sits where the title rests; the title rises into it.
        welcomeClip = NewRect("WelcomeClip", overlay);
        welcomeClip.pivot = new Vector2(0f, 1f);
        welcomeClip.gameObject.AddComponent<RectMask2D>();
        welcome = NewRect("Welcome", welcomeClip).gameObject.AddComponent<TextMeshProUGUI>();
        welcomeRect = welcome.rectTransform;
        welcome.font = O5KitAdapters.Ctx.Fonts.Medium;
        welcome.fontSize = WelcomeSize;
        welcome.color = Color.white;
        welcome.alignment = TextAlignmentOptions.TopLeft;
        welcome.enableWordWrapping = false;
        // Same tracking as the wizard title (O5Factory.ControlText), so the hand-off matches.
        welcome.characterSpacing = -3f;
        welcome.raycastTarget = false;
        welcome.text = MainCore.Tr.Get("SETUP_WELCOME_TITLE", "Welcome to Overlayer");
        welcome.alpha = 0f;
        // Top-left pivot/anchor so the flight can land on the wizard title's corner.
        welcomeRect.anchorMin = welcomeRect.anchorMax = welcomeRect.pivot = new Vector2(0f, 1f);
    }

    private static Image Layer(string name, Sprite sprite, float size) {
        var r = NewRect(name, logoRoot);
        r.sizeDelta = new Vector2(size, size);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = Color.clear;
        return img;
    }

    private static void Finish() {
        SetupWizard.HoldReveal = false;
        SetTopBarLogoVisible(true);
        if (SetupWizard.TryGetTitleRest(out TextMeshProUGUI title, out _)) {
            title.alpha = 1f;
        }
        DestroyOverlay();
    }

    // Window closed/disposed mid-show: drop it. A pending first run will retry later.
    internal static void Abort() {
        SetupWizard.HoldReveal = false;
        SetTopBarLogoVisible(true);
        if (handedOff && SetupWizard.TryGetTitleRest(out TextMeshProUGUI title, out _)) {
            title.alpha = 1f;
        }
        DestroyOverlay();
    }

    private static void DestroyOverlay() {
        if (overlay != null) {
            overlay.gameObject.SetActive(false);
            overlay.SetParent(null, false);
            UnityEngine.Object.Destroy(overlay.gameObject);
            overlay = null;
        }
        if (inputBlocker != null) {
            inputBlocker.SetActive(false);
            inputBlocker.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(inputBlocker);
            inputBlocker = null;
        }
    }

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return r;
    }

    private static Sprite MakeWhite() {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) {
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) {
            px[i] = new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Color Fade(Color c, float a) => new(c.r, c.g, c.b, a);

    private static float Step01(float t, float start, float duration) => Mathf.Clamp01((t - start) / duration);

    // Overlayer's shared ease evaluator (same presets as effect tags and O5Ease).
    private static float E(string ease, float x) => UI.Easing.EaseMath.Evaluate(ease, x);
}
