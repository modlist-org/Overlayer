using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Core;
using Overlayer.IO.Fx;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.ModuleAPI;
using Overlayer.Overlay;
using Overlayer.Tag.Core;
using Overlayer.UI;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// KeyViewer keys from the HUD editor, when the KeyViewer module is installed: a key, a KPS box and
// a total box in the add menus, and a KeyViewer tab in the side panel for them. The module isn't
// changed for this: the keys are plain objects driven by its tags (KV_IsKeyHeld, KV_EaseScalar,
// KV_Count) and drawn with its key images, like the keys of an imported profile.
// A key is a group (moved and scaled like anything else) holding the key itself. Its settings
// stay on the group as JSON, so the tab can build it again after each change.
internal static class HudKeyViewer {
    private const string Module = "KeyViewer";
    private const string SpecKey = "keyviewer.key";
    private const string StateKey = "keyviewer.state";
    // Press counts of these keys are kept under this name by the module.
    private const string CountPrefix = "KeyViewer_Editor";
    private const string BackgroundSprite = "KeyViewer_Background_v3";
    private const string OutlineSprite = "KeyViewer_Outline_v3";

    private static bool registered;

    private static bool ModuleThere => TagManager.TryGet("KV_IsKeyHeld", out _) && TagManager.TryGet("KV_EaseScalar", out _)
        && TagManager.TryGet("KV_Count", out _);

    /// <summary>Offers KeyViewer things while the module is there (called when the editor opens).</summary>
    public static void Sync() {
        bool there = ModuleThere;
        if (there == registered) {
            return;
        }
        registered = there;
        if (!there) {
            HudEditorApi.RemoveByModule(Module);
            return;
        }
        HudEditorApi.AddCreator(new HudCreator {
            Id = "keyviewer.key", Module = Module, Name = "KeyViewer key",
            Description = "A key that lights up while it's held, with a press count",
            Build = () => [Make(Spec.Key(FreeCode()))],
        });
        HudEditorApi.AddCreator(new HudCreator {
            Id = "keyviewer.kps", Module = Module, Name = "KeyViewer KPS",
            Description = "Key presses per second, in a key's box",
            Build = () => [Make(Spec.Info("KPS", "{Kps:0,F0}"))],
        });
        HudEditorApi.AddCreator(new HudCreator {
            Id = "keyviewer.total", Module = Module, Name = "KeyViewer total",
            Description = "Presses of the keys made here, added up",
            Build = () => [Make(Spec.Info("Total", "{HudKeysTotal}"))],
        });
        HudEditorApi.AddPanelTab(new HudPanelTab {
            Id = "keyviewer", Module = Module, Title = "KeyViewer",
            Target = KeyOf, Build = BuildTab, Tick = TickTab,
        });
    }

    private static string T(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    // ---- settings of a key ----

    private sealed class Spec {
        public string Kind = "key";         // key, info
        public string Code = "D";
        public string Name;                 // info: the caption (KPS, Total)
        public string Value;                // info: the template under it
        public string Label = string.Empty; // empty: the key's name
        public bool Count = true;
        public bool Outline = true;
        public float[] Box = [0f, 0f, 0f, 0.4f];
        public float[] BoxHeld = [1f, 1f, 1f, 1f];
        public float[] Text = [1f, 1f, 1f, 1f];
        public float[] TextHeld = [0f, 0f, 0f, 1f];
        public float PressScale = 0.92f;
        public float Ms = 80f;

        [JsonIgnore]
        public bool IsInfo => Kind == "info";

        public static Spec Key(string code) => new() { Code = code };

        public static Spec Info(string name, string value) => new() { Kind = "info", Code = "None", Name = name, Value = value };

        public string Write() => JsonConvert.SerializeObject(this);

        public static Spec Read(string json) {
            try {
                return JsonConvert.DeserializeObject<Spec>(json) ?? Key("D");
            } catch {
                return Key("D");
            }
        }

        [JsonIgnore]
        public string Caption => IsInfo ? Name : string.IsNullOrEmpty(Label) ? Code : Label;

        [JsonIgnore]
        public Vector2 Size => new(100f, IsInfo || Count ? 150f : 100f);
    }

    private static Color C(float[] v) => v is { Length: >= 3 } ? new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f) : Color.white;

    private static float[] F(Color c) => [c.r, c.g, c.b, c.a];

    // ---- building ----

    public static OvObject KeyOf(OvObject obj) {
        for (var o = obj; o != null; o = o.Parent) {
            if (o.Config.Meta != null && o.Config.Meta.ContainsKey(SpecKey)) {
                return o;
            }
        }
        return null;
    }

    private static IEnumerable<OvObject> Walk(OvObject o) => new[] { o }.Concat(o.Children.SelectMany(Walk));

    private static IEnumerable<Spec> AllSpecs() => OverlayCore.Canvases.Where(c => c != null)
        .SelectMany(c => c.OvObjects).SelectMany(Walk)
        .Where(o => o.Config.Meta != null && o.Config.Meta.ContainsKey(SpecKey))
        .Select(o => Spec.Read(o.Config.Meta[SpecKey]));

    // A key no other editor key has yet.
    private static string FreeCode() {
        var used = new HashSet<string>(AllSpecs().Where(s => !s.IsInfo).Select(s => s.Code), StringComparer.OrdinalIgnoreCase);
        string[] order = ["D", "F", "J", "K", "S", "L", "A", "Semicolon", "Space", "Z", "X", "C", "V", "B", "N", "M"];
        return order.FirstOrDefault(c => !used.Contains(c)) ?? "D";
    }

    private static OvObject Make(Spec spec) {
        var group = new OvObject();
        var rt = group.Config.RectTransformConfig;
        rt.AnchorMin.Value = rt.AnchorMax.Value = rt.Pivot.Value = new Vector2(0.5f, 0.5f);
        group.Config.Meta = new Dictionary<string, string> {
            [StateKey] = "hud." + Guid.NewGuid().ToString("N").Substring(0, 8),
        };
        Fill(group, spec);
        return group;
    }

    // (Re)builds what's inside the group from the spec; the group keeps its place.
    private static void Fill(OvObject group, Spec spec) {
        foreach (var child in group.Children.ToList()) {
            child.Detach();
            child.Dispose();
        }
        group.Config.Meta[SpecKey] = spec.Write();
        group.Config.Name.Value = (spec.IsInfo ? "KeyViewer " : "Key ") + spec.Caption;
        group.Config.RectTransformConfig.SizeDelta.Value = spec.Size;
        group.ApplyComponent();
        group.ApplyConfig();
        string state = group.Config.Meta[StateKey];

        // The key's origin: everything inside presses in around it.
        var key = Part("Key", Vector2.zero, Vector2.zero);
        if (!spec.IsInfo && !Mathf.Approximately(spec.PressScale, 1f)) {
            key.Config.RectTransformConfig.Scale = FxValue<Vector3>.FromExpression(
                $"[{Ease(spec, state + ".scale.x", 1f, spec.PressScale)}, {Ease(spec, state + ".scale.y", 1f, spec.PressScale)}, 1]");
        }
        Attach(group, key);

        Vector2 size = spec.Size;
        Attach(key, Image("Background", BackgroundSprite, size, ColorFx(spec, state + ".bg", spec.Box, spec.BoxHeld)));
        if (spec.Outline) {
            Attach(key, Image("Outline", OutlineSprite, size, FxValue<Color>.FromValue(Color.white)));
        }
        bool second = spec.IsInfo || spec.Count;
        float quarter = size.y / 4f;
        Attach(key, Text("Text", Escape(spec.Caption), 75f, new Vector2(0f, second ? quarter : 0f), size,
            GradientFx(spec, state + ".text", spec.Text, spec.TextHeld)));
        if (second) {
            string value = spec.IsInfo ? spec.Value : $"{{KV_Count({CountPrefix},{spec.Code})}}";
            Attach(key, Text("CountText", value, 50f, new Vector2(0f, -quarter), size,
                GradientFx(spec, state + ".count", spec.Text, spec.TextHeld)));
        }
    }

    // Braces in a label would be read as tags.
    private static string Escape(string text) => text.Replace("{", "{'{'}").Replace("}", "{'}'}");

    private static void Attach(OvObject parent, OvObject child) {
        parent.Attach(child);
        child.ApplyComponent();
        child.ApplyConfig();
    }

    private static OvObject Part(string name, Vector2 position, Vector2 size) {
        var obj = new OvObject();
        obj.Config.Name.Value = name;
        var rt = obj.Config.RectTransformConfig;
        rt.AnchorMin.Value = rt.AnchorMax.Value = rt.Pivot.Value = new Vector2(0.5f, 0.5f);
        rt.AnchoredPosition.Value = position;
        rt.SizeDelta.Value = size;
        return obj;
    }

    private static OvObject Image(string name, string sprite, Vector2 size, FxValue<Color> color) {
        var obj = Part(name, Vector2.zero, size);
        obj.Config.ImageConfig = new ImageSettings();
        obj.Config.ImageConfig.SpriteKey = FxValue<string>.FromValue(sprite);
        obj.Config.ImageConfig.Type.Value = UnityEngine.UI.Image.Type.Sliced;
        obj.Config.ImageConfig.RaycastTarget.Value = false;
        obj.Config.ImageConfig.Color = color;
        return obj;
    }

    // Like an imported key's text: midline, shrinks to fit, no wrapping.
    private static OvObject Text(string name, string template, float fontSize, Vector2 position, Vector2 size, FxValue<GradientColor> color) {
        var obj = Part(name, position, size);
        var tmp = obj.Config.TextConfig = new TextMeshProUGUISettings();
        tmp.FontSize.Value = fontSize;
        tmp.Alignment.Value = TextAlignmentOptions.Midline;
        tmp.AutoSize.Value = true;
        tmp.FontSizeRange.Value = new Vector2(0f, fontSize);
        tmp.RichText.Value = true;
        tmp.EnableShadow.Value = false;
        tmp.TextWrappingMode.Value = 0;
        tmp.OverFlowMode.Value = TextOverflowModes.Overflow;
        tmp.Color = color;
        obj.Config.TextEngineConfig = OvTextSettings.FromLegacy(template);
        obj.Config.ContentSizeFitterConfig = new ContentSizeFitterSettings();
        return obj;
    }

    private static string N(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    // One number that moves between released and pressed while the key is held (the module's easing).
    private static string Ease(Spec spec, string slot, float released, float pressed) {
        if (Mathf.Approximately(released, pressed)) {
            return N(released);
        }
        string q(string s) => "\"" + s + "\"";
        return $"Tag.KV_EaseScalar({q(spec.Code)}, {q(slot)}, {q(N(released))}, {q(N(pressed))}, "
            + $"{q(N(spec.Ms / 1000f))}, {q("OutExpo")}, {q(N(spec.Ms * 2f / 1000f))}, {q("OutExpo")})";
    }

    private static string EaseColor(Spec spec, string slot, float[] released, float[] pressed) {
        var r = C(released);
        var p = C(pressed);
        return $"[{Ease(spec, slot + ".r", r.r, p.r)}, {Ease(spec, slot + ".g", r.g, p.g)}, "
            + $"{Ease(spec, slot + ".b", r.b, p.b)}, {Ease(spec, slot + ".a", r.a, p.a)}]";
    }

    private static FxValue<Color> ColorFx(Spec spec, string slot, float[] released, float[] pressed)
        => spec.IsInfo || C(released) == C(pressed) ? FxValue<Color>.FromValue(C(released))
            : FxValue<Color>.FromExpression(EaseColor(spec, slot, released, pressed));

    private static FxValue<GradientColor> GradientFx(Spec spec, string slot, float[] released, float[] pressed)
        => spec.IsInfo || C(released) == C(pressed) ? FxValue<GradientColor>.FromValue(new GradientColor(C(released), true))
            : FxValue<GradientColor>.FromExpression(EaseColor(spec, slot, released, pressed));

    // ---- the total ----

    private static int totalFrame = -1;
    private static int totalCache;
    private static readonly List<string> liveCodes = [];
    private static readonly Dictionary<string, string> codeOfSpec = [];

    /// <summary>Presses of the editor's KeyViewer keys that are there now, added up (deleted ones don't count).</summary>
    [Tag(Name = "HudKeysTotal", TagType = TagType.Hide, Desc = "[KeyViewer] Presses of the HUD editor's KeyViewer keys, added up")]
    public static int HudKeysTotal() {
        int frame = Time.frameCount;
        if (frame == totalFrame) {
            return totalCache;
        }
        totalFrame = frame;
        if (!TagManager.TryGet("KV_Count", out var count)) {
            return totalCache = 0;
        }
        liveCodes.Clear();
        foreach (var obj in OverlayCore.Canvases.Where(c => c != null).SelectMany(c => c.OvObjects).SelectMany(Walk)) {
            if (obj.Config.Meta == null || !obj.Config.Meta.TryGetValue(SpecKey, out string json)) {
                continue;
            }
            if (!codeOfSpec.TryGetValue(json, out string code)) {
                var spec = Spec.Read(json);
                code = spec.IsInfo ? null : spec.Code;
                if (codeOfSpec.Count > 512) {
                    codeOfSpec.Clear();
                }
                codeOfSpec[json] = code;
            }
            if (!string.IsNullOrEmpty(code) && !liveCodes.Contains(code)) {
                liveCodes.Add(code);
            }
        }
        int total = 0;
        foreach (string code in liveCodes) {
            try {
                total += Convert.ToInt32(count.Invoke(CountPrefix, code));
            } catch {
            }
        }
        return totalCache = total;
    }

    // ---- the tab ----

    private static bool listening;
    private static HudPanelContext listeningFor;
    private static O5Button bindButton;

    private static void Change(HudPanelContext ctx, Action<Spec> edit) {
        var group = ctx.Object;
        ctx.Edit(() => {
            var spec = Spec.Read(group.Config.Meta[SpecKey]);
            edit(spec);
            Fill(group, spec);
        });
    }

    private static void BuildTab(HudPanelContext ctx) {
        listening = false;
        bindButton = null;
        var spec = Spec.Read(ctx.Object.Config.Meta[SpecKey]);
        var ui = ctx.Ui;
        var parent = ctx.Content;

        if (spec.IsInfo) {
            var kind = O5Factory.ControlText(ui, O5Factory.Row(ui, parent, 34f), 18f);
            kind.richText = true;
            kind.text = string.Format(T("HUD_KV_INFO", "Shows: <b>{0}</b>"), spec.Name);
        } else {
            // Bind: click, then press the key.
            bindButton = ctx.Track(O5Factory.Button(ui, O5Factory.Row(ui, parent, 44f), "kv_bind", new O5ButtonOptions {
                Text = string.Format(T("HUD_KV_BIND", "Key: <b>{0}</b>   (click, then press a key)"), spec.Code),
                OnClick = () => {
                    listening = true;
                    listeningFor = ctx;
                },
            }));
        }

        ctx.Track(O5Factory.Input(ui, O5Factory.Row(ui, parent), "kv_label", new O5InputOptions {
            Placeholder = string.Format(T("HUD_KV_LABEL_PH", "Label (empty: {0})"), spec.IsInfo ? spec.Name : spec.Code),
            Value = spec.IsInfo ? (spec.Name ?? string.Empty) : spec.Label,
            OnEndEdit = v => Change(ctx, s => {
                if (s.IsInfo) {
                    s.Name = string.IsNullOrWhiteSpace(v) ? s.Name : v;
                } else {
                    s.Label = v ?? string.Empty;
                }
            }),
        }));

        if (!spec.IsInfo) {
            ctx.Track(O5Factory.Toggle(ui, O5Factory.Row(ui, parent), "kv_count", new O5ToggleOptions {
                Text = T("HUD_KV_COUNT", "Show press count"), Value = spec.Count, DefaultValue = true,
                OnChanged = v => Change(ctx, s => s.Count = v),
            }));
        }
        ctx.Track(O5Factory.Toggle(ui, O5Factory.Row(ui, parent), "kv_outline", new O5ToggleOptions {
            Text = T("HUD_KV_OUTLINE", "Outline"), Value = spec.Outline, DefaultValue = true,
            OnChanged = v => Change(ctx, s => s.Outline = v),
        }));

        ColorRow(ctx, "kv_box", T("HUD_KV_BOX", "Box"), C(spec.Box), (s, c) => s.Box = F(c));
        if (!spec.IsInfo) {
            ColorRow(ctx, "kv_box_held", T("HUD_KV_BOX_HELD", "Box while held"), C(spec.BoxHeld), (s, c) => s.BoxHeld = F(c));
        }
        ColorRow(ctx, "kv_text", T("HUD_KV_TEXT", "Text"), C(spec.Text), (s, c) => s.Text = F(c));
        if (!spec.IsInfo) {
            ColorRow(ctx, "kv_text_held", T("HUD_KV_TEXT_HELD", "Text while held"), C(spec.TextHeld), (s, c) => s.TextHeld = F(c));

            var size = ctx.Track(O5Factory.Slider(ui, O5Factory.Row(ui, parent), "kv_press", new O5SliderOptions {
                Min = 70, Max = 110, Label = T("HUD_KV_PRESS", "Size while held (%)"), Format = "F0",
            }));
            size.Set(spec.PressScale * 100f, false);
            size.OnComplete = v => Change(ctx, s => s.PressScale = (float)v / 100f);

            var speed = ctx.Track(O5Factory.Slider(ui, O5Factory.Row(ui, parent), "kv_speed", new O5SliderOptions {
                Min = 0, Max = 400, Label = T("HUD_KV_SPEED", "Animation (ms)"), Format = "F0",
            }));
            speed.Set(spec.Ms, false);
            speed.OnComplete = v => Change(ctx, s => s.Ms = (float)v);
        }

        var note = O5Factory.ControlText(ui, O5Factory.Row(ui, parent, 40f), 14f);
        note.enableWordWrapping = true;
        note.color = new Color(1f, 1f, 1f, 0.5f);
        note.text = T("HUD_KV_NOTE", "Move and size it like anything else. Keys of imported KeyViewer profiles are set up in the KeyViewer page.");
    }

    private static void ColorRow(HudPanelContext ctx, string id, string label, Color value, Action<Spec, Color> set) {
        var picker = ctx.Track(O5Factory.ColorPicker(ctx.Ui, O5Factory.Row(ctx.Ui, ctx.Content, 50f), id, new O5ColorPickerOptions {
            CanvasRect = UICore.CanvasObj.GetComponent<RectTransform>(),
            CanvasCamera = UICore.Canvas ? UICore.Canvas.worldCamera : null,
            Label = label,
        }));
        picker.Set(value, false);
        // Saved when let go, so dragging the picker doesn't pile up undo steps.
        picker.OnComplete = c => Change(ctx, s => set(s, c));
    }

    // Waiting for the key to bind: the first key pressed (not the click that started it).
    private static void TickTab(HudPanelContext ctx) {
        if (!listening || !ReferenceEquals(ctx, listeningFor)) {
            return;
        }
        if (bindButton != null) {
            bindButton.Text = T("HUD_KV_LISTEN", "Press a key...   (Esc: cancel)");
        }
        if (Input.GetKeyDown(KeyCode.Escape)) {
            listening = false;
            ctx.Rebuild();
            return;
        }
        foreach (KeyCode code in Enum.GetValues(typeof(KeyCode))) {
            if (code == KeyCode.None || code == KeyCode.Mouse0 || code == KeyCode.Escape || !Input.GetKeyDown(code)) {
                continue;
            }
            listening = false;
            string name = code.ToString();
            Change(ctx, s => s.Code = name);
            return;
        }
    }
}
