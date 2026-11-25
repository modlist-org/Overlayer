using O5Kit.Behaviour;
using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI;

// View-only scroll position on the window's right border, as thick as the border itself: it
// follows whichever scroll view the mouse is over (the innermost one that can scroll) and fades
// in and out with it.
internal static class ScrollIndicator {
    // Window outline art: 12.5-unit corner radius, 3.125-unit stroke (CircleOutline128O16P512).
    private const float Corner = 12.5f;
    private const float Stroke = 3.125f;
    private const float MinThumb = 24f;
    private const float FadeSpeed = 6f;
    private const float RescanSeconds = 0.5f;
    // Overflow below this (layout padding/rounding) counts as nothing to scroll.
    private const float MinScroll = 8f;
    private const float Follow = 18f;
    // Title bar height (UICore TopBar): the track starts below it, not at the window's top edge.
    private const float TopInset = 60f;

    private static RectTransform panel, thumb;
    private static Image thumbImg;
    private static readonly List<UIScrollController> scrollers = [];
    private static float rescanAt, alpha;
    private static float targetFrac, targetSize = 1f, shownFrac, shownSize = 1f;
    private static readonly List<RaycastResult> hits = [];
    private static PointerEventData pointer;
    private static EventSystem pointerEs;

    public static void Create(RectTransform window, Transform after) {
        panel = window;
        var go = new GameObject("ScrollIndicator");
        go.transform.SetParent(window, false);
        go.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);
        thumb = go.AddComponent<RectTransform>();
        thumb.anchorMin = thumb.anchorMax = new Vector2(1f, 1f);
        thumb.pivot = new Vector2(1f, 1f);
        thumbImg = go.AddComponent<Image>();
        thumbImg.sprite = MainCore.Spr.Shapes.Get(new O5Shape(Stroke * 0.5f));
        thumbImg.type = Image.Type.Sliced;
        thumbImg.raycastTarget = false;
        alpha = 0f;
        Apply();
    }

    public static void Tick() {
        if (panel == null || thumb == null || !panel.gameObject.activeInHierarchy) {
            return;
        }
        if (Time.unscaledTime >= rescanAt) {
            rescanAt = Time.unscaledTime + RescanSeconds;
            scrollers.Clear();
            foreach (var s in panel.GetComponentsInChildren<UIScrollController>(false)) {
                scrollers.Add(s);
            }
        }

        UIScrollController hovered = null;
        Vector2 mouse = O5Input.MousePosition;
        Transform top = TopHit(mouse);
        foreach (var s in scrollers) {
            // Nothing to scroll: no bar at all (an outer view that can scroll may still show).
            if (s == null || !s.isActiveAndEnabled || s.content == null || s.viewport == null
                || s.MaxOffset < MinScroll || !Reachable(s.transform)) {
                continue;
            }
            var rect = s.GetComponent<RectTransform>();
            if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, null)) {
                continue;
            }
            // Covered by something drawn over it (the setup wizard over the pages ...): the
            // pointer is on that, not on this view.
            if (top != null && !top.IsChildOf(s.transform) && DrawsAfter(top, s.transform)) {
                continue;
            }
            // Overlapping: the one drawn on top wins (later in draw order; nested ones draw after their parent).
            if (hovered == null || DrawsAfter(s.transform, hovered.transform)) {
                hovered = s;
            }
        }

        if (hovered != null) {
            float max = hovered.MaxOffset;
            targetSize = Mathf.Clamp01(hovered.viewport.rect.height / Mathf.Max(1f, hovered.content.rect.height));
            targetFrac = Mathf.Clamp01(hovered.content.anchoredPosition.y / max);
            if (alpha <= 0.001f) {
                // Appearing: start where it belongs instead of sliding in from the last view.
                shownSize = targetSize;
                shownFrac = targetFrac;
            }
        }
        float dt = Time.unscaledDeltaTime;
        // Scroll easing off: the bar jumps with the content instead of gliding.
        float k = MainCore.Conf.ScrollEasing.Value ? 1f - Mathf.Exp(-Follow * dt) : 1f;
        shownSize = Mathf.Lerp(shownSize, targetSize, k);
        shownFrac = Mathf.Lerp(shownFrac, targetFrac, k);
        alpha = Mathf.MoveTowards(alpha, hovered != null ? 1f : 0f, dt * FadeSpeed);
        Apply();
    }

    // Topmost UI element under the pointer inside this window, or null.
    private static Transform TopHit(Vector2 mouse) {
        var es = EventSystem.current;
        if (es == null) {
            return null;
        }
        if (!ReferenceEquals(pointerEs, es)) {
            pointerEs = es;
            pointer = new PointerEventData(es);
        }
        pointer.position = mouse;
        hits.Clear();
        es.RaycastAll(pointer, hits);
        foreach (var hit in hits) {
            if (hit.gameObject != null) {
                var t = hit.gameObject.transform;
                return t.IsChildOf(panel) ? t : null;
            }
        }
        return null;
    }

    // Hidden pages stay active but sit under a CanvasGroup at alpha 0 / no raycasts: the pointer
    // isn't really over them, so they must not win the hover.
    private static bool Reachable(Transform t) {
        for (; t != null; t = t.parent) {
            var group = t.GetComponent<CanvasGroup>();
            if (group != null) {
                if (group.alpha <= 0.01f || !group.blocksRaycasts) {
                    return false;
                }
                if (group.ignoreParentGroups) {
                    return true;
                }
            }
            if (t == panel) {
                break;
            }
        }
        return true;
    }

    // UI draw order within the window: depth-first hierarchy order.
    private static bool DrawsAfter(Transform a, Transform b) {
        // Popups (dropdown lists ...) can sit on their own sorted canvas: higher order draws on top.
        int orderA = SortOrder(a), orderB = SortOrder(b);
        if (orderA != orderB) {
            return orderA > orderB;
        }
        var pathA = Path(a);
        var pathB = Path(b);
        for (int i = 0; i < Mathf.Min(pathA.Count, pathB.Count); i++) {
            if (pathA[i] != pathB[i]) {
                return pathA[i] > pathB[i];
            }
        }
        return pathA.Count > pathB.Count; // a is nested inside b
    }

    private static int SortOrder(Transform t) {
        var canvas = t.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.sortingOrder : 0;
    }

    private static List<int> Path(Transform t) {
        var path = new List<int>();
        for (; t != null && t != panel; t = t.parent) {
            path.Add(t.GetSiblingIndex());
        }
        path.Reverse();
        return path;
    }

    // Runs from below the title bar down to the bottom rounded corner.
    private static void Apply() {
        float track = Mathf.Max(0f, panel.rect.height - TopInset - Corner);
        float size = Mathf.Min(track, Mathf.Max(MinThumb, track * shownSize));
        thumb.sizeDelta = new Vector2(Stroke, size);
        thumb.anchoredPosition = new Vector2(0f, -TopInset - (track - size) * shownFrac);
        Color c = UIColors.ObjectActive;
        thumbImg.color = new Color(c.r, c.g, c.b, alpha);
        bool visible = alpha > 0.001f;
        if (thumb.gameObject.activeSelf != visible) {
            thumb.gameObject.SetActive(visible);
        }
    }
}
