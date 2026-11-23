using System.Reflection;
using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Eval;
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
    private const float ReverbWetMix = 0.05f;
    private const float GlobalPitchScale = 1.4f;
    private const float ClipMasterGain = 3.6f;

    private enum ClipId {
        Click,
        Back,
        Open,
        Close,
        MenuOpen,
        MenuClose,
        SliderTick,
        OllyVoice,
        KeyType,
        Backspace,
        InputSelect,
        InputDeselect,
        DdOpen,
        DdClose,
        DdPick,
        Default,
        FxOn,
        FxOff,
        ToggleOn,
        ToggleOff,
        EvalOk,
        EvalSame,
        EvalErr,
        Count,
    }

    private static GameObject host;
    private static readonly AudioSource[] Sources = new AudioSource[4];
    private static readonly AudioClip[] Clips = new AudioClip[(int)ClipId.Count];
    private static int sourceCursor;

    private static bool ready;
    private static bool disposed;
    private static AudioSettings.AudioConfigurationChangeHandler audioConfigHandler;
    private static volatile bool audioConfigDirty;
    private static float nextRetryAt;
    private static ClipId lastTransitionClip;
    private static float lastTransitionAt = -10f;

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

    // shortcut: reads private O5Slider._hovered / O5Object._tickables via reflection to stay out of O5Kit repo, replace with O5UiEvents hooks if O5Kit adds them.
    private static readonly FieldInfo HoveredSliderField =
        typeof(O5Slider).GetField("_hovered", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo TickablesField =
        typeof(O5Object).GetField("_tickables", BindingFlags.Static | BindingFlags.NonPublic);
    private static O5Slider lastHoveredSlider;
    private static double lastHoveredSliderValue;

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
    private static int lastInputAnchor;
    private static int lastInputFocus;
    private static O5Slider lastFocusedSlider;
    private static double lastFocusedSliderBaseValue;
    private static EvalState? lastSliderEvalState;

    private static float MasterVolume => MainCore.Conf != null ? Mathf.Clamp01(MainCore.Conf.UISoundVolume) : 0.4f;

    public static void Tick() {
        if (disposed) {
            return;
        }
        HoldToConfirm.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));

        if (MasterVolume <= 0.001f) {
            lastHover = null;
            lastHoveredSlider = null;
            ClearFocusedInputState();
            ClearActiveSlider();
            return;
        }

        bool uiVisible = (UICore.CanvasObj != null && UICore.CanvasObj.activeInHierarchy) || Tutorial.Tutorial.IsRunning;
        if (!uiVisible) {
            lastHover = null;
            lastHoveredSlider = null;
            ClearFocusedInputState();
            ClearActiveSlider();
            return;
        }

        TickActiveSlider();

        var es = EventSystem.current;
        if (es == null) {
            lastHover = null;
            lastHoveredSlider = null;
            ClearFocusedInputState();
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
                PlayOneShot(isBack ? ClipId.Back : ClipId.Click, 1.0f, RandPitch(1f));
            }
        }

        bool resetHandled = false;
        if (current != null && O5Input.GetMouseButtonDown(2)
            && MainCore.Conf != null && MainCore.Conf.MiddleClickToDefault.Value) {
            if (IsSliderTarget(current)) {
                resetHandled = TryPlaySliderReset(current);
            } else {
                resetHandled = TryPlayValueReset(current);
            }
        }

        if (!resetHandled) {
            var hoveredSlider = GetHoveredSlider();
            if (hoveredSlider != null && !hoveredSlider.IsDisposed) {
                lastHoveredSlider = hoveredSlider;
                lastHoveredSliderValue = hoveredSlider.Value;
            } else {
                lastHoveredSlider = null;
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
        PlayOneShot(ClipId.SliderTick, 0.59f, RandPitch(0.6f));
    }

    public static void PlayOpen() => PlayTransition(ClipId.Open, 0.92f);
    public static void PlayClose() => PlayTransition(ClipId.Close, 0.89f);
    public static void PlayMenuOpen() => PlayTransition(ClipId.MenuOpen, 0.96f);
    public static void PlayMenuClose() => PlayTransition(ClipId.MenuClose, 0.94f);
    public static void PlayConfirm() => PlayOneShot(ClipId.Click, 1.0f, RandPitch(1f));
    public static void PlayFxToggle(bool enabled) => PlayOneShot(enabled ? ClipId.FxOn : ClipId.FxOff, 0.92f, RandPitch(1f));
    public static void PlayToggle(bool on) => PlayOneShot(on ? ClipId.ToggleOn : ClipId.ToggleOff, 0.94f, RandPitch(1f));
    public static void PlayDropdownToggle(bool expanded) => PlayOneShot(expanded ? ClipId.DdOpen : ClipId.DdClose, 0.92f, RandPitch(1f));
    public static void PlayDropdownPick() => PlayOneShot(ClipId.DdPick, 0.89f, RandPitch(1f));
    public static void PlayDefault() => PlayOneShot(ClipId.Default, 0.92f, RandPitch(1f));

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
        PlayOneShot(ClipId.OllyVoice, 0.80f, RandPitch(1.02f, 0.09f));
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
        PlayOneShot(ClipId.SliderTick, 0.65f, RandPitch(Mathf.Lerp(0.86f, 1.32f, Mathf.Clamp01(normalized))));
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
            PlayOneShot(ClipId.SliderTick, 0.65f, RandPitch(Mathf.Lerp(0.86f, 1.32f, p)));
        }
    }

    public static void Dispose() {
        disposed = true;
        if (audioConfigHandler != null) {
            try {
                AudioSettings.OnAudioConfigurationChanged -= audioConfigHandler;
            } catch {
            }
            audioConfigHandler = null;
        }
        audioConfigDirty = false;
        nextRetryAt = 0f;
        lastTransitionAt = -10f;

        lastHover = null;
        lastHoveredSlider = null;
        ClearActiveSlider();
        HoldToConfirm.ClearAll();
        cachedEs = null;
        pointerData = null;
        Hits.Clear();

        DestroyAllClips();

        if (host != null) {
            Object.Destroy(host);
            host = null;
        }
        Array.Clear(Sources, 0, Sources.Length);
        ClearFocusedInputState();
        ready = false;
    }

    private static void ClearFocusedInputState() {
        lastFocusedInput = null;
        lastInputText = null;
        lastInputAnchor = 0;
        lastInputFocus = 0;
        lastFocusedSlider = null;
        lastSliderEvalState = null;
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
                PlayOneShot(ClipId.InputDeselect, 0.85f, RandPitch(1f));
            }
            if (cur == null) {
                ClearFocusedInputState();
                return;
            }
            PlayOneShot(ClipId.InputSelect, 0.89f, RandPitch(1f));
            lastFocusedInput = cur;
            lastInputText = cur.text ?? string.Empty;
            lastInputAnchor = cur.selectionAnchorPosition;
            lastInputFocus = cur.selectionFocusPosition;
            lastFocusedSlider = FindSliderForInput(cur);
            if (lastFocusedSlider != null) {
                lastFocusedSliderBaseValue = lastFocusedSlider.Value;
                lastSliderEvalState = ComputeSliderEvalState(lastFocusedSlider, lastInputText);
            } else {
                lastSliderEvalState = null;
            }
            return;
        }

        if (cur == null) {
            return;
        }

        if (lastFocusedSlider != null) {
            if (lastFocusedSlider.IsDisposed) {
                lastFocusedSlider = null;
                lastSliderEvalState = null;
            } else if (Math.Abs(lastFocusedSlider.Value - lastFocusedSliderBaseValue) > 1e-12) {
                lastFocusedSliderBaseValue = lastFocusedSlider.Value;
                lastInputText = cur.text ?? string.Empty;
                lastInputAnchor = cur.selectionAnchorPosition;
                lastInputFocus = cur.selectionFocusPosition;
                lastSliderEvalState = ComputeSliderEvalState(lastFocusedSlider, lastInputText);
                return;
            }
        }

        string txt = cur.text ?? string.Empty;
        string prev = lastInputText ?? string.Empty;
        int anchor = cur.selectionAnchorPosition;
        int focus = cur.selectionFocusPosition;
        bool backKey = O5Input.GetKey(KeyCode.Backspace) || O5Input.GetKey(KeyCode.Delete);

        if (!string.Equals(txt, prev, StringComparison.Ordinal)) {
            bool isErase = backKey || txt.Length < prev.Length;
            lastInputText = txt;
            lastInputAnchor = anchor;
            lastInputFocus = focus;
            if (now - lastKeyAt >= KeyCooldown) {
                lastKeyAt = now;
                PlayOneShot(isErase ? ClipId.Backspace : ClipId.KeyType, 0.92f, RandPitch(1f, 0.085f));
            }
            if (lastFocusedSlider != null) {
                EvalState state = ComputeSliderEvalState(lastFocusedSlider, txt);
                if (lastSliderEvalState != state) {
                    lastSliderEvalState = state;
                    switch (state) {
                        case EvalState.Error:
                            PlayOneShot(ClipId.EvalErr, 0.90f, 1f);
                            break;
                        case EvalState.Ok:
                            PlayOneShot(ClipId.EvalOk, 0.90f, 1f);
                            break;
                        case EvalState.Same:
                            PlayOneShot(ClipId.EvalSame, 0.90f, 1f);
                            break;
                    }
                }
            }
        } else if (O5Input.GetKeyDown(KeyCode.Backspace) && now - lastKeyAt >= KeyCooldown) {
            lastKeyAt = now;
            PlayOneShot(ClipId.Backspace, 0.65f, RandPitch(1f, 0.085f));
        } else if (focus != lastInputFocus || anchor != lastInputAnchor) {
            bool leftNav = O5Input.GetKey(KeyCode.LeftArrow) || O5Input.GetKey(KeyCode.Home);
            bool rightNav = O5Input.GetKey(KeyCode.RightArrow) || O5Input.GetKey(KeyCode.End);
            if (!O5Input.GetMouseButton(0) && (leftNav || rightNav) && now - lastKeyAt >= KeyCooldown) {
                bool movedRight = focus != lastInputFocus ? focus > lastInputFocus : anchor > lastInputAnchor;
                lastKeyAt = now;
                PlayOneShot(ClipId.SliderTick, 0.68f, RandPitch(movedRight ? 1.02f : 0.78f, 0.02f));
            }
            lastInputAnchor = anchor;
            lastInputFocus = focus;
        }
    }

    private static O5Slider FindSliderForInput(TMP_InputField input) {
        if (input == null || !string.Equals(input.gameObject.name, "ValueInput", StringComparison.Ordinal)) {
            return null;
        }
        var hovered = GetHoveredSlider();
        if (hovered != null && !hovered.IsDisposed && hovered.InputCore?.InputField == input) {
            return hovered;
        }
        if (TickablesField?.GetValue(null) is System.Collections.IList list) {
            for (int i = 0; i < list.Count; i++) {
                if (list[i] is O5Slider s && !s.IsDisposed && s.InputCore?.InputField == input) {
                    return s;
                }
            }
        }
        return null;
    }

    private static EvalState ComputeSliderEvalState(O5Slider slider, string val) {
        var (rawResult, rawState) = Evaluator<double>.Evaluate(val, slider.Value);
        EvalState previewState = rawState;
        if (rawState != EvalState.Error && slider.ClampInput) {
            double previewMin = Math.Max(slider.Min, slider.EffectiveInputMin);
            double previewMax = Math.Min(slider.Max, slider.EffectiveInputMax);
            if (previewMin > previewMax) {
                previewMin = slider.EffectiveInputMin;
                previewMax = slider.EffectiveInputMax;
            }
            if (rawResult < previewMin) {
                previewState = EvalState.UnderRange;
            } else if (rawResult > previewMax) {
                previewState = EvalState.OverRange;
            }
        }
        return previewState;
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
        return (UICore.MenuContent != null && p == UICore.MenuContent)
            || (go.name == "Bg" && (p.name == "Dropdown" || p.name == "MultiDropdown"))
            || (go.name == "Row" && p.name == "List");
    }

    private static bool IsSliderTarget(GameObject go) =>
        go != null && (go.GetComponent<Slider>() != null
            || (go.transform.Find("Fill") is RectTransform && go.transform.Find("ValueInput") != null));

    private static O5Slider GetHoveredSlider() =>
        HoveredSliderField?.GetValue(null) as O5Slider;

    private static bool TryPlaySliderReset(GameObject current) {
        if (HoveredSliderField == null) {
            var fill = current.transform.Find("Fill") as RectTransform;
            PlaySliderTick(fill != null ? Mathf.Clamp01(fill.anchorMax.x) : 0.5f);
            return false;
        }
        var slider = GetHoveredSlider();
        if (slider == null || slider.IsDisposed || slider.Rect == null || slider.Rect.gameObject != current) {
            return false;
        }
        if (!slider.DefaultValue.HasValue) {
            return false;
        }
        double target = slider.DefaultValue.Value;
        if (double.IsNaN(target)) {
            return false;
        }
        if (slider.DragFilter != null) {
            target = slider.DragFilter(target);
        }
        if (slider.ClampDrag) {
            if (target < slider.Min) {
                target = slider.Min;
            } else if (target > slider.Max) {
                target = slider.Max;
            }
        }
        bool changed = Math.Abs(slider.Value - target) > 1e-6
            || (ReferenceEquals(lastHoveredSlider, slider) && Math.Abs(lastHoveredSliderValue - target) > 1e-6)
            || (!ReferenceEquals(lastHoveredSlider, slider) && slider.ChangedImage != null && slider.ChangedImage.color.a > 0.01f);
        lastHoveredSlider = slider;
        lastHoveredSliderValue = target;
        if (changed) {
            PlaySliderTick((float)slider.Normalize(target));
        }
        return true;
    }

    // Middle-click default sound for value controls (toggle/bool, dropdowns, segmented,
    // input, color picker): plays only when the value actually differs from default,
    // so plain buttons and already-default controls stay silent.
    private static bool TryPlayValueReset(GameObject current) {
        var control = FindValueControl(current);
        if (control == null) {
            return false;
        }
        var type = control.GetType();
        if (type.GetProperty("HasDefault")?.GetValue(control) is bool hasDefault && !hasDefault) {
            return true;
        }
        var defProp = type.GetProperty("DefaultValue");
        if (defProp == null) {
            return true;
        }
        object def = defProp.GetValue(control);
        if (def == null) {
            return true;
        }
        object val = type.GetProperty("Value")?.GetValue(control);
        if (!Equals(val, def)) {
            PlayDefault();
        }
        return true;
    }

    private static O5Object FindValueControl(GameObject current) {
        if (TickablesField?.GetValue(null) is not System.Collections.IList list) {
            return null;
        }
        O5Object best = null;
        int bestDepth = -1;
        Transform curT = current.transform;
        foreach (object item in list) {
            if (item is not O5Object o || o.IsDisposed || o.Rect == null || !o.Rect) {
                continue;
            }
            if (o is O5Slider) {
                continue;
            }
            RectTransform r = o.Rect;
            if (r.gameObject != current && !curT.IsChildOf(r)) {
                continue;
            }
            // Dropdown list rows are buttons, not reset targets.
            if (o.GetType().GetProperty("ListObject")?.GetValue(o) is GameObject listObj && listObj != null) {
                Transform lt = listObj.transform;
                if (curT == lt || curT.IsChildOf(lt)) {
                    continue;
                }
            }
            int depth = 0;
            for (Transform t = r.transform; t != null; t = t.parent) {
                depth++;
            }
            if (depth > bestDepth) {
                bestDepth = depth;
                best = o;
            }
        }
        return best;
    }

    private static void PlayTransition(ClipId clip, float volumeScale) {
        if (disposed) {
            return;
        }
        float now = Time.unscaledTime;
        if (lastTransitionClip == clip && now - lastTransitionAt < 0.04f) {
            return;
        }
        lastTransitionClip = clip;
        lastTransitionAt = now;
        suppressHoverUntil = now + 0.14f;
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
                PlayOneShot(ClipId.SliderTick, 0.65f, RandPitch(Mathf.Lerp(0.86f, 1.32f, v)));
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
            PlayOneShot(ClipId.SliderTick, 0.65f, RandPitch(Mathf.Lerp(0.86f, 1.32f, fillX)));
        }
    }

    private static void ClearActiveSlider() {
        activeSlider = null;
        activeSliderFill = null;
        activeSliderInput = null;
        activeUnitySlider = null;
        lastSliderText = null;
    }

    private static void DestroyAllClips() {
        for (int i = 0; i < Clips.Length; i++) {
            if (Clips[i] != null) {
                Object.Destroy(Clips[i]);
                Clips[i] = null;
            }
        }
    }

    private static void BuildAllClips() {
        Clips[(int)ClipId.Click] = BuildClickClip();
        Clips[(int)ClipId.Back] = BuildBackClip();
        Clips[(int)ClipId.Open] = BuildPanelClip("MSFS_Open", 0.195f, opening: true);
        Clips[(int)ClipId.Close] = BuildPanelClip("MSFS_Close", 0.160f, opening: false);
        Clips[(int)ClipId.MenuOpen] = BuildPanelClip("MSFS_MenuOpen", 0.180f, opening: true);
        Clips[(int)ClipId.MenuClose] = BuildPanelClip("MSFS_MenuClose", 0.150f, opening: false);
        Clips[(int)ClipId.SliderTick] = BuildSliderTickClip();
        Clips[(int)ClipId.OllyVoice] = BuildOllyVoiceClip();
        Clips[(int)ClipId.KeyType] = BuildKeyClip(backspace: false);
        Clips[(int)ClipId.Backspace] = BuildKeyClip(backspace: true);
        Clips[(int)ClipId.InputSelect] = BuildFocusClip(selected: true);
        Clips[(int)ClipId.InputDeselect] = BuildFocusClip(selected: false);
        Clips[(int)ClipId.DdOpen] = BuildDropdownToggleClip(opening: true);
        Clips[(int)ClipId.DdClose] = BuildDropdownToggleClip(opening: false);
        Clips[(int)ClipId.DdPick] = BuildDropdownPickClip();
        Clips[(int)ClipId.Default] = BuildDefaultClip();
        Clips[(int)ClipId.FxOn] = BuildFxToggleClip(enabled: true);
        Clips[(int)ClipId.FxOff] = BuildFxToggleClip(enabled: false);
        Clips[(int)ClipId.ToggleOn] = BuildToggleClip(on: true);
        Clips[(int)ClipId.ToggleOff] = BuildToggleClip(on: false);
        Clips[(int)ClipId.EvalOk] = BuildEvalClip("UI_EvalOk", 261.63f, doubleTap: false);
        Clips[(int)ClipId.EvalSame] = BuildEvalClip("UI_EvalSame", 261.63f, doubleTap: true);
        Clips[(int)ClipId.EvalErr] = BuildEvalClip("UI_EvalErr", 392.00f, doubleTap: true);
    }

    private static void OnAudioConfigurationChanged(bool deviceWasChanged) => audioConfigDirty = true;

    private static void PlayOneShot(ClipId id, float volumeScale, float pitch) {
        float master = MasterVolume;
        if (disposed || master <= 0.001f) {
            return;
        }
        EnsureReady();
        var clip = Clips[(int)id];
        if (clip == null) {
            return;
        }
        var s = Sources[sourceCursor++ & (Sources.Length - 1)];
        if (s == null || !s.isActiveAndEnabled) {
            return;
        }
        float pitchScale = id switch {
            ClipId.DdOpen or ClipId.DdClose or ClipId.DdPick or ClipId.OllyVoice => 1f,
            ClipId.Click => GlobalPitchScale * 1.4f,
            _ => GlobalPitchScale,
        };
        s.pitch = pitch * pitchScale;
        s.PlayOneShot(clip, volumeScale * master);
    }

    private static void EnsureReady() {
        if (disposed || MasterVolume <= 0.001f) {
            return;
        }
        bool configChanged = audioConfigDirty;
        if (configChanged) {
            audioConfigDirty = false;
        }
        bool hostAlive = !configChanged && host != null && host.activeInHierarchy;
        if (hostAlive) {
            for (int i = 0; i < Sources.Length; i++) {
                if (Sources[i] == null || !Sources[i].enabled) {
                    hostAlive = false;
                    break;
                }
            }
        }
        bool clipsAlive = !configChanged;
        if (clipsAlive) {
            for (int i = 0; i < Clips.Length; i++) {
                if (Clips[i] == null) {
                    clipsAlive = false;
                    break;
                }
            }
        }
        if (ready && hostAlive && clipsAlive) {
            return;
        }
        float now = Time.unscaledTime;
        if (!configChanged && now < nextRetryAt) {
            return;
        }
        if (audioConfigHandler == null) {
            try {
#if ML && IL2CPP
                audioConfigHandler = new Action<bool>(OnAudioConfigurationChanged);
#else
                audioConfigHandler = OnAudioConfigurationChanged;
#endif
                AudioSettings.OnAudioConfigurationChanged += audioConfigHandler;
            } catch {
                audioConfigHandler = null;
            }
        }
        ready = true;
        try {
            // A scene wipe, inactive root, or audio engine reset (AudioSettings.Reset at
            // startup / device change) invalidates Sources or wipes procedural clip PCM data.
            if (!hostAlive) {
                if (host != null) {
                    Object.Destroy(host);
                    host = null;
                }
                Array.Clear(Sources, 0, Sources.Length);

                var parent = MainCore.Runtime != null && MainCore.Root != null && MainCore.Root.activeInHierarchy
                    ? MainCore.Root.transform
                    : null;
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
            }

            if (!clipsAlive) {
                DestroyAllClips();
                BuildAllClips();
            }

            bool allOk = host != null && host.activeInHierarchy;
            if (allOk) {
                for (int i = 0; i < Sources.Length; i++) {
                    if (Sources[i] == null || !Sources[i].enabled) {
                        allOk = false;
                        break;
                    }
                }
            }
            if (allOk) {
                for (int i = 0; i < Clips.Length; i++) {
                    if (Clips[i] == null) {
                        allOk = false;
                        break;
                    }
                }
            }
            if (allOk) {
                nextRetryAt = 0f;
            } else {
                ready = false;
                nextRetryAt = now + 1f;
            }
        } catch (Exception e) {
            ready = false;
            nextRetryAt = now + 1f;
            try {
                MainCore.Log.Wrn($"[UISound] Could not create UI audio: {e.Message}");
            } catch {
            }
        }
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

    private static AudioClip BuildClickClip() {
        // Muffled matte dual-sine thock matching the damped timbre of Back / Key / Toggle (no bright highs or noise snap).
        const float duration = 0.038f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float f1 = 140f + 125f * Mathf.Exp(-t * 170f);
            float f2 = 285f + 145f * Mathf.Exp(-t * 195f);
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;

            float e1 = (1f - Mathf.Exp(-t / 0.0018f)) * Mathf.Exp(-t / 0.0082f);
            float e2 = (1f - Mathf.Exp(-t / 0.0014f)) * Mathf.Exp(-t / 0.0045f);

            float s = Mathf.Sin((float)ph1) * e1 * 0.44f + Mathf.Sin((float)ph2) * e2 * 0.11f;
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

    private static AudioClip BuildDefaultClip() {
        // Soft two-note drop for "back to default" (middle click): D5 -> A4, dry and short.
        const float duration = 0.058f;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        double ph1 = 0.0, ph2 = 0.0;
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float tB = t - 0.020f;
            float e1 = (1f - Mathf.Exp(-t / 0.0012f)) * Mathf.Exp(-t / 0.010f);
            float e2 = tB > 0f ? (1f - Mathf.Exp(-tB / 0.0012f)) * Mathf.Exp(-tB / 0.012f) : 0f;
            ph1 += 2.0 * Math.PI * 587.33 / SampleRate;
            if (tB > 0f) {
                ph2 += 2.0 * Math.PI * 440.0 / SampleRate;
            }
            float s = Mathf.Sin((float)ph1) * e1 * 0.20f
                + (tB > 0f ? Mathf.Sin((float)ph2) * e2 * 0.18f : 0f);
            data[i] = FinishSample(s, i, n, 0.006f);
        }
        return CreateClip("UI_Default", data);
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

    private static AudioClip BuildEvalClip(string name, float heardFreq, bool doubleTap) {
        // Heavy matte damped thock matching Click / Back / Key timbre (C4 = Do, G4 = Sol after GlobalPitchScale).
        float realDuration = doubleTap ? 0.115f : 0.058f;
        float duration = realDuration * GlobalPitchScale;
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        float fBase = heardFreq / GlobalPitchScale;
        double ph1 = 0.0, ph2 = 0.0, ph1b = 0.0, ph2b = 0.0;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float rt = t / GlobalPitchScale;
            float f1 = fBase * (1f + 0.22f * Mathf.Exp(-rt * 260f));
            float f2 = fBase * 2f * (1f + 0.15f * Mathf.Exp(-rt * 300f));
            ph1 += 2.0 * Math.PI * f1 / SampleRate;
            ph2 += 2.0 * Math.PI * f2 / SampleRate;
            float e1 = (1f - Mathf.Exp(-rt / 0.0020f)) * Mathf.Exp(-rt / 0.0115f);
            float e2 = (1f - Mathf.Exp(-rt / 0.0014f)) * Mathf.Exp(-rt / 0.0045f);
            float s = Mathf.Sin((float)ph1) * e1 * 0.38f + Mathf.Sin((float)ph2) * e2 * 0.06f;

            float rt2 = rt - 0.058f;
            if (doubleTap && rt2 > 0f) {
                float f1b = fBase * (1f + 0.22f * Mathf.Exp(-rt2 * 260f));
                float f2b = fBase * 2f * (1f + 0.15f * Mathf.Exp(-rt2 * 300f));
                ph1b += 2.0 * Math.PI * f1b / SampleRate;
                ph2b += 2.0 * Math.PI * f2b / SampleRate;
                float e1b = (1f - Mathf.Exp(-rt2 / 0.0020f)) * Mathf.Exp(-rt2 / 0.0115f);
                float e2b = (1f - Mathf.Exp(-rt2 / 0.0014f)) * Mathf.Exp(-rt2 / 0.0045f);
                s += Mathf.Sin((float)ph1b) * e1b * 0.38f + Mathf.Sin((float)ph2b) * e2b * 0.06f;
            }
            data[i] = FinishSample(s, i, n, 0.005f);
        }
        return CreateClip(name, data);
    }

    private static AudioClip BuildPanelClip(string name, float duration, bool opening) {
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        float lp = 0f, lp2 = 0f, hp = 0f, prev = 0f;
        uint rng = opening ? 0x9E3779B9u : 0x85EBCA6Bu;

        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float u = t / duration;

            // Smooth satin air sweep
            float cutoff = opening ? Mathf.Lerp(220f, 900f, u) : Mathf.Lerp(760f, 220f, u);
            float alpha = Mathf.Clamp01((2f * Mathf.PI * cutoff) / (SampleRate + 2f * Mathf.PI * cutoff));
            float noise = NextNoise(ref rng);
            lp += alpha * (noise - lp);
            lp2 += alpha * (lp - lp2);
            hp = 0.96f * (hp + lp2 - prev);
            prev = lp2;
            float airEnv = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.45f);
            float air = hp * airEnv * (opening ? 0.26f : 0.22f);

            data[i] = FinishSample(air, i, n, 0.006f);
        }
        return CreateClip(name, data);
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
        if (clip == null) {
            return null;
        }
        clip.hideFlags = HideFlags.HideAndDontSave;
        if (!clip.SetData(wet, 0)) {
            Object.Destroy(clip);
            return null;
        }
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

            // Master clips to full scale (loudest clip peaks ~0.94) so volume 1.0 outputs full loudness without clipping.
            output[i] = FinishSample((x + v * wetMix) * ClipMasterGain, i, total, 0.025f, sr);
        }
        return output;
    }
}
