using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Core;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Easing;

/// <summary>
/// Attaches a compact 3:1 easing curve preview to the right side of every row (and header)
/// of an <see cref="O5Dropdown{T}"/>, with an animated dot that plays along the curve on hover.
/// </summary>
internal static class EaseDropdownCurveHelper {
    private const int Samples = 24;
    private const float MoveSeconds = 0.85f;
    private const float HoldSeconds = 0.30f;

    private sealed class CurveWidget {
        public RectTransform Box;
        public RectTransform Dot;
        public readonly List<RectTransform> Segments = [];
        public readonly List<Image> SegmentImages = [];
        public string EaseKey;
        public float MinY;
        public float MaxY = 1f;
        public float BoxW;
        public float BoxH;
        public bool Hovered;

        public Vector2 PlotPoint(float t, float y) {
            const float padX = 4f;
            const float padY = 3.5f;
            float px = Mathf.Lerp(padX, BoxW - padX, t);
            float u = Mathf.InverseLerp(MinY, MaxY, y);
            float py = Mathf.Lerp(padY, BoxH - padY, u);
            return new Vector2(px, py);
        }

        public void SetCurve(string easeKey) {
            EaseKey = easeKey;
            bool has = !string.IsNullOrEmpty(easeKey);
            if (Box != null) {
                Box.gameObject.SetActive(has);
            }
            if (!has) {
                if (Dot != null) {
                    Dot.gameObject.SetActive(false);
                }
                return;
            }

            float lo = 0f;
            float hi = 1f;
            var ys = new float[Samples + 1];
            for (int i = 0; i <= Samples; i++) {
                float y = EaseMath.Evaluate(easeKey, i / (float)Samples);
                ys[i] = y;
                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }
            float pad = Mathf.Max(0.05f, (hi - lo) * 0.08f);
            MinY = lo - pad;
            MaxY = hi + pad;

            const float thickness = 1.6f;
            for (int i = 0; i < Samples; i++) {
                Vector2 a = PlotPoint(i / (float)Samples, ys[i]);
                Vector2 b = PlotPoint((i + 1) / (float)Samples, ys[i + 1]);
                Vector2 dir = b - a;
                var seg = Segments[i];
                if (dir.sqrMagnitude < 1e-6f) {
                    seg.gameObject.SetActive(false);
                    continue;
                }
                seg.anchoredPosition = (a + b) * 0.5f;
                seg.sizeDelta = new Vector2(dir.magnitude + thickness * 0.7f, thickness);
                seg.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                seg.gameObject.SetActive(true);
            }
        }
    }

    public static Action Attach<T>(
        O5Dropdown<T> dropdown,
        Func<T, string> getEaseKey,
        Action<O5Object> track = null
    ) {
        if (dropdown == null || dropdown.IsDisposed || dropdown.Rect == null) {
            return static () => { };
        }

        var ctx = O5KitAdapters.Ctx;
        float rowH = ctx.Theme.ControlHeightFor(ctx.Theme.FontSizeBody);
        float boxH = Mathf.Max(18f, rowH - 12f);
        float boxW = boxH * 3f; // 3:1 aspect ratio

        var rowWidgets = new List<CurveWidget>();
        CurveWidget headerWidget = null;
        CurveWidget activeHover = null;
        float hoverClock = 0f;
        T lastHeaderValue = dropdown.Value;
        bool hasLastHeader = true;

        static Color NormalSegmentColor() => O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.90f);

        static void SetSegmentColor(CurveWidget widget, Color color) {
            for (int i = 0; i < widget.SegmentImages.Count; i++) {
                if (widget.SegmentImages[i] != null) {
                    widget.SegmentImages[i].color = color;
                }
            }
        }

        void SetHover(CurveWidget widget, bool on) {
            if (widget == null) {
                return;
            }
            widget.Hovered = on;
            if (on && !string.IsNullOrEmpty(widget.EaseKey)) {
                if (activeHover != widget && activeHover != null) {
                    if (activeHover.Dot != null) {
                        activeHover.Dot.gameObject.SetActive(false);
                    }
                    SetSegmentColor(activeHover, NormalSegmentColor());
                }
                activeHover = widget;
                hoverClock = 0f;
                SetSegmentColor(widget, Color.white);
                if (widget.Dot != null) {
                    widget.Dot.gameObject.SetActive(true);
                    widget.Dot.anchoredPosition = widget.PlotPoint(0f, EaseMath.Evaluate(widget.EaseKey, 0f));
                }
            } else if (!on) {
                SetSegmentColor(widget, NormalSegmentColor());
                if (activeHover == widget) {
                    if (widget.Dot != null) {
                        widget.Dot.gameObject.SetActive(false);
                    }
                    activeHover = null;
                }
            }
        }

        // Attach to dropdown header (positioned left of the foldout triangle)
        if (dropdown.Label != null && dropdown.Label.transform.parent is RectTransform headerRect) {
            headerWidget = CreateWidget("EaseCurve_Header", headerRect, boxW, boxH, rightOffset: -44f);
            string initKey = dropdown.Value != null ? getEaseKey(dropdown.Value) : null;
            headerWidget.SetCurve(initKey);
            dropdown.Label.rectTransform.offsetMax = new Vector2(-(boxW + 52f), 0f);

            var trigger = headerRect.gameObject.GetComponent<EventTrigger>()
                ?? headerRect.gameObject.AddComponent<EventTrigger>();
            UnityUtils.AddEvents(trigger,
                (EventTriggerType.PointerEnter, _ => SetHover(headerWidget, true)),
                (EventTriggerType.PointerExit, _ => SetHover(headerWidget, false)));
        }

        void RebuildRows() {
            if (dropdown.IsDisposed || dropdown.ListRect == null) {
                return;
            }
            foreach (var w in rowWidgets) {
                if (w.Box != null) {
                    UnityEngine.Object.Destroy(w.Box.gameObject);
                }
            }
            rowWidgets.Clear();
            if (activeHover != headerWidget) {
                activeHover = null;
            }

            int count = Mathf.Min(dropdown.Values.Count, dropdown.ListRect.childCount);
            for (int i = 0; i < count; i++) {
                var rowRect = dropdown.ListRect.GetChild(i) as RectTransform;
                if (rowRect == null) {
                    continue;
                }
                string easeKey = getEaseKey(dropdown.Values[i]);
                if (string.IsNullOrEmpty(easeKey)) {
                    continue;
                }

                var widget = CreateWidget("EaseCurve", rowRect, boxW, boxH, rightOffset: -10f);
                widget.SetCurve(easeKey);
                rowWidgets.Add(widget);

                var textChild = rowRect.Find("Text") as RectTransform;
                if (textChild != null) {
                    textChild.offsetMax = new Vector2(-(boxW + 18f), 0f);
                }

                var trigger = rowRect.gameObject.GetComponent<EventTrigger>()
                    ?? rowRect.gameObject.AddComponent<EventTrigger>();
                var captured = widget;
                UnityUtils.AddEvents(trigger,
                    (EventTriggerType.PointerEnter, _ => SetHover(captured, true)),
                    (EventTriggerType.PointerExit, _ => SetHover(captured, false)));
            }
        }

        RebuildRows();
        dropdown.OnListRebuilt += RebuildRows;

        void RefreshPalette() {
            Color c = NormalSegmentColor();
            if (headerWidget != null && activeHover != headerWidget) {
                SetSegmentColor(headerWidget, c);
            }
            for (int i = 0; i < rowWidgets.Count; i++) {
                if (activeHover != rowWidgets[i]) {
                    SetSegmentColor(rowWidgets[i], c);
                }
            }
        }

        UIColors.OnPaletteChanged += RefreshPalette;
        dropdown.OnDisposed += () => {
            dropdown.OnListRebuilt -= RebuildRows;
            UIColors.OnPaletteChanged -= RefreshPalette;
        };

        var watcher = new O5Watcher(ctx, dropdown.Id + "_ease_curves", dropdown.Rect, () => {
            if (dropdown.IsDisposed) {
                return;
            }

            if (headerWidget != null) {
                if (!hasLastHeader || !EqualityComparer<T>.Default.Equals(lastHeaderValue, dropdown.Value)) {
                    lastHeaderValue = dropdown.Value;
                    hasLastHeader = true;
                    string key = dropdown.Value != null ? getEaseKey(dropdown.Value) : null;
                    headerWidget.SetCurve(key);
                }
            }

            if (!dropdown.Expanded && activeHover != null && activeHover != headerWidget) {
                SetHover(activeHover, false);
            }

            if (activeHover != null && activeHover.Dot != null && !string.IsNullOrEmpty(activeHover.EaseKey)) {
                hoverClock = (hoverClock + Time.unscaledDeltaTime) % (MoveSeconds + HoldSeconds);
                float t = Mathf.Clamp01(hoverClock / MoveSeconds);
                float y = EaseMath.Evaluate(activeHover.EaseKey, t);
                activeHover.Dot.anchoredPosition = activeHover.PlotPoint(t, y);
            }
        });
        track?.Invoke(watcher);

        return () => {
            RebuildRows();
            if (headerWidget != null) {
                string key = dropdown.Value != null ? getEaseKey(dropdown.Value) : null;
                headerWidget.SetCurve(key);
            }
        };
    }

    private static CurveWidget CreateWidget(string name, RectTransform parent, float boxW, float boxH, float rightOffset) {
        var box = UiStyle.NewRect(name, parent);
        box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f);
        box.pivot = new Vector2(1f, 0.5f);
        box.anchoredPosition = new Vector2(rightOffset, 0f);
        box.sizeDelta = new Vector2(boxW, boxH);

        var bg = box.gameObject.AddComponent<Image>();
        bg.sprite = Factory.Page.PageDocs.GetSharpFillSprite();
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1f;
        bg.color = new Color(0f, 0f, 0f, 0.38f);
        bg.raycastTarget = false;
        box.gameObject.AddComponent<RectMask2D>();

        var widget = new CurveWidget {
            Box = box,
            BoxW = boxW,
            BoxH = boxH,
        };

        var lineColor = O5Kit.Core.O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.90f);
        for (int i = 0; i < Samples; i++) {
            var seg = UiStyle.NewRect("Seg", box);
            seg.anchorMin = seg.anchorMax = Vector2.zero;
            seg.pivot = new Vector2(0.5f, 0.5f);
            var img = seg.gameObject.AddComponent<Image>();
            img.color = lineColor;
            img.raycastTarget = false;
            widget.Segments.Add(seg);
            widget.SegmentImages.Add(img);
        }

        var dotRect = UiStyle.NewRect("Dot", box);
        dotRect.anchorMin = dotRect.anchorMax = Vector2.zero;
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(7f, 7f);
        dotRect.SetAsLastSibling();
        var dotImg = dotRect.gameObject.AddComponent<Image>();
        dotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
        dotImg.color = Color.white;
        dotImg.raycastTarget = false;
        dotRect.gameObject.SetActive(false);
        widget.Dot = dotRect;

        return widget;
    }
}
