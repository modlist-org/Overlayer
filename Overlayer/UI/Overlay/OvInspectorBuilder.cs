using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Overlay;

internal sealed class OvInspectorBuilder(
    RectTransform content,
    List<UIObject> controls,
    Action apply,
    Action save,
    Action rebuild,
    Action hierarchyChanged
) {
    private readonly RectTransform content = content;
    private readonly List<UIObject> controls = controls;
    private readonly Action apply = apply;
    private readonly Action save = save;
    private readonly Action rebuild = rebuild;
    private readonly Action hierarchyChanged = hierarchyChanged;

    public void BuildCanvas(OvCanvas canvas, Action<string> nameChanged) {
        var (_, identity) = Card("Canvas", false);
        Input(identity, "Canvas Name", "", canvas.Config.Name, value => {
            canvas.Config.Name = value;
            canvas.ApplyConfig();
            nameChanged(value);
        }, "canvas_name", hierarchyChanged);

        var group = canvas.Config.CanvasGroupConfig;
        Slider(identity, "Opacity", 1f, 0f, 1f, group.Alpha, value => group.Alpha = value, "canvas_alpha");
        Toggle(identity, "Interactable", false, group.Interactable, value => group.Interactable = value, "canvas_interactable");
        Toggle(identity, "Blocks Raycasts", true, group.BlocksRaycasts, value => group.BlocksRaycasts = value, "canvas_raycast");
        Toggle(identity, "Ignore Parent Groups", false, group.IgnoreParentGroups, value => group.IgnoreParentGroups = value, "canvas_ignore_parent");

        var (_, rendering) = Card("Rendering", false);
        var canvasCfg = canvas.Config.CanvasConfig;
        EnumDropDown(rendering, "Render Mode", RenderMode.ScreenSpaceOverlay, canvasCfg.RenderMode, value => canvasCfg.RenderMode = value, "canvas_render_mode");
        Slider(rendering, "Sorting Order", 32760f, -32768f, 32767f, canvasCfg.SortingOrder, value => canvasCfg.SortingOrder = Mathf.RoundToInt(value), "canvas_sort", "F0");
        Toggle(rendering, "Pixel Perfect", false, canvasCfg.PixelPerfect, value => canvasCfg.PixelPerfect = value, "canvas_pixel_perfect");
        Toggle(rendering, "Graphic Raycaster", true, canvas.Config.GraphicRaycasterConfig.Enabled, value => canvas.Config.GraphicRaycasterConfig.Enabled = value, "canvas_graphic_raycast");

        var (_, scaling) = Card("Scaling", false);
        var scale = canvas.Config.CanvasScalerConfig;
        EnumDropDown(scaling, "Scale Mode", CanvasScaler.ScaleMode.ScaleWithScreenSize, scale.UiScaleMode, value => scale.UiScaleMode = value, "canvas_scale_mode");
        Vector2Sliders(scaling, "Reference", new Vector2(1920, 1080), 1f, 8192f, () => scale.ReferenceResolution, value => scale.ReferenceResolution = value, "canvas_reference", "F0");
        Slider(scaling, "Match Width / Height", 0.5f, 0f, 1f, scale.MatchWidthOrHeight, value => scale.MatchWidthOrHeight = value, "canvas_match");
        Slider(scaling, "Scale Factor", 1f, 0.01f, 10f, scale.ScaleFactor, value => scale.ScaleFactor = value, "canvas_scale_factor");
    }

    public void BuildObject(OvObject obj) {
        var (_, identity) = Card("Object", false);
        Input(identity, "Object Name", "OvObject", obj.Config.Name, value => {
            obj.Config.Name = value;
            apply();
        }, "obj_name", hierarchyChanged);
        Toggle(identity, "Visible", true, obj.Config.Enabled, value => obj.Config.Enabled = value, "obj_visible");

        var group = obj.Config.CanvasGroupConfig;
        Slider(identity, "Opacity", 1f, 0f, 1f, group.Alpha, value => group.Alpha = value, "obj_alpha");
        Toggle(identity, "Interactable", false, group.Interactable, value => group.Interactable = value, "obj_interactable");
        Toggle(identity, "Blocks Raycasts", false, group.BlocksRaycasts, value => group.BlocksRaycasts = value, "obj_raycast");

        BuildTransform(obj.Config.RectTransformConfig);

        if(obj.Config.TextConfig != null) {
            BuildText(obj, obj.Config.TextConfig);
        }
        if(obj.Config.ImageConfig != null) {
            BuildImage(obj, obj.Config.ImageConfig);
        }
        if(obj.Config.ShadowConfig != null) {
            BuildShadow(obj, obj.Config.ShadowConfig);
        }
        if(obj.Config.OutlineConfig != null) {
            BuildOutline(obj, obj.Config.OutlineConfig);
        }
        if(obj.Config.MaskConfig != null) {
            BuildMask(obj, obj.Config.MaskConfig);
        }
        if(obj.Config.HasRectMask2D) {
            var (_, rectMask) = Card("Rect Mask 2D", true, () => {
                obj.Config.HasRectMask2D = false;
                RefreshComponents(obj);
            });
            Label(rectMask, "Clips child graphics to this object's rectangle.");
        }

        BuildAddComponent(obj);
    }

    private void BuildTransform(RectTransformSettings cfg) {
        var (_, basic) = Card("Transform", false);
        Vector2Sliders(basic, "Position", Vector2.zero, -10000f, 10000f, () => cfg.AnchoredPosition, value => cfg.AnchoredPosition = value, "transform_position", "F1");
        Vector2Sliders(basic, "Size", new Vector2(200, 200), -10000f, 10000f, () => cfg.SizeDelta, value => cfg.SizeDelta = value, "transform_size", "F1");
        Vector2Sliders(basic, "Pivot", new Vector2(0.5f, 0.5f), 0f, 1f, () => cfg.Pivot, value => cfg.Pivot = value, "transform_pivot");

        var (_, anchors) = Card("Anchors & Offsets", false);
        Vector2Sliders(anchors, "Anchor Min", Vector2.zero, 0f, 1f, () => cfg.AnchorMin, value => cfg.AnchorMin = value, "transform_anchor_min");
        Vector2Sliders(anchors, "Anchor Max", Vector2.one, 0f, 1f, () => cfg.AnchorMax, value => cfg.AnchorMax = value, "transform_anchor_max");
        Vector2Sliders(anchors, "Offset Min", Vector2.zero, -10000f, 10000f, () => cfg.OffsetMin, value => cfg.OffsetMin = value, "transform_offset_min", "F1");
        Vector2Sliders(anchors, "Offset Max", Vector2.zero, -10000f, 10000f, () => cfg.OffsetMax, value => cfg.OffsetMax = value, "transform_offset_max", "F1");
    }

    private void BuildText(OvObject obj, TextMeshProUGUISettings cfg) {
        var (_, card) = Card("Text", true, () => {
            obj.Config.TextConfig = null;
            RefreshComponents(obj);
        });

        CodeEditor(card, cfg.Text, value => {
            cfg.Text = value;
            apply();
        });
        Slider(card, "Font Size", 42f, 1f, 512f, cfg.FontSize, value => cfg.FontSize = value, "text_size", "F1");
        Toggle(card, "Rich Text", true, cfg.RichText, value => cfg.RichText = value, "text_rich");
        Toggle(card, "Auto Size", false, cfg.AutoSize, value => cfg.AutoSize = value, "text_auto_size");
        Vector2Sliders(card, "Font Range", new Vector2(16, 64), 1f, 512f, () => cfg.FontSizeRange, value => cfg.FontSizeRange = value, "text_font_range", "F1");
        EnumDropDown(card, "Alignment", TextAlignmentOptions.Center, cfg.Alignment, value => cfg.Alignment = value, "text_alignment");
        EnumDropDown(card, "Wrapping", TextWrappingModes.Normal, cfg.TextWrappingMode, value => cfg.TextWrappingMode = value, "text_wrapping");
        EnumDropDown(card, "Overflow", TextOverflowModes.Overflow, cfg.OverFlowMode, value => cfg.OverFlowMode = value, "text_overflow");
        Slider(card, "Line Spacing", 0f, -100f, 100f, cfg.LineSpacing, value => cfg.LineSpacing = value, "text_line_spacing", "F1");
        Slider(card, "Character Spacing", 0f, -100f, 100f, cfg.CharacterSpacing, value => cfg.CharacterSpacing = value, "text_char_spacing", "F1");
        Slider(card, "Word Spacing", 0f, -100f, 100f, cfg.WordSpacing, value => cfg.WordSpacing = value, "text_word_spacing", "F1");
        ColorSliders(card, "Color", () => (Color)cfg.Color, value => cfg.Color = value, "text_color");
        Toggle(card, "Material Outline", false, cfg.EnableOutline, value => cfg.EnableOutline = value, "text_outline");
        Slider(card, "Outline Width", 0.2f, 0f, 1f, cfg.OutlineWidth, value => cfg.OutlineWidth = value, "text_outline_width");
        Slider(card, "Outline Softness", 0f, 0f, 1f, cfg.OutlineSoftness, value => cfg.OutlineSoftness = value, "text_outline_softness");
        ColorSliders(card, "Outline", () => cfg.OutlineColor, value => cfg.OutlineColor = value, "text_outline_color");
    }

    private void BuildImage(OvObject obj, ImageSettings cfg) {
        var (_, card) = Card("Image", true, () => {
            obj.Config.ImageConfig = null;
            RefreshComponents(obj);
        });

        Input(card, "Sprite Key", "User resource key", cfg.SpriteKey, value => {
            cfg.SpriteKey = string.IsNullOrWhiteSpace(value) ? null : value;
            apply();
        }, "image_sprite");
        ColorSliders(card, "Color", () => cfg.Color, value => cfg.Color = value, "image_color");
        Toggle(card, "Preserve Aspect", false, cfg.PreserveAspect, value => cfg.PreserveAspect = value, "image_aspect");
        EnumDropDown(card, "Image Type", Image.Type.Simple, cfg.Type, value => cfg.Type = value, "image_type");
        EnumDropDown(card, "Fill Method", Image.FillMethod.Horizontal, cfg.FillMethod, value => cfg.FillMethod = value, "image_fill_method");
        Slider(card, "Fill Amount", 1f, 0f, 1f, cfg.FillAmount, value => cfg.FillAmount = value, "image_fill_amount");
    }

    private void BuildShadow(OvObject obj, ShadowSettings cfg) {
        var (_, card) = Card("Shadow", true, () => {
            obj.Config.ShadowConfig = null;
            RefreshComponents(obj);
        });
        Vector2Sliders(card, "Distance", new Vector2(6, -6), -100f, 100f, () => cfg.EffectDistance, value => cfg.EffectDistance = value, "shadow_distance", "F1");
        ColorSliders(card, "Color", () => cfg.EffectColor, value => cfg.EffectColor = value, "shadow_color");
        Toggle(card, "Use Graphic Alpha", true, cfg.UseGraphicAlpha, value => cfg.UseGraphicAlpha = value, "shadow_alpha");
    }

    private void BuildOutline(OvObject obj, OutlineSettings cfg) {
        var (_, card) = GenerateUI.ComponentCard(content, "Outline", cfg.Enabled, value => {
            cfg.Enabled = value;
            ApplyAndSave();
        }, () => {
            obj.Config.OutlineConfig = null;
            RefreshComponents(obj);
        });
        Vector2Sliders(card, "Distance", new Vector2(1, -1), -100f, 100f, () => cfg.EffectDistance, value => cfg.EffectDistance = value, "outline_distance", "F1");
        ColorSliders(card, "Color", () => cfg.EffectColor, value => cfg.EffectColor = value, "outline_color");
    }

    private void BuildMask(OvObject obj, MaskSettings cfg) {
        var (_, card) = Card("Mask", true, () => {
            obj.Config.MaskConfig = null;
            RefreshComponents(obj);
        });
        Toggle(card, "Show Mask Graphic", true, cfg.ShowMaskGraphic, value => cfg.ShowMaskGraphic = value, "mask_graphic");
    }

    private void BuildAddComponent(OvObject obj) {
        var options = new List<string> { "Add Component..." };
        if(obj.Config.TextConfig == null) options.Add("Text");
        if(obj.Config.ImageConfig == null) options.Add("Image");
        if(obj.Config.ShadowConfig == null) options.Add("Shadow");
        if(obj.Config.OutlineConfig == null) options.Add("Outline");
        if(obj.Config.MaskConfig == null) options.Add("Mask");
        if(!obj.Config.HasRectMask2D) options.Add("Rect Mask 2D");
        if(options.Count == 1) return;

        var row = GenerateUI.Row(content, 50f);
        var dropdown = GenerateUI.DropDown(row, options[0], options[0], options, value => value, selected => {
            switch(selected) {
                case "Text": obj.Config.TextConfig = new TextMeshProUGUISettings(); break;
                case "Image": obj.Config.ImageConfig = new ImageSettings(); break;
                case "Shadow": obj.Config.ShadowConfig = new ShadowSettings(); break;
                case "Outline": obj.Config.OutlineConfig = new OutlineSettings(); break;
                case "Mask": obj.Config.MaskConfig = new MaskSettings(); break;
                case "Rect Mask 2D": obj.Config.HasRectMask2D = true; break;
                default: return;
            }
            RefreshComponents(obj);
        }, "add_component");
        Track(dropdown);
    }

    private (RectTransform Card, RectTransform Content) Card(string title, bool removable, Action remove = null) {
        return GenerateUI.ComponentCard(content, title, true, null, remove, removable, showActiveToggle: false);
    }

    private void Input(Transform parent, string label, string defaultValue, string value, Action<string> changed, string id, Action finished = null) {
        var row = GenerateUI.Row(parent, 50f);
        var input = GenerateUI.Input(row, defaultValue, value, changed, label, null, id, _ => {
            finished?.Invoke();
            save();
        });
        Track(input);
    }

    private void CodeEditor(Transform parent, string value, Action<string> changed) {
        var row = GenerateUI.Row(parent, 132f);
        TextMeshProUGUI lineNumbers = null;

        void OnTextChanged(string text) {
            UpdateLineNumbers(lineNumbers, text);
            changed(text);
        }

        var input = GenerateUI.Input(
            row,
            "Text",
            value,
            OnTextChanged,
            "Text / tag expression",
            null,
            "text_content",
            _ => save(),
            multiline: true,
            monospace: true
        );

        const float gutterWidth = 44f;
        var text = input.InputField.textComponent;
        text.fontSize = 16f;
        text.characterSpacing = 0f;
        text.lineSpacing = 4f;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        input.Placeholder.fontSize = 16f;
        input.Placeholder.characterSpacing = 0f;
        input.Placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform viewport = input.InputField.textViewport;
        Vector2 viewportMin = viewport.offsetMin;
        Vector2 viewportMax = viewport.offsetMax;
        viewport.offsetMin = new Vector2(viewportMin.x + gutterWidth + 10f, viewportMin.y);

        var gutterObj = new GameObject("LineNumberGutter");
        gutterObj.transform.SetParent(input.InputField.transform, false);
        gutterObj.transform.SetAsFirstSibling();
        var gutterRect = gutterObj.AddComponent<RectTransform>();
        gutterRect.anchorMin = new Vector2(0f, 0f);
        gutterRect.anchorMax = new Vector2(0f, 1f);
        gutterRect.pivot = new Vector2(0f, 0.5f);
        gutterRect.offsetMin = new Vector2(viewportMin.x, viewportMin.y);
        gutterRect.offsetMax = new Vector2(viewportMin.x + gutterWidth, viewportMax.y);
        var gutterImage = gutterObj.AddComponent<Image>();
        gutterImage.color = new Color(0f, 0f, 0f, 0.16f);
        gutterImage.raycastTarget = false;
        gutterObj.AddComponent<RectMask2D>();

        var separatorObj = new GameObject("Separator");
        separatorObj.transform.SetParent(gutterObj.transform, false);
        var separatorRect = separatorObj.AddComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(1f, 0f);
        separatorRect.anchorMax = Vector2.one;
        separatorRect.pivot = new Vector2(1f, 0.5f);
        separatorRect.sizeDelta = new Vector2(1f, 0f);
        var separator = separatorObj.AddComponent<Image>();
        separator.color = new Color(1f, 1f, 1f, 0.1f);
        separator.raycastTarget = false;

        lineNumbers = GenerateUI.AddText(gutterObj.transform, true);
        lineNumbers.name = "LineNumbers";
        lineNumbers.font = text.font;
        lineNumbers.fontSize = text.fontSize;
        lineNumbers.characterSpacing = 0f;
        lineNumbers.lineSpacing = text.lineSpacing;
        lineNumbers.alignment = TextAlignmentOptions.TopRight;
        lineNumbers.verticalAlignment = VerticalAlignmentOptions.Top;
        lineNumbers.textWrappingMode = TextWrappingModes.NoWrap;
        lineNumbers.color = new Color(1f, 1f, 1f, 0.28f);
        lineNumbers.raycastTarget = false;
        var numbersRect = lineNumbers.rectTransform;
        numbersRect.anchorMin = Vector2.zero;
        numbersRect.anchorMax = Vector2.one;
        numbersRect.pivot = text.rectTransform.pivot;
        numbersRect.offsetMin = new Vector2(4f, 0f);
        numbersRect.offsetMax = new Vector2(-8f, 0f);

        var follower = gutterObj.AddComponent<UILineNumberGutter>();
        follower.Source = text.rectTransform;
        follower.LineNumbers = numbersRect;

        UpdateLineNumbers(lineNumbers, value);
        Track(input);
    }

    private static void UpdateLineNumbers(TextMeshProUGUI lineNumbers, string value) {
        if(lineNumbers == null) {
            return;
        }

        int count = 1;
        foreach(char c in value ?? string.Empty) {
            if(c == '\n') {
                count++;
            }
        }
        lineNumbers.text = string.Join("\n", Enumerable.Range(1, count));
    }

    private void Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format = "F2") {
        var row = GenerateUI.Row(parent, 50f);
        var slider = GenerateUI.Slider(row, defaultValue, min, max, value, format, false, null, newValue => {
            changed(newValue);
            apply();
        }, _ => save(), label, id);
        Track(slider);
    }

    private void Toggle(Transform parent, string label, bool defaultValue, bool value, Action<bool> changed, string id) {
        var row = GenerateUI.Row(parent, 50f);
        var toggle = GenerateUI.Toggle(row, defaultValue, value, newValue => {
            changed(newValue);
            ApplyAndSave();
        }, label, id);
        Track(toggle);
    }

    private void EnumDropDown<T>(Transform parent, string label, T defaultValue, T value, Action<T> changed, string id) where T : struct, Enum {
        var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.DropDown(row, defaultValue, value, values, option => $"{label}: {option}", newValue => {
            changed(newValue);
            ApplyAndSave();
        }, id);
        Track(dropdown);
    }

    private void Vector2Sliders(Transform parent, string label, Vector2 defaults, float min, float max, Func<Vector2> get, Action<Vector2> set, string id, string format = "F2") {
        Slider(parent, $"{label} X", defaults.x, min, max, get().x, value => set(new Vector2(value, get().y)), id + "_x", format);
        Slider(parent, $"{label} Y", defaults.y, min, max, get().y, value => set(new Vector2(get().x, value)), id + "_y", format);
    }

    private void ColorSliders(Transform parent, string label, Func<Color> get, Action<Color> set, string id) {
        Slider(parent, $"{label} R", 1f, 0f, 1f, get().r, value => { var color = get(); color.r = value; set(color); }, id + "_r");
        Slider(parent, $"{label} G", 1f, 0f, 1f, get().g, value => { var color = get(); color.g = value; set(color); }, id + "_g");
        Slider(parent, $"{label} B", 1f, 0f, 1f, get().b, value => { var color = get(); color.b = value; set(color); }, id + "_b");
        Slider(parent, $"{label} A", 1f, 0f, 1f, get().a, value => { var color = get(); color.a = value; set(color); }, id + "_a");
    }

    private void Label(Transform parent, string text) {
        var row = GenerateUI.Row(parent, 34f);
        var label = GenerateUI.AddText(row, true);
        label.text = text;
        label.fontSize = 16f;
        label.color = new Color(1f, 1f, 1f, 0.55f);
    }

    private void Track(UIObject control) {
        control.Rect.offsetMax = Vector2.zero;
        controls.Add(control);
    }

    private void ApplyAndSave() {
        apply();
        save();
    }

    private void RefreshComponents(OvObject obj) {
        obj.ApplyComponent();
        obj.ApplyConfig();
        save();
        rebuild();
    }
}
