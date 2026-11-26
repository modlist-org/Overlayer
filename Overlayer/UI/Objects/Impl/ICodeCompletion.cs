using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Core;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Objects.Impl;

internal interface ICodeCompletion {
    bool IsActive { get; }
    bool HandleKey(KeyCode key);
    void Refresh(bool composing);
}

internal sealed class CompositeCompletion(params ICodeCompletion[] popups) : ICodeCompletion {
    private readonly ICodeCompletion[] popups = popups;

    public bool IsActive {
        get {
            foreach (var popup in popups) {
                if (popup != null && popup.IsActive) {
                    return true;
                }
            }

            return false;
        }
    }

    public bool HandleKey(KeyCode key) {
        foreach (var popup in popups) {
            if (popup != null && popup.HandleKey(key)) {
                return true;
            }
        }

        return false;
    }

    public void Refresh(bool composing) {
        foreach (var popup in popups) {
            popup?.Refresh(composing);
        }
    }
}

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
internal sealed class CompletionPopupController
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    internal const float PopupPad = 6f;
    internal const float PopupCorner = 8f;
    internal const float RowCorner = 5f;
    internal const float ScrollbarStroke = 3.125f;
    internal const float ScrollbarRightGap = 4f;
    internal const float ScrollbarReserve = 10f;

    private const float MinThumb = 20f;
    private const float Follow = 18f;
    private const float InitialDelay = 0.35f;
    private const float RepeatInterval = 0.05f;

    private static CompletionPopupController hoveredController;

    private RectTransform popupRect;
    private RectTransform thumbRect;
    private Image thumbImage;
    private UICodeInputField input;
    private Func<bool> isActive;
    private Action<int> onMoveSelection;
    private Action<int> onScrollTo;

    private KeyCode repeatKey;
    private float nextRepeatTime;
    private bool repeating;

    private int curWindowStart;
    private int curVisibleCount;
    private int curTotalCount;
    private float targetSize = 1f;
    private float targetFrac;
    private float shownSize = 1f;
    private float shownFrac;

    private bool draggingScroll;
    private int dragAnchor;
    private int dragFocus;

    internal static bool AnyHovered => hoveredController != null;
    internal bool IsDraggingScroll => draggingScroll;

    internal void Configure(
        RectTransform popupRect,
        UICodeInputField input,
        Func<bool> isActive,
        Action<int> onMoveSelection,
        Action<int> onScrollTo
    ) {
        this.popupRect = popupRect;
        this.input = input;
        this.isActive = isActive;
        this.onMoveSelection = onMoveSelection;
        this.onScrollTo = onScrollTo;

        if (thumbRect == null && popupRect != null) {
            GameObject thumb = new("ScrollIndicator");
            thumb.transform.SetParent(popupRect, false);
            thumbRect = thumb.AddComponent<RectTransform>();
            thumbRect.anchorMin = new(1f, 1f);
            thumbRect.anchorMax = new(1f, 1f);
            thumbRect.pivot = new(1f, 1f);

            thumbImage = thumb.AddComponent<Image>();
            thumbImage.sprite = MainCore.Spr.Shapes.Get(new O5Shape(ScrollbarStroke * 0.5f));
            thumbImage.type = Image.Type.Sliced;
            thumbImage.color = UIColors.ObjectActive;
            thumbImage.raycastTarget = false;
            thumb.SetActive(false);
        }
    }

    internal void UpdateScrollState(int windowStart, int visibleCount, int totalCount, bool snap) {
        curWindowStart = windowStart;
        curVisibleCount = visibleCount;
        curTotalCount = totalCount;

        int maxStart = Math.Max(0, totalCount - visibleCount);
        targetSize = totalCount > 0 ? Mathf.Clamp01(visibleCount / (float)totalCount) : 1f;
        targetFrac = maxStart > 0 ? Mathf.Clamp01(windowStart / (float)maxStart) : 0f;

        if (snap) {
            shownSize = targetSize;
            shownFrac = targetFrac;
        }

        ApplyThumb();
    }

    internal void Reset() {
        repeating = false;
        draggingScroll = false;
        if (hoveredController == this) {
            hoveredController = null;
        }
        if (thumbRect != null && thumbRect.gameObject.activeSelf) {
            thumbRect.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => Reset();

    private void OnDestroy() => Reset();

    private void Update() {
        if (popupRect == null || isActive == null || !isActive()) {
            Reset();
            return;
        }

        UpdateKeyRepeat();
        UpdatePointerAndScroll();
        UpdateThumbAnimation();
    }

    private void UpdateKeyRepeat() {
        KeyCode key = GetHeldNavigationKey();
        if (key == KeyCode.None) {
            repeating = false;
            return;
        }

        if (!repeating || repeatKey != key) {
            repeatKey = key;
            repeating = true;
            nextRepeatTime = Time.unscaledTime + InitialDelay;
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextRepeatTime) {
            return;
        }

        onMoveSelection?.Invoke(key == KeyCode.DownArrow ? 1 : -1);
        nextRepeatTime = now + RepeatInterval;
    }

    private void UpdatePointerAndScroll() {
        Vector2 mouse = O5Input.MousePosition;
        bool overPopup = RectTransformUtility.RectangleContainsScreenPoint(popupRect, mouse, null);
        if (overPopup || draggingScroll) {
            hoveredController = this;
        } else if (hoveredController == this) {
            hoveredController = null;
        }

        int maxStart = Math.Max(0, curTotalCount - curVisibleCount);
        if (maxStart <= 0) {
            draggingScroll = false;
            return;
        }

        if (overPopup && !draggingScroll) {
            float wheel = O5Input.MouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.0001f) {
                int step = wheel > 0f ? -1 : 1;
                int next = MathCompat.Clamp(curWindowStart + step, 0, maxStart);
                if (next != curWindowStart) {
                    onScrollTo?.Invoke(next);
                }
            }
        }

        if (!draggingScroll && overPopup && O5Input.GetMouseButtonDown(0) && IsInScrollbarStrip(mouse)) {
            draggingScroll = true;
            if (input != null) {
                dragAnchor = input.selectionAnchorPosition;
                dragFocus = input.selectionFocusPosition;
            }
            JumpScrollToPointer(mouse, maxStart);
            input?.RestoreFocusWithCaret(dragAnchor, dragFocus);
            return;
        }

        if (!draggingScroll) {
            return;
        }

        if (!O5Input.GetMouseButton(0)) {
            draggingScroll = false;
            input?.RestoreFocusWithCaret(dragAnchor, dragFocus);
            return;
        }

        JumpScrollToPointer(mouse, maxStart);
        if (input != null && !input.isFocused) {
            input.RestoreFocusWithCaret(dragAnchor, dragFocus);
        }
    }

    private bool IsInScrollbarStrip(Vector2 screenPoint) {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRect, screenPoint, null, out Vector2 local)) {
            return false;
        }

        Rect rect = popupRect.rect;
        float stripWidth = PopupPad + ScrollbarReserve;
        return local.x >= rect.xMax - stripWidth && local.x <= rect.xMax
            && local.y >= rect.yMin && local.y <= rect.yMax;
    }

    private void JumpScrollToPointer(Vector2 screenPoint, int maxStart) {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRect, screenPoint, null, out Vector2 local)) {
            return;
        }

        Rect rect = popupRect.rect;
        float trackTop = rect.yMax - PopupPad;
        float trackHeight = Mathf.Max(1f, rect.height - PopupPad * 2f);
        float frac = Mathf.Clamp01((trackTop - local.y) / trackHeight);
        int target = MathCompat.Clamp(Mathf.RoundToInt(frac * maxStart), 0, maxStart);
        if (target != curWindowStart) {
            onScrollTo?.Invoke(target);
        }
    }

    private void UpdateThumbAnimation() {
        float animSpeed = MainCore.Conf.AnimationSpeed.Value;
        if (animSpeed <= 0f || !MainCore.Conf.ScrollEasing.Value) {
            shownSize = targetSize;
            shownFrac = targetFrac;
        } else {
            float k = 1f - Mathf.Exp(-Follow * animSpeed * Time.unscaledDeltaTime);
            shownSize = Mathf.Lerp(shownSize, targetSize, k);
            shownFrac = Mathf.Lerp(shownFrac, targetFrac, k);
        }

        ApplyThumb();
    }

    private void ApplyThumb() {
        if (thumbRect == null || thumbImage == null || popupRect == null) {
            return;
        }

        bool scrollable = curTotalCount > curVisibleCount && curVisibleCount > 0;
        if (thumbRect.gameObject.activeSelf != scrollable) {
            thumbRect.gameObject.SetActive(scrollable);
        }
        if (!scrollable) {
            return;
        }

        float track = Mathf.Max(0f, popupRect.rect.height - PopupPad * 2f);
        float size = Mathf.Min(track, Mathf.Max(MinThumb, track * shownSize));
        thumbRect.sizeDelta = new(ScrollbarStroke, size);
        thumbRect.anchoredPosition = new(-ScrollbarRightGap, -PopupPad - (track - size) * shownFrac);
        thumbImage.color = UIColors.ObjectActive;
        thumbRect.SetAsLastSibling();
    }

    private static KeyCode GetHeldNavigationKey() {
        if (O5Input.GetKey(KeyCode.DownArrow)) {
            return KeyCode.DownArrow;
        }
        if (O5Input.GetKey(KeyCode.UpArrow)) {
            return KeyCode.UpArrow;
        }
        return KeyCode.None;
    }
}
