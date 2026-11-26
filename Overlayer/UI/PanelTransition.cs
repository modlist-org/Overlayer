using O5Kit.Core;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI;

/// <summary>Panel open/close transitions other than <see cref="PanelFadeMode.FlyIn"/>.</summary>
internal static class PanelTransition {
    // Unit vector from top-left to bottom-right (UI space, y up).
    private static readonly Vector2 Diagonal = new(0.70710678f, -0.70710678f);

    private static ITweenHandle tween;
    private static RectTransform wipe;
    private static Vector3 wipePanelWorld;
    private static readonly List<(RectMask2D Rect, RectTransform Proxy, List<Transform> Children)> stencilSwaps = new();

    /// <summary>True while a transition is running (panel off its resting state).</summary>
    public static bool IsPlaying => tween != null && tween.IsAlive;

    /// <summary>Configured main window opacity (0.2-1).</summary>
    public static float Opacity => Mathf.Clamp(MainCore.Conf.WindowOpacity.Value, 0.2f, 1f);

    /// <summary>Applies <see cref="Opacity"/> to the panel unless a transition owns its alpha.</summary>
    public static void ApplyOpacity() {
        RectTransform panel = UICore.Panel;
        if (panel != null && !IsPlaying) {
            Group(panel).alpha = Opacity;
        }
    }

    /// <summary>Stops any running transition and restores the panel's neutral state (no wipe parent, scale 1, no rotation, alpha 1).</summary>
    public static void Kill() {
        tween?.Kill();
        tween = null;
        Restore();
    }

    /// <summary>Plays <paramref name="mode"/> in place around <paramref name="home"/>.</summary>
    /// <param name="mode">Transition style. FlyIn is handled by UICore.</param>
    /// <param name="show">True to appear, false to disappear.</param>
    /// <param name="home">Panel's resting anchoredPosition.</param>
    /// <param name="done">Runs after the transition ends (after the panel is restored when showing, before when hiding).</param>
    public static void Play(PanelFadeMode mode, bool show, Vector2 home, System.Action done) {
        Kill();
        RectTransform panel = UICore.Panel;
        panel.anchoredPosition = home;

        float duration;
        O5Ease ease;
        System.Action<float> apply;
        switch (mode) {
            case PanelFadeMode.DiagonalSwipe:
                BeginWipe(panel, show);
                apply = v => ApplyWipe(panel, home, show, v);
                duration = 0.3f;
                ease = O5Ease.InOutCubic;
                break;
            case PanelFadeMode.Taskbar: {
                Vector2 dock = new(0f, -CanvasHalf().y);
                apply = v => {
                    panel.anchoredPosition = Vector2.LerpUnclamped(dock, home, v);
                    panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.05f, 1f, v);
                    Group(panel).alpha = Mathf.Clamp01(v * 3f) * Opacity;
                };
                duration = 0.2f;
                ease = show ? O5Ease.OutCubic : O5Ease.InCubic;
                break;
            }
            case PanelFadeMode.Clock: {
                // Pivot: center of a screen-sized space directly left of the screen.
                Vector2 pivot = new(-CanvasHalf().x * 2f, 0f);
                Vector2 arm = home - pivot;
                BeginStencilClip(panel);
                apply = v => {
                    float angle = (1f - v) * 90f; // counter-clockwise when hiding
                    float rad = angle * 0.017453292f;
                    float c = Mathf.Cos(rad);
                    float s = Mathf.Sin(rad);
                    panel.anchoredPosition = pivot + new Vector2(arm.x * c - arm.y * s, arm.x * s + arm.y * c);
                    panel.localRotation = Quaternion.Euler(0f, 0f, angle);
                };
                duration = 0.22f;
                ease = show ? O5Ease.OutCubic : O5Ease.InCubic;
                break;
            }
            default:
                apply = v => Group(panel).alpha = v * Opacity;
                duration = 0.18f;
                ease = O5Ease.OutSine;
                break;
        }

        apply(show ? 0f : 1f);
        tween = O5KitAdapters.Ctx.Tween.TweenFloat(
            () => 0f,
            t => {
                if (panel) {
                    apply(show ? t : 1f - t);
                }
            },
            1f,
            duration,
            () => {
                tween = null;
                if (!show) {
                    done?.Invoke();
                }
                Restore();
                if (panel) {
                    panel.anchoredPosition = home;
                }
                if (show) {
                    done?.Invoke();
                }
            },
            ease);
    }

    private static Vector2 CanvasHalf() {
        float scale = UICore.Canvas != null && UICore.Canvas.scaleFactor > 0f ? UICore.Canvas.scaleFactor : 1f;
        return new Vector2(Screen.width * 0.5f / scale, Screen.height * 0.5f / scale);
    }

    private static CanvasGroup Group(RectTransform panel) {
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        return group != null ? group : panel.gameObject.AddComponent<CanvasGroup>();
    }

    // A 45°-rotated stencil mask sweeps along the diagonal; the panel is
    // parented under it and pinned to its world position each frame.
    private static void BeginWipe(RectTransform panel, bool show) {
        var go = new GameObject("PanelWipe");
        wipe = go.AddComponent<RectTransform>();
        wipe.SetParent(panel.parent, false);
        wipe.SetSiblingIndex(panel.GetSiblingIndex());
        wipe.anchorMin = wipe.anchorMax = new Vector2(0.5f, 0.5f);
        float side = (panel.sizeDelta.x + panel.sizeDelta.y) * 2f;
        wipe.sizeDelta = new Vector2(side, side);
        // Local +x points along Diagonal. Showing keeps the area behind the
        // edge, hiding keeps the area ahead of it, so both sweep TL -> BR.
        wipe.pivot = show ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        wipe.localRotation = Quaternion.Euler(0f, 0f, -45f);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        go.AddComponent<Mask>().showMaskGraphic = false;

        wipePanelWorld = panel.position;
        panel.SetParent(wipe, true);
    }

    private static void ApplyWipe(RectTransform panel, Vector2 home, bool show, float v) {
        if (wipe == null) {
            return;
        }
        // Half the panel's extent along the diagonal.
        float half = (panel.sizeDelta.x + panel.sizeDelta.y) * 0.35355339f;
        float edge = show ? Mathf.LerpUnclamped(-half, half, v) : Mathf.LerpUnclamped(half, -half, v);
        wipe.anchoredPosition = home + Diagonal * edge;
        panel.position = wipePanelWorld;
        panel.rotation = Quaternion.identity;
    }

    // RectMask2D clips axis-aligned and breaks under rotation. Until Restore,
    // each active one is replaced by a stretched proxy child with a stencil
    // Mask (rotation-safe) that temporarily holds the host's children; a proxy
    // avoids touching hosts that already carry a Graphic (e.g. EmptyGraphic).
    private static void BeginStencilClip(RectTransform panel) {
        foreach (RectMask2D rect in panel.GetComponentsInChildren<RectMask2D>(false)) {
            if (!rect.enabled) {
                continue;
            }
            Transform host = rect.transform;
            var children = new List<Transform>();
            for (int i = 0; i < host.childCount; i++) {
                children.Add(host.GetChild(i));
            }

            var go = new GameObject("StencilClip");
            var proxy = go.AddComponent<RectTransform>();
            proxy.SetParent(host, false);
            proxy.anchorMin = Vector2.zero;
            proxy.anchorMax = Vector2.one;
            proxy.pivot = rect.rectTransform.pivot;
            proxy.offsetMin = Vector2.zero;
            proxy.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            Image hostImage = host.GetComponent<Image>();
            if (hostImage != null && hostImage.sprite != null) {
                img.sprite = hostImage.sprite;
                img.type = hostImage.type;
                img.pixelsPerUnitMultiplier = hostImage.pixelsPerUnitMultiplier;
            }
            go.AddComponent<Mask>().showMaskGraphic = false;

            foreach (Transform child in children) {
                child.SetParent(proxy, false);
            }
            rect.enabled = false;
            stencilSwaps.Add((rect, proxy, children));
        }
    }

    private static void EndStencilClip() {
        // Reverse order so nested proxies unwind inside-out.
        for (int i = stencilSwaps.Count - 1; i >= 0; i--) {
            (RectMask2D rect, RectTransform proxy, List<Transform> children) = stencilSwaps[i];
            Transform host = rect != null ? rect.transform : null;
            if (host != null) {
                for (int c = 0; c < children.Count; c++) {
                    if (children[c] != null) {
                        children[c].SetParent(host, false);
                        children[c].SetSiblingIndex(c);
                    }
                }
                rect.enabled = true;
            }
            if (proxy != null) {
                UnityEngine.Object.DestroyImmediate(proxy.gameObject);
            }
        }
        stencilSwaps.Clear();
    }

    private static void Restore() {
        EndStencilClip();
        RectTransform panel = UICore.Panel;
        if (wipe != null) {
            if (panel != null && panel.parent == wipe) {
                panel.SetParent(wipe.parent, true);
                panel.SetSiblingIndex(wipe.GetSiblingIndex());
            }
            UnityEngine.Object.Destroy(wipe.gameObject);
            wipe = null;
        }
        if (panel == null) {
            return;
        }
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
        Group(panel).alpha = Opacity;
    }
}
