using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using Overlayer.UI.Utility;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Localization;
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
    private static readonly EaseLibrary.Entry NoSaved = new(string.Empty, string.Empty);

    private static readonly CurveModel model = new();
    private static CurveGraph graph;
    private static O5Toggle smoothToggle, monoToggle, heightsToggle, animToggle;
    private static O5Slider xSlider, ySlider;
    private static O5InputField curveField, nameField;
    private static O5Dropdown<string> presetDropdown, compareDropdown;
    private static O5Dropdown<EaseLibrary.Entry> savedDropdown;
    private static Action refreshCompareCurves, refreshSavedCurves;
    private static O5Button undoButton, redoButton, loadButton, deleteButton;
    private static O5Button copyTagButton;
    private static TextMeshProUGUI exampleText;
    private static string comparePreset = string.Empty;
    private static bool endpointHeights;
    private static string lastLang;
    private static readonly List<Action> langRefreshers = [];
    private static Action<TranslationFailState> onLangLoadEnd;

    private static O5Context Ctx => O5KitAdapters.Ctx;
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
    private static string Tr(string key, string fallback, params object[] args) => string.Format(MainCore.Tr.Get(key, fallback), args);

    private static void BindLang(Action refresh) {
        langRefreshers.Add(refresh);
        refresh();
    }

    private static void RefreshLanguage() {
        lastLang = MainCore.Tr.Language;
        foreach (var refresh in langRefreshers) {
            refresh();
        }
        graph?.UpdateHint();
    }

    private static readonly string[] Presets = Enum.GetNames(typeof(O5Ease));

    // ---- page ----

    /// <summary>Builds the Easing page: graph, preview and tools on top, Basic / Advanced tabs below.</summary>
    public static void Create(RectTransform page) {
        langRefreshers.Clear();
        var (_, content, _) = O5Factory.ScrollView(Ctx, page, 12f, 18f);

        graph = new CurveGraph(O5Factory.Row(Ctx, content, 300f), model);
        Fill(graph.Rect);
        graph.ComparePreset = () => comparePreset;
        graph.EndpointHeights = () => endpointHeights;
        graph.SelectionChanged = _ => SyncPoint();

        BuildPreview(content);

        var tools = UiStyle.HRow(content, 4f, 36f);
        undoButton = UiStyle.IconButton(UiStyle.Cell(tools, 36f), "easing_undo", MainCore.Spr.Get(UISprite.TurnArrow128), () => graph.ApplyHistory(model.Undo()));
        redoButton = UiStyle.IconButton(UiStyle.Cell(tools, 36f), "easing_redo", MainCore.Spr.Get(UISprite.TurnArrow128), () => graph.ApplyHistory(model.Redo()));
        if (redoButton.Icon != null) {
            redoButton.Icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
        UiStyle.Cell(tools, 0f);
        LocButton(tools, "tidy", "EASING_TIDY", "Tidy points", () => {
            model.Tidy();
            graph.Select(-1);
        }, () => Tr("EASING_TIDY_TIP", "Reduces unnecessary points while keeping the same shape."), 130f);
        LocButton(tools, "reset", "EASING_RESET", "Reset", () => {
            model.Reset();
            graph.Select(-1);
        }, () => Tr("EASING_RESET_TIP", "Resets back to a straight line."), 90f);

        var tabs = new UiStyle.Tabs(content, "easing", Tr("BASIC", "Basic"), Tr("ADVANCED", "Advanced"));
        BindLang(() => {
            tabs.TabButton(0).Text = Tr("BASIC", "Basic");
            tabs.TabButton(1).Text = Tr("ADVANCED", "Advanced");
        });
        BuildBasic(tabs[0]);
        BuildAdvanced(tabs[1]);

        lastLang = MainCore.Tr.Language;
        if (onLangLoadEnd != null) {
            MainCore.Tr.OnLoadEnd -= onLangLoadEnd;
        }
        onLangLoadEnd = state => {
            if (state == TranslationFailState.Success && graph != null) {
                RefreshLanguage();
            }
        };
        MainCore.Tr.OnLoadEnd += onLangLoadEnd;
        model.Changed -= OnModelChanged;
        model.Changed += OnModelChanged;
        model.HistoryChanged -= UpdateHistoryButtons;
        model.HistoryChanged += UpdateHistoryButtons;
        OnModelChanged();
        UpdateHistoryButtons();
    }

    public static void Dispose() {
        if (onLangLoadEnd != null) {
            MainCore.Tr.OnLoadEnd -= onLangLoadEnd;
            onLangLoadEnd = null;
        }
        model.Changed -= OnModelChanged;
        model.HistoryChanged -= UpdateHistoryButtons;
        langRefreshers.Clear();
        tangentButtons.Clear();
        undoButton = redoButton = loadButton = deleteButton = copyTagButton = null;
        graph = null;
    }

    /// <summary>Per frame while the page is showing (input, hover, preview loop).</summary>
    public static void Tick() {
        if (graph == null) {
            return;
        }
        if (UICore.CanvasObj == null || !UICore.CanvasObj.activeSelf
            || UICore.CurrentMenuState != (int)OriginalMenuState.Easing) {
            graph.ReleasePin();
            return;
        }
        if (lastLang != MainCore.Tr.Language) {
            RefreshLanguage();
        }
        graph.Tick();
    }

    private static void BuildBasic(Transform parent) {
        LocSection(parent, "EASING_SHAPE", "Shape");
        smoothToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_smooth", new O5ToggleOptions {
            Text = Tr("EASING_SMOOTH", "Smooth"),
            Value = model.Kind != CustomEaseKind.Lines,
            DefaultValue = true,
        });
        smoothToggle.OnChanged = _ => ApplyKind();
        BindLang(() => smoothToggle.Label.text = Tr("EASING_SMOOTH", "Smooth"));
        UiStyle.Tooltip(smoothToggle, () => Tr("EASING_SMOOTH_TIP", "When on, draws a smooth curve through every point; when off, draws straight lines."));

        LocSection(parent, "EASING_SELECTED_POINT", "Selected point");
        xSlider = PointSlider(parent, "x", "X", 0, 1, v => new Vector2((float)v, Selected().y));
        ySlider = PointSlider(parent, "y", "Y", CurveModel.MinY, CurveModel.MaxY, v => new Vector2(Selected().x, (float)v));

        LocSection(parent, "EASING_PRESETS", "Presets");
        presetDropdown = O5Factory.DropDown(Ctx, O5Factory.Row(Ctx, parent), "easing_preset", new O5DropdownOptions<string> {
            Values = [NoPreset, .. Presets],
            Value = NoPreset,
            ShowIndexIndicator = false,
            Display = v => v == NoPreset ? Tr("EASING_START_PRESET", "Start from a preset…") : v,
            OnChanged = v => {
                if (string.IsNullOrEmpty(v) || v == NoPreset) {
                    return;
                }
                model.LoadPreset(v);
                graph.Select(-1);
                presetDropdown?.Set(NoPreset, false);
            },
        });
        var refreshPresetCurves = EaseDropdownCurveHelper.Attach(presetDropdown, v => string.IsNullOrEmpty(v) || v == NoPreset ? null : v);
        compareDropdown = O5Factory.DropDown(Ctx, O5Factory.Row(Ctx, parent), "easing_compare", new O5DropdownOptions<string> {
            Values = CompareValues(),
            Value = NoPreset,
            Display = v => v == NoPreset ? Tr("EASING_COMPARE_WITH", "Compare with…") : Tr("EASING_COMPARING_WITH", "Comparing with {0}", v),
            OnChanged = v => {
                comparePreset = v == NoPreset ? string.Empty : v ?? string.Empty;
                graph.MarkDirty();
            },
        });
        refreshCompareCurves = EaseDropdownCurveHelper.Attach(compareDropdown, v => string.IsNullOrEmpty(v) || v == NoPreset ? null : v);
        BindLang(() => {
            presetDropdown.SetValues([NoPreset, .. Presets]);
            presetDropdown.Set(presetDropdown.Value, false);
            refreshPresetCurves?.Invoke();
            compareDropdown.SetValues(CompareValues());
            compareDropdown.Set(compareDropdown.Value, false);
            refreshCompareCurves?.Invoke();
        });

        LocSection(parent, "EASING_USE_IN_TAG", "Use in a tag");
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
        UiStyle.Tooltip(curveField, () => Tr("EASING_CURVE_TEXT_TIP", "The curve as text. Paste one here (or a preset name) to edit it."));
        exampleText = O5Factory.ControlText(Ctx, O5Factory.Row(Ctx, parent, 40f), 15f);
        exampleText.color = UiStyle.Caption;
        exampleText.enableWordWrapping = false;
        exampleText.overflowMode = TextOverflowModes.Ellipsis;
        var copy = UiStyle.HRow(parent, 4f, 36f);
        CopyButton(copy, "copy_curve", "EASING_COPY_CURVE", "Copy curve", () => model.Text);
        copyTagButton = CopyButton(copy, "copy_tag", "EASING_COPY_TAG", "Copy example tag", ExampleTag);

        BuildSaved(parent);
    }

    // Purple dot follows the curve, orange dot the compared preset.
    private static void BuildPreview(Transform parent) {
        var row = O5Factory.Row(Ctx, parent, 62f);
        var bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle128P512);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.22f);
        bg.raycastTarget = false;

        var track = NewRect("Track", row);
        Fill(track);
        track.offsetMin = new Vector2(20f, 22f);
        track.offsetMax = new Vector2(-20f, -6f);

        var rail = UiStyle.NewRect("Rail", track).gameObject.AddComponent<Image>();
        rail.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rail.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rail.rectTransform.sizeDelta = new Vector2(0f, 1f);
        rail.color = new Color(1f, 1f, 1f, 0.12f);
        rail.raycastTarget = false;

        var unitRail = UiStyle.NewRect("UnitRail", track).gameObject.AddComponent<Image>();
        unitRail.rectTransform.anchorMin = unitRail.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        unitRail.rectTransform.pivot = new Vector2(0f, 0.5f);
        unitRail.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
        unitRail.color = new Color(1f, 1f, 1f, 0.28f);
        unitRail.raycastTarget = false;

        RectTransform TickLine(string name, Color color) {
            var line = UiStyle.NewRect(name, track);
            line.anchorMin = Vector2.zero;
            line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.offsetMin = line.offsetMax = Vector2.zero;
            line.sizeDelta = new Vector2(1f, 0f);
            var img = line.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return line;
        }

        var underLine = TickLine("UnderLine", new Color(1f, 1f, 1f, 0.22f));
        var overLine = TickLine("OverLine", new Color(1f, 1f, 1f, 0.22f));
        var zeroLine = TickLine("ZeroLine", new Color(1f, 1f, 1f, 0.42f));
        var oneLine = TickLine("OneLine", new Color(1f, 1f, 1f, 0.42f));

        var labels = NewRect("Labels", row);
        Fill(labels);
        labels.offsetMin = new Vector2(20f, 2f);
        labels.offsetMax = new Vector2(-20f, -42f);

        TextMeshProUGUI ScaleLabel(string name, TextAlignmentOptions align, Color color, float pivotX, float width, string text) {
            var t = O5Factory.ControlText(Ctx, labels, 11f, true);
            t.gameObject.name = name;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            t.rectTransform.pivot = new Vector2(pivotX, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(width, 18f);
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            TmpCompat.SetNoWrap(t);
            t.text = text;
            return t;
        }

        Color edgeColor = O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.8f);
        var underLabel = ScaleLabel("UnderLabel", TextAlignmentOptions.MidlineLeft, edgeColor, 0f, 64f, string.Empty);
        var overLabel = ScaleLabel("OverLabel", TextAlignmentOptions.MidlineRight, edgeColor, 1f, 64f, string.Empty);
        var zeroLabel = ScaleLabel("ZeroLabel", TextAlignmentOptions.Center, UiStyle.Muted, 0.5f, 44f, "0");
        var oneLabel = ScaleLabel("OneLabel", TextAlignmentOptions.Center, UiStyle.Muted, 0.5f, 44f, "1");
        var zeroOneLabel = ScaleLabel("ZeroOneLabel", TextAlignmentOptions.Center, UiStyle.Muted, 0.5f, 44f, "0-1");

        var curveDot = PreviewDot(track, "Curve", UIColors.ObjectActive, 12f);
        var compareDot = PreviewDot(track, "Compare", new Color(1f, 0.75f, 0.35f, 0.85f), 10f);
        compareDot.gameObject.SetActive(false);

        graph.BindPreview(track, curveDot, compareDot, unitRail.rectTransform,
            zeroLine, oneLine, underLine, overLine,
            zeroLabel, oneLabel, zeroOneLabel, underLabel, overLabel);
    }

    private static RectTransform PreviewDot(RectTransform track, string name, Color color, float size) {
        var dot = NewRect(name, track);
        dot.anchorMin = dot.anchorMax = new Vector2(0f, 0.5f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(size, size);
        dot.anchoredPosition = Vector2.zero;
        var img = dot.gameObject.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISprite.Circle128);
        img.color = color;
        img.raycastTarget = false;
        return dot;
    }

    private static void BuildSaved(Transform parent) {
        LocSection(parent, "EASING_SAVED_CURVES", "Saved curves");
        var saveRow = UiStyle.HRow(parent, 4f);
        nameField = O5Factory.Input(Ctx, UiStyle.Cell(saveRow, 0f), "easing_name", new O5InputOptions {
            Placeholder = Tr("EASING_NAME", "Name"),
        });
        BindLang(() => nameField.Placeholder.text = Tr("EASING_NAME", "Name"));
        LocButton(saveRow, "save", "SAVE", "Save", () => {
            string name = nameField.Value;
            if (string.IsNullOrWhiteSpace(name)) {
                return;
            }
            EaseLibrary.Put(name, model.Text);
            RefreshSaved(name.Trim());
        }, () => Tr("EASING_SAVE_TIP", "Saves or overwrites a curve with this name."), 110f);

        var savedList = EaseLibrary.Entries.ToList();
        bool hasSaved = savedList.Count > 0;
        savedDropdown = O5Factory.DropDown(Ctx, O5Factory.Row(Ctx, parent), "easing_saved", new O5DropdownOptions<EaseLibrary.Entry> {
            Values = hasSaved ? savedList : [NoSaved],
            Value = hasSaved ? savedList[0] : NoSaved,
            Display = e => e == null || ReferenceEquals(e, NoSaved) || string.IsNullOrEmpty(e.Name) ? Tr("EASING_NO_SAVED", "No saved curves") : e.Name,
        });
        refreshSavedCurves = EaseDropdownCurveHelper.Attach(savedDropdown, e => e == null || ReferenceEquals(e, NoSaved) || string.IsNullOrEmpty(e.Curve) ? null : e.Curve);
        BindLang(() => RefreshSaved(savedDropdown?.Value?.Name));

        var loadRow = UiStyle.HRow(parent, 4f, 36f);
        loadButton = LocButton(loadRow, "load", "EASING_LOAD", "Load", () => {
            var entry = savedDropdown.Value;
            if (entry == null || ReferenceEquals(entry, NoSaved) || string.IsNullOrEmpty(entry.Curve)) {
                return;
            }
            model.Load(entry.Curve);
            nameField.Set(entry.Name, false);
            graph.Select(-1);
        });
        deleteButton = LocButton(loadRow, "delete", "EASING_DELETE", "Delete", null);
        Utility.HoldToConfirm.Attach(deleteButton, () => {
            var entry = savedDropdown.Value;
            if (entry == null || ReferenceEquals(entry, NoSaved) || string.IsNullOrEmpty(entry.Curve)) {
                return;
            }
            EaseLibrary.Remove(entry);
            RefreshSaved(null);
        }, canConfirm: () => savedDropdown.Value != null && !ReferenceEquals(savedDropdown.Value, NoSaved));
        loadButton.SetBlocked(!hasSaved, true);
        deleteButton.SetBlocked(!hasSaved, true);
    }

    private static List<string> CompareValues() {
        var list = new List<string> { NoPreset };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NoPreset };
        foreach (var entry in EaseLibrary.Entries) {
            string n = entry?.Name?.Trim();
            if (!string.IsNullOrEmpty(n) && seen.Add(n)) {
                list.Add(n);
            }
        }
        foreach (string preset in Presets) {
            if (seen.Add(preset)) {
                list.Add(preset);
            }
        }
        return list;
    }

    private static void RefreshSaved(string select) {
        if (savedDropdown != null) {
            var list = EaseLibrary.Entries.ToList();
            bool hasSaved = list.Count > 0;
            if (!hasSaved) {
                savedDropdown.SetExpanded(false);
                savedDropdown.SetValues([NoSaved]);
                savedDropdown.Set(NoSaved, false);
            } else {
                savedDropdown.SetValues(list);
                var pick = list.FirstOrDefault(e => string.Equals(e.Name, select, StringComparison.OrdinalIgnoreCase)) ?? list[0];
                savedDropdown.Set(pick, false);
            }
            loadButton?.SetBlocked(!hasSaved, true);
            deleteButton?.SetBlocked(!hasSaved, true);
            refreshSavedCurves?.Invoke();
        }

        if (compareDropdown != null) {
            var compareValues = CompareValues();
            compareDropdown.SetValues(compareValues);
            refreshCompareCurves?.Invoke();
            if (!string.IsNullOrEmpty(comparePreset) && !compareValues.Any(v => string.Equals(v, comparePreset, StringComparison.OrdinalIgnoreCase))) {
                comparePreset = string.Empty;
                compareDropdown.Set(NoPreset, false);
                graph?.MarkDirty();
            }
        }
    }

    // ---- advanced controls ----

    private static void BuildAdvanced(Transform parent) {
        LocSection(parent, "EASING_SHAPE", "Shape");
        monoToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_mono", new O5ToggleOptions {
            Text = Tr("EASING_NO_OVERSHOOT", "No overshoot"),
            Value = model.Kind == CustomEaseKind.Mono,
            DefaultValue = false,
        });
        monoToggle.OnChanged = _ => ApplyKind();
        BindLang(() => monoToggle.Label.text = Tr("EASING_NO_OVERSHOOT", "No overshoot"));
        UiStyle.Tooltip(monoToggle, () => Tr("EASING_NO_OVERSHOOT_TIP", "Smooths the curve while keeping it within the range of surrounding points."));

        heightsToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_heights", new O5ToggleOptions {
            Text = Tr("EASING_MOVABLE_ENDPOINTS", "Movable start / end height"),
            Value = endpointHeights,
            DefaultValue = false,
        });
        heightsToggle.OnChanged = v => {
            endpointHeights = v;
            graph?.MarkDirty();
            SyncPoint();
        };
        BindLang(() => heightsToggle.Label.text = Tr("EASING_MOVABLE_ENDPOINTS", "Movable start / end height"));

        LocSection(parent, "EASING_ANIM_CURVE", "Animation curve");
        animToggle = O5Factory.Toggle(Ctx, O5Factory.Row(Ctx, parent), "easing_anim", new O5ToggleOptions {
            Text = Tr("EASING_TANGENT_HANDLES", "Tangent handles"),
            Value = model.IsAnim,
            DefaultValue = false,
        });
        animToggle.OnChanged = _ => ApplyKind();
        BindLang(() => animToggle.Label.text = Tr("EASING_TANGENT_HANDLES", "Tangent handles"));
        UiStyle.Tooltip(animToggle, () => Tr("EASING_TANGENT_HANDLES_TIP", "Like Unity's AnimationCurve: each point gets handles that set its slope. "
            + "Drag a handle to bend the curve; hold Alt to move one side only."));
        var tangentRow = UiStyle.HRow(parent, 4f, 36f);
        tangentButtons.Clear();
        tangentButtons.Add(LocButton(tangentRow, "tan_auto", "EASING_AUTO_SLOPES", "Auto slopes", () => model.AutoTangents(), () => Tr("EASING_AUTO_SLOPES_TIP", "Smooths the slope of every point.")));
        tangentButtons.Add(LocButton(tangentRow, "tan_flat", "EASING_FLAT", "Flat", () => model.Flatten(graph.Selected), () => Tr("EASING_FLAT_TIP", "Flattens the slope of the selected point.")));
        tangentButtons.Add(LocButton(tangentRow, "tan_join", "EASING_JOIN_HANDLES", "Join handles", () => model.Join(graph.Selected), () => Tr("EASING_JOIN_HANDLES_TIP", "Rejoins both handles of the selected point to share the same slope.")));
    }

    private static readonly List<O5Button> tangentButtons = [];


    // ---- state sync ----

    private static void ApplyKind() {
        bool anim = animToggle != null && animToggle.Value;
        bool smooth = smoothToggle == null || smoothToggle.Value;
        bool mono = monoToggle != null && monoToggle.Value;
        model.SetKind(anim ? CustomEaseKind.Anim : !smooth ? CustomEaseKind.Lines : mono ? CustomEaseKind.Mono : CustomEaseKind.Curve);
    }

    private static void OnModelChanged() {
        animToggle?.Set(model.IsAnim, false);
        // Tangent handles decide the shape themselves; the other shape options wait until they're off.
        smoothToggle?.SetBlocked(model.IsAnim, true);
        monoToggle?.SetBlocked(model.IsAnim || model.Kind == CustomEaseKind.Lines, true);
        if (!model.IsAnim) {
            smoothToggle?.Set(model.Kind != CustomEaseKind.Lines, false);
            if (model.Kind == CustomEaseKind.Mono) {
                monoToggle?.Set(true, false);
            } else if (model.Kind == CustomEaseKind.Curve) {
                monoToggle?.Set(false, false);
            }
        }
        if (!endpointHeights && model.Points.Count >= 2
            && (!Mathf.Approximately(model.Points[0].y, 0f) || !Mathf.Approximately(model.Points[model.Points.Count - 1].y, 1f))) {
            endpointHeights = true;
            heightsToggle?.Set(true, false);
        }
        if (curveField != null && !curveField.InputField.isFocused) {
            curveField.Set(model.Text, false);
        }
        if (exampleText != null) {
            exampleText.text = ExampleTag();
        }
        SyncPoint();
    }

    private static void UpdateHistoryButtons() {
        undoButton?.SetBlocked(!model.CanUndo, true);
        redoButton?.SetBlocked(!model.CanRedo, true);
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
        for (int b = 0; b < tangentButtons.Count; b++) {
            // "Auto slopes" works on every point; the rest need a selected point.
            tangentButtons[b].SetBlocked(!model.IsAnim || (b > 0 && !has), true);
        }
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

    /// <summary>The Copy example tag button's rect while the page is showing, for the tutorial to point at.</summary>
    internal static RectTransform CopyTagRect {
        get {
            if (copyTagButton == null || copyTagButton.IsDisposed) {
                return null;
            }
            var r = copyTagButton.Rect;
            return r != null && r.gameObject.activeInHierarchy ? r : null;
        }
    }

    private static void Copy(string text) {
        GUIUtility.systemCopyBuffer = text;
        MainCore.Log.Msg($"[Easing] Copied: {text}");
    }

    // Docs-style copy button: label swaps to a check for a moment, then back.
    private static O5Button CopyButton(Transform row, string id, string key, string fallback, Func<string> getText) {
        var button = LocButton(row, id, key, fallback, () => { });
        Image check = null;
        ITweenHandle reset = null;
        button.OnClick = () => {
            Copy(getText());
            if (button?.Label == null) {
                return;
            }
            if (check == null) {
                var go = new GameObject("Check");
                go.transform.SetParent(button.Rect, false);
                check = go.AddComponent<Image>();
                check.sprite = MainCore.Spr.Get(UISprite.Check128);
                check.preserveAspect = true;
                var r = check.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(20f, 20f);
                check.raycastTarget = false;
                check.gameObject.SetActive(false);
            }
            button.Label.gameObject.SetActive(false);
            check.gameObject.SetActive(true);
            reset?.Kill();
            reset = Ctx.Tween.TweenFloat(() => 0f, _ => { }, 1f, 0.85f, () => {
                if (check != null) {
                    check.gameObject.SetActive(false);
                }
                if (button?.Label != null) {
                    button.Label.gameObject.SetActive(true);
                }
            });
        };
        return button;
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

    private static void LocSection(Transform parent, string key, string fallback) {
        var label = UiStyle.Section(parent, Tr(key, fallback));
        BindLang(() => label.text = Tr(key, fallback).ToUpperInvariant());
    }

    private static O5Button LocButton(Transform row, string id, string key, string fallback, Action onClick, Func<string> tooltip = null, float width = 0f) {
        var button = UiStyle.Button(UiStyle.Cell(row, width), "easing_" + id, Tr(key, fallback), onClick);
        BindLang(() => button.Text = Tr(key, fallback));
        UiStyle.Tooltip(button, tooltip);
        return button;
    }
}
