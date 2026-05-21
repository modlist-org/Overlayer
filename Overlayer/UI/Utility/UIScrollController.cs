using DG.Tweening;
using UnityEngine;

public class UIScrollController : MonoBehaviour {
    public RectTransform content;
    public RectTransform viewport;

    public float wheelStrength = 0.32f;

    public float dragSensitivity = 1f;
    public float dragToScrollRatio = 1f;

    public float scrollDuration = 0.2f;
    public Ease scrollEase = Ease.OutCirc;

    private bool rightDragging;
    private Vector2 lastMousePos;

    private float targetY;
    private Tween scrollTween;

    private void Awake() {
        if(content != null) {
            targetY = content.anchoredPosition.y;
        }
    }

    private void Update() {
        if(content == null || viewport == null) {
            return;
        }

        HandleWheel();
        HandleRightDrag();
    }

    private void HandleWheel() {
        float wheel = Input.mouseScrollDelta.y;

        if(Mathf.Abs(wheel) <= 0.0001f) {
            return;
        }

        AddDelta(-wheel * wheelStrength);
        ApplyTween();
    }

    private void HandleRightDrag() {
        if(Input.GetMouseButtonDown(1)) {
            rightDragging = true;
        }

        if(Input.GetMouseButtonUp(1)) {
            rightDragging = false;
            ApplyTween();
        }

        if(!rightDragging) {
            return;
        }

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);

        if(maxOffset <= 0f) {
            return;
        }

        Vector2 mouse = Input.mousePosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            mouse,
            null,
            out Vector2 local
        );

        float normalized = 1f - Mathf.Clamp01(
            (local.y + (viewportHeight * 0.5f)) / viewportHeight
        );

        targetY = normalized * maxOffset;

        ApplyTween();
    }

    private void AddDelta(float deltaNormalized) {
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);

        targetY += deltaNormalized * maxOffset;
        targetY = Mathf.Clamp(targetY, 0f, maxOffset);
    }

    private void ApplyTween() {
        scrollTween?.Kill();

        scrollTween = DOTween.To(
                () => content.anchoredPosition.y,
                x => {
                    content.anchoredPosition = new Vector2(
                        content.anchoredPosition.x,
                        x
                    );
                },
                targetY,
                scrollDuration
            )
            .SetEase(scrollEase)
            .SetUpdate(true);
    }

    public void SetContent(RectTransform content, RectTransform viewport) {
        this.content = content;
        this.viewport = viewport;

        if(content != null) {
            targetY = content.anchoredPosition.y;
        }
    }
}