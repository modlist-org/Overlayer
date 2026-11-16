using O5Kit.Behaviour;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
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

// Pieces of the side panel come off by dragging: grab a section's title (or a card's header in
// All properties: Object, Rect Transform, Text ...) and drag it out of the panel, and it becomes
// its own window that follows the pointer. Drag a window by its title back onto the panel (or
// press its X) to put it back. Which ones are out, and where, is remembered.
internal static partial class HudEditor {
    private const float DockTitleHeight = 34f;
    private const float MinDockWidth = 300f;
    private const float MinDockHeight = 120f;
    private const string CardPrefix = "card:";

    private sealed class Dock {
        public string Id;
        public string Title;
        public RectTransform Section; // panel sections: header + body, in the panel (null for cards)
        public RectTransform Body;    // what moves (a panel section's body, or a card)
        public RectTransform Window;  // null while docked
        public RectTransform Content; // the window's scroll content
        public Func<bool> Visible;
    }

    private static readonly List<Dock> docks = [];
    // Title rows you can grab: what tearing one off does.
    private static readonly Dictionary<Transform, Action> grabbable = [];
    private static Transform pressedHeader;
    // What exactly was pressed and how tall its card was then (cards fold on press).
    private static GameObject pressedObject;
    private static float pressedHeight;
    private static Dock tearing;
    // A window being carried by something inside it (a card's header), and where it was grabbed.
    private static Dock moving;
    private static Vector2 movingOffset;
    // A window being carried by its title bar (drag events).
    private static Dock titleDragging;
    private static Image dropHint;

    /// <summary>A section with a title you can drag out; returns where its controls go.</summary>
    private static RectTransform DockSection(Transform parent, string id, string title, Func<bool> visible) {
        var section = VerticalGroup("Dock_" + id, parent);
        var label = UiStyle.Section(section, title);
        var header = (RectTransform)label.transform.parent;
        // Something the pointer can land on (the caption itself doesn't take clicks).
        header.gameObject.AddComponent<Image>().color = Color.clear;
        var body = VerticalGroup("Body", section);
        var dock = new Dock { Id = id, Title = title, Section = section, Body = body, Visible = visible };
        docks.Add(dock);
        grabbable[header] = () => Tear(dock);
        header.AddToolTip(O5KitAdapters.Ctx, "Drag out of the panel to make it a window");
        return body;
    }

    // ---- inspector cards ----

    /// <summary>After All properties was (re)built: cards can be dragged out, ones that are out go to their windows.</summary>
    private static void SetUpCards(RectTransform content) {
        foreach (var key in grabbable.Keys.Where(k => k == null || k.name == "Header").ToList()) {
            grabbable.Remove(key);
        }
        for (int i = 0; i < content.childCount; i++) {
            var card = (RectTransform)content.GetChild(i);
            var header = card.Find("Header");
            string title = header?.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (header == null || title == null) {
                continue;
            }
            string id = CardPrefix + title;
            var dock = docks.FirstOrDefault(d => d.Id == id);
            if (dock?.Window != null) {
                dock.Body = card;
                card.SetParent(dock.Content, false);
                i--;
            }
            grabbable[header] = () => {
                var d = docks.FirstOrDefault(x => x.Id == id) ?? NewCardDock(id, title);
                d.Body = card;
                // The press that started the drag folded the card (cards fold on mouse down): unfold.
                var fold = pressedObject != null ? pressedObject.GetComponentInParent<OventHandler>() : null;
                if (fold != null && fold.name == "TitleTrigger" && fold.transform.IsChildOf(card)) {
                    fold.OnClick?.Invoke(PointerEventData.InputButton.Left);
                }
                Tear(d, pressedHeight);
            };
        }
    }

    private static Dock NewCardDock(string id, string title) {
        Dock dock = null;
        dock = new Dock {
            Id = id,
            Title = title,
            // Shown while this object has that card.
            Visible = () => inspector != null && dock.Content != null && dock.Content.childCount > 0,
        };
        docks.Add(dock);
        return dock;
    }

    // ---- tearing off ----

    private static void Tear(Dock dock, float bodyHeight = 0f) {
        if (dock.Window != null) {
            return;
        }
        O5KitAdapters.Ctx.Tooltip.Hide();
        Vector2 at = ToLocal(Input.mousePosition);
        float height = bodyHeight > 0f ? Mathf.Clamp(bodyHeight + DockTitleHeight + 28f, MinDockHeight, 640f) : 0f;
        Detach(dock, new Rect(at.x - 60f, at.y + DockTitleHeight * 0.5f, 0f, height));
        tearing = dock;
    }

    // Per frame (before drawing): grab / tear / follow, and show windows only when they'd show.
    private static void TickDocks() {
        Vector2 mouse = Input.mousePosition;
        if (Input.GetMouseButtonDown(0)) {
            pressedHeader = GrabbableUnder(mouse);
            pressedObject = pressedHeader != null && raycastHits.Count > 0 ? raycastHits[0].gameObject : null;
            pressedHeight = pressedHeader != null && pressedHeader.parent is RectTransform card ? card.rect.height : 0f;
            // A header inside a window that's already out: carry the window.
            var owner = pressedHeader == null ? null : docks.FirstOrDefault(d => d.Window != null && pressedHeader.IsChildOf(d.Window));
            if (owner != null) {
                moving = owner;
                movingOffset = owner.Window.anchoredPosition - ToLocal(mouse);
                pressedHeader = null;
            }
        }
        bool overPanel = panel != null && panelShown && RectTransformUtility.RectangleContainsScreenPoint(panel, mouse, null);
        if (!Input.GetMouseButton(0)) {
            pressedHeader = null;
            // Let go over the panel: back in.
            var carried = tearing ?? moving;
            tearing = moving = null;
            if (carried != null) {
                if (overPanel) {
                    Attach(carried);
                } else {
                    SaveDocks();
                }
            }
        } else if (tearing != null && tearing.Window != null) {
            Vector2 at = ToLocal(mouse);
            tearing.Window.anchoredPosition = new Vector2(at.x - 60f, at.y + DockTitleHeight * 0.5f);
        } else if (moving != null && moving.Window != null) {
            moving.Window.anchoredPosition = ToLocal(mouse) + movingOffset;
        } else if (pressedHeader != null && panel != null
            && !RectTransformUtility.RectangleContainsScreenPoint(panel, mouse, null)
            && grabbable.TryGetValue(pressedHeader, out var tear)) {
            pressedHeader = null;
            tear();
        }

        ShowDropHint((tearing != null || moving != null || titleDragging != null) && overPanel);

        foreach (var dock in docks) {
            bool visible = dock.Visible();
            if (dock.Window != null) {
                dock.Window.gameObject.SetActive(visible || ReferenceEquals(dock, tearing));
            } else if (dock.Section != null && dock.Section.gameObject.activeSelf != visible) {
                dock.Section.gameObject.SetActive(visible);
            }
        }
    }

    private static Transform GrabbableUnder(Vector2 mouse) {
        var events = EventSystem.current;
        if (events == null) {
            return null;
        }
        raycastHits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = mouse }, raycastHits);
        if (raycastHits.Count == 0 || raycastHits[0].gameObject == null) {
            return null;
        }
        for (var t = raycastHits[0].gameObject.transform; t != null && t != root; t = t.parent) {
            if (grabbable.ContainsKey(t)) {
                return t;
            }
        }
        return null;
    }

    // ---- windows ----

    private static void Detach(Dock dock, Rect place) {
        var ctx = O5KitAdapters.Ctx;
        float width = place.width > 0f ? place.width : Mathf.Max(MinDockWidth, PanelWidth - 36f);
        float height = place.height;
        if (height <= 0f) {
            float wanted = 200f;
            if (dock.Body != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(dock.Body);
                wanted = LayoutUtility.GetPreferredHeight(dock.Body);
            }
            height = Mathf.Clamp(wanted + DockTitleHeight + 28f, MinDockHeight, 640f);
        }

        var window = UiStyle.Rounded("Dock_" + dock.Id, root, UiStyle.Surface).rectTransform;
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0f, 1f);
        window.anchoredPosition = place.position;
        window.sizeDelta = new Vector2(Mathf.Max(MinDockWidth, width), Mathf.Max(MinDockHeight, height));
        window.GetComponent<Image>().raycastTarget = true;
        dock.Window = window;

        // Title bar: drag to move; dropped on the panel it goes back in.
        var bar = NewImage("Title", window, Color.clear);
        bar.raycastTarget = true;
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.sizeDelta = new Vector2(0f, DockTitleHeight);
        UnityUtils.AddEvents(bar.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe) {
                    titleDragging = dock;
                    window.anchoredPosition += pe.delta / UiScale;
                }
            }),
            (EventTriggerType.EndDrag, e => {
                titleDragging = null;
                if (e is PointerEventData pe && panelShown && RectTransformUtility.RectangleContainsScreenPoint(panel, pe.position, null)) {
                    Attach(dock);
                } else {
                    SaveDocks();
                }
            }));
        var title = O5Factory.ControlText(ctx, bar.rectTransform, 13f, true);
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(14f, 0f);
        title.rectTransform.offsetMax = new Vector2(-40f, 0f);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 6f;
        title.color = UiStyle.Caption;
        title.raycastTarget = false;
        // A card shows its own name in its header.
        title.text = dock.Section != null ? dock.Title.ToUpperInvariant() : string.Empty;
        var backCell = NewRect("PutBack", bar.rectTransform);
        backCell.anchorMin = backCell.anchorMax = backCell.pivot = new Vector2(1f, 0.5f);
        backCell.anchoredPosition = new Vector2(-8f, 0f);
        backCell.sizeDelta = new Vector2(24f, 24f);
        var back = Track(UiStyle.IconButton(backCell, "plus_hud_undock_" + dock.Id.Replace(' ', '_'), MainCore.Spr.Get(UISprite.X128), () => Attach(dock), 7f));
        UiStyle.Tooltip(back, "Put it back in the panel (or drag it onto the panel)");

        var scrollHost = NewRect("Scroll", window);
        Stretch(scrollHost);
        scrollHost.offsetMin = new Vector2(12f, 12f);
        scrollHost.offsetMax = new Vector2(-12f, -DockTitleHeight);
        var (_, content, _) = O5Factory.ScrollView(ctx, scrollHost, 8f, 0f);
        dock.Content = content;
        dock.Body?.SetParent(content, false);
        dock.Section?.gameObject.SetActive(false);

        DockResizeGrip(dock);
    }

    // The panel lights up while a window held over it would go back in when let go.
    private static void ShowDropHint(bool show) {
        if (panel == null) {
            return;
        }
        if (dropHint == null) {
            dropHint = UiStyle.Rounded("DropHint", panel, new Color(0.569f, 0.604f, 1f, 0.16f));
            dropHint.raycastTarget = false;
            Stretch(dropHint.rectTransform);
        }
        dropHint.gameObject.SetActive(show);
        if (show) {
            dropHint.rectTransform.SetAsLastSibling();
        }
    }

    private static void DockResizeGrip(Dock dock) {
        var grip = NewImage("Resize", dock.Window, Color.clear);
        grip.raycastTarget = true;
        var r = grip.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
        r.sizeDelta = new Vector2(22f, 22f);
        var mark = NewImage("Mark", r, UiStyle.Muted);
        mark.rectTransform.sizeDelta = new Vector2(12f, 1.5f);
        mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        UnityUtils.AddEvents(grip.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe && dock.Window != null) {
                    var size = dock.Window.sizeDelta + new Vector2(pe.delta.x, -pe.delta.y) / UiScale;
                    dock.Window.sizeDelta = new Vector2(Mathf.Max(MinDockWidth, size.x), Mathf.Max(MinDockHeight, size.y));
                }
            }),
            (EventTriggerType.EndDrag, _ => SaveDocks()));
    }

    private static void Attach(Dock dock) {
        if (dock.Window == null) {
            return;
        }
        O5KitAdapters.Ctx.Tooltip.Hide();
        if (dock.Section != null) {
            dock.Body.SetParent(dock.Section, false);
            dock.Section.gameObject.SetActive(true);
        } else {
            // A card goes back among the others; the rebuild puts it in its place.
            dock.Body?.SetParent(inspectorContent, false);
            docks.Remove(dock);
            RefreshInspectorSoon();
        }
        UnityEngine.Object.Destroy(dock.Window.gameObject);
        dock.Window = dock.Content = null;
        if (ReferenceEquals(tearing, dock)) {
            tearing = null;
        }
        if (ReferenceEquals(moving, dock)) {
            moving = null;
        }
        titleDragging = null;
        ShowDropHint(false);
        SaveDocks();
        RefreshPanel();
    }

    // "id|x|y|w|h" for every piece that's out.
    private static void SaveDocks() {
        var list = docks.Where(d => d.Window != null).Select(d => string.Join("|", d.Id,
            F(d.Window.anchoredPosition.x), F(d.Window.anchoredPosition.y), F(d.Window.sizeDelta.x), F(d.Window.sizeDelta.y))).ToList();
        MainCore.Conf.HudDetached.Value = list;
        MainCore.ConfMgr.RequestSave();

        static string F(float v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void RestoreDocks() {
        foreach (string entry in MainCore.Conf.HudDetached.Value ?? []) {
            var parts = entry.Split('|');
            if (parts.Length != 5) {
                continue;
            }
            var dock = docks.FirstOrDefault(d => d.Id == parts[0])
                ?? (parts[0].StartsWith(CardPrefix, StringComparison.Ordinal) ? NewCardDock(parts[0], parts[0].Substring(CardPrefix.Length)) : null);
            if (dock == null || dock.Window != null) {
                continue;
            }
            float P(int i) => float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
            // Kept on screen if the resolution got smaller since.
            float x = Mathf.Clamp(P(1), -root.rect.width * 0.5f, root.rect.width * 0.5f - 80f);
            float y = Mathf.Clamp(P(2), -root.rect.height * 0.5f + 40f, root.rect.height * 0.5f);
            Detach(dock, new Rect(x, y, P(3), P(4)));
        }
    }
}
