using Microsoft.ClearScript;
using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI;
using Overlayer.UI.Factory;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.V8.Scripting.UI;

// Script-made menu tabs, the same way modules add theirs
// (MenuFactory.CreateItem + PageFactory.CreatePageBase + O5Factory controls).
//
//   AddTab("My Tab", tab => {
//       tab.Header("Hello");
//       tab.Button("Click", () => Log.Msg("hi"));
//       const s = tab.Slider("Speed", 0, 10, 5, v => Store.Set("speed", v));
//       s.Set(3); // returned controls are the real O5Kit objects
//   }, { icon: "Gear128" });
//
// Builders run on the main thread. Tabs are removed automatically when the
// script reloads, is disabled or deleted.
public class JSUIHost(string filePath) {
    public const string BindingScript = @"
        Object.defineProperty(globalThis, 'AddTab', {
            value: function(name, build, options) { return __OverlayerAddTab(name, build, options); },
            writable: true,
            configurable: true
        });
        Object.defineProperty(globalThis, 'RebuildTab', {
            value: function(handle, build) { return __OverlayerRebuildTab(handle, build); },
            writable: true,
            configurable: true
        });
        Object.defineProperty(globalThis, 'RemoveTab', {
            value: function(handle) { return __OverlayerRemoveTab(handle); },
            writable: true,
            configurable: true
        });
    ";

    // Module tabs use small ids (KeyViewer: 101); keep script tabs well clear.
    private const int FirstTabId = 10000;

    private sealed class Tab {
        public int Id;
        public string File;
        public string Name;
        public ScriptObject Build;
        public UISprite Icon;
        public RectTransform Page;
        public RectTransform Content;
        public readonly List<O5Object> Controls = [];
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<int, Tab> Tabs = [];
    private static int nextId = FirstTabId;

    public string FilePath { get; } = filePath;

    public int AddTab(object name, object build, object options) {
        if (name is not string text || string.IsNullOrWhiteSpace(text)) {
            MainCore.Log.Wrn($"[{nameof(JSUIHost)}] AddTab: name must be a non-empty string ({Path.GetFileName(FilePath)}).");
            return -1;
        }
        if (build is not ScriptObject fn) {
            MainCore.Log.Wrn($"[{nameof(JSUIHost)}] AddTab: build must be a function ({Path.GetFileName(FilePath)}).");
            return -1;
        }

        UISprite icon = UISprite.CodeBlock128;
        if (options is ScriptObject opts && opts.GetProperty("icon") is string iconName
            && Enum.TryParse(iconName, true, out UISprite parsed)) {
            icon = parsed;
        }

        Tab tab;
        lock (Sync) {
            tab = new Tab { Id = nextId++, File = FilePath, Name = text, Build = fn, Icon = icon };
            Tabs[tab.Id] = tab;
        }
        MainThread.Enqueue(() => Create(tab));
        return tab.Id;
    }

    /// <summary>Clears the tab and runs its build again next frame. A new build function replaces the old one.</summary>
    public bool RebuildTab(object handle, object build) {
        Tab tab = Find(handle);
        if (tab == null) {
            return false;
        }
        if (build is ScriptObject fn) {
            tab.Build = fn;
        }
        MainThread.Enqueue(() => {
            lock (Sync) {
                if (!Tabs.ContainsKey(tab.Id)) {
                    return;
                }
            }
            if (tab.Content == null) {
                return; // not built yet; Create runs the (possibly replaced) build
            }
            new JSUIBuilder(tab.Id, tab.Content, tab.Controls).Clear();
            RunBuild(tab);
        });
        return true;
    }

    private Tab Find(object handle) {
        int id;
        try {
            id = Convert.ToInt32(handle);
        } catch {
            return null;
        }
        lock (Sync) {
            return Tabs.TryGetValue(id, out var tab) && tab.File == FilePath ? tab : null;
        }
    }

    public bool RemoveTab(object handle) {
        int id;
        try {
            id = Convert.ToInt32(handle);
        } catch {
            return false;
        }
        Tab tab;
        lock (Sync) {
            if (!Tabs.TryGetValue(id, out tab) || tab.File != FilePath) {
                return false;
            }
            Tabs.Remove(id);
        }
        MainThread.Enqueue(() => Destroy(tab));
        return true;
    }

    public static void RemoveFile(string file) => RemoveWhere(tab => tab.File == file);

    public static void RemoveAll() => RemoveWhere(_ => true);

    // Snapshot under the lock so tabs re-added by a reload after this call survive.
    private static void RemoveWhere(Func<Tab, bool> match) {
        List<Tab> removed;
        lock (Sync) {
            removed = [.. Tabs.Values.Where(match)];
            foreach (var tab in removed) {
                Tabs.Remove(tab.Id);
            }
        }
        if (removed.Count > 0) {
            MainThread.Enqueue(() => removed.ForEach(Destroy));
        }
    }

    private static void Create(Tab tab) {
        lock (Sync) {
            if (!Tabs.ContainsKey(tab.Id)) {
                return; // removed before it was built
            }
        }
        if (UICore.MenuContent == null) {
            return;
        }

        MenuFactory.CreateItem(UICore.MenuContent, tab.Name, MainCore.Spr.Get(tab.Icon), tab.Id);
        tab.Page = PageFactory.CreatePageBase(tab.Id);
        (_, tab.Content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, tab.Page, 12f, 18f);
        RunBuild(tab);
    }

    private static void RunBuild(Tab tab) {
        try {
            MainCore.V8.InvokeCallback(tab.Build, [new JSUIBuilder(tab.Id, tab.Content, tab.Controls)]);
        } catch (Exception e) {
            MainCore.Log.Wrn($"[{nameof(JSUIHost)}] Tab '{tab.Name}' build failed ({Path.GetFileName(tab.File)}): {e.Message}");
        }
    }

    private static void Destroy(Tab tab) {
        if (tab.Page == null) {
            return; // never built
        }
        foreach (var control in tab.Controls) {
            try { control.Dispose(); } catch { }
        }
        tab.Controls.Clear();
        MenuFactory.RemoveItem(tab.Id);
        UICore.Pages.Remove(tab.Id);
        if (tab.Page) {
            UnityEngine.Object.Destroy(tab.Page.gameObject);
        }
        tab.Page = null;
        tab.Content = null;
    }
}

/// <summary>Passed to AddTab builders. Every method adds one control and returns the O5Kit control.
/// Top-level/card builders stack controls vertically; <see cref="Row"/> builders place them side by side.</summary>
public sealed class JSUIBuilder {
    private readonly int _tabId;
    private readonly List<O5Object> _controls;
    private readonly bool _horizontal;
    private static int _counter;

    /// <summary>Container transform; raw Unity UI can be parented here.</summary>
    public Transform Root { get; }

    internal JSUIBuilder(int tabId, Transform root, List<O5Object> controls, bool horizontal = false) {
        _tabId = tabId;
        Root = root;
        _controls = controls;
        _horizontal = horizontal;
    }

    private static O5Context Ctx => O5KitAdapters.Ctx;
    private string NextId() => $"js_tab_{_tabId}_{_counter++}";

    // Vertical: a full-width row per control. Horizontal: an equal-width cell in the row.
    private RectTransform Slot() {
        if (!_horizontal) {
            return O5Factory.Row(Ctx, Root);
        }
        GameObject cell = new("Cell");
        cell.transform.SetParent(Root, false);
        var rect = cell.AddComponent<RectTransform>();
        var le = cell.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 0f;
        return rect;
    }

    private T Track<T>(T control) where T : O5Object {
        _controls.Add(control);
        return control;
    }

    private static Action<T> Callback<T>(object fn) {
        if (fn is not ScriptObject so) {
            return null;
        }
        return value => {
            try {
                MainCore.V8.InvokeCallback(so, [value]);
            } catch (Exception e) {
                MainCore.Log.Wrn($"[{nameof(JSUIBuilder)}] Callback error: {e.Message}");
            }
        };
    }

    public object Header(string text) {
        var tmp = O5Factory.ControlTextH1(Ctx, Slot());
        tmp.text = text;
        return tmp;
    }

    public object Text(string text, double size = 18) {
        var tmp = O5Factory.ControlText(Ctx, Slot(), (float)size);
        tmp.text = text;
        return tmp;
    }

    public O5Button Button(string text, object onClick) {
        var cb = Callback<object>(onClick);
        return Track(O5Factory.Button(Ctx, Slot(), cb == null ? null : () => cb(null), text, NextId()));
    }

    public O5Toggle Toggle(string text, bool value, object onChanged)
        => Track(O5Factory.Toggle(Ctx, Slot(), null, value, Callback<bool>(onChanged), text, NextId()));

    public O5Slider Slider(string text, double min, double max, double value, object onChanged, string format = "0.##")
        => Track(O5Factory.Slider(Ctx, Slot(), null, min, max, value, format, ClampMode.All,
            null, null, Callback<double>(onChanged), null, text, NextId()));

    public O5InputField Input(string placeholder, string value, object onChanged, bool multiline = false)
        => Track(O5Factory.Input(Ctx, Slot(), null, value, Callback<string>(onChanged), placeholder ?? string.Empty, null, NextId(),
            multiline: multiline));

    public O5Dropdown<string> Dropdown(object values, string value, object onChanged) {
        List<string> list = [];
        if (values is ScriptObject array) {
            int length = Convert.ToInt32(array.GetProperty("length"));
            for (int i = 0; i < length; i++) {
                list.Add(array.GetProperty(i)?.ToString() ?? string.Empty);
            }
        }
        return Track(O5Factory.DropDown<string>(Ctx, Slot(), null, value, list, s => s, Callback<string>(onChanged), NextId()));
    }

    public O5ColorPicker Color(string label, UnityEngine.Color value, object onChanged)
        => Track(O5Factory.ColorPicker(Ctx, Slot(), UICore.CanvasObj.GetComponent<RectTransform>(),
            UICore.Canvas ? UICore.Canvas.worldCamera : null, null, value, Callback<UnityEngine.Color>(onChanged), null, NextId(), label));

    /// <summary>Removes everything this builder (tab, card or row) contains, so it can be filled again.</summary>
    public void Clear() {
        for (int i = _controls.Count - 1; i >= 0; i--) {
            var control = _controls[i];
            if (control.Rect == null || control.Rect.IsChildOf(Root)) {
                try { control.Dispose(); } catch { }
                _controls.RemoveAt(i);
            }
        }
        // Deactivate first: Destroy is deferred and layout groups skip inactive children.
        for (int i = Root.childCount - 1; i >= 0; i--) {
            var child = Root.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    /// <summary>Horizontal row; controls added to the returned builder share its width equally.</summary>
    public JSUIBuilder Row(double spacing = 8) {
        var row = Slot();
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = (float)spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return new JSUIBuilder(_tabId, row, _controls, horizontal: true);
    }

    /// <summary>Titled group; returns a builder for its contents.</summary>
    public JSUIBuilder Card(string title) {
        var (_, content) = O5Factory.Card(Ctx, Root, title, true, null, null, showDeleteButton: false, showActiveToggle: false);
        return new JSUIBuilder(_tabId, content, _controls);
    }
}
