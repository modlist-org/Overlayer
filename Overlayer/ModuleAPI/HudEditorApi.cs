using O5Kit.Core;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.ModuleAPI;

/// <summary>
/// Something a module lets the HUD editor create (a KeyViewer key, say). It shows in the
/// top bar's add menu and in the right-click menu, and goes in like a new text does.
/// </summary>
public sealed class HudCreator {
    public string Id;
    public string Module;
    public string Name;
    public string Description;
    /// <summary>Optional icon for the add menu.</summary>
    public Sprite Icon;
    /// <summary>Fresh objects, not attached anywhere; the editor puts them where the user asked.</summary>
    public Func<IEnumerable<OvObject>> Build;
}

/// <summary>A tab of a module's own in the HUD editor's side panel, next to the normal one.</summary>
public sealed class HudPanelTab {
    public string Id;
    public string Module;
    public string Title;
    /// <summary>
    /// The object the tab is about for the selected one (itself, or the parent it belongs to),
    /// or null when the tab doesn't apply. The tab only shows when this isn't null.
    /// </summary>
    public Func<OvObject, OvObject> Target;
    /// <summary>Fills the tab. Called again after every edit made through the context.</summary>
    public Action<HudPanelContext> Build;
    /// <summary>Optional: every frame while the tab shows (waiting for a key press, say).</summary>
    public Action<HudPanelContext> Tick;
}

/// <summary>What a panel tab gets to build with.</summary>
public sealed class HudPanelContext {
    private readonly Action<Action> edit;
    private readonly Action rebuild;
    private readonly List<O5Object> controls;

    internal HudPanelContext(OvObject obj, RectTransform content, O5Context ui, List<O5Object> controls, Action<Action> edit, Action rebuild) {
        Object = obj;
        Content = content;
        Ui = ui;
        this.controls = controls;
        this.edit = edit;
        this.rebuild = rebuild;
    }

    /// <summary>The object the tab is about (what <see cref="HudPanelTab.Target"/> gave).</summary>
    public OvObject Object { get; }
    /// <summary>Where the tab's controls go (a vertical list).</summary>
    public RectTransform Content { get; }
    public O5Context Ui { get; }

    /// <summary>Keeps a control so it's cleaned up when the tab is built again or closed.</summary>
    public T Track<T>(T control) where T : O5Object {
        controls.Add(control);
        return control;
    }

    /// <summary>Changes the object: one undo step, saved, and the tab is built again.</summary>
    public void Edit(Action change) => edit(change);

    /// <summary>Builds the tab again (after a change made some other way).</summary>
    public void Rebuild() => rebuild();
}

/// <summary>What modules add to the HUD editor.</summary>
public static class HudEditorApi {
    private static readonly List<HudCreator> creators = [];
    private static readonly List<HudPanelTab> tabs = [];

    /// <summary>Raised when something is added or removed (the editor rebuilds its menus).</summary>
    public static event Action Changed;

    public static IReadOnlyList<HudCreator> Creators => creators;
    public static IReadOnlyList<HudPanelTab> Tabs => tabs;

    public static void AddCreator(HudCreator creator) {
        if (creator?.Build == null || string.IsNullOrEmpty(creator.Id)) {
            return;
        }
        creators.RemoveAll(c => c.Id == creator.Id);
        creators.Add(creator);
        Changed?.Invoke();
    }

    public static void AddPanelTab(HudPanelTab tab) {
        if (tab?.Target == null || tab.Build == null || string.IsNullOrEmpty(tab.Id)) {
            return;
        }
        tabs.RemoveAll(t => t.Id == tab.Id);
        tabs.Add(tab);
        Changed?.Invoke();
    }

    /// <summary>Everything a module added (call when it's disposed).</summary>
    public static void RemoveByModule(string module) {
        int removed = creators.RemoveAll(c => c.Module == module) + tabs.RemoveAll(t => t.Module == module);
        if (removed > 0) {
            Changed?.Invoke();
        }
    }
}
