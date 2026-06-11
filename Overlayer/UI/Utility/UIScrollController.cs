using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Overlayer.Compat.OVC;
using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Utility;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class UIScrollController
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public RectTransform content;
    public RectTransform viewport;

    public float wheelStrength = 42f;

    public float dragSensitivity = 1f;
    public float dragToScrollRatio = 1f;

    public float scrollDuration = 0.2f;
    public Easing scrollEase = Easing.OutCirc;

    private bool rightDragging;

    private float targetY;
    private GTween scrollTween;

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
        float wheel = OVC_Input.MouseScrollDelta.y;

        if(Mathf.Abs(wheel) <= 0.0001f) {
            return;
        }

        AddDelta(-wheel * wheelStrength);
        ApplyTween();
    }

    private void HandleRightDrag() {
        if(OVC_Input.GetMouseButtonDown(1)) {
            rightDragging = true;
        }

        if(OVC_Input.GetMouseButtonUp(1)) {
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

        Vector2 mouse = OVC_Input.MousePosition;

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

    private void AddDelta(float deltaPixels) {
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);

        targetY += deltaPixels;
        targetY = Mathf.Clamp(targetY, 0f, maxOffset);
    }

    private void ApplyTween() {
        scrollTween?.Kill();

        scrollTween = GTweenExtensions.Tween(
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
        .SetEasing(scrollEase);
        MainCore.TC.Play(scrollTween);
    }

    public void SetContent(RectTransform content, RectTransform viewport) {
        this.content = content;
        this.viewport = viewport;

        if(content != null) {
            targetY = content.anchoredPosition.y;
        }
    }
}