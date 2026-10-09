using System;
using System.Collections.Generic;
using O5Kit.Core;
using Overlayer.Compat;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Tween;

public static class O5TweenExtensions {
    private sealed class MultiHandle : ITweenHandle {
        private readonly List<ITweenHandle> _handles = [];

        public void Add(ITweenHandle handle) {
            if (handle != null) {
                _handles.Add(handle);
            }
        }

        public bool IsAlive {
            get {
                foreach (var h in _handles) {
                    try {
                        if (h != null && h.IsAlive) {
                            return true;
                        }
                    } catch {
                    }
                }

                return false;
            }
        }

        public void Kill(bool complete = false) {
            foreach (var h in _handles) {
                try {
                    h?.Kill(complete);
                } catch {
                }
            }
        }
    }

    private static ITweenRunner Runner => O5KitAdapters.Ctx.Tween;

    private static void WhenDone(ref int pending, Action done) {
        if (--pending == 0) {
            try {
                done?.Invoke();
            } catch {
            }
        }
    }

    public static ITweenHandle TColor(this Image img, Color to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        return runner.TweenColor(
            () => img ? img.color : to,
            v => {
                if (img) {
                    img.color = v;
                }
            },
            to, duration, done, ease);
    }

    public static ITweenHandle TAlpha(this Graphic g, float to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        return runner.TweenFloat(
            () => g ? g.color.a : to,
            v => {
                if (g) {
                    var c = g.color;
                    c.a = v;
                    g.color = c;
                }
            },
            to, duration, done, ease);
    }

    public static ITweenHandle TFade(this CanvasGroup g, float to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        return runner.TweenFloat(
            () => g ? g.alpha : to,
            v => {
                if (g) {
                    g.alpha = v;
                }
            },
            to, duration, done, ease);
    }

    public static ITweenHandle TAnchorPos(this RectTransform r, Vector2 to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        var multi = new MultiHandle();
        int pending = 2;
        multi.Add(runner.TweenFloat(
            () => r ? r.anchoredPosition.x : to.x,
            v => {
                if (r) {
                    var p = r.anchoredPosition;
                    p.x = v;
                    r.anchoredPosition = p;
                }
            },
            to.x, duration, () => WhenDone(ref pending, done), ease));
        multi.Add(runner.TweenFloat(
            () => r ? r.anchoredPosition.y : to.y,
            v => {
                if (r) {
                    var p = r.anchoredPosition;
                    p.y = v;
                    r.anchoredPosition = p;
                }
            },
            to.y, duration, () => WhenDone(ref pending, done), ease));
        return multi;
    }

    public static ITweenHandle TOffsetMin(this RectTransform r, Vector2 to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        var multi = new MultiHandle();
        int pending = 2;
        multi.Add(runner.TweenFloat(
            () => r ? r.offsetMin.x : to.x,
            v => {
                if (r) {
                    var p = r.offsetMin;
                    p.x = v;
                    r.offsetMin = p;
                }
            },
            to.x, duration, () => WhenDone(ref pending, done), ease));
        multi.Add(runner.TweenFloat(
            () => r ? r.offsetMin.y : to.y,
            v => {
                if (r) {
                    var p = r.offsetMin;
                    p.y = v;
                    r.offsetMin = p;
                }
            },
            to.y, duration, () => WhenDone(ref pending, done), ease));
        return multi;
    }

    public static ITweenHandle TSizeDelta(this RectTransform r, Vector2 to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        var multi = new MultiHandle();
        int pending = 2;
        multi.Add(runner.TweenFloat(
            () => r ? r.sizeDelta.x : to.x,
            v => {
                if (r) {
                    var p = r.sizeDelta;
                    p.x = v;
                    r.sizeDelta = p;
                }
            },
            to.x, duration, () => WhenDone(ref pending, done), ease));
        multi.Add(runner.TweenFloat(
            () => r ? r.sizeDelta.y : to.y,
            v => {
                if (r) {
                    var p = r.sizeDelta;
                    p.y = v;
                    r.sizeDelta = p;
                }
            },
            to.y, duration, () => WhenDone(ref pending, done), ease));
        return multi;
    }

    public static ITweenHandle TScale(this Transform t, Vector3 to, float duration, O5Ease ease, Action done = null) {
        var runner = Runner;
        var multi = new MultiHandle();
        int pending = 3;
        multi.Add(runner.TweenFloat(
            () => t ? t.localScale.x : to.x,
            v => {
                if (t) {
                    var p = t.localScale;
                    p.x = v;
                    t.localScale = p;
                }
            },
            to.x, duration, () => WhenDone(ref pending, done), ease));
        multi.Add(runner.TweenFloat(
            () => t ? t.localScale.y : to.y,
            v => {
                if (t) {
                    var p = t.localScale;
                    p.y = v;
                    t.localScale = p;
                }
            },
            to.y, duration, () => WhenDone(ref pending, done), ease));
        multi.Add(runner.TweenFloat(
            () => t ? t.localScale.z : to.z,
            v => {
                if (t) {
                    var p = t.localScale;
                    p.z = v;
                    t.localScale = p;
                }
            },
            to.z, duration, () => WhenDone(ref pending, done), ease));
        return multi;
    }
}
