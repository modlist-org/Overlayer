using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Async;
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

// Double-click a text to type into it right where it is; and the tab on the right edge that
// pulls the side panel back out when it's hidden.
internal static partial class HudEditor {
    // ---- edit text in place ----

    private static RectTransform inlineHost;
    private static O5InputField inlineField;
    private static HudElement pendingInlineEdit;

    private static bool CanEditText(HudElement el) => el != null && el.IsAlive && !el.ReadOnly
        && el.Object.Config.TextConfig != null && el.Object.Config.TextEngineConfig?.Text.UseFx != true;

    public static void BeginInlineEdit(HudElement el) {
        if (!CanEditText(el)) {
            return;
        }
        EndInlineEdit();
        if (!ReferenceEquals(Primary, el) || selection.Count != 1) {
            Select(el);
        }
        el.GetScreenCorners(corners);
        Bounds(corners, out float minX, out float maxX, out float minY, out float maxY);
        Vector2 a = ToLocal(new Vector2(minX, minY)), b = ToLocal(new Vector2(maxX, maxY));
        inlineHost = NewRect("InlineText", root);
        inlineHost.anchorMin = inlineHost.anchorMax = new Vector2(0.5f, 0.5f);
        inlineHost.pivot = new Vector2(0f, 1f);
        // At least big enough to type in comfortably.
        var size = new Vector2(Mathf.Max(b.x - a.x + 24f, 260f), Mathf.Max(b.y - a.y + 12f, 48f));
        inlineHost.sizeDelta = size;
        inlineHost.anchoredPosition = new Vector2(a.x - 12f, b.y + 6f);
        inlineHost.SetAsLastSibling();

        string text = TextOf(el.Object);
        inlineField = O5Factory.Input(O5KitAdapters.Ctx, inlineHost, "plus_hud_inline", new O5InputOptions {
            Value = text,
            Multiline = true,
            OnChanged = v => QuickEdit("text", TextOf, SetText, v, false),
            OnEndEdit = v => {
                QuickEdit("text", TextOf, SetText, v, true);
                MainThread.Enqueue(() => {
                    EndInlineEdit();
                    SyncPanelValues();
                });
            },
        });
        var r = inlineField.Rect;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        // Enter finishes; a text that already has several lines keeps Enter for new lines.
        inlineField.InputField.lineType = text.Contains('\n') ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.MultiLineSubmit;
        inlineField.InputField.ActivateInputField();
        inlineField.InputField.Select();
        inlineField.InputField.MoveTextEnd(false);
    }

    private static void EndInlineEdit() {
        if (inlineField != null) {
            var field = inlineField;
            inlineField = null;
            field.Dispose();
        }
        if (inlineHost != null) {
            UnityEngine.Object.Destroy(inlineHost.gameObject);
            inlineHost = null;
        }
    }

    // ---- pull tab ----

    private const float TabWidth = 22f;
    private const float TabHeight = 104f;

    private static RectTransform pullTab;
    private static bool pullingPanel;
    private static readonly Color TabColor = new(0.2f, 0.2f, 0.25f, 0.95f);
    private static readonly Color TabHover = new(0.28f, 0.28f, 0.36f, 0.98f);
    private static float pulled;

    private static void BuildPullTab() {
        // Plain: the rounded sprite's corners are wider than this narrow tab.
        var tab = NewImage("PullTab", root, TabColor);
        tab.raycastTarget = true;
        pullTab = tab.rectTransform;
        pullTab.anchorMin = pullTab.anchorMax = new Vector2(1f, 0.5f);
        pullTab.pivot = new Vector2(1f, 0.5f);
        // Pokes out from the right edge (the rounded right side goes off screen).
        pullTab.sizeDelta = new Vector2(TabWidth + 10f, TabHeight);
        pullTab.anchoredPosition = new Vector2(10f, 0f);
        for (int i = 0; i < 3; i++) {
            var line = NewImage("Grip", pullTab, new Color(1f, 1f, 1f, 0.75f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(2f, 28f);
            line.rectTransform.anchoredPosition = new Vector2(7f + i * 5f, 0f);
        }
        UnityUtils.AddEvents(tab.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => tab.color = TabHover),
            (EventTriggerType.PointerExit, _ => tab.color = TabColor),
            (EventTriggerType.PointerClick, _ => OpenPanelFromTab()),
            (EventTriggerType.BeginDrag, _ => {
                pullingPanel = true;
                pulled = 0f;
                ApplyPanelLayout();
                SlidePanel();
            }),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe) {
                    pulled = Mathf.Clamp(pulled - pe.delta.x / UiScale, 0f, PanelWidth + Margin);
                    SlidePanel();
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                pullingPanel = false;
                // Pulled out a third of the way or more: it stays (pinned).
                if (pulled >= PanelWidth * 0.3f) {
                    OpenPanelFromTab();
                } else {
                    panel.anchoredPosition = new Vector2(-Margin, -Margin);
                    ApplyPanelLayout();
                }
            }));
        pullTab.AddToolTip(O5KitAdapters.Ctx, "Pull out (or click) to show the panel");
    }

    // Hidden panel slides in from the right edge as far as it was pulled.
    private static void SlidePanel() {
        if (panel == null) {
            return;
        }
        panel.anchoredPosition = new Vector2(-Margin + (PanelWidth + Margin) - pulled, -Margin);
        pullTab.anchoredPosition = new Vector2(10f - pulled, 0f);
    }

    private static void OpenPanelFromTab() {
        pullingPanel = false;
        O5KitAdapters.Ctx.Tooltip.Hide();
        MainCore.Conf.HudPanelPinned.Value = true;
        MainCore.ConfMgr.RequestSave();
        UpdatePinLabel();
        panel.anchoredPosition = new Vector2(-Margin, -Margin);
        ApplyPanelLayout();
    }

    private static void UpdatePullTab() {
        if (pullTab == null) {
            return;
        }
        pullTab.gameObject.SetActive(!panelShown || pullingPanel);
        if (!pullingPanel) {
            pullTab.anchoredPosition = new Vector2(10f, 0f);
        }
        pullTab.SetAsLastSibling();
    }
}
