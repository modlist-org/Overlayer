using GTweens.Extensions;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tween;

public static class GTweenExtensions {
    public static GTween GTAlpha(this CanvasGroup target, float to, float duration)
        => GTweens.Extensions.GTweenExtensions.Tween(() => target.alpha, x => target.alpha = x, to, duration);

    extension(Graphic target) {
        public GTween GTAlpha(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target.color.a,
                x => {
                    var c = target.color;
                    c.a = x;
                    target.color = c;
                },
                to,
                duration
            );
        }

        public GTween GTColor(Color to, float duration) {
            var from = target.color;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => target.color = Color.Lerp(from, to, x),
                1f,
                duration
            );
        }
    }

    public static GTween GTFade(this CanvasGroup target, float to, float duration) {
        return GTweens.Extensions.GTweenExtensions.Tween(
            () => target.alpha,
            x => target.alpha = x,
            to,
            duration
        );
    }

    extension(RectTransform target) {
        public GTween GTAnchorPos(Vector2 to, float duration) {
            var from = target.anchoredPosition;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => target.anchoredPosition = Vector2.LerpUnclamped(from, to, x),
                1f,
                duration
            );
        }

        public GTween GTAnchorPosX(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target.anchoredPosition.x,
                x => {
                    var pos = target.anchoredPosition;
                    pos.x = x;
                    target.anchoredPosition = pos;
                },
                to,
                duration
            );
        }

        public GTween GTAnchorPosY(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target.anchoredPosition.y,
                x => {
                    var pos = target.anchoredPosition;
                    pos.y = x;
                    target.anchoredPosition = pos;
                },
                to,
                duration
            );
        }

        public GTween GTSizeDelta(Vector2 to, float duration) {
            var from = target.sizeDelta;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => target.sizeDelta = Vector2.LerpUnclamped(from, to, x),
                1f,
                duration
            );
        }

        public GTween GTOffsetMin(Vector2 to, float duration) {
            var from = target.offsetMin;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => target.offsetMin = Vector2.LerpUnclamped(from, to, x),
                1f,
                duration
            );
        }

        public GTween GTRotate(Vector3 to, float duration) {
            var fromRot = target.localRotation;
            var toRot = Quaternion.Euler(to);

            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => target.localRotation = Quaternion.Slerp(fromRot, toRot, x),
                1f,
                duration
            );
        }
    }
}