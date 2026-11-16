using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using Overlayer.UI.Utility;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Easing;

// Easing page: draw a curve by placing points, preview it, copy it into a tag.
internal static class CurveEditor {
    private const string NoPreset = "—";

    private static readonly CurveModel model = new();
    private static CurveGraph graph;
    private static O5Toggle smoothToggle, monoToggle, heightsToggle;
    private static O5Slider xSlider, ySlider;
    private static O5InputField curveField, nameField;
    private static O5Dropdown<string> presetDropdown, compareDropdown;
    private static O5Dropdown<EaseLibrary.Entry> savedDropdown;
    private static TextMeshProUGUI exampleText;
    private static string comparePreset = string.Empty;
    private static bool endpointHeights;

    private static O5Context Ctx => O5KitAdapters.Ctx;

    private static readonly string[] Presets = Enum.GetNames(typeof(O5Ease));

    // ---- page ----

    /// <summary>Builds the Easing page: graph, preview and tools on top, Basic / Advanced tabs below.</summary>
    public static void Create(RectTransform page) {
        var (_, content, _) = O5Factory.ScrollView(Ctx, page, 12f, 18f);

        graph = new CurveGraph(O5Factory.Row(Ctx, content, 300f), model);
        Fill(graph.Rect);
        graph.ComparePreset = () => comparePreset;
        graph.EndpointHeights = () => endpointHeights;
        graph.SelectionChanged = _ => SyncPoint();

        BuildPreview(content);

        var tools = UiStyle.HRow(content, 4f, 36f);
        Button(tools, "undo", "Undo", () => model.Undo(), "Ctrl+Z", 80f);
        Button(tools, "redo", "Redo", () => model.Redo(), "Ctrl+Y", 80f);
        UiStyle.Cell(tools, 0f);
        Button(tools, "tidy", "Tidy points", () => {
            model.Tidy();
            graph.Select(-1);
        }, "Fewer points, same shape", 130f);
        Button(tools, "reset", "Reset", () => {
            model.Reset();
            graph.Select(-1);
        }, "Back to a straight line", 90f);

        var tabs = new UiStyle.Tabs(content, "easing", "Basic", "Advanced");
        BuildBasic(tabs[0]);
        BuildAdvanced(tabs[1]);

        model.Changed += OnModelChanged;
        OnModelChanged();
    }

    /// <summary>Per frame while the page is showing (input, hover, preview loop).</summary>
    public static void Tick() {
        if (graph == null || UICore.CanvasObj == null || !UICore.CanvasObj.activeSelf
            || UICore.CurrentMenuState != (int)OriginalMenuState.Easing) {
            return;
        }
        graph.Tick();
    }

    private static void BuildBasic(Transform parent) {
        UiStyle.Section(parent, "Shape");
        smoothToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_smooth", new O5ToggleOptions {
            Text = "Smooth",
            Value = model.Kind != CustomEaseKind.Lines,
            DefaultValue = true,
        });
        smoothToggle.OnChanged = _ => ApplyKind();
        UiStyle.Tooltip(smoothToggle, "On: a smooth curve through every point. Off: straight lines.");

        UiStyle.Section(parent, "Selected point");
        xSlider = PointSlider(parent, "x", "X", 0, 1, v => new Vector2((float)v, Selected().y));
        ySlider = PointSlider(parent, "y", "Y", CurveModel.MinY, CurveModel.MaxY, v => new Vector2(Selected().x, (float)v));

        UiStyle.Section(parent, "Presets");
        presetDropdown = O5Factory.DropDown(Ctx, O5Factory.Row(Ctx, parent), "easing_preset", new O5DropdownOptions<string> {
            Values = [NoPreset, .. Presets],
            Value = NoPreset,
            Display = v => v == NoPreset ? "Start from a preset…" : v,
            OnChanged = v => {
                if (string.IsNullOrEmpty(v) || v == NoPreset) {
                    return;
                }
                model.LoadPreset(v);
                graph.Select(-1);
                presetDropdown?.Set(NoPreset, false);
            },
        });
        compareDropdown = O5Factory.DropDown(Ctx, O5Factory.Row(Ctx, parent), "easing_compare", new O5DropdownOptions<string> {
            Values = [NoPreset, .. Presets],
            Value = NoPreset,
            Display = v => v == NoPreset ? "Compare with…" : "Comparing with " + v,
            OnChanged = v => {
                comparePreset = v == NoPreset ? string.Empty : v ?? string.Empty;
                graph.MarkDirty();
            },
        });

        UiStyle.Section(parent, "Use in a tag");
        curveField = O5Factory.Input(Ctx, O5Factory.Row(Ctx, parent), "easing_text", new O5InputOptions {
            Value = model.Text,
            Placeholder = "curve(0,0;1,1)",
            Monospace = true,
            OnEndEdit = text => {
                if (text == model.Text) {
                    return;
                }
                if (!model.Load(text)) {
                    curveField.Set(model.Text, false);
                }
                graph.Select(-1);
            },
        });
        UiStyle.Tooltip(curveField, "The curve as text. Paste one here (or a preset name) to edit it.");
        exampleText = O5Factory.ControlText(Ctx, O5Factory.Row(Ctx, parent, 40f), 15f);
        exampleText.color = UiStyle.Caption;
        exampleText.enableWordWrapping = true;
        var copy = UiStyle.HRow(parent, 4f, 36f);
        Button(copy, "copy_curve", "Copy curve", () => Copy(model.Text));
        Button(copy, "copy_tag", "Copy example tag", () => Copy(ExampleTag()));

        BuildSaved(parent);
    }

    // Purple box follows the curve, orange box the compared preset.
    private static void BuildPreview(Transform parent) {
        var row = O5Factory.Row(Ctx, parent, 48f);
        var bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.22f);
        bg.raycastTarget = false;

        var track = NewRect("Track", row);
        Fill(track);
        track.offsetMin = new Vector2(18f, 8f);
        track.offsetMax = new Vector2(-18f, -8f);
        var rail = UiStyle.NewRect("Rail", track).gameObject.AddComponent<Image>();
        rail.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rail.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rail.rectTransform.sizeDelta = new Vector2(0f, 1f);
        rail.color = UiStyle.Line;
        rail.raycastTarget = false;
        graph.PreviewTrack = track;
        graph.PreviewBox = Box(track, "Curve", new Color(0.569f, 0.604f, 1f, 1f), 9f);
        graph.CompareBox = Box(track, "Compare", new Color(1f, 0.75f, 0.35f, 0.8f), -9f);
    }

    private static RectTransform Box(RectTransform track, string name, Color color, float y) {
        var box = NewRect(name, track);
        box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
        box.pivot = new Vector2(0f, 0.5f);
        box.sizeDelta = new Vector2(28f, 14f);
        box.anchoredPosition = new Vector2(0f, y);
        var img = box.gameObject.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return box;
    }

    private static void BuildSaved(Transform parent) {
        UiStyle.Section(parent, "Saved curves");
        var saveRow = UiStyle.HRow(parent, 4f);
        nameField = O5Factory.Input(Ctx, UiStyle.Cell(saveRow, 0f), "easing_name", new O5InputOptions {
            Placeholder = "Name",
        });
        Button(saveRow, "save", "Save", () => {
            string name = nameField.Value;
            if (string.IsNullOrWhiteSpace(name)) {
                return;
            }
            EaseLibrary.Put(name, model.Text);
            RefreshSaved(name.Trim());
        }, "Saves (or overwrites) a curve with this name", 110f);

        var loadRow = UiStyle.HRow(parent, 4f);
        savedDropdown = O5Factory.DropDown(Ctx, UiStyle.Cell(loadRow, 0f), "easing_saved", new O5DropdownOptions<EaseLibrary.Entry> {
            Values = EaseLibrary.Entries.ToList(),
            Display = e => e == null ? "No saved curves" : e.Name,
        });
        Button(loadRow, "load", "Load", () => {
            var entry = savedDropdown.Value;
            if (entry == null) {
                return;
            }
            model.Load(entry.Curve);
            nameField.Set(entry.Name, false);
            graph.Select(-1);
        }, null, 90f);
        Button(loadRow, "delete", "Delete", () => {
            var entry = savedDropdown.Value;
            if (entry == null) {
                return;
            }
            EaseLibrary.Remove(entry);
            RefreshSaved(null);
        }, null, 90f);
        if (EaseLibrary.Entries.Count > 0) {
            savedDropdown.Set(EaseLibrary.Entries[0], false);
        }
    }

    private static void RefreshSaved(string select) {
        if (savedDropdown == null) {
            return;
        }
        var list = EaseLibrary.Entries.ToList();
        savedDropdown.SetValues(list);
        var pick = list.FirstOrDefault(e => string.Equals(e.Name, select, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
        savedDropdown.Set(pick, false);
    }

    // ---- advanced controls ----

    private static void BuildAdvanced(Transform parent) {
        UiStyle.Section(parent, "Shape");
        monoToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_mono", new O5ToggleOptions {
            Text = "No overshoot",
            Value = model.Kind == CustomEaseKind.Mono,
            DefaultValue = false,
        });
        monoToggle.OnChanged = _ => ApplyKind();
        UiStyle.Tooltip(monoToggle, "Smooth, but never goes above or below the points around it (monotone).");

        heightsToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_heights", new O5ToggleOptions {
            Text = "Movable start / end height",
            Value = endpointHeights,
            DefaultValue = false,
        });
        heightsToggle.OnChanged = v => {
            endpointHeights = v;
            graph?.MarkDirty();
            SyncPoint();
        };
    }


    // ---- state sync ----

    private static void ApplyKind() {
        bool smooth = smoothToggle == null || smoothToggle.Value;
        bool mono = monoToggle != null && monoToggle.Value;
        model.SetKind(!smooth ? CustomEaseKind.Lines : mono ? CustomEaseKind.Mono : CustomEaseKind.Curve);
    }

    private static void OnModelChanged() {
        smoothToggle?.Set(model.Kind != CustomEaseKind.Lines, false);
        if (model.Kind == CustomEaseKind.Mono) {
            monoToggle?.Set(true, false);
        } else if (model.Kind == CustomEaseKind.Curve) {
            monoToggle?.Set(false, false);
        }
        if (curveField != null && !curveField.InputField.isFocused) {
            curveField.Set(model.Text, false);
        }
        if (exampleText != null) {
            exampleText.text = ExampleTag();
        }
        SyncPoint();
    }

    private static Vector2 Selected() {
        int i = graph != null ? graph.Selected : -1;
        return i >= 0 && i < model.Points.Count ? model.Points[i] : Vector2.zero;
    }

    private static void SyncPoint() {
        if (graph == null || xSlider == null) {
            return;
        }
        int i = graph.Selected;
        bool has = i >= 0 && i < model.Points.Count;
        bool endpoint = has && model.IsEndpoint(i);
        xSlider.SetBlocked(!has || endpoint, true);
        ySlider.SetBlocked(!has || (endpoint && !endpointHeights), true);
        if (!has) {
            return;
        }
        xSlider.Set(model.Points[i].x, false);
        ySlider.Set(model.Points[i].y, false);
    }

    private static O5Slider PointSlider(Transform parent, string id, string label, double min, double max, Func<double, Vector2> point) {
        var slider = O5Factory.Slider(Ctx, O5Factory.Row(Ctx, parent), "easing_" + id, new O5SliderOptions {
            Min = min,
            Max = max,
            Label = label,
            Format = "F3",
        });
        slider.OnChanged = v => {
            if (graph == null || graph.Selected < 0) {
                return;
            }
            model.Move(graph.Selected, point(v), endpointHeights);
        };
        slider.OnComplete = _ => model.Snapshot();
        slider.SetBlocked(true, true);
        return slider;
    }

    private static string ExampleTag() => $"{{EasedValue(Combo,0,300,{model.Text})}}";

    private static void Copy(string text) {
        GUIUtility.systemCopyBuffer = text;
        MainCore.Log.Msg($"[Easing] Copied: {text}");
    }

    // ---- layout helpers ----

    private static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void Fill(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static O5Button Button(Transform row, string id, string text, Action onClick, string tooltip = null, float width = 0f) {
        var button = UiStyle.Button(UiStyle.Cell(row, width), "easing_" + id, text, onClick);
        UiStyle.Tooltip(button, tooltip);
        return button;
    }
}
