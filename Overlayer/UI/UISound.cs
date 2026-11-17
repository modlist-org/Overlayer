using O5Kit.Behaviour;
using O5Kit.Input;
using Overlayer.Core;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI;

/// <summary>
/// Global MSFS2020 + osu!lazer style procedural UI audio across Overlayer
/// (hover, click, back, slider drag tick, hold-to-confirm charge/confirm, wind-only menu open/close, overlay open/close).
/// </summary>
internal static class UISound {
    private const int SampleRate = 44100;
    private const float HoverCooldown = 0.035f;
    private const float SliderTickCooldown = 0.045f;
    private const float HoldTickCooldown = 0.042f;
    private const float VoiceCooldown = 0.055f;
    private const float KeyCooldown = 0.022f;
    private const float PitchJitter = 0.035f;
    private const float ReverbWetMix = 0.20f;

    private static GameObject host;
    private static readonly AudioSource[] Sources = new AudioSource[4];
    private static int sourceCursor;

    private static AudioClip hoverClip;
    private static AudioClip clickClip;
    private static AudioClip backClip;
    private static AudioClip openClip;
    private static AudioClip closeClip;
    private static AudioClip menuOpenClip;
    private static AudioClip menuCloseClip;
    private static AudioClip sliderTickClip;
    private static AudioClip ollyVoiceClip;
    private static AudioClip keyTypeClip;
    private static AudioClip backspaceClip;
    private static AudioClip inputSelectClip;
    private static AudioClip inputDeselectClip;
    private static AudioClip ddOpenClip;
    private static AudioClip ddCloseClip;
    private static AudioClip ddPickClip;
    private static AudioClip fxOnClip;
    private static AudioClip fxOffClip;
    private static AudioClip toggleOnClip;
    private static AudioClip toggleOffClip;

    private static bool ready;
    private static bool disposed;

    private static EventSystem cachedEs;
    private static PointerEventData pointerData;
#if ML && IL2CPP
    private static readonly Il2CppSystem.Collections.Generic.List<RaycastResult> Hits = new();
#else
    private static readonly List<RaycastResult> Hits = [];
#endif

    private static GameObject lastHover;
    private static float lastHoverAt = -10f;
    private static float suppressHoverUntil;

    // Active slider tracking while mouse button is held down (handles DragBlocker / warped cursor).
    private static Transform activeSlider;
    private static RectTransform activeSliderFill;
    private static TMP_InputField activeSliderInput;
    private static Slider activeUnitySlider;
    private static string lastSliderText;
    private static float lastSliderFillX;
    private static float lastUnitySliderVal;
    private static float lastSliderTickAt = -10f;
    private static float lastHoldTickAt = -10f;
    private static float lastVoiceAt = -10f;
    private static float lastKeyAt = -10f;

    private static TMP_InputField lastFocusedInput;
    private static string lastInputText;

    private static float MasterVolume => MainCore.Conf != null ? Mathf.Clamp01(MainCore.Conf.UISoundVolume) : 0.4f;

    public static void Tick() {
        if (disposed) {
            return;
        }
        HoldToConfirm.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));

        if (MasterVolume <= 0.001f) {
            lastHover = null;
            lastFocusedInput = null;
            lastInputText = null;
            ClearActiveSlider();
            return;
        }

        bool uiVisible = (UICore.CanvasObj != null && UICore.CanvasObj.activeInHierarchy) || Tutorial.Tutorial.IsRunning;
        if (!uiVisible) {
            lastHover = null;
            lastFocusedInput = null;
            lastInputText = null;
            ClearActiveSlider();
            return;
        }

        TickActiveSlider();

        var es = EventSystem.current;
        if (es == null) {
            lastHover = null;
            lastFocusedInput = null;
            lastInputText = null;
            return;
        }

        float now = Time.unscaledTime;
        TickFocusedInput(es, now);

        if (!ReferenceEquals(cachedEs, es)) {
            cachedEs = es;
            pointerData = new PointerEventData(es);
        }

        pointerData.position = O5Input.MousePosition;
        Hits.Clear();
        es.RaycastAll(pointerData, Hits);

        GameObject current = null;
        if (Hits.Count > 0) {
            var topGo = Hits[0].gameObject;
            if (topGo != null) {
                current = FindInteractive(topGo.transform);
            }
        }

        if (!ReferenceEquals(current, lastHover)) {
            lastHover = current;
            if (current != null && !string.Equals(current.name, "CanvasActionsTile", StringComparison.Ordinal)) {
                PlayHover();
            }
        }

        if (current != null && O5Input.GetMouseButtonDown(0)) {
            if (!TryBeginSlider(current.transform)
                && !HoldToConfirm.IsHoldTarget(current)
                && !IsSpecializedClickTarget(current)) {
                bool isBack = IsBackOrClose(current.name);
                PlayOneShot(isBack ? backClip : clickClip, 0.85f, RandPitch(1f));
            }
        }
    }

    public static void PlayHover() {
        if (disposed || MasterVolume <= 0.001f || O5Input.GetMouseButton(0)) {
            return;
        }
        float now = Time.unscaledTime;
        if (now < suppressHoverUntil || now - lastHoverAt < HoverCooldown) {
            return;
        }
        lastHoverAt = now;
        PlayOneShot(hoverClip, 0.50f, RandPitch(1f));
    }

    public static void PlayOpen() => PlayTransition(openClip, 0.78f);
    public static void PlayClose() => PlayTransition(closeClip, 0.76f);
    public static void PlayMenuOpen() => PlayTransition(menuOpenClip, 0.82f);
    public static void PlayMenuClose() => PlayTransition(menuCloseClip, 0.80f);
    public static void PlayConfirm() => PlayOneShot(clickClip, 0.85f, RandPitch(1f));
    public static void PlayFxToggle(bool enabled) => PlayOneShot(enabled ? fxOnClip : fxOffClip, 0.78f, RandPitch(1f));
    public static void PlayToggle(bool on) => PlayOneShot(on ? toggleOnClip : toggleOffClip, 0.80f, RandPitch(1f));
    public static void PlayDropdownToggle(bool expanded) => PlayOneShot(expanded ? ddOpenClip : ddCloseClip, 0.78f, RandPitch(1f));
    public static void PlayDropdownPick() => PlayOneShot(ddPickClip, 0.76f, RandPitch(1f));

    /// <summary>Soft visual-novel style blip while Olly is typing dialogue.</summary>
    public static void PlayOllyVoice() {
        if (disposed || MasterVolume <= 0.001f) {
            return;
        }
        float now = Time.unscaledTime;
        if (now - lastVoiceAt < VoiceCooldown) {
            return;
        }
        lastVoiceAt = now;
        PlayOneShot(ollyVoiceClip, 0.68f, RandPitch(1.02f, 0.09f));
    }

    /// <summary>Plays a pitched slider notch tick (for wheel or external slider adjustments).</summary>
    public static void PlaySliderTick(float normalized) {
        if (disposed) {
            return;
        }
        float now = Time.unscaledTime;
        if (now - lastSliderTickAt < SliderTickCooldown) {
            return;
        }
        lastSliderTickAt = now;
        PlayOneShot(sliderTickClip, 0.55f, RandPitch(Mathf.Lerp(0.86f, 1.32f, Mathf.Clamp01(normalized))));
    }

    /// <summary>Plays rising slider-tick pips while holding delete-confirm.</summary>
    public static void SetHoldCharge(float? progress) {
        if (disposed) {
            return;
        }
        if (!progress.HasValue) {
            lastHoldTickAt = -10f;
            return;
        }
        float now = Time.unscaledTime;
        if (now - lastHoldTickAt >= HoldTickCooldown) {
            lastHoldTickAt = now;
            float p = Mathf.Clamp01(progress.Value);
            PlayOneShot(sliderTickClip, 0.55f, RandPitch(Mathf.Lerp(0.86f, 1.32f, p)));
        }
    }

    public static void Dispose() {
        disposed = true;
        lastHover = null;
        ClearActiveSlider();
        HoldToConfirm.ClearAll();
        cachedEs = null;
        pointerData = null;
        Hits.Clear();

        DestroyClip(ref hoverClip);
        DestroyClip(ref clickClip);
        DestroyClip(ref backClip);
        DestroyClip(ref openClip);
        DestroyClip(ref closeClip);
        DestroyClip(ref menuOpenClip);
        DestroyClip(ref menuCloseClip);
        DestroyClip(ref sliderTickClip);
        DestroyClip(ref ollyVoiceClip);
        DestroyClip(ref keyTypeClip);
        DestroyClip(ref backspaceClip);
        DestroyClip(ref inputSelectClip);
        DestroyClip(ref inputDeselectClip);
        DestroyClip(ref ddOpenClip);
        DestroyClip(ref ddCloseClip);
        DestroyClip(ref ddPickClip);
        DestroyClip(ref fxOnClip);
        DestroyClip(ref fxOffClip);
        DestroyClip(ref toggleOnClip);
        DestroyClip(ref toggleOffClip);

        if (host != null) {
            Object.Destroy(host);
            host = null;
        }
        Array.Clear(Sources, 0, Sources.Length);
        lastFocusedInput = null;
        lastInputText = null;
        ready = false;
    }

    private static float RandPitch(float basePitch, float jitter = PitchJitter) =>
        basePitch * UnityEngine.Random.Range(1f - jitter, 1f + jitter);

    private static void TickFocusedInput(EventSystem es, float now) {
        var selGo = es.currentSelectedGameObject;
        var cur = selGo != null ? selGo.GetComponent<TMP_InputField>() : null;
        if (cur != null && !cur.isFocused) {
            cur = null;
        }

        if (!ReferenceEquals(cur, lastFocusedInput)) {
            if (lastFocusedInput != null) {
                PlayOneShot(inputDeselectClip, 0.72f, RandPitch(1f));
            }
            if (cur != null) {
                PlayOneShot(inputSelectClip, 0.76f, RandPitch(1f));
            }
            lastFocusedInput = cur;
            lastInputText = cur != null ? cur.text : null;
            return;
        }

        if (cur == null) {
            return;
        }

        string txt = cur.text ?? string.Empty;
        string prev = lastInputText ?? string.Empty;
        bool backKey = O5Input.GetKey(KeyCode.Backspace) || O5Input.GetKey(KeyCode.Delete);

        if (!string.Equals(txt, prev, StringComparison.Ordinal)) {
            bool isErase = backKey || txt.Length < prev.Length;
            lastInputText = txt;
            if (now - lastKeyAt >= KeyCooldown) {
                lastKeyAt = now;
                PlayOneShot(isErase ? backspaceClip : keyTypeClip, 0.78f, RandPitch(1f, 0.085f));
            }
        } else if (O5Input.GetKeyDown(KeyCode.Backspace) && now - lastKeyAt >= KeyCooldown) {
            lastKeyAt = now;
            PlayOneShot(backspaceClip, 0.55f, RandPitch(1f, 0.085f));
        }
    }

    private static bool IsSpecializedClickTarget(GameObject go) {
        if (go == null) {
            return false;
        }
        if (go.name.EndsWith("_use_fx", StringComparison.Ordinal) || go.transform.Find("ToggleCircle") != null) {
            return true;
        }
        var p = go.transform.parent;
        if (p == null) {
            return false;
        }
        return (go.name == "Bg" && (p.name == "Dropdown" || p.name == "MultiDropdown"))
            || (go.name == "Row" && p.name == "List");
    }

    private static void PlayTransition(AudioClip clip, float volumeScale) {
        if (disposed) {
            return;
        }
        suppressHoverUntil = Time.unscaledTime + 0.14f;
        lastHover = null;
        PlayOneShot(clip, volumeScale, RandPitch(1f));
    }

    private static bool TryBeginSlider(Transform t) {
        if (t.GetComponent<Slider>() is { } unitySlider) {
            activeSlider = t;
            activeUnitySlider = unitySlider;
            lastUnitySliderVal = unitySlider.normalizedValue;
            return true;
        }
        var fill = t.Find("Fill") as RectTransform;
        var valInput = t.Find("ValueInput");
        if (fill != null && valInput != null) {
            activeSlider = t;
            activeSliderFill = fill;
            activeSliderInput = valInput.GetComponent<TMP_InputField>();
            lastSliderText = activeSliderInput != null ? activeSliderInput.text : null;
            lastSliderFillX = fill.anchorMax.x;
            return true;
        }
        return false;
    }

    private static void TickActiveSlider() {
        if (activeSlider == null || !O5Input.GetMouseButton(0)) {
            ClearActiveSlider();
            return;
        }

        float now = Time.unscaledTime;
        if (now - lastSliderTickAt < SliderTickCooldown) {
            return;
        }

        if (activeUnitySlider != null) {
            float v = activeUnitySlider.normalizedValue;
            if (Mathf.Abs(v - lastUnitySliderVal) > 0.002f) {
                lastUnitySliderVal = v;
                lastSliderTickAt = now;
                PlayOneShot(sliderTickClip, 0.55f, RandPitch(Mathf.Lerp(0.86f, 1.32f, v)));
            }
            return;
        }

        float fillX = activeSliderFill != null ? Mathf.Clamp01(activeSliderFill.anchorMax.x) : 0.5f;
        bool changed;
        if (activeSliderInput != null) {
            // Only tick on real value text changes; Fill.anchorMax.x tweens smoothly over 0.6s.
            string txt = activeSliderInput.text;
            changed = !string.Equals(txt, lastSliderText, StringComparison.Ordinal);
            if (changed) {
                lastSliderText = txt;
            }
        } else {
            changed = Mathf.Abs(fillX - lastSliderFillX) > 0.002f;
        }
        if (changed) {
            lastSliderFillX = fillX;
            lastSliderTickAt = now;
            PlayOneShot(sliderTickClip, 0.55f, RandPitch(Mathf.Lerp(0.86f, 1.32f, fillX)));
        }
    }

    private static void ClearActiveSlider() {
        activeSlider = null;
        activeSliderFill = null;
        activeSliderInput = null;
        activeUnitySlider = null;
        lastSliderText = null;
    }

    private static void DestroyClip(ref AudioClip clip) {
        if (clip != null) {
            Object.Destroy(clip);
            clip = null;
        }
    }

    private static void PlayOneShot(AudioClip clip, float volumeScale, float pitch) {
        float master = MasterVolume;
        if (disposed || master <= 0.001f || clip == null && ready) {
            return;
        }
        EnsureReady();
        if (clip == null) {
            return;
        }
        var s = Sources[sourceCursor++ & (Sources.Length - 1)];
        if (s == null) {
            return;
        }
        s.pitch = pitch;
        s.PlayOneShot(clip, volumeScale * master);
    }

    private static void EnsureReady() {
        if (ready || disposed || MasterVolume <= 0.001f) {
            return;
        }
        ready = true;

        var parent = MainCore.Runtime != null && MainCore.Root != null ? MainCore.Root.transform : null;
        host = new GameObject("Overlayer.UISound");
        if (parent != null) {
            host.transform.SetParent(parent, false);
        } else {
            Object.DontDestroyOnLoad(host);
        }
        host.hideFlags = HideFlags.HideAndDontSave;

        for (int i = 0; i < Sources.Length; i++) {
            Sources[i] = CreateSource(host, loop: false, volume: 1.0f);
        }

        hoverClip = BuildHoverClip();
        clickClip = BuildClickClip();
        backClip = BuildBackClip();
        openClip = BuildPanelClip("MSFS_Open", 0.195f, opening: true, includeChime: true);
        closeClip = BuildPanelClip("MSFS_Close", 0.160f, opening: false, includeChime: true);
        menuOpenClip = BuildPanelClip("MSFS_MenuOpen", 0.180f, opening: true, includeChime: false);
        menuCloseClip = BuildPanelClip("MSFS_MenuClose", 0.150f, opening: false, includeChime: false);
        sliderTickClip = BuildSliderTickClip();
        ollyVoiceClip = BuildOllyVoiceClip();
        keyTypeClip = BuildKeyClip(backspace: false);
        backspaceClip = BuildKeyClip(backspace: true);
        inputSelectClip = BuildFocusClip(selected: true);
        inputDeselectClip = BuildFocusClip(selected: false);
        ddOpenClip = BuildDropdownToggleClip(opening: true);
        ddCloseClip = BuildDropdownToggleClip(opening: false);
        ddPickClip = BuildDropdownPickClip();
        fxOnClip = BuildFxToggleClip(enabled: true);
        fxOffClip = BuildFxToggleClip(enabled: false);
        toggleOnClip = BuildToggleClip(on: true);
        toggleOffClip = BuildToggleClip(on: false);
    }

    private static AudioSource CreateSource(GameObject go, bool loop, float volume) {
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.loop = loop;
        s.volume = volume;
        return s;
    }

    private static GameObject FindInteractive(Transform leaf) {
        Transform uiRoot = UICore.CanvasObj != null ? UICore.CanvasObj.transform : null;
        bool inUiCore = uiRoot != null && leaf.IsChildOf(uiRoot);
        if (!inUiCore && !Tutorial.Tutorial.Owns(leaf)) {
            return null;
        }

        for (Transform t = leaf; t != null && t != uiRoot; t = t.parent) {
            string n = t.name;
            if (n.IndexOf("Blocker", StringComparison.OrdinalIgnoreCase) >= 0) {
                return null;
            }
            if (t.GetComponent<Selectable>() is { } sel) {
                return sel.IsInteractable() ? t.gameObject : null;
            }
            if (t.GetComponent<NonRaycastButton>() != null || t.GetComponent<OventHandler>() != null) {
                return t.gameObject;
            }
            if (t.GetComponent<EventTrigger>() is { } et
                && t.GetComponent<DragHandler>() == null
                && t.GetComponent<ResizeHandle>() == null
                && t.GetComponent<ScrollRect>() == null) {
                foreach (var entry in et.triggers) {
                    if (entry.eventID is EventTriggerType.PointerEnter or EventTriggerType.PointerClick or EventTriggerType.PointerDown) {
                        return t.gameObject;
                    }
                }
            }
        }
        return null;
    }

    private static bool IsBackOrClose(string name) =>
        name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0;

    // ---- Procedural MSFS2020 + osu!lazer UI Audio Synthesis ----

    private static AudioClip BuildHoverClip() {
        // Preset C (57408080.wav @ 48kHz, 0.85x pitch): noise-excited dual crystal resonators
        // (10,153 Hz Q28 + 10,668 Hz Q12 shimmer) with zero sine carrier.
        const int sr = 48000;
        const float duration = 0.032f;
        int n = (int)(sr * duration);
        var data = new float[n];
        uint rng = 0x57408080u;

        double w0 = 2.0 * Math.PI * (11945.0 * 0.85) / sr;
        double al0 = Math.Sin(w0) / (2.0 * 28.0);
        double b0 = al0, b2 = -al0, a0 = 1.0 + al0, a1 = -2.0 * Math.Cos(w0), a2 = 1.0 - al0;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

        double w1 = 2.0 * Math.PI * (12550.0 * 0.85) / sr;
        double al1 = Math.Sin(w1) / (2.0 * 12.0);
        double b0s = al1, b2s = -al1, a0s = 1.0 + al1, a1s = -2.0 * Math.Cos(w1), a2s = 1.0 - al1;
        double sx1 = 0, sx2 = 0, sy1 = 0, sy2 = 0;

        float peak = 0f;
        for (int i = 0; i < n; i++) {
            float t = (float)i / sr;
            float p1 = 0.55f * Mathf.Exp(-Mathf.Pow((t - 0.0014f) / 0.0007f, 2f));
            float p2 = 1.00f * Mathf.Exp(-Mathf.Pow((t - 0.0038f) / 0.0015f, 2f));
            float p3 = 0.22f * Mathf.Exp(-Mathf.Pow((t - 0.0096f) / 0.0016f, 2f));
            float env = (p1 + p2 + p3) * (t < 0.0004f ? t / 0.0004f : 1f);

            double ns = NextNoise(ref rng) * env;
            double y = (b0 * ns + b2 * x2 - a1 * y1 - a2 * y2) / a0;
            x2 = x1; x1 = ns; y2 = y1; y1 = y;

            double sy = (b0s * ns + b2s * sx2 - a1s * sy1 - a2s * sy2) / a0s;
            sx2 = sx1; sx1 = ns; sy2 = sy1; sy1 = sy;

            float s = (float)(y * 2.6 + sy * (2.6 * 0.30));
            data[i] = s;
            if (Mathf.Abs(s) > peak) {
                peak = Mathf.Abs(s);
            }
        }

        float norm = peak > 0f ? (0.80f * 0.95f) / peak : 1f;
        for (int i = 0; i < n; i++) {
            data[i] = FinishSample(data[i] * norm, i, n, 0.004f, sr);
        }
        return CreateClip("MSFS_Hover", data, sr);
    }

    private static AudioClip BuildClickClip() {
        // Calibrated to ref.wav (@1.14s, @1.50s, @5.28s, @5.85s): dry matte 170-390Hz body + 730/1340Hz damped click.
        const float duration = 0.036f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0, ph3 = 0.0;
        float lp = 0f;
        uint rng = 0x2468ACE1u;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = 155f + 210f * Mathf.Exp(-t * 210f);
            float f2 = 390f + 340f * Mathf.Exp(-t * 240f);
            float f3 = 1340f * Mathf.Exp(-t * 45f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            ph3 += 2.0 * Math.PI * f3 / SampleRate;

            float e1 = (1f - Mathf.Exp(-t / 0.0012f)) * Mathf.Exp(-t / 0.0072f);
            float e2 = (1f - Mathf.Exp(-t / 0.0008f)) * Mathf.Exp(-t / 0.0048f);
            float e3 = (1f - Mathf.Exp(-t / 0.0006f)) * Mathf.Exp(-t / 0.0032f);
            float ns = NextNoise(ref rng);
            lp += 0.28f * (ns - lp);

            float s = Mathf.Sin((float)ph1) * e1 * 0.34f
                + Mathf.Sin((float)ph2) * e2 * 0.19f
                + Mathf.Sin((float)ph3) * e3 * 0.075f
                + lp * Mathf.Exp(-t / 0.0022f) * 0.07f;
            data[i] = FinishSample(s, i, n, 0.004f);
        }
        return CreateClip("MSFS_Click", data);
    }

    private static AudioClip BuildBackClip() {
        const float duration = 0.038f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = 125f + 145f * Mathf.Exp(-t * 180f);
            float f2 = 310f + 240f * Mathf.Exp(-t * 190f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            float e1 = (1f - Mathf.Exp(-t / 0.0013f)) * Mathf.Exp(-t / 0.0080f);
            float e2 = (1f - Mathf.Exp(-t / 0.0010f)) * Mathf.Exp(-t / 0.0052f);

            float s = Mathf.Sin((float)ph1) * e1 * 0.32f + Mathf.Sin((float)ph2) * e2 * 0.14f;
            data[i] = FinishSample(s, i, n, 0.004f);
        }
        return CreateClip("MSFS_Back", data);
    }

    private static AudioClip BuildSliderTickClip() {
        // Short matte notch pip pitched with slider fill.
        const float duration = 0.018f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = 290f + 230f * Mathf.Exp(-t * 280f);
            float f2 = 680f + 360f * Mathf.Exp(-t * 320f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            float env = (1f - Mathf.Exp(-t / 0.0007f)) * Mathf.Exp(-t / 0.0032f);
            float s = (Mathf.Sin((float)ph1) * 0.22f + Mathf.Sin((float)ph2) * 0.11f) * env;
            data[i] = FinishSample(s, i, n, 0.0025f);
        }
        return CreateClip("Lazer_SliderTick", data);
    }

    private static AudioClip BuildOllyVoiceClip() {
        // Soft visual-novel tok-tok blip: warm rounded 375->450Hz voice formant, zero harsh highs.
        const float duration = 0.036f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f = 375f + 75f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.018f));
            ph += 2.0 * Math.PI * f / SampleRate;
            float env = (1f - Mathf.Exp(-t / 0.0035f)) * Mathf.Exp(-t / 0.011f);
            float s = (Mathf.Sin((float)ph) * 0.85f + Mathf.Sin((float)(ph * 2.0)) * 0.15f) * env * 0.24f;
            data[i] = FinishSample(s, i, n, 0.006f);
        }
        return CreateClip("VN_OllyVoice", data);
    }

    private static AudioClip BuildKeyClip(bool backspace) {
        // Thocky + bouncy soft keycap tap (deeper pitch for Backspace).
        float duration = backspace ? 0.046f : 0.042f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = backspace ? 72f + 82f * Mathf.Exp(-t * 145f) : 95f + 115f * Mathf.Exp(-t * 165f);
            float f2 = backspace ? 310f + 140f * Mathf.Exp(-t * 180f) : 430f + 190f * Mathf.Exp(-t * 210f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            float e1 = (1f - Mathf.Exp(-t / (backspace ? 0.0014f : 0.0012f))) * Mathf.Exp(-t / (backspace ? 0.011f : 0.0095f));
            float e2 = (1f - Mathf.Exp(-t / (backspace ? 0.0010f : 0.0008f))) * Mathf.Exp(-t / (backspace ? 0.005f : 0.0042f));
            float s = Mathf.Sin((float)ph1) * e1 * (backspace ? 0.36f : 0.34f)
                + Mathf.Sin((float)ph2) * e2 * (backspace ? 0.11f : 0.13f);
            data[i] = FinishSample(s, i, n, 0.005f);
        }
        return CreateClip(backspace ? "UI_KeyBackspace" : "UI_KeyType", data);
    }

    private static AudioClip BuildFocusClip(bool selected) {
        const float duration = 0.032f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f = selected ? 240f + 180f * (1f - Mathf.Exp(-t * 120f)) : 350f - 140f * (1f - Mathf.Exp(-t * 120f));
            ph += 2.0 * Math.PI * f / SampleRate;
            float env = (1f - Mathf.Exp(-t / 0.0015f)) * Mathf.Exp(-t / 0.0085f);
            data[i] = FinishSample(Mathf.Sin((float)ph) * env * 0.24f, i, n, 0.005f);
        }
        return CreateClip(selected ? "UI_InputSelect" : "UI_InputDeselect", data);
    }

    private static AudioClip BuildDropdownToggleClip(bool opening) {
        // Tactile double-notch flip distinct from menu wind.
        const float duration = 0.045f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        float fA = opening ? 290f : 430f;
        float fB = opening ? 470f : 255f;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float tB = t - 0.012f;
            float eA = (1f - Mathf.Exp(-t / 0.001f)) * Mathf.Exp(-t / 0.0055f);
            float eB = tB > 0f ? (1f - Mathf.Exp(-tB / 0.001f)) * Mathf.Exp(-tB / 0.0075f) : 0f;
            float s = Mathf.Sin(2f * Mathf.PI * fA * t) * eA * 0.22f
                + (tB > 0f ? Mathf.Sin(2f * Mathf.PI * fB * tB) * eB * 0.24f : 0f);
            data[i] = FinishSample(s, i, n, 0.005f);
        }
        return CreateClip(opening ? "UI_DropdownOpen" : "UI_DropdownClose", data);
    }

    private static AudioClip BuildDropdownPickClip() {
        const float duration = 0.026f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f = 380f + 180f * Mathf.Exp(-t * 170f);
            ph += 2.0 * Math.PI * f / SampleRate;
            float env = (1f - Mathf.Exp(-t / 0.0008f)) * Mathf.Exp(-t / 0.0055f);
            data[i] = FinishSample(Mathf.Sin((float)ph) * env * 0.24f, i, n, 0.004f);
        }
        return CreateClip("UI_DropdownPick", data);
    }

    private static AudioClip BuildFxToggleClip(bool enabled) {
        // Distinct crystalline fifth double-spark for Fx toggles (not wind).
        const float duration = 0.065f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        float fA = enabled ? 523.25f : 783.99f;
        float fB = enabled ? 783.99f : 523.25f;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float tB = t - 0.014f;
            float eA = (1f - Mathf.Exp(-t / 0.002f)) * Mathf.Exp(-t / 0.016f);
            float eB = tB > 0f ? (1f - Mathf.Exp(-tB / 0.002f)) * Mathf.Exp(-tB / 0.020f) : 0f;
            float sA = (Mathf.Sin(2f * Mathf.PI * fA * t) + 0.22f * Mathf.Sin(4f * Mathf.PI * fA * t)) * eA * 0.18f;
            float sB = tB > 0f ? (Mathf.Sin(2f * Mathf.PI * fB * tB) + 0.20f * Mathf.Sin(4f * Mathf.PI * fB * tB)) * eB * 0.20f : 0f;
            data[i] = FinishSample(sA + sB, i, n, 0.008f);
        }
        return CreateClip(enabled ? "UI_FxOn" : "UI_FxOff", data);
    }

    private static AudioClip BuildToggleClip(bool on) {
        // Soft "dik / buk" bool toggle (higher crisp tap when ON, warmer lower thump when OFF).
        float duration = on ? 0.028f : 0.034f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = on ? 340f + 160f * Mathf.Exp(-t * 190f) : 135f + 115f * Mathf.Exp(-t * 165f);
            float f2 = on ? 760f + 220f * Mathf.Exp(-t * 240f) : 350f + 140f * Mathf.Exp(-t * 210f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            float e1 = (1f - Mathf.Exp(-t / (on ? 0.0010f : 0.0012f))) * Mathf.Exp(-t / (on ? 0.0065f : 0.0085f));
            float e2 = (1f - Mathf.Exp(-t / (on ? 0.0007f : 0.0009f))) * Mathf.Exp(-t / (on ? 0.0032f : 0.0040f));
            float s = Mathf.Sin((float)ph1) * e1 * (on ? 0.25f : 0.30f)
                + Mathf.Sin((float)ph2) * e2 * (on ? 0.11f : 0.08f);
            data[i] = FinishSample(s, i, n, 0.004f);
        }
        return CreateClip(on ? "UI_ToggleOn" : "UI_ToggleOff", data);
    }

    private static AudioClip BuildPanelClip(string name, float duration, bool opening, bool includeChime) {
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        float lp = 0f, lp2 = 0f, hp = 0f, prev = 0f;
        uint rng = opening ? 0x9E3779B9u : 0x85EBCA6Bu;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float u = t / duration;

            // Smooth satin air sweep (pure wind for Menu drawer)
            float cutoff = opening ? Mathf.Lerp(220f, 900f, u) : Mathf.Lerp(760f, 220f, u);
            float alpha = Mathf.Clamp01((2f * Mathf.PI * cutoff) / (SampleRate + 2f * Mathf.PI * cutoff));
            float noise = NextNoise(ref rng);
            lp += alpha * (noise - lp);
            lp2 += alpha * (lp - lp2);
            hp = 0.96f * (hp + lp2 - prev);
            prev = lp2;
            float airEnv = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.45f);
            float air = hp * airEnv * (opening ? 0.26f : 0.22f);

            float tone = 0f;
            if (includeChime) {
                tone = opening
                    ? BellTone(t, 0.000f, 293.66f, 0.015f, 0.048f, 0.036f) + BellTone(t, 0.018f, 440.00f, 0.015f, 0.055f, 0.030f)
                    : BellTone(t, 0.000f, 440.00f, 0.012f, 0.042f, 0.030f) + BellTone(t, 0.018f, 293.66f, 0.014f, 0.048f, 0.034f);
            }

            data[i] = FinishSample(air + tone, i, n, 0.006f);
        }
        return CreateClip(name, data);
    }

    private static float BellTone(float t, float delay, float freq, float attack, float decay, float gain) {
        float lt = t - delay;
        if (lt <= 0f) {
            return 0f;
        }
        float env = (1f - Mathf.Exp(-lt / attack)) * Mathf.Exp(-lt / decay);
        return Mathf.Sin(2f * Mathf.PI * freq * lt) * env * gain;
    }

    private static float FinishSample(float sample, int idx, int total, float tailFadeSec, int sampleRate = SampleRate) {
        int tailSamples = (int)(sampleRate * tailFadeSec);
        int rem = total - 1 - idx;
        if (rem < tailSamples && tailSamples > 0) {
            float k = (float)rem / tailSamples;
            sample *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * k);
        }
        return Mathf.Clamp(sample, -0.95f, 0.95f);
    }

    private static float NextNoise(ref uint state) {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return ((state & 0xFFFF) / 32768f) - 1f;
    }

    private static AudioClip CreateClip(string name, float[] dry, int sr = SampleRate) {
        float[] wet = ApplyWideRoomReverb(dry, sr, ReverbWetMix);
        var clip = AudioClip.Create(name, wet.Length, 1, sr, false);
        clip.SetData(wet, 0);
        return clip;
    }

    private static float[] ApplyWideRoomReverb(float[] dry, int sr, float wetMix) {
        int tailSamples = (int)(sr * 0.42f);
        int total = dry.Length + tailSamples;
        var output = new float[total];

        // 5 early reflection taps + 4 parallel damped combs + 2 series allpass diffusers
        int[] erDelay = { (int)(0.011f * sr), (int)(0.023f * sr), (int)(0.037f * sr), (int)(0.053f * sr), (int)(0.071f * sr) };
        float[] erGain = { 0.32f, -0.26f, 0.21f, -0.16f, 0.12f };

        int[] cLen = { (int)(0.0297f * sr), (int)(0.0371f * sr), (int)(0.0411f * sr), (int)(0.0437f * sr) };
        var combs = new float[4][];
        var cIdx = new int[4];
        var cLp = new float[4];
        for (int c = 0; c < 4; c++) {
            combs[c] = new float[cLen[c]];
        }

        int[] apLen = { (int)(0.0050f * sr), (int)(0.0017f * sr) };
        var aps = new float[2][];
        var apIdx = new int[2];
        for (int a = 0; a < 2; a++) {
            aps[a] = new float[apLen[a]];
        }

        for (int i = 0; i < total; i++) {
            float x = i < dry.Length ? dry[i] : 0f;
            float er = 0f;
            for (int e = 0; e < erDelay.Length; e++) {
                int srcIdx = i - erDelay[e];
                if (srcIdx >= 0 && srcIdx < dry.Length) {
                    er += dry[srcIdx] * erGain[e];
                }
            }

            float combSum = 0f;
            for (int c = 0; c < 4; c++) {
                float[] buf = combs[c];
                int idx = cIdx[c];
                float delayed = buf[idx];
                cLp[c] = delayed * 0.65f + cLp[c] * 0.35f;
                buf[idx] = x + cLp[c] * 0.76f;
                cIdx[c] = idx + 1 < buf.Length ? idx + 1 : 0;
                combSum += delayed;
            }

            float v = combSum * 0.18f + er * 0.45f;
            for (int a = 0; a < 2; a++) {
                float[] buf = aps[a];
                int idx = apIdx[a];
                float bufOut = buf[idx];
                float apOut = -v + bufOut;
                buf[idx] = v + bufOut * 0.5f;
                apIdx[a] = idx + 1 < buf.Length ? idx + 1 : 0;
                v = apOut;
            }

            // 1.5x clip headroom so default 0.40 master matches prior 0.60 source gain,
            // and 1.00 master reaches full 2.5x loudness without exceeding PlayOneShot 1.0.
            output[i] = FinishSample((x + v * wetMix) * 1.5f, i, total, 0.025f, sr);
        }
        return output;
    }
}
