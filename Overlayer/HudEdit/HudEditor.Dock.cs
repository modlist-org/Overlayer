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

// Sections of the side panel that come off: the button by a section's title pops it out into
// its own window (move it by its title, resize it from the corner); the button on the window
// puts it back. Which ones are out, and where, is remembered.
internal static partial class HudEditor {
    private const float DockTitleHeight = 34f;
    private const float MinDockWidth = 300f;
    private const float MinDockHeight = 160f;

    private sealed class Dock {
        public string Id;
        public string Title;
        public RectTransform Section; // header + body, in the panel
        public RectTransform Body;    // what moves between the panel and the window
        public RectTransform Window;  // null while docked
        public RectTransform Content; // the window's scroll content
        public Func<bool> Visible;
    }

    private static readonly List<Dock> docks = [];

    /// <summary>A section with a title and a pop-out button; returns where its controls go.</summary>
    private static RectTransform DockSection(Transform parent, string id, string title, Func<bool> visible) {
        var section = VerticalGroup("Dock_" + id, parent);
        var label = UiStyle.Section(section, title);
        var header = (RectTransform)label.transform.parent;
        var body = VerticalGroup("Body", section);
        var dock = new Dock { Id = id, Title = title, Section = section, Body = body, Visible = visible };
        docks.Add(dock);

        var cell = NewRect("PopOut", header);
        cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(1f, 0.5f);
        cell.sizeDelta = new Vector2(22f, 22f);
        var button = Track(UiStyle.IconButton(cell, "plus_hud_dock_" + id, MainCore.Spr.Get(UISprite.Expand128), () => Detach(dock, null), 6f));
        UiStyle.Tooltip(button, "Take this out of the panel into its own window");
        return body;
    }

    private static void Detach(Dock dock, Rect? place) {
        if (dock.Window != null) {
            return;
        }
        O5KitAdapters.Ctx.Tooltip.Hide();
        var ctx = O5KitAdapters.Ctx;
        float width = Mathf.Max(MinDockWidth, PanelWidth - 36f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(dock.Body);
        float height = Mathf.Clamp(LayoutUtility.GetPreferredHeight(dock.Body) + DockTitleHeight + 28f, MinDockHeight, 640f);
        // First time: just left of the side panel, near the top.
        Rect at = place ?? new Rect(root.rect.width * 0.5f - PanelWidth - Margin * 2f - width,
            root.rect.height * 0.5f - Margin - 60f, width, height);

        var window = UiStyle.Rounded("Dock_" + dock.Id, root, UiStyle.Surface).rectTransform;
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0f, 1f);
        window.anchoredPosition = at.position;
        window.sizeDelta = new Vector2(Mathf.Max(MinDockWidth, at.width), Mathf.Max(MinDockHeight, at.height));
        window.GetComponent<Image>().raycastTarget = true;
        dock.Window = window;

        // Title bar: drag to move, button to put it back.
        var bar = NewImage("Title", window, Color.clear);
        bar.raycastTarget = true;
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.sizeDelta = new Vector2(0f, DockTitleHeight);
        UnityUtils.AddEvents(bar.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.Drag, e => {
                if (e is PointerEventData pe) {
                    window.anchoredPosition += pe.delta / UiScale;
                }
            }),
            (EventTriggerType.EndDrag, _ => SaveDocks()));
        var title = O5Factory.ControlText(ctx, bar.rectTransform, 13f, true);
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(14f, 0f);
        title.rectTransform.offsetMax = new Vector2(-40f, 0f);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 6f;
        title.color = UiStyle.Caption;
        title.raycastTarget = false;
        title.text = dock.Title.ToUpperInvariant();
        var backCell = NewRect("PutBack", bar.rectTransform);
        backCell.anchorMin = backCell.anchorMax = backCell.pivot = new Vector2(1f, 0.5f);
        backCell.anchoredPosition = new Vector2(-8f, 0f);
        backCell.sizeDelta = new Vector2(24f, 24f);
        var back = Track(UiStyle.IconButton(backCell, "plus_hud_undock_" + dock.Id, MainCore.Spr.Get(UISprite.X128), () => Attach(dock), 7f));
        UiStyle.Tooltip(back, "Put it back in the panel");

        var scrollHost = NewRect("Scroll", window);
        Stretch(scrollHost);
        scrollHost.offsetMin = new Vector2(12f, 12f);
        scrollHost.offsetMax = new Vector2(-12f, -DockTitleHeight);
        var (_, content, _) = O5Factory.ScrollView(ctx, scrollHost, 8f, 0f);
        dock.Content = content;
        dock.Body.SetParent(content, false);
        dock.Section.gameObject.SetActive(false);

        DockResizeGrip(dock);
        SaveDocks();
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
        dock.Body.SetParent(dock.Section, false);
        dock.Section.gameObject.SetActive(true);
        UnityEngine.Object.Destroy(dock.Window.gameObject);
        dock.Window = dock.Content = null;
        SaveDocks();
        RefreshPanel();
    }

    // "id|x|y|w|h" for every section that's out.
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
            var dock = docks.FirstOrDefault(d => parts.Length == 5 && d.Id == parts[0]);
            if (dock == null) {
                continue;
            }
            float P(int i) => float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
            // Kept on screen if the resolution got smaller since.
            float x = Mathf.Clamp(P(1), -root.rect.width * 0.5f, root.rect.width * 0.5f - 80f);
            float y = Mathf.Clamp(P(2), -root.rect.height * 0.5f + 40f, root.rect.height * 0.5f);
            Detach(dock, new Rect(x, y, P(3), P(4)));
        }
    }

    // Per frame: a window shows only when its section would (in the panel, its parents decide).
    private static void TickDocks() {
        foreach (var dock in docks) {
            bool visible = dock.Visible();
            if (dock.Window != null) {
                dock.Window.gameObject.SetActive(visible);
            } else if (dock.Section.gameObject.activeSelf != visible) {
                dock.Section.gameObject.SetActive(visible);
            }
        }
    }
}
