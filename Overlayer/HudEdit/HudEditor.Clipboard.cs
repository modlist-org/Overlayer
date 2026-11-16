using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.HudEdit;

// Ctrl+C / Ctrl+V (offset) / Ctrl+Shift+V (in place) for objects, Ctrl+Alt+C / Ctrl+Alt+V for
// their look (text style, image color, opacity). Objects also go to the system clipboard as
// text, so they can be pasted in another session.
internal static partial class HudEditor {
    private const string ClipboardPrefix = "overlayer-hud:";

    // Each copied object with where its pivot was on screen.
    private static readonly List<(JToken Json, Vector2 Screen)> clipboard = [];

    private sealed class Style {
        public TextMeshProUGUISettings Text;
        public Color? ImageColor;
        public float? Alpha;
    }

    private static Style copiedStyle;

    public static void CopySelection() {
        var targets = selection.Where(e => e.IsAlive).ToList();
        targets.RemoveAll(e => targets.Any(o => o != e && IsAncestor(o.Object, e.Object)));
        if (targets.Count == 0) {
            return;
        }
        clipboard.Clear();
        foreach (var el in targets) {
            clipboard.Add((el.Object.Serialize(), el.Rect.position));
        }
        try {
            var array = new JArray(clipboard.Select(c => new JObject {
                ["Object"] = c.Json,
                ["X"] = c.Screen.x,
                ["Y"] = c.Screen.y,
            }));
            GUIUtility.systemCopyBuffer = ClipboardPrefix + array.ToString(Newtonsoft.Json.Formatting.None);
        } catch {
        }
    }

    // Prefer the system clipboard when it holds objects (copied in another session).
    private static void ReadSystemClipboard() {
        string text;
        try {
            text = GUIUtility.systemCopyBuffer;
        } catch {
            return;
        }
        if (string.IsNullOrEmpty(text) || !text.StartsWith(ClipboardPrefix)) {
            return;
        }
        try {
            var array = JArray.Parse(text.Substring(ClipboardPrefix.Length));
            clipboard.Clear();
            foreach (var item in array) {
                clipboard.Add((item["Object"], new Vector2(item.Value<float>("X"), item.Value<float>("Y"))));
            }
        } catch {
        }
    }

    /// <summary>Pastes next to what's selected (as its sibling) or into the main canvas.</summary>
    public static void Paste(bool inPlace) {
        ReadSystemClipboard();
        if (clipboard.Count == 0) {
            return;
        }
        var anchor = Primary is { IsAlive: true, Locked: false } p ? p : null;
        var slot = anchor != null ? SlotOf(anchor) : ResolveSlot(Placement.MainCanvas);
        if (slot.Canvas == null) {
            return;
        }
        var insertAt = anchor != null ? new Slot(slot.Canvas, slot.Parent, slot.Index + 1) : slot;
        Vector2 offset = inPlace ? Vector2.zero : new Vector2(20f, -20f) * UiScale;

        var created = new List<(OvObject Obj, Slot Slot)>();
        foreach (var (json, screen) in clipboard) {
            var obj = new OvObject(bare: json["Config"]?["RectTransformConfig"] == null);
            obj.Deserialize(json);
            obj.ApplyComponent();
            AttachAt(obj, insertAt);
            PlaceAt(obj, screen + offset);
            int index = insertAt.Parent != null ? insertAt.Parent.Children.IndexOf(obj) : insertAt.Canvas.OvObjects.IndexOf(obj);
            created.Add((obj, new Slot(insertAt.Canvas, insertAt.Parent, index)));
            if (insertAt.Index >= 0) {
                insertAt = new Slot(insertAt.Canvas, insertAt.Parent, index + 1);
            }
        }
        undo.Push(new ActionCommand(
            () => {
                foreach (var (obj, s) in Enumerable.Reverse(created)) {
                    ToTrash(obj, s);
                }
            },
            () => {
                foreach (var (obj, s) in created) {
                    FromTrash(obj, s);
                }
            }));
        AfterStructureChange(null, created.Select(c => c.Obj));
    }

    public static void CopyStyle() {
        var el = Primary;
        if (el == null || !el.IsAlive) {
            return;
        }
        var cfg = el.Object.Config;
        copiedStyle = new Style {
            Text = cfg.TextConfig?.Copy(),
            ImageColor = cfg.ImageConfig != null && !cfg.ImageConfig.Color.UseFx ? cfg.ImageConfig.Color.StaticValue : null,
            Alpha = cfg.CanvasGroupConfig != null && !cfg.CanvasGroupConfig.Alpha.UseFx ? cfg.CanvasGroupConfig.Alpha.StaticValue : null,
        };
    }

    /// <summary>Applies the copied look; text keeps its own content.</summary>
    public static void PasteStyle() {
        if (copiedStyle == null) {
            return;
        }
        var targets = selection.Where(e => e.IsAlive && !e.Locked).Select(e => e.Object).ToList();
        if (targets.Count == 0) {
            return;
        }
        var before = targets.Select(o => (Obj: o, Text: o.Config.TextConfig?.Copy(),
            Color: o.Config.ImageConfig?.Color.StaticValue, Alpha: o.Config.CanvasGroupConfig?.Alpha.StaticValue)).ToList();
        var copiedStyleSnapshot = new Style { Text = copiedStyle.Text?.Copy(), ImageColor = copiedStyle.ImageColor, Alpha = copiedStyle.Alpha };

        void Apply(OvObject o, TextMeshProUGUISettings text, Color? color, float? alpha) {
            if (text != null && o.Config.TextConfig != null) {
                o.Config.TextConfig = text.Copy();
            }
            if (color.HasValue && o.Config.ImageConfig != null && !o.Config.ImageConfig.Color.UseFx) {
                o.Config.ImageConfig.Color.StaticValue = color.Value;
            }
            if (alpha.HasValue && o.Config.CanvasGroupConfig != null && !o.Config.CanvasGroupConfig.Alpha.UseFx) {
                o.Config.CanvasGroupConfig.Alpha.StaticValue = alpha.Value;
            }
            o.ApplyConfig();
        }
        void Redo() {
            foreach (var o in targets) {
                Apply(o, copiedStyleSnapshot.Text, copiedStyleSnapshot.ImageColor, copiedStyleSnapshot.Alpha);
            }
            Save();
            RefreshInspectorSoon();
        }
        Redo();
        undo.Push(new ActionCommand(() => {
            foreach (var (o, text, color, alpha) in before) {
                Apply(o, text, color, alpha);
            }
            Save();
            RefreshInspectorSoon();
        }, Redo));
    }
}
