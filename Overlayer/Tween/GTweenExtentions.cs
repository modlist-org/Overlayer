using GTweens.Extensions;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tween;

public static class GTweenExtentions {
    public static GTween GTAlpha(this CanvasGroup target, float to, float duration)
        => GTweenExtensions.Tween(() => target.alpha, x => target.alpha = x, to, duration);

    public static GTween GTAlpha(this Graphic target, float to, float duration) {
        return GTweenExtensions.Tween(
            () => target.color.a,
            x => {
                Color c = target.color;
                c.a = x;
                target.color = c;
            },
            to,
            duration
        );
    }

    public static GTween GTColor(this Graphic target, Color to, float duration) {
        Color from = target.color;
        return GTweenExtensions.Tween(
            () => 0f,
            x => target.color = Color.Lerp(from, to, x),
            1f,
            duration
        );
    }

    public static GTween GTFade(this CanvasGroup target, float to, float duration) {
        return GTweenExtensions.Tween(
            () => target.alpha,
            x => target.alpha = x,
            to,
            duration
        );
    }

    public static GTween GTAnchorPos(this RectTransform target, Vector2 to, float duration) {
        Vector2 from = target.anchoredPosition;
        return GTweenExtensions.Tween(
            () => 0f,
            x => target.anchoredPosition = Vector2.LerpUnclamped(from, to, x),
            1f,
            duration
        );
    }

    public static GTween GTAnchorPosX(this RectTransform target, float to, float duration) {
        return GTweenExtensions.Tween(
            () => target.anchoredPosition.x,
            x => {
                Vector2 pos = target.anchoredPosition;
                pos.x = x;
                target.anchoredPosition = pos;
            },
            to,
            duration
        );
    }
    public static GTween GTAnchorPosY(this RectTransform target, float to, float duration) {
        return GTweenExtensions.Tween(
            () => target.anchoredPosition.y,
            x => {
                Vector2 pos = target.anchoredPosition;
                pos.y = x;
                target.anchoredPosition = pos;
            },
            to,
            duration
        );
    }

    public static GTween GTSizeDelta(this RectTransform target, Vector2 to, float duration) {
        Vector2 from = target.sizeDelta;
        return GTweenExtensions.Tween(
            () => 0f,
            x => target.sizeDelta = Vector2.LerpUnclamped(from, to, x),
            1f,
            duration
        );
    }

    public static GTween GTOffsetMin(this RectTransform target, Vector2 to, float duration) {
        Vector2 from = target.offsetMin;
        return GTweenExtensions.Tween(
            () => 0f,
            x => target.offsetMin = Vector2.LerpUnclamped(from, to, x),
            1f,
            duration
        );
    }

    public static GTween GTRotate(this RectTransform target, Vector3 to, float duration) {
        Quaternion fromRot = target.localRotation;
        Quaternion toRot = Quaternion.Euler(to);

        return GTweenExtensions.Tween(
            () => 0f,
            x => target.localRotation = Quaternion.Slerp(fromRot, toRot, x),
            1f,
            duration
        );
    }
}