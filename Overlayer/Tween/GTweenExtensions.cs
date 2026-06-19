using GTweens.Tweens;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tween;

public static class GTweenExtensions {
    public static GTween GTAlpha(this CanvasGroup target, float to, float duration)
        => GTweens.Extensions.GTweenExtensions.Tween(() => target ? target.alpha : 0f, x => { if(target) { target.alpha = x; } }, to, duration);

    extension(Graphic target) {
        public GTween GTAlpha(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target ? target.color.a : 0f,
                x => {
                    if(target) {
                        var c = target.color;
                        c.a = x;
                        target.color = c;
                    }
                },
                to,
                duration
            );
        }

        public GTween GTColor(Color to, float duration) {
            var from = target ? target.color : Color.white;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => { if(target) { target.color = Color.Lerp(from, to, x); } },
                1f,
                duration
            );
        }

        public GTween GTColorRGB(Color to, float duration) {
            var from = target ? target.color : Color.white;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => {
                    if(target) {
                        Color lerped = Color.Lerp(from, to, x);
                        target.color = new Color(lerped.r, lerped.g, lerped.b, target.color.a);
                    }
                },
                1f,
                duration
            );
        }
    }

    public static GTween GTFade(this CanvasGroup target, float to, float duration) {
        return GTweens.Extensions.GTweenExtensions.Tween(
            () => target ? target.alpha : 0f,
            x => { if(target) { target.alpha = x; } },
            to,
            duration
        );
    }

    extension(RectTransform target) {
        public GTween GTAnchorPos(Vector2 to, float duration) {
            var from = target ? target.anchoredPosition : Vector2.zero;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => { if(target) { target.anchoredPosition = Vector2.LerpUnclamped(from, to, x); } },
                1f,
                duration
            );
        }

        public GTween GTAnchorPosX(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target ? target.anchoredPosition.x : 0f,
                x => {
                    if(target) {
                        var pos = target.anchoredPosition;
                        pos.x = x;
                        target.anchoredPosition = pos;
                    }
                },
                to,
                duration
            );
        }

        public GTween GTAnchorPosY(float to, float duration) {
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => target ? target.anchoredPosition.y : 0f,
                x => {
                    if(target) {
                        var pos = target.anchoredPosition;
                        pos.y = x;
                        target.anchoredPosition = pos;
                    }
                },
                to,
                duration
            );
        }

        public GTween GTSizeDelta(Vector2 to, float duration) {
            var from = target ? target.sizeDelta : Vector2.zero;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => { if(target) { target.sizeDelta = Vector2.LerpUnclamped(from, to, x); } },
                1f,
                duration
            );
        }

        public GTween GTOffsetMin(Vector2 to, float duration) {
            var from = target ? target.offsetMin : Vector2.zero;
            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => { if(target) { target.offsetMin = Vector2.LerpUnclamped(from, to, x); } },
                1f,
                duration
            );
        }

        public GTween GTRotate(Vector3 to, float duration) {
            Vector3 from = target ? target.localEulerAngles : Vector3.zero;
            Vector3 targetAngle = to;

            Vector3 delta = new(
                Mathf.DeltaAngle(from.x, targetAngle.x),
                Mathf.DeltaAngle(from.y, targetAngle.y),
                Mathf.DeltaAngle(from.z, targetAngle.z)
            );

            return GTweens.Extensions.GTweenExtensions.Tween(
                () => 0f,
                x => { if(target) { target.localEulerAngles = from + (delta * x); } },
                1f,
                duration
            );
        }
    }
}