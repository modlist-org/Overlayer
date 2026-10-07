// Menu tab API example. Copy into JS/Script to use: adds a "Tab Example"
// tab to the Overlayer menu showing every control, plus a {TabExample} tag
// that prints the current settings.
//
// Globals:
//   AddTab(name, build, options?) -> handle (or -1)
//       options: { icon: "Gear128" }  (any UISprite name, default CodeBlock128)
//   RebuildTab(handle, build?)       clear the tab and run build again next frame;
//                                    passing build replaces the old one
//   RemoveTab(handle)                remove the tab now
// Tabs are removed automatically when the script reloads, is disabled or deleted.
//
// Builder (the `tab` passed to build; Card/Row return another builder):
//   Header(text)                                   -> TMP text (.text settable)
//   Text(text, size = 18)                          -> TMP text
//   Button(text, onClick)                          -> O5Button
//   Toggle(text, value, onChanged(bool))           -> O5Toggle
//   Slider(text, min, max, value, onChanged(num), format = "0.##") -> O5Slider
//   Input(placeholder, value, onChanged(str), multiline = false)   -> O5InputField
//   Dropdown(values[], value, onChanged(str))      -> O5Dropdown
//   Color(label, color, onChanged(color))          -> O5ColorPicker
//   Card(title)        titled group, vertical builder for its contents
//   Row(spacing = 8)   side-by-side builder, children share the width
//   Clear()            remove everything this builder holds
//   Root               container Transform (parent raw Unity UI here)
//
// Returned controls are the real O5Kit objects:
//   .Value, .Set(value, invoke = true), .Reset(), .SetBlocked(bool), .Dispose()
//   O5Button: .Click(), .Label.text
//
// Builders and callbacks run on the main thread, so Unity API is safe in them.
// Settings persist through Prefs (JS/Script/Prefs.json).

const KEY = "TabExample.";
const MODES = ["Simple", "Detailed", "Debug"];

const state = {
    enabled: Prefs.Get(KEY + "enabled", true),
    speed: Prefs.Get(KEY + "speed", 5),
    name: Prefs.Get(KEY + "name", "player"),
    notes: Prefs.Get(KEY + "notes", ""),
    mode: Prefs.Get(KEY + "mode", "Simple"),
    // Prefs only stores JSON values, so the color is kept as "r,g,b,a".
    color: Prefs.Get(KEY + "color", "1,0.5,0,1"),
    clicks: 0,
};

function save(key, value) {
    state[key] = value;
    Prefs.Set(KEY + key, value);
}

function toColor(text) {
    const [r, g, b, a] = String(text).split(",").map(Number);
    return new Color(r, g, b, a);
}

function fromColor(c) {
    return [c.r, c.g, c.b, c.a].map(v => +v.toFixed(3)).join(",");
}

let handle = -1;

function build(tab) {
    tab.Header("Tab Example");
    tab.Text("Every control the tab API offers. Changes are saved to Prefs.", 16);

    // Basic controls. Keep the returned objects to drive them from code.
    const general = tab.Card("General");
    const enabled = general.Toggle("Enabled", state.enabled, v => {
        save("enabled", v);
        speed.SetBlocked(!v);
    });
    const speed = general.Slider("Speed", 0, 10, state.speed, v => save("speed", v), "0.0");
    speed.SetBlocked(!state.enabled);
    general.Input("Player name", state.name, v => save("name", v));
    general.Input("Notes (multiline)", state.notes, v => save("notes", v), true);

    const look = tab.Card("Appearance");
    look.Dropdown(MODES, state.mode, v => {
        save("mode", v);
        // Rebuild so the mode-specific card below changes.
        RebuildTab(handle);
    });
    look.Color("Accent color", toColor(state.color), c => save("color", fromColor(c)));

    // Controls shown only in some modes: rebuilt from scratch on mode change.
    if (state.mode !== "Simple") {
        const info = tab.Card(state.mode + " info");
        info.Text("Enabled: " + state.enabled + ", speed: " + state.speed);
        if (state.mode === "Debug") {
            info.Text("Prefs file: JS/Script/Prefs.json", 14);
        }
    }

    // Row: buttons side by side. Text objects can be updated later via .text.
    const counter = tab.Text("Clicks: " + state.clicks);
    const row = tab.Row();
    row.Button("Click me", () => {
        state.clicks++;
        counter.text = "Clicks: " + state.clicks;
    });
    row.Button("Max speed", () => speed.Set(10));
    row.Button("Toggle", () => enabled.Set(!enabled.Value));

    // Card contents can be cleared and refilled without rebuilding the tab.
    const log = tab.Card("Log");
    const logRow = tab.Row();
    logRow.Button("Add entry", () => log.Text(new Date().toLocaleTimeString() + " entry", 14));
    logRow.Button("Clear log", () => log.Clear());

    const reset = tab.Row();
    reset.Button("Reset settings", () => {
        for (const key of ["enabled", "speed", "name", "notes", "mode", "color"]) {
            Prefs.Remove(KEY + key);
        }
        Object.assign(state, {
            enabled: true, speed: 5, name: "player", notes: "", mode: "Simple", color: "1,0.5,0,1",
        });
        RebuildTab(handle);
    });
    reset.Button("Remove tab", () => RemoveTab(handle));
}

handle = AddTab("Tab Example", build, { icon: "Star128" });

// Show the settings anywhere with {TabExample}.
RegisterTag("TabExample", () =>
    state.enabled ? `${state.name} | ${state.mode} | speed ${state.speed}` : "disabled", {
    Desc: "Current Tab Example settings."
});
