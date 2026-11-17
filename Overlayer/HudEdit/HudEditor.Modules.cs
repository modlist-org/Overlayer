using Newtonsoft.Json.Linq;
using O5Kit.Control;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.ModuleAPI;
using Overlayer.Overlay;
using Overlayer.UI.Utility;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// What modules add to the editor (HudEditorApi): things to create, from the top bar's add menu
// and the right-click menu, and tabs of their own in the side panel, shown next to the normal
// one when they apply to what's selected (a KeyViewer key, say).
internal static partial class HudEditor {
    private static O5Button addMoreButton;
    private static RectTransform tabStrip, tabHost, panelBody;
    private static readonly List<O5Kit.Core.O5Object> tabStripControls = [];
    private static readonly List<(O5Button Button, HudPanelTab Tab)> tabButtons = [];
    private static readonly List<O5Kit.Core.O5Object> tabControls = [];
    // The tabs shown now, and the one picked (null: the normal panel). The pick is remembered,
    // so selecting another key opens on the KeyViewer tab again.
    private static readonly List<HudPanelTab> shownTabs = [];
    private static HudPanelTab activeTab;
    private static string preferredTab;
    private static OvObject tabTarget;
    private static HudPanelContext tabContext;
    private static bool tabDirty;

    // ---- creating ----

    private static void UpdateAddMoreButton() {
        if (addMoreButton != null) {
            addMoreButton.Rect.parent.gameObject.SetActive(HudEditorApi.Creators.Count > 0);
        }
    }

    private static void OpenAddMenu(Vector2 at) {
        var items = new List<(string Text, string Shortcut, Action Run, bool Danger)?>();
        foreach (var creator in HudEditorApi.Creators) {
            var c = creator;
            items.Add(($"{c.Name}<pos=55%><alpha=#55>{c.Module}", null, () => CreateFrom(c, CurrentPlacement, null), false));
        }
        ShowMenu("AddMenu", at, items);
    }

    /// <summary>Puts what a module builds where new things go, like a new text: one undo step.</summary>
    private static void CreateFrom(HudCreator creator, Placement placement, Vector2? screen) {
        List<OvObject> made;
        try {
            made = creator.Build()?.Where(o => o != null).ToList() ?? [];
        } catch (Exception e) {
            MainCore.Log.Wrn($"[HudEditor] '{creator.Name}' ({creator.Module}) couldn't be made: {e.Message}");
            return;
        }
        if (made.Count == 0) {
            return;
        }
        if (placement == Placement.Parent) {
            placement = Placement.MainCanvas;
        }
        var slot = ResolveSlot(placement);
        if (slot.Canvas == null) {
            return;
        }
        Vector2 at = screen ?? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var placed = new List<(OvObject Obj, Slot Slot)>();
        foreach (var obj in made) {
            AttachAt(obj, new Slot(slot.Canvas, slot.Parent, slot.Index < 0 ? -1 : slot.Index + placed.Count));
            int index = slot.Parent != null ? slot.Parent.Children.IndexOf(obj) : slot.Canvas.OvObjects.IndexOf(obj);
            placed.Add((obj, new Slot(slot.Canvas, slot.Parent, index)));
        }
        // The first one lands on the point; the rest keep their places relative to it.
        var first = made[0].RectTransform;
        Vector2 shift = first != null ? at - (Vector2)first.position : Vector2.zero;
        foreach (var obj in made) {
            if (obj.RectTransform != null) {
                PlaceAtSoon(obj, (Vector2)obj.RectTransform.position + shift);
            }
        }
        undo.Push(new ActionCommand(
            () => { foreach (var (obj, s) in Enumerable.Reverse(placed)) ToTrash(obj, s); },
            () => { foreach (var (obj, s) in placed) FromTrash(obj, s); }));
        AfterStructureChange(null, made);
    }

    // ---- panel tabs ----

    private static OvObject TargetOf(HudPanelTab tab, OvObject selected) {
        try {
            return tab.Target(selected);
        } catch {
            return null;
        }
    }

    // Called with every panel refresh: which tabs apply, which one shows, what it's about.
    private static void RefreshTabs() {
        if (tabStrip == null) {
            return;
        }
        UpdateAddMoreButton();
        var selected = selection.Count == 1 && !Primary.ReadOnly ? Primary.Object : null;
        var applying = selected == null ? [] : HudEditorApi.Tabs.Where(t => TargetOf(t, selected) != null).ToList();
        if (!applying.SequenceEqual(shownTabs)) {
            shownTabs.Clear();
            shownTabs.AddRange(applying);
            BuildTabStrip();
        }
        var tab = shownTabs.FirstOrDefault(t => t.Id == preferredTab);
        var target = tab != null ? TargetOf(tab, selected) : null;
        if (!ReferenceEquals(tab, activeTab) || !ReferenceEquals(target, tabTarget)) {
            activeTab = tab;
            tabTarget = target;
            ShowTab();
        }
    }

    private static void BuildTabStrip() {
        foreach (var c in tabStripControls) {
            c.Dispose();
        }
        tabStripControls.Clear();
        tabButtons.Clear();
        for (int i = tabStrip.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(tabStrip.GetChild(i).gameObject);
        }
        tabStrip.gameObject.SetActive(shownTabs.Count > 0);
        if (shownTabs.Count == 0) {
            return;
        }
        AddTabButton("object", "Object", null);
        foreach (var tab in shownTabs) {
            AddTabButton(tab.Id, tab.Title, tab);
        }
        UiStyle.Cell(tabStrip, 0f);
        UpdateTabButtons();
    }

    private static void AddTabButton(string id, string title, HudPanelTab tab) {
        float width = Mathf.Max(80f, title.Length * 10f + 34f);
        var button = UiStyle.Button(UiStyle.Cell(tabStrip, width), "plus_hud_tab_" + id, title, () => {
            preferredTab = tab?.Id;
            RefreshTabs();
            UpdateTabButtons();
        });
        tabButtons.Add((button, tab));
        tabStripControls.Add(button);
    }

    // The picked tab stands out, like a browser's.
    private static void UpdateTabButtons() {
        foreach (var (b, tab) in tabButtons) {
            bool on = ReferenceEquals(tab, activeTab);
            b.NormalColor = on ? new Color(0.569f, 0.604f, 1f, 1f) : new Color(1f, 1f, 1f, 0.06f);
            b.UpdateVisual(true);
            if (b.Label != null) {
                b.Label.color = on ? Color.white : UiStyle.Muted;
            }
        }
    }

    private static void ShowTab() {
        bool module = activeTab != null && tabTarget != null;
        panelBody.gameObject.SetActive(!module);
        tabHost.gameObject.SetActive(module);
        UpdateTabButtons();
        RebuildTab();
    }

    private static void RebuildTab() {
        foreach (var c in tabControls) {
            c.Dispose();
        }
        tabControls.Clear();
        for (int i = tabHost.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(tabHost.GetChild(i).gameObject);
        }
        tabContext = null;
        if (activeTab == null || tabTarget == null) {
            return;
        }
        var target = tabTarget;
        var context = tabContext = new HudPanelContext(target, tabHost, O5KitAdapters.Ctx, tabControls,
            change => TabEdit(target, change), () => tabDirty = true);
        try {
            activeTab.Build(context);
        } catch (Exception e) {
            MainCore.Log.Wrn($"[HudEditor] Tab '{activeTab.Title}' ({activeTab.Module}) failed: {e.Message}");
        }
    }

    // A tab's edit: the whole object before and after, so undo puts it back as it was.
    private static void TabEdit(OvObject target, Action change) {
        if (target?.GameObject == null) {
            return;
        }
        JToken before = target.Serialize();
        try {
            change();
        } catch (Exception e) {
            MainCore.Log.Wrn($"[HudEditor] Tab edit failed: {e.Message}");
        }
        JToken after = target.Serialize();
        if (JToken.DeepEquals(before, after)) {
            return;
        }
        void Restore(JToken state) {
            if (target.GameObject == null) {
                return;
            }
            target.Deserialize(state);
            target.ApplyComponent();
            target.ApplyConfig();
            AfterStructureChange(target);
            tabDirty = true;
        }
        undo.Push(new ActionCommand(() => Restore(before), () => Restore(after)));
        AfterStructureChange(target);
        layersDirty = true;
        tabDirty = true;
    }

    // Per frame: a tab asked to be built again (not while typing in it).
    private static void TickTabs() {
        if (tabDirty && !TypingInField()) {
            tabDirty = false;
            tabTarget = null;
            RefreshTabs();
        }
        if (activeTab?.Tick != null && tabContext != null) {
            try {
                activeTab.Tick(tabContext);
            } catch (Exception e) {
                MainCore.Log.Wrn($"[HudEditor] Tab '{activeTab.Title}' tick failed: {e.Message}");
            }
        }
    }

    private static void ForgetTabs() {
        foreach (var c in tabStripControls.Concat(tabControls)) {
            c.Dispose();
        }
        tabStripControls.Clear();
        tabButtons.Clear();
        tabControls.Clear();
        shownTabs.Clear();
        activeTab = null;
        tabTarget = null;
        tabContext = null;
        tabDirty = false;
        tabStrip = tabHost = panelBody = null;
        addMoreButton = null;
    }
}
