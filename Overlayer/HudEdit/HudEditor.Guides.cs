using O5Kit.Behaviour;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// Rulers along the top and left of the free area, in screen pixels. Drag out of a ruler to
// make a guide line (top ruler: a horizontal one), drag a guide to move it, drag it back onto
// its ruler to remove it. Things snap to guides. Guides are kept as a fraction of the screen,
// so they stay in the same spot at another resolution.
internal static partial class HudEditor {
    private const float RulerPx = 22f;
    private const float GuideHitPx = 9f;

    private static readonly Color RulerColor = new(0.11f, 0.11f, 0.13f, 0.92f);
    private static readonly Color RulerTick = new(1f, 1f, 1f, 0.35f);
    private static readonly Color UserGuideColor = new(0.25f, 0.85f, 1f, 0.9f);

    private static RectTransform rulerLayer, rulerTop, rulerLeft, guideLayer;
    private static O5Kit.Control.O5Button rulersButton;
    private static TextMeshProUGUI guideLabel;
    private static readonly List<(RectTransform Rect, TextMeshProUGUI Label, bool Vertical, int Pixel)> rulerTicks = [];
    private static readonly List<RectTransform> guideRects = [];

    // The guide being dragged: from a ruler (index -1) or an existing one; Vertical = an x line.
    private static int guideDragIndex = -2;
    private static bool guideDragVertical;
    private static Vector2 guideDragPos;

    private static bool RulersShown => MainCore.Conf.HudRulers.Value;

    private static List<string> GuideList => MainCore.Conf.HudGuides.Value ??= [];

    // "x0.5" = a vertical line half way across, "y0.25" = a horizontal line a quarter up.
    private static bool TryGuide(string s, out bool vertical, out float screen) {
        vertical = s.Length > 1 && s[0] == 'x';
        screen = 0f;
        if (s.Length < 2 || (s[0] != 'x' && s[0] != 'y')
            || !float.TryParse(s.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) {
            return false;
        }
        screen = f * (vertical ? Screen.width : Screen.height);
        return true;
    }

    private static string GuideString(bool vertical, float screen)
        => (vertical ? "x" : "y") + (screen / (vertical ? Screen.width : Screen.height)).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Screen positions of the guides (x lines, y lines), for snapping.</summary>
    private static void GuideLines(List<float> xs, List<float> ys) {
        if (!RulersShown) {
            return;
        }
        foreach (var s in GuideList) {
            if (TryGuide(s, out bool vertical, out float at)) {
                (vertical ? xs : ys).Add(at);
            }
        }
    }

    private static void BuildRulers() {
        rulerLayer = NewRect("Rulers", root);
        Stretch(rulerLayer);
        guideLayer = NewRect("Guides", rulerLayer);
        Stretch(guideLayer);

        rulerTop = Band("RulerTop", false);
        rulerTop.anchorMin = new Vector2(0f, 1f);
        rulerTop.anchorMax = Vector2.one;
        rulerTop.pivot = new Vector2(0.5f, 1f);
        rulerLeft = Band("RulerLeft", true);
        rulerLeft.anchorMin = Vector2.zero;
        rulerLeft.anchorMax = new Vector2(0f, 1f);
        rulerLeft.pivot = new Vector2(0f, 0.5f);

        guideLabel = O5Factory.ControlText(O5KitAdapters.Ctx, rulerLayer, 13f, true);
        guideLabel.rectTransform.anchorMin = guideLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        guideLabel.rectTransform.sizeDelta = new Vector2(80f, 20f);
        guideLabel.color = UserGuideColor;
        guideLabel.raycastTarget = false;
        guideLabel.gameObject.SetActive(false);
        ApplyRulerLayout(0f, 0f);
    }

    private static RectTransform Band(string name, bool vertical) {
        var band = NewImage(name, rulerLayer, RulerColor);
        band.raycastTarget = true;
        band.gameObject.AddComponent<RectMask2D>();
        UnityUtils.AddEvents(band.gameObject.AddComponent<EventTrigger>(),
            // Dragging out of the top ruler makes a horizontal line, out of the left one a vertical line.
            (EventTriggerType.BeginDrag, e => BeginGuideDrag(-1, vertical, e as PointerEventData)),
            (EventTriggerType.Drag, e => DragGuide(e as PointerEventData)),
            (EventTriggerType.EndDrag, e => EndGuideDrag(e as PointerEventData)));
        return band.rectTransform;
    }

    // Rulers sit in the free area between the side panels.
    private static void ApplyRulerLayout(float left, float right) {
        if (rulerLayer == null) {
            return;
        }
        rulerLayer.gameObject.SetActive(RulersShown);
        rulerTop.offsetMin = new Vector2(left + RulerPx, -RulerPx);
        rulerTop.offsetMax = new Vector2(-right, 0f);
        rulerLeft.offsetMin = new Vector2(left, 0f);
        rulerLeft.offsetMax = new Vector2(left + RulerPx, -RulerPx);
    }

    private static void ToggleRulers() {
        MainCore.Conf.HudRulers.Value = !RulersShown;
        MainCore.ConfMgr.RequestSave();
        UpdateRulersButton();
        ApplyPanelLayout();
    }

    private static void UpdateRulersButton() {
        if (rulersButton == null) {
            return;
        }
        rulersButton.NormalColor = RulersShown ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        rulersButton.UpdateVisual(true);
    }

    // ---- dragging guides ----

    private static void BeginGuideDrag(int index, bool vertical, PointerEventData e) {
        if (e == null || e.button != PointerEventData.InputButton.Left) {
            return;
        }
        guideDragIndex = index;
        guideDragVertical = vertical;
        guideDragPos = e.position;
    }

    private static void DragGuide(PointerEventData e) {
        if (guideDragIndex < -1 || e == null) {
            return;
        }
        guideDragPos = e.position;
        // Shift: whole tens of pixels.
        if (Shift) {
            guideDragPos = new Vector2(Mathf.Round(guideDragPos.x / 10f) * 10f, Mathf.Round(guideDragPos.y / 10f) * 10f);
        }
    }

    private static void EndGuideDrag(PointerEventData e) {
        if (guideDragIndex < -1) {
            return;
        }
        int index = guideDragIndex;
        guideDragIndex = -2;
        Vector2 at = guideDragPos;
        // Dropped back on its ruler (or off the screen): gone.
        bool remove = OverRuler(guideDragVertical ? rulerLeft : rulerTop, at)
            || at.x < 0f || at.y < 0f || at.x > Screen.width || at.y > Screen.height;
        var before = GuideList.ToList();
        var list = GuideList;
        if (index >= 0 && index < list.Count) {
            list.RemoveAt(index);
        }
        if (!remove) {
            list.Add(GuideString(guideDragVertical, guideDragVertical ? at.x : at.y));
        }
        var after = list.ToList();
        if (before.SequenceEqual(after)) {
            return;
        }
        void Set(List<string> guides) {
            MainCore.Conf.HudGuides.Value = guides.ToList();
            MainCore.ConfMgr.RequestSave();
        }
        Set(after);
        undo.Push(new ActionCommand(() => Set(before), () => Set(after)));
    }

    private static bool OverRuler(RectTransform ruler, Vector2 screen)
        => ruler != null && RectTransformUtility.RectangleContainsScreenPoint(ruler, screen, null);

    // ---- drawing ----

    private static void RenderRulers() {
        if (rulerLayer == null || !RulersShown) {
            return;
        }
        // Ticks every 50 px, numbers every 100.
        if (rulerTicks.Count == 0 || rulerTicks.Count(t => t.Vertical) != Screen.height / 50 + 1
            || rulerTicks.Count(t => !t.Vertical) != Screen.width / 50 + 1) {
            BuildTicks();
        }
        foreach (var (rect, _, vertical, px) in rulerTicks) {
            var band = vertical ? rulerLeft : rulerTop;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(band, vertical ? new Vector2(0f, px) : new Vector2(px, 0f), null, out var local);
            rect.anchoredPosition = vertical ? new Vector2(0f, local.y) : new Vector2(local.x, 0f);
        }

        var lines = GuideList;
        while (guideRects.Count < lines.Count + 1) {
            guideRects.Add(NewGuideRect(guideRects.Count));
        }
        int used = 0;
        for (int i = 0; i < lines.Count; i++) {
            if (i == guideDragIndex || !TryGuide(lines[i], out bool vertical, out float at)) {
                continue;
            }
            PlaceGuide(guideRects[used++], vertical, at, i);
        }
        // The one being dragged (new or moved).
        if (guideDragIndex >= -1) {
            float at = guideDragVertical ? guideDragPos.x : guideDragPos.y;
            PlaceGuide(guideRects[used++], guideDragVertical, at, guideDragIndex);
            guideLabel.gameObject.SetActive(true);
            guideLabel.text = Mathf.RoundToInt(at).ToString();
            guideLabel.rectTransform.anchoredPosition = ToLocal(guideDragPos + new Vector2(30f, 16f) * UiScale);
            guideLabel.rectTransform.SetAsLastSibling();
        } else {
            guideLabel.gameObject.SetActive(false);
        }
        for (int i = used; i < guideRects.Count; i++) {
            guideRects[i].gameObject.SetActive(false);
        }
    }

    private static void BuildTicks() {
        foreach (var t in rulerTicks) {
            if (t.Rect != null) {
                UnityEngine.Object.Destroy(t.Rect.gameObject);
            }
        }
        rulerTicks.Clear();
        void Add(bool vertical, int px) {
            bool major = px % 100 == 0;
            var band = vertical ? rulerLeft : rulerTop;
            var tick = NewRect("Tick", band);
            tick.anchorMin = tick.anchorMax = vertical ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0f);
            tick.pivot = vertical ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0f);
            tick.sizeDelta = vertical ? new Vector2(major ? 8f : 4f, 1f) : new Vector2(1f, major ? 8f : 4f);
            tick.gameObject.AddComponent<Image>().color = RulerTick;
            tick.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI label = null;
            if (major) {
                label = O5Factory.ControlText(O5KitAdapters.Ctx, tick, 10f, true);
                label.raycastTarget = false;
                label.color = UiStyle.Caption;
                label.text = px.ToString();
                label.enableWordWrapping = false;
                var r = label.rectTransform;
                r.anchorMin = r.anchorMax = vertical ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(40f, 14f);
                if (vertical) {
                    // Numbers run up the left ruler.
                    r.localEulerAngles = new Vector3(0f, 0f, 90f);
                    r.anchoredPosition = new Vector2(-8f, 0f);
                    label.alignment = TextAlignmentOptions.Center;
                } else {
                    r.pivot = new Vector2(0f, 1f);
                    r.anchoredPosition = new Vector2(3f, 12f);
                    label.alignment = TextAlignmentOptions.TopLeft;
                }
            }
            rulerTicks.Add((tick, label, vertical, px));
        }
        for (int x = 0; x <= Screen.width; x += 50) {
            Add(false, x);
        }
        for (int y = 0; y <= Screen.height; y += 50) {
            Add(true, y);
        }
    }

    private static RectTransform NewGuideRect(int n) {
        var hit = NewImage("Guide" + n, guideLayer, Color.clear);
        hit.raycastTarget = true;
        var line = NewImage("Line", hit.rectTransform, UserGuideColor);
        Stretch(line.rectTransform);
        var rect = hit.rectTransform;
        UnityUtils.AddEvents(hit.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.BeginDrag, e => {
                if (rect.gameObject.name.StartsWith("Guide:") && int.TryParse(rect.gameObject.name.Substring(6), out int index)) {
                    BeginGuideDrag(index, rect.sizeDelta.y > rect.sizeDelta.x, e as PointerEventData);
                }
            }),
            (EventTriggerType.Drag, e => DragGuide(e as PointerEventData)),
            (EventTriggerType.EndDrag, e => EndGuideDrag(e as PointerEventData)));
        return rect;
    }

    private static void PlaceGuide(RectTransform rect, bool vertical, float screen, int index) {
        rect.gameObject.name = "Guide:" + index;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 local = ToLocal(vertical ? new Vector2(screen, 0f) : new Vector2(0f, screen));
        float span = vertical ? root.rect.height : root.rect.width;
        rect.anchoredPosition = vertical ? new Vector2(local.x, 0f) : new Vector2(0f, local.y);
        rect.sizeDelta = vertical ? new Vector2(GuideHitPx, span) : new Vector2(span, GuideHitPx);
        var line = rect.GetChild(0) as RectTransform;
        line.anchorMin = vertical ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
        line.anchorMax = vertical ? new Vector2(0.5f, 1f) : new Vector2(1f, 0.5f);
        line.sizeDelta = vertical ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
        line.anchoredPosition = Vector2.zero;
        rect.gameObject.SetActive(true);
    }

    private static void DestroyRulers() {
        rulerLayer = rulerTop = rulerLeft = guideLayer = null;
        guideLabel = null;
        rulersButton = null;
        rulerTicks.Clear();
        guideRects.Clear();
        guideDragIndex = -2;
    }
}
