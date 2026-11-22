using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.HudEdit;

// Auto anchor: divides the parent/screen into a 3x3 (9-zone) grid, automatically pins the
// selected object's anchor & pivot to the nearest of the 9 points, and draws a connecting
// line between the object's 9-point and the screen/parent's 9-point with a 0.3s ease.
internal static partial class HudEditor {
    private const float AutoAnchorEaseDuration = 0.3f;
    private const float AutoAnchorEdgeInset = 8f;
    // Hysteresis deadzone: must cross 3/4 of the 0.5 step between adjacent anchors (0, 0.5, 1) to switch.
    private const float AutoAnchorLowThreshold = 0.125f;     // 0.5 -> 0   (3/4 back from 0.5 to 0)
    private const float AutoAnchorMidLowThreshold = 0.375f;  // 0   -> 0.5 (3/4 from 0 to 0.5)
    private const float AutoAnchorMidHighThreshold = 0.625f; // 1   -> 0.5 (3/4 back from 1 to 0.5)
    private const float AutoAnchorHighThreshold = 0.875f;    // 0.5 -> 1   (3/4 from 0.5 to 1)
    private const float AutoAnchorDotOuter = 10f;
    private const float AutoAnchorDotInner = 6f;

    private static Color AutoAnchorLineColor => O5Kit.Core.O5Palette.WithAlpha(Overlayer.UI.UIColors.ObjectActive, 0.85f);

    private static O5Kit.Control.O5Button autoAnchorButton;
    private static RectTransform autoAnchorLine;
    private static Image autoAnchorLineImage;
    private static RectTransform autoAnchorObjDot;
    private static RectTransform autoAnchorRefDot;

    private static HudElement autoAnchorTracked;
    private static Vector2 autoAnchorCur = new(0.5f, 0.5f);
    private static Vector2 autoAnchorFrom = new(0.5f, 0.5f);
    private static Vector2 autoAnchorTarget = new(0.5f, 0.5f);
    private static float autoAnchorAnimStart = -10f;

    private static readonly Vector3[] autoAnchorCorners = new Vector3[4];
    private static readonly Vector3[] autoAnchorRefCorners = new Vector3[4];

    private static bool AutoAnchorOn => MainCore.Conf.HudAutoAnchor.Value;

    private static void BuildAutoAnchorGizmos() {
        autoAnchorLineImage = NewImage("AutoAnchorLine", gizmoLayer, AutoAnchorLineColor);
        autoAnchorLine = autoAnchorLineImage.rectTransform;
        autoAnchorLine.gameObject.SetActive(false);

        autoAnchorObjDot = BuildAutoAnchorDot("AutoAnchorObjDot", pixelHandleLayer);
        autoAnchorRefDot = BuildAutoAnchorDot("AutoAnchorRefDot", pixelHandleLayer);
        ResetAutoAnchorAnim();
    }

    private static RectTransform BuildAutoAnchorDot(string name, Transform parent) {
        var circle = MainCore.Spr.Get(UISprite.Circle256);
        var host = NewRect(name, parent);
        host.anchorMin = host.anchorMax = Vector2.zero;
        host.pivot = new Vector2(0.5f, 0.5f);
        host.sizeDelta = new Vector2(AutoAnchorDotOuter, AutoAnchorDotOuter);

        var ring = NewImage("Ring", host, SelectedColor, circle);
        Stretch(ring.rectTransform);

        var core = NewImage("Core", host, Color.white, circle);
        var cr = core.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(AutoAnchorDotInner, AutoAnchorDotInner);

        host.gameObject.SetActive(false);
        return host;
    }

    private static void DestroyAutoAnchorGizmos() {
        autoAnchorButton = null;
        autoAnchorLine = null;
        autoAnchorLineImage = null;
        autoAnchorObjDot = null;
        autoAnchorRefDot = null;
        ResetAutoAnchorAnim();
    }

    private static void ResetAutoAnchorAnim() {
        autoAnchorTracked = null;
        autoAnchorAnimStart = -10f;
    }

    private static void ToggleAutoAnchor() {
        MainCore.Conf.HudAutoAnchor.Value = !AutoAnchorOn;
        MainCore.ConfMgr.RequestSave();
        UpdateAutoAnchorButton();
        ResetAutoAnchorAnim();

        if (!AutoAnchorOn || selection.Count == 0) {
            return;
        }
        CommitPanelEdit();
        var changes = new List<(HudElement, HudElement.State, HudElement.State)>();
        foreach (var el in selection) {
            if (!el.IsAlive || !CanPin(el)) {
                continue;
            }
            var before = el.Capture();
            if (ApplyAutoAnchorToElement(el)) {
                changes.Add((el, before, el.Capture()));
            }
        }
        if (changes.Count > 0) {
            Commit(changes);
        }
    }

    private static void UpdateAutoAnchorButton() {
        if (autoAnchorButton == null) {
            return;
        }
        autoAnchorButton.NormalColor = AutoAnchorOn ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        autoAnchorButton.UpdateVisual(true);
    }

    private static float SnapThird(float t) => t < (1f / 3f) ? 0f : t > (2f / 3f) ? 1f : 0.5f;

    private static bool TryGetGridAxis(float value, out float snapped) {
        if (Mathf.Abs(value) < 1e-3f) {
            snapped = 0f;
            return true;
        }
        if (Mathf.Abs(value - 0.5f) < 1e-3f) {
            snapped = 0.5f;
            return true;
        }
        if (Mathf.Abs(value - 1f) < 1e-3f) {
            snapped = 1f;
            return true;
        }
        snapped = 0.5f;
        return false;
    }

    private static float SnapAxisWithDeadzone(float t, float? currentGrid) {
        if (!currentGrid.HasValue) {
            return SnapThird(t);
        }
        float cur = currentGrid.Value;
        if (cur < 0.25f) {
            return t >= AutoAnchorHighThreshold ? 1f : t >= AutoAnchorMidLowThreshold ? 0.5f : 0f;
        }
        if (cur > 0.75f) {
            return t <= AutoAnchorLowThreshold ? 0f : t <= AutoAnchorMidHighThreshold ? 0.5f : 1f;
        }
        return t <= AutoAnchorLowThreshold ? 0f : t >= AutoAnchorHighThreshold ? 1f : 0.5f;
    }

    private static bool TryGetCurrentGridAnchor(HudElement el, out Vector2 grid) {
        var cfg = el?.Config;
        if (cfg != null) {
            Vector2 min = cfg.AnchorMin.StaticValue;
            Vector2 max = cfg.AnchorMax.StaticValue;
            Vector2 pivot = cfg.Pivot.StaticValue;
            if ((min - max).sqrMagnitude < 1e-4f
                && (min - pivot).sqrMagnitude < 1e-4f
                && TryGetGridAxis(min.x, out float gx)
                && TryGetGridAxis(min.y, out float gy)) {
                grid = new Vector2(gx, gy);
                return true;
            }
        }
        grid = default;
        return false;
    }

    // Reference box in screen pixels: parent rect if valid, otherwise the full screen.
    private static Rect GetAutoAnchorReferenceRect(HudElement el) {
        if (el?.Rect?.parent is RectTransform parentRect) {
            parentRect.GetWorldCorners(autoAnchorRefCorners);
            Bounds(autoAnchorRefCorners, out float minX, out float maxX, out float minY, out float maxY);
            if (maxX - minX >= 1f && maxY - minY >= 1f) {
                return Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
        }
        return Rect.MinMaxRect(0f, 0f, Screen.width, Screen.height);
    }

    private static Vector2 ComputeAutoAnchor(HudElement el, out Rect refRect) {
        refRect = GetAutoAnchorReferenceRect(el);
        el.GetScreenCorners(autoAnchorCorners);
        Vector2 center = (autoAnchorCorners[0] + autoAnchorCorners[1] + autoAnchorCorners[2] + autoAnchorCorners[3]) * 0.25f;
        float nx = refRect.width > 1e-3f ? Mathf.Clamp01((center.x - refRect.xMin) / refRect.width) : 0.5f;
        float ny = refRect.height > 1e-3f ? Mathf.Clamp01((center.y - refRect.yMin) / refRect.height) : 0.5f;
        bool hasCurrent = TryGetCurrentGridAnchor(el, out Vector2 current);
        if (!hasCurrent && ReferenceEquals(el, autoAnchorTracked)) {
            hasCurrent = true;
            current = autoAnchorTarget;
        }
        return new Vector2(
            SnapAxisWithDeadzone(nx, hasCurrent ? current.x : null),
            SnapAxisWithDeadzone(ny, hasCurrent ? current.y : null));
    }

    /// <summary>
    /// Updates <paramref name="el"/>'s anchor &amp; pivot to the nearest 9-division point
    /// while preserving its exact screen position. Returns true if the anchor/pivot changed.
    /// </summary>
    private static bool ApplyAutoAnchorToElement(HudElement el) {
        if (!AutoAnchorOn || el == null || !el.IsAlive || !CanPin(el)) {
            return false;
        }
        Vector2 target = ComputeAutoAnchor(el, out _);
        var cfg = el.Config;
        if ((cfg.AnchorMin.StaticValue - target).sqrMagnitude < 1e-6f
            && (cfg.AnchorMax.StaticValue - target).sqrMagnitude < 1e-6f
            && (cfg.Pivot.StaticValue - target).sqrMagnitude < 1e-6f) {
            return false;
        }
        Vector2 size = el.Rect.rect.size;
        if (el.Rect.parent is RectTransform parentRect) {
            Vector2 oldPivot = el.Rect.pivot;
            Vector3 localNewPivot = new((target.x - oldPivot.x) * size.x, (target.y - oldPivot.y) * size.y, 0f);
            Vector3 parentLocalPoint = el.Rect.localPosition + (el.Rect.localRotation * Vector3.Scale(localNewPivot, el.Rect.localScale));
            Rect pRect = parentRect.rect;
            Vector2 anchorInParent = new(pRect.xMin + target.x * pRect.width, pRect.yMin + target.y * pRect.height);
            cfg.AnchorMin.StaticValue = target;
            cfg.AnchorMax.StaticValue = target;
            cfg.Pivot.StaticValue = target;
            cfg.SizeDelta.StaticValue = size;
            cfg.AnchoredPosition.StaticValue = (Vector2)parentLocalPoint - anchorInParent;
            cfg.ToUnity(el.Object.GameObject);
        } else {
            el.Rect.GetWorldCorners(autoAnchorCorners);
            Vector2 cornerBefore = autoAnchorCorners[0];
            cfg.AnchorMin.StaticValue = target;
            cfg.AnchorMax.StaticValue = target;
            cfg.Pivot.StaticValue = target;
            cfg.SizeDelta.StaticValue = size;
            cfg.ToUnity(el.Object.GameObject);
            el.Rect.GetWorldCorners(autoAnchorCorners);
            cfg.AnchoredPosition.StaticValue += el.ScreenToParentDelta(cornerBefore - (Vector2)autoAnchorCorners[0]);
            cfg.ToUnity(el.Object.GameObject);
        }
        return true;
    }

    private static float EvalAutoAnchorEase(float t) {
        if (t <= 0f) {
            return 0f;
        }
        if (t >= 1f) {
            return 1f;
        }
        // Normalized OutExpo so t=1 reaches exact 1.0 cleanly.
        const float norm = 1f / (1f - 0.0009765625f);
        return (1f - Mathf.Pow(2f, -10f * t)) * norm;
    }

    private static void RenderAutoAnchor() {
        if (autoAnchorLine == null || autoAnchorObjDot == null || autoAnchorRefDot == null) {
            return;
        }
        var el = AutoAnchorOn && selection.Count == 1 && Primary is { IsAlive: true } p ? p : null;
        if (el == null) {
            autoAnchorLine.gameObject.SetActive(false);
            autoAnchorObjDot.gameObject.SetActive(false);
            autoAnchorRefDot.gameObject.SetActive(false);
            autoAnchorTracked = null;
            return;
        }

        Vector2 target = ComputeAutoAnchor(el, out Rect refRect);
        if (!ReferenceEquals(el, autoAnchorTracked)) {
            autoAnchorTracked = el;
            autoAnchorCur = autoAnchorFrom = autoAnchorTarget = target;
            autoAnchorAnimStart = -10f;
        } else if ((target - autoAnchorTarget).sqrMagnitude > 1e-4f) {
            autoAnchorFrom = autoAnchorCur;
            autoAnchorTarget = target;
            autoAnchorAnimStart = Time.unscaledTime;
        }

        float elapsed = Time.unscaledTime - autoAnchorAnimStart;
        if (elapsed >= 0f && elapsed < AutoAnchorEaseDuration) {
            float eased = EvalAutoAnchorEase(elapsed / AutoAnchorEaseDuration);
            autoAnchorCur = Vector2.LerpUnclamped(autoAnchorFrom, autoAnchorTarget, eased);
        } else {
            autoAnchorCur = autoAnchorTarget;
        }

        // Object dot: bilinearly interpolated on the object's screen quad (works even when rotated).
        el.GetScreenCorners(autoAnchorCorners);
        Vector2 bottom = Vector2.LerpUnclamped(autoAnchorCorners[0], autoAnchorCorners[3], autoAnchorCur.x);
        Vector2 top = Vector2.LerpUnclamped(autoAnchorCorners[1], autoAnchorCorners[2], autoAnchorCur.x);
        Vector2 objPt = Vector2.LerpUnclamped(bottom, top, autoAnchorCur.y);

        // Screen/parent dot: slight inset only on edges flush with the screen border so the dot is never clipped.
        float minX = refRect.xMin <= 1f ? refRect.xMin + AutoAnchorEdgeInset : refRect.xMin;
        float maxX = refRect.xMax >= Screen.width - 1f ? refRect.xMax - AutoAnchorEdgeInset : refRect.xMax;
        float minY = refRect.yMin <= 1f ? refRect.yMin + AutoAnchorEdgeInset : refRect.yMin;
        float maxY = refRect.yMax >= Screen.height - 1f ? refRect.yMax - AutoAnchorEdgeInset : refRect.yMax;
        Vector2 refPt = new(
            Mathf.LerpUnclamped(minX, maxX, autoAnchorCur.x),
            Mathf.LerpUnclamped(minY, maxY, autoAnchorCur.y));

        if ((refPt - objPt).sqrMagnitude > 1f) {
            if (autoAnchorLineImage != null) {
                autoAnchorLineImage.color = AutoAnchorLineColor;
            }
            PlacePixelLine(autoAnchorLine, objPt, refPt, 1.5f);
            autoAnchorLine.SetAsLastSibling();
        } else {
            autoAnchorLine.gameObject.SetActive(false);
        }

        autoAnchorObjDot.anchoredPosition = objPt;
        autoAnchorObjDot.gameObject.SetActive(true);
        autoAnchorObjDot.SetAsLastSibling();

        autoAnchorRefDot.anchoredPosition = refPt;
        autoAnchorRefDot.gameObject.SetActive(true);
        autoAnchorRefDot.SetAsLastSibling();
    }
}
