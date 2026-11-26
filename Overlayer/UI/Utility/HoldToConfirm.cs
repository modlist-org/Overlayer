using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Input;
using Overlayer.Compat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Utility;

/// <summary>
/// osu!lazer HoldToConfirmContainer-style dangerous button helper:
/// holding left mouse fills crimson red from left to right (and tints the button outline)
/// and fires <c>onConfirm</c> at 100%; holding Shift confirms immediately, and releasing or leaving early smoothly drains back.
/// </summary>
internal static class HoldToConfirm {
    private static readonly Color FillRed = new(0.90f, 0.20f, 0.26f, 0.90f);
    private static readonly Color OutlineRed = new(0.95f, 0.22f, 0.28f, 1f);

    private sealed class Entry {
        public GameObject ButtonGo;
        public RectTransform FillRect;
        public Image OutlineImg;
        public Action OnConfirm;
        public Func<bool> CanConfirm;
        public Action OnBlockedClick;
        public float Duration;
        public float Progress;
        public bool Holding;
        public bool KeyboardHolding;
        public bool Hovered;
    }

    private static readonly List<Entry> Active = [];
    private static readonly Dictionary<int, Entry> ById = [];

    public static bool IsHoldTarget(GameObject go) {
        if (go == null) {
            return false;
        }
        if (ById.TryGetValue(go.GetInstanceID(), out var e)) {
            return e.CanConfirm == null || e.CanConfirm();
        }
        return go.transform.Find("HoldFillClip") != null;
    }

    public static bool SetKeyboardHold(GameObject go, bool holding, bool instantConfirm) {
        if (go == null || !ById.TryGetValue(go.GetInstanceID(), out var entry)) {
            return false;
        }
        if (holding) {
            if (entry.CanConfirm != null && !entry.CanConfirm()) {
                entry.OnBlockedClick?.Invoke();
                return false;
            }
            if (instantConfirm) {
                ConfirmNow(entry);
                return false;
            }
            entry.Holding = true;
            entry.KeyboardHolding = true;
            if (!Active.Contains(entry)) {
                Active.Add(entry);
            }
            return true;
        }
        entry.KeyboardHolding = false;
        if (!O5Input.GetMouseButton(0)) {
            entry.Holding = false;
        }
        return false;
    }

    public static void Attach(
        O5Button button,
        Action onConfirm,
        float duration = 0.55f,
        Func<bool> canConfirm = null,
        Action onBlockedClick = null) {
        if (button?.Rect == null) {
            return;
        }
        button.OnClick = null;

        var clipRect = UiStyle.NewRect("HoldFillClip", button.Rect);
        clipRect.SetSiblingIndex(0);
        clipRect.anchorMin = Vector2.zero;
        clipRect.anchorMax = Vector2.one;
        clipRect.offsetMin = clipRect.offsetMax = Vector2.zero;

        var clipImg = clipRect.gameObject.AddComponent<Image>();
        if (button.Background != null) {
            clipImg.sprite = button.Background.sprite;
            clipImg.type = button.Background.type;
        }
        clipImg.color = Color.white;
        clipImg.raycastTarget = false;
        clipRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var fillRect = UiStyle.NewRect("Fill", clipRect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        var fillImg = fillRect.gameObject.AddComponent<Image>();
        fillImg.color = FillRed;
        fillImg.raycastTarget = false;

        var outlineImg = button.Rect.Find("Hover")?.GetComponent<Image>();
        var go = button.Rect.gameObject;

        var entry = new Entry {
            ButtonGo = go,
            FillRect = fillRect,
            OutlineImg = outlineImg,
            OnConfirm = onConfirm,
            CanConfirm = canConfirm,
            OnBlockedClick = onBlockedClick,
            Duration = Mathf.Max(0.15f, duration),
        };
        ById[go.GetInstanceID()] = entry;

        var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, _ => entry.Hovered = true),
            (EventTriggerType.PointerDown, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe && pe.button == PointerEventData.InputButton.Left
                    && (entry.CanConfirm == null || entry.CanConfirm())) {
                    if (IsShiftHeld()) {
                        ConfirmNow(entry);
                        return;
                    }
                    entry.Holding = true;
                    if (!Active.Contains(entry)) {
                        Active.Add(entry);
                    }
                }
            }),
            (EventTriggerType.PointerClick, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe && pe.button == PointerEventData.InputButton.Left
                    && entry.CanConfirm != null && !entry.CanConfirm()) {
                    entry.OnBlockedClick?.Invoke();
                }
            }),
            (EventTriggerType.PointerUp, _ => entry.Holding = false),
            (EventTriggerType.PointerExit, _ => {
                entry.Hovered = false;
                entry.Holding = false;
            }));
    }

    public static void Tick(float dt) {
        if (Active.Count == 0) {
            return;
        }

        float? maxHoldingProgress = null;
        bool mouseDown = O5Input.GetMouseButton(0);

        for (int i = Active.Count - 1; i >= 0; i--) {
            var e = Active[i];
            if (e.ButtonGo == null || !e.ButtonGo.activeInHierarchy || e.FillRect == null) {
                if (e.ButtonGo != null) {
                    ById.Remove(e.ButtonGo.GetInstanceID());
                }
                Active.RemoveAt(i);
                continue;
            }

            if (e.Holding && (mouseDown || e.KeyboardHolding)) {
                e.Progress = IsShiftHeld() ? 1f : Mathf.Min(1f, e.Progress + dt / e.Duration);
            } else {
                e.Holding = false;
                e.KeyboardHolding = false;
                e.Progress = Mathf.Max(0f, e.Progress - dt / 0.18f);
            }

            e.FillRect.anchorMax = new Vector2(e.Progress, 1f);
            ApplyOutlineColor(e);

            if (e.Progress >= 1f) {
                ConfirmNow(e);
                if (i > Active.Count) {
                    i = Active.Count;
                }
                continue;
            }

            if (e.Holding) {
                maxHoldingProgress = maxHoldingProgress.HasValue
                    ? Mathf.Max(maxHoldingProgress.Value, e.Progress)
                    : e.Progress;
            } else if (e.Progress <= 0f) {
                Active.RemoveAt(i);
            }
        }

        UISound.SetHoldCharge(maxHoldingProgress);
    }

    private static bool IsShiftHeld() =>
        O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

    private static void ConfirmNow(Entry e) {
        e.Holding = false;
        e.KeyboardHolding = false;
        e.Progress = 0f;
        if (e.FillRect != null) {
            e.FillRect.anchorMax = new Vector2(0f, 1f);
        }
        ApplyOutlineColor(e);
        Active.Remove(e);
        UISound.SetHoldCharge(null);
        UISound.PlayConfirm();
        e.OnConfirm?.Invoke();
    }

    private static void ApplyOutlineColor(Entry e) {
        if (e.OutlineImg == null || O5KitAdapters.Ctx == null) {
            return;
        }
        var theme = O5KitAdapters.Ctx.Theme;
        Color baseColor = e.Hovered ? theme.ControlOutlineHover : theme.ControlOutlineIdle;
        e.OutlineImg.color = Color.Lerp(baseColor, OutlineRed, e.Progress);
    }

    public static void ClearAll() {
        Active.Clear();
        ById.Clear();
        UISound.SetHoldCharge(null);
    }
}
