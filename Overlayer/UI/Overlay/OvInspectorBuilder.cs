using Overlayer.IO.UnityComponent.Impl;
using Overlayer.IO.UnityComponent;
using Overlayer.IO.Overlay;
using Overlayer.IO.User;
using Overlayer.Overlay;
using Overlayer.Compat.OVC;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Core;
using Overlayer.TextEngine.Highlight;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
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
    private enum AnchorMode { Custom = -1, Min, Middle, Max, Stretch }

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

        BuildTransform(obj);

        if(obj.Config.TextConfig != null) {
            BuildText(obj, obj.Config.TextConfig);
        }
        if(obj.Config.ImageConfig != null) {
            BuildImage(obj, obj.Config.ImageConfig);
        }
        if(obj.Config.ContentSizeFitterConfig != null) {
            BuildContentSizeFitter(obj, obj.Config.ContentSizeFitterConfig);
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
            var (_, rectMask) = GenerateUI.ComponentCard(content, "Rect Mask 2D", obj.Config.RectMask2DEnabled, value => {
                obj.Config.RectMask2DEnabled = value;
                ApplyAndSave();
            }, () => {
                obj.Config.HasRectMask2D = false;
                obj.Config.RectMask2DEnabled = true;
                RefreshComponents(obj);
            });
            Label(rectMask, "Clips child graphics to this object's rectangle.");
        }

        BuildAddComponent(obj);
    }

    private void BuildTransform(OvObject obj) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        var (_, basic) = Card("Rect Transform", false);
        Action refreshPositionFields = null;
        RectTransform rectLayout = CompactRow(basic, 92f, 6f);
        Action refreshAnchor = AnchorPresetControl(rectLayout, obj, () => refreshPositionFields?.Invoke());
        refreshPositionFields = BuildRectPositionFields(rectLayout, obj);

        NumericPropertyRow(basic, "Position", new[] {
            ("Z", 0f, (Func<float>)(() => cfg.AnchoredPositionZ), (Action<float>)(value => cfg.AnchoredPositionZ = value), "rect_position_z")
        }, "F1");
        NumericPropertyRow(basic, "Rotation", new[] {
            ("X", 0f, (Func<float>)(() => cfg.RotationXY.x), (Action<float>)(value => cfg.RotationXY.x = value), "rect_rotation_x"),
            ("Y", 0f, (Func<float>)(() => cfg.RotationXY.y), (Action<float>)(value => cfg.RotationXY.y = value), "rect_rotation_y"),
            ("Z", 0f, (Func<float>)(() => cfg.Rotation), (Action<float>)(value => cfg.Rotation = value), "rect_rotation_z")
        }, "F1");
        NumericPropertyRow(basic, "Scale", new[] {
            ("X", 1f, (Func<float>)(() => cfg.Scale.x), (Action<float>)(value => cfg.Scale.x = value), "rect_scale_x"),
            ("Y", 1f, (Func<float>)(() => cfg.Scale.y), (Action<float>)(value => cfg.Scale.y = value), "rect_scale_y"),
            ("Z", 1f, (Func<float>)(() => cfg.Scale.z), (Action<float>)(value => cfg.Scale.z = value), "rect_scale_z")
        }, "F2");
        NumericPropertyRow(basic, "Pivot", new[] {
            ("X", 0.5f, (Func<float>)(() => cfg.Pivot.x), (Action<float>)(value => cfg.Pivot.x = value), "rect_pivot_x"),
            ("Y", 0.5f, (Func<float>)(() => cfg.Pivot.y), (Action<float>)(value => cfg.Pivot.y = value), "rect_pivot_y")
        }, "F2");

        var (_, anchors) = Card("Anchors", false);
        NumericPropertyRow(anchors, "Min", new[] {
            ("X", 0f, (Func<float>)(() => cfg.AnchorMin.x), (Action<float>)(value => { cfg.AnchorMin.x = value; refreshAnchor(); refreshPositionFields(); }), "transform_anchor_min_x"),
            ("Y", 0f, (Func<float>)(() => cfg.AnchorMin.y), (Action<float>)(value => { cfg.AnchorMin.y = value; refreshAnchor(); refreshPositionFields(); }), "transform_anchor_min_y")
        }, "F2");
        NumericPropertyRow(anchors, "Max", new[] {
            ("X", 1f, (Func<float>)(() => cfg.AnchorMax.x), (Action<float>)(value => { cfg.AnchorMax.x = value; refreshAnchor(); refreshPositionFields(); }), "transform_anchor_max_x"),
            ("Y", 1f, (Func<float>)(() => cfg.AnchorMax.y), (Action<float>)(value => { cfg.AnchorMax.y = value; refreshAnchor(); refreshPositionFields(); }), "transform_anchor_max_y")
        }, "F2");
    }

    private void BuildText(OvObject obj, TextMeshProUGUISettings cfg) {
        OvTextSettings textCfg = obj.Config.TextEngineConfig ??= OvTextSettings.FromLegacy(cfg.Text);
        var (_, card) = ComponentCard("Text", cfg, () => {
            obj.Config.TextConfig = null;
            obj.Config.TextEngineConfig = null;
            RefreshComponents(obj);
        });

        CodeEditor(card, "Playing Text", "text_playing", textCfg.PlayingText, value => {
            textCfg.PlayingText = value;
            cfg.Text = value;
            apply();
        }, () => obj.GameObject.GetComponent<OvObject.TextEngineUpdater>()?.PlayingEngine);
        CodeEditor(card, "Not Playing Text", "text_not_playing", textCfg.NotPlayingText, value => {
            textCfg.NotPlayingText = value;
            apply();
        }, () => obj.GameObject.GetComponent<OvObject.TextEngineUpdater>()?.NotPlayingEngine);
        Slider(card, "Font Size", 48f, 1f, 512f, cfg.FontSize, value => cfg.FontSize = value, "text_size", "F1");
        Toggle(card, "Rich Text", true, cfg.RichText, value => cfg.RichText = value, "text_rich");
        Toggle(card, "Auto Size", false, cfg.AutoSize, value => cfg.AutoSize = value, "text_auto_size");
        Vector2Sliders(card, "Font Range", new Vector2(16, 64), 1f, 512f, () => cfg.FontSizeRange, value => cfg.FontSizeRange = value, "text_font_range", "F1");
        EnumDropDown(card, "Alignment", TextAlignmentOptions.Center, cfg.Alignment, value => cfg.Alignment = value, "text_alignment");
        EnumDropDown(card, "Wrapping", TextWrappingModes.Normal, cfg.TextWrappingMode, value => cfg.TextWrappingMode = value, "text_wrapping");
        EnumDropDown(card, "Overflow", TextOverflowModes.Overflow, cfg.OverFlowMode, value => cfg.OverFlowMode = value, "text_overflow");
        Slider(card, "Line Spacing", 0f, -100f, 100f, cfg.LineSpacing, value => cfg.LineSpacing = value, "text_line_spacing", "F1");
        Slider(card, "Character Spacing", 0f, -100f, 100f, cfg.CharacterSpacing, value => cfg.CharacterSpacing = value, "text_char_spacing", "F1");
        Slider(card, "Word Spacing", 0f, -100f, 100f, cfg.WordSpacing, value => cfg.WordSpacing = value, "text_word_spacing", "F1");
        ColorSliders(card, "Color", Color.white, () => (Color)cfg.Color, value => cfg.Color = value, "text_color");
        Toggle(card, "Material Outline", false, cfg.EnableOutline, value => cfg.EnableOutline = value, "text_outline");
        Slider(card, "Outline Width", 0.05f, 0f, 0.25f, cfg.OutlineWidth, value => cfg.OutlineWidth = value, "text_outline_width");
        Slider(card, "Outline Softness", 0f, 0f, 1f, cfg.OutlineSoftness, value => cfg.OutlineSoftness = value, "text_outline_softness");
        Slider(card, "Face Dilate", 0f, -1f, 1f, cfg.FaceDilate, value => cfg.FaceDilate = value, "text_face_dilate");
        ColorSliders(card, "Outline", Color.black, () => cfg.OutlineColor, value => cfg.OutlineColor = value, "text_outline_color");
        Toggle(card, "Material Shadow", true, cfg.EnableShadow, value => cfg.EnableShadow = value, "text_shadow");
        Vector2Sliders(card, "Shadow Offset", new Vector2(0.75f, -0.75f), -1f, 1f, () => cfg.ShadowOffset, value => cfg.ShadowOffset = value, "text_shadow_offset", "F2");
        Slider(card, "Shadow Dilate", 1f, 0f, 1f, cfg.ShadowDilate, value => cfg.ShadowDilate = value, "text_shadow_dilate");
        Slider(card, "Shadow Softness", 0.5f, 0f, 1f, cfg.ShadowSoftness, value => cfg.ShadowSoftness = value, "text_shadow_softness");
        ColorSliders(card, "Shadow", new Color(0f, 0f, 0f, 0.5f), () => cfg.ShadowColor, value => cfg.ShadowColor = value, "text_shadow_color");
    }

    private void BuildImage(OvObject obj, ImageSettings cfg) {
        var (_, card) = ComponentCard("Image", cfg, () => {
            obj.Config.ImageConfig = null;
            RefreshComponents(obj);
        });

        SpriteDropDown(card, cfg);
        ColorSliders(card, "Color", Color.white, () => cfg.Color, value => cfg.Color = value, "image_color");
        Toggle(card, "Raycast Target", true, cfg.RaycastTarget, value => cfg.RaycastTarget = value, "image_raycast");
        Toggle(card, "Preserve Aspect", false, cfg.PreserveAspect, value => cfg.PreserveAspect = value, "image_aspect");
        Toggle(card, "Use Sprite Mesh", false, cfg.UseSpriteMesh, value => cfg.UseSpriteMesh = value, "image_sprite_mesh");
        EnumDropDown(card, "Image Type", Image.Type.Simple, cfg.Type, value => cfg.Type = value, "image_type");
        Toggle(card, "Fill Center", true, cfg.FillCenter, value => cfg.FillCenter = value, "image_fill_center");
        Slider(card, "Pixels Per Unit", 1f, 0.01f, 10f, cfg.PixelsPerUnitMultiplier, value => cfg.PixelsPerUnitMultiplier = value, "image_pixels_per_unit");
        EnumDropDown(card, "Fill Method", Image.FillMethod.Horizontal, cfg.FillMethod, value => cfg.FillMethod = value, "image_fill_method");
        Slider(card, "Fill Amount", 1f, 0f, 1f, cfg.FillAmount, value => cfg.FillAmount = value, "image_fill_amount");
        Slider(card, "Fill Origin", 0f, 0f, 3f, cfg.FillOrigin, value => cfg.FillOrigin = Mathf.RoundToInt(value), "image_fill_origin", "F0");
        Toggle(card, "Fill Clockwise", true, cfg.FillClockwise, value => cfg.FillClockwise = value, "image_fill_clockwise");
    }

    private void BuildContentSizeFitter(OvObject obj, ContentSizeFitterSettings cfg) {
        var (_, card) = ComponentCard("Content Size Fitter", cfg, () => {
            obj.Config.ContentSizeFitterConfig = null;
            RefreshComponents(obj);
        }, () => RefreshDrivenLayout(obj));
        EnumDropDown(card, "Horizontal Fit", ContentSizeFitter.FitMode.PreferredSize, cfg.HorizontalFit, value => cfg.HorizontalFit = value, "content_size_horizontal", () => RefreshDrivenLayout(obj));
        EnumDropDown(card, "Vertical Fit", ContentSizeFitter.FitMode.PreferredSize, cfg.VerticalFit, value => cfg.VerticalFit = value, "content_size_vertical", () => RefreshDrivenLayout(obj));
    }

    private void BuildShadow(OvObject obj, ShadowSettings cfg) {
        var (_, card) = ComponentCard("Shadow", cfg, () => {
            obj.Config.ShadowConfig = null;
            RefreshComponents(obj);
        });
        Vector2Sliders(card, "Distance", new Vector2(6, -6), -100f, 100f, () => cfg.EffectDistance, value => cfg.EffectDistance = value, "shadow_distance", "F1");
        ColorSliders(card, "Color", Color.black, () => cfg.EffectColor, value => cfg.EffectColor = value, "shadow_color");
        Toggle(card, "Use Graphic Alpha", true, cfg.UseGraphicAlpha, value => cfg.UseGraphicAlpha = value, "shadow_alpha");
    }

    private void BuildOutline(OvObject obj, OutlineSettings cfg) {
        var (_, card) = ComponentCard("Outline", cfg, () => {
            obj.Config.OutlineConfig = null;
            RefreshComponents(obj);
        });
        Vector2Sliders(card, "Distance", new Vector2(1, -1), -100f, 100f, () => cfg.EffectDistance, value => cfg.EffectDistance = value, "outline_distance", "F1");
        ColorSliders(card, "Color", Color.red, () => cfg.EffectColor, value => cfg.EffectColor = value, "outline_color");
        Toggle(card, "Use Graphic Alpha", true, cfg.UseGraphicAlpha, value => cfg.UseGraphicAlpha = value, "outline_alpha");
    }

    private void BuildMask(OvObject obj, MaskSettings cfg) {
        var (_, card) = ComponentCard("Mask", cfg, () => {
            obj.Config.MaskConfig = null;
            RefreshComponents(obj);
        });
        Toggle(card, "Show Mask Graphic", true, cfg.ShowMaskGraphic, value => cfg.ShowMaskGraphic = value, "mask_graphic");
    }

    private void BuildAddComponent(OvObject obj) {
        var options = new List<string> { "Add Component..." };
        if(obj.Config.TextConfig == null && obj.Config.ImageConfig == null) {
            options.Add("Text");
            options.Add("Image");
        }
        if(obj.Config.ShadowConfig == null) options.Add("Shadow");
        if(obj.Config.OutlineConfig == null) options.Add("Outline");
        if(obj.Config.MaskConfig == null) options.Add("Mask");
        if(obj.Config.ContentSizeFitterConfig == null) options.Add("Content Size Fitter");
        if(!obj.Config.HasRectMask2D) options.Add("Rect Mask 2D");
        if(options.Count == 1) return;

        var row = GenerateUI.Row(content, 50f);
        var dropdown = GenerateUI.DropDown(row, options[0], options[0], options, value => value, selected => {
            switch(selected) {
                case "Text":
                    obj.Config.TextConfig = new TextMeshProUGUISettings();
                    obj.Config.TextEngineConfig = new OvTextSettings();
                    break;
                case "Image": obj.Config.ImageConfig = new ImageSettings(); break;
                case "Shadow": obj.Config.ShadowConfig = new ShadowSettings(); break;
                case "Outline": obj.Config.OutlineConfig = new OutlineSettings(); break;
                case "Mask": obj.Config.MaskConfig = new MaskSettings(); break;
                case "Content Size Fitter": obj.Config.ContentSizeFitterConfig = new ContentSizeFitterSettings(); break;
                case "Rect Mask 2D":
                    obj.Config.HasRectMask2D = true;
                    obj.Config.RectMask2DEnabled = true;
                    break;
                default: return;
            }
            RefreshComponents(obj);
        }, "add_component");
        Track(dropdown);
    }

    private (RectTransform Card, RectTransform Content) Card(string title, bool removable, Action remove = null) {
        return GenerateUI.ComponentCard(content, title, true, null, remove, removable, showActiveToggle: false);
    }

    private (RectTransform Card, RectTransform Content) ComponentCard(
        string title,
        UnityComponentSettingsBase settings,
        Action remove,
        Action enabledChanged = null
    ) {
        return GenerateUI.ComponentCard(content, title, settings.ComponentEnabled, value => {
            settings.ComponentEnabled = value;
            if(enabledChanged == null) ApplyAndSave();
            else enabledChanged();
        }, remove);
    }

    private void Input(Transform parent, string label, string defaultValue, string value, Action<string> changed, string id, Action finished = null) {
        var row = GenerateUI.Row(parent, 50f);
        var input = GenerateUI.Input(row, defaultValue, value, changed, label, null, id, _ => {
            finished?.Invoke();
            save();
        });
        Track(input);
    }

    private void CodeEditor(
        Transform parent,
        string label,
        string id,
        string value,
        Action<string> changed,
        Func<TextEngineCore> getEngine
    ) {
        const float editorHeight = 132f;
        const float diagnosticsLineHeight = 20f;
        var row = GenerateUI.Row(parent, editorHeight + 28f);
        var rowLayout = row.GetComponent<LayoutElement>();
        TextMeshProUGUI lineNumbers = null;
        string displayedText = value ?? string.Empty;
        string diagnosticsKey = null;
        int? hoverGeometryKey = null;
        bool? hoverComposing = null;
        CompileDiagnostic[] displayedDiagnostics = [];
        bool diagnosticsCompiling = false;
        TagSyntaxSpan[] syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);

        void OnTextChanged(string text) {
            displayedText = text ?? string.Empty;
            syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
            diagnosticsKey = null;
            UpdateLineNumbers(lineNumbers, displayedText);
            changed(text);
        }

        var input = GenerateUI.Input(
            row,
            null,
            value,
            OnTextChanged,
            $"{label} / tag expression",
            null,
            id,
            _ => save(),
            multiline: true,
            monospace: true,
            codeEditor: true
        );
        var codeInput = (UICodeInputField)input.InputField;

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
        float diagnosticsHeight = 28f;
        Vector2 baseViewportMin = viewportMin;
        viewportMin.y += diagnosticsHeight;
        viewport.offsetMin = new Vector2(viewportMin.x + gutterWidth + 10f, viewportMin.y);

        var diagnosticsObj = new GameObject("Diagnostics");
        diagnosticsObj.transform.SetParent(input.InputField.transform, false);
        var diagnosticsRect = diagnosticsObj.AddComponent<RectTransform>();
        diagnosticsRect.anchorMin = Vector2.zero;
        diagnosticsRect.anchorMax = new Vector2(1f, 0f);
        diagnosticsRect.pivot = new Vector2(0.5f, 0f);
        diagnosticsRect.offsetMin = new Vector2(12f, 8f);
        diagnosticsRect.offsetMax = new Vector2(-12f, 8f + diagnosticsHeight);
        var diagnosticsBg = diagnosticsObj.AddComponent<Image>();
        diagnosticsBg.color = new Color(0f, 0f, 0f, 0.14f);
        diagnosticsBg.raycastTarget = false;

        var diagnosticsText = GenerateUI.AddText(diagnosticsObj.transform, true);
        diagnosticsText.font = text.font;
        diagnosticsText.fontSize = 13f;
        diagnosticsText.characterSpacing = 0f;
        diagnosticsText.alignment = TextAlignmentOptions.TopLeft;
        diagnosticsText.verticalAlignment = VerticalAlignmentOptions.Top;
        diagnosticsText.textWrappingMode = TextWrappingModes.NoWrap;
        diagnosticsText.overflowMode = TextOverflowModes.Overflow;
        diagnosticsText.color = new Color(1f, 1f, 1f, 0.42f);
        diagnosticsText.raycastTarget = false;
        diagnosticsText.rectTransform.offsetMin = new Vector2(8f, 0f);
        diagnosticsText.rectTransform.offsetMax = new Vector2(-8f, 0f);

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

        var diagnosticHoverRoot = new GameObject("DiagnosticHoverTargets");
        diagnosticHoverRoot.transform.SetParent(text.transform, false);
        var diagnosticHoverRect = diagnosticHoverRoot.AddComponent<RectTransform>();
        diagnosticHoverRect.anchorMin = Vector2.zero;
        diagnosticHoverRect.anchorMax = Vector2.one;
        diagnosticHoverRect.pivot = text.rectTransform.pivot;
        diagnosticHoverRect.offsetMin = Vector2.zero;
        diagnosticHoverRect.offsetMax = Vector2.zero;

        codeInput.AfterLabelUpdate = (sourceText, composing) => {
            int geometryKey = BuildTextGeometryKey(sourceText);
            if(hoverGeometryKey != geometryKey || hoverComposing != composing) {
                hoverGeometryKey = geometryKey;
                hoverComposing = composing;
                RebuildDiagnosticHoverTargets(
                    diagnosticHoverRect,
                    sourceText,
                    displayedText,
                    composing || diagnosticsCompiling ? [] : displayedDiagnostics
                );
            }
            ApplySyntaxHighlighting(sourceText, composing ? null : displayedText, composing ? [] : syntaxSpans);
        };

        void SetDiagnosticsHeight(int diagnosticCount) {
            int lines = Math.Max(1, diagnosticCount);
            diagnosticsHeight = lines * diagnosticsLineHeight + 8f;
            float rowHeight = editorHeight + diagnosticsHeight;
            rowLayout.minHeight = rowHeight;
            rowLayout.preferredHeight = rowHeight;

            diagnosticsRect.offsetMax = new Vector2(-12f, 8f + diagnosticsHeight);
            viewportMin = baseViewportMin;
            viewportMin.y += diagnosticsHeight;
            viewport.offsetMin = new Vector2(viewportMin.x + gutterWidth + 10f, viewportMin.y);
            gutterRect.offsetMin = new Vector2(viewportMin.x, viewportMin.y);
        }

        void RefreshDiagnostics() {
            var engine = getEngine?.Invoke();
            var state = engine?.State ?? TextEngineState.Idle;
            if(state == TextEngineState.Compiling) {
                if(!diagnosticsCompiling) {
                    diagnosticsCompiling = true;
                    hoverGeometryKey = null;
                }
                return;
            }

            diagnosticsCompiling = false;
            var diagnostics = state == TextEngineState.Ready || state == TextEngineState.Error
                ? engine.GetDiagnostics()
                : [];
            string key = BuildDiagnosticsKey(state, diagnostics, displayedText);
            bool diagnosticsChanged = key != diagnosticsKey;

            if(diagnosticsChanged) {
                diagnosticsKey = key;
                displayedDiagnostics = diagnostics;
                syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
                hoverGeometryKey = null;
                SetDiagnosticsHeight(diagnostics.Length);
                UpdateLineNumbers(lineNumbers, displayedText, diagnostics);
                UpdateDiagnosticsBar(diagnosticsText, state, diagnostics, displayedText);
            }
        }

        RefreshDiagnostics();
        controls.Add(new UIWatcher(id + "_diagnostics", diagnosticsRect, RefreshDiagnostics));
        Track(input);
    }

    private static void UpdateLineNumbers(
        TextMeshProUGUI lineNumbers,
        string value,
        CompileDiagnostic[] diagnostics = null
    ) {
        if(lineNumbers == null) {
            return;
        }

        value ??= string.Empty;
        int count = 1;
        foreach(char c in value) {
            if(c == '\n') {
                count++;
            }
        }

        var severities = new CompileSeverity?[count];
        foreach(var diagnostic in diagnostics ?? []) {
            int line = GetLine(value, diagnostic.Context.Index);
            if(line < 0 || line >= count) {
                continue;
            }

            if(!severities[line].HasValue || diagnostic.Severity > severities[line].Value) {
                severities[line] = diagnostic.Severity;
            }
        }

        lineNumbers.text = string.Join("\n", Enumerable.Range(0, count).Select(i =>
            severities[i] switch {
                CompileSeverity.Error => $"<color=#E2676D>{i + 1}</color>",
                CompileSeverity.Warning => $"<color=#FFE591>{i + 1}</color>",
                CompileSeverity.Info => $"<color=#96B7FF>{i + 1}</color>",
                _ => (i + 1).ToString()
            }
        ));
    }

    private static void UpdateDiagnosticsBar(
        TextMeshProUGUI label,
        TextEngineState state,
        CompileDiagnostic[] diagnostics,
        string source
    ) {
        if(state == TextEngineState.Compiling) {
            label.text = "Checking...";
            label.color = new Color(1f, 1f, 1f, 0.42f);
            return;
        }

        if(diagnostics.Length == 0) {
            label.text = "No problems";
            label.color = new Color(0.588f, 1f, 0.569f, 0.62f);
            return;
        }

        label.text = string.Join("\n", diagnostics
            .OrderBy(d => d.Context.Index)
            .ThenByDescending(d => d.Severity)
            .Select(d => $"<color={SeverityColor(d.Severity)}>L{GetLine(source, d.Context.Index) + 1}  [{d.Severity}]  {FormatDiagnostic(d)}</color>"));
        label.color = Color.white;
    }

    private static void RebuildDiagnosticHoverTargets(
        RectTransform root,
        TMP_Text sourceText,
        string source,
        CompileDiagnostic[] diagnostics
    ) {
        if(root.childCount > 0) {
            Tooltip.Hide();
        }
        for(int i = root.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }

        if(diagnostics == null || diagnostics.Length == 0) {
            return;
        }

        sourceText.ForceMeshUpdate();
        var groups = diagnostics
            .GroupBy(d => (d.Context.Index, d.Context.Length))
            .ToArray();

        foreach(var group in groups) {
            int start = Math.Clamp(group.Key.Index, 0, source.Length);
            int end = Math.Clamp(start + Math.Max(1, group.Key.Length), start, source.Length);
            string tooltip = string.Join("\n", group
                .OrderByDescending(d => d.Severity)
                .Select(d => $"Line {GetLine(source, d.Context.Index) + 1} [{d.Severity}] {FormatDiagnostic(d)}"));
            Color underlineColor = SeverityUnityColor(group.Max(d => d.Severity));
            var characters = sourceText.textInfo.characterInfo
                .Take(sourceText.textInfo.characterCount)
                .Where(c => c.index >= start && c.index < end && c.isVisible)
                .GroupBy(c => c.lineNumber);

            foreach(var line in characters) {
                float left = line.Min(c => c.bottomLeft.x) - 2f;
                float right = line.Max(c => c.topRight.x) + 2f;
                float bottom = line.Min(c => c.descender) - 3f;
                float top = line.Max(c => c.ascender) + 2f;

                var target = new GameObject("DiagnosticHover");
                target.transform.SetParent(root, false);
                var rect = target.AddComponent<RectTransform>();
                rect.anchorMin = root.pivot;
                rect.anchorMax = root.pivot;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(left, bottom);
                rect.sizeDelta = new Vector2(right - left, top - bottom);
                var image = target.AddComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;

                var underline = new GameObject("Underline");
                underline.transform.SetParent(target.transform, false);
                var underlineRect = underline.AddComponent<RectTransform>();
                underlineRect.anchorMin = new Vector2(0f, 0f);
                underlineRect.anchorMax = new Vector2(1f, 0f);
                underlineRect.pivot = new Vector2(0.5f, 0.5f);
                underlineRect.anchoredPosition = new Vector2(0f, 3f);
                underlineRect.sizeDelta = new Vector2(0f, 2f);
                var underlineImage = underline.AddComponent<Image>();
                underlineImage.color = underlineColor;
                underlineImage.raycastTarget = false;
                target.transform.AddToolTip(tooltip);
            }
        }
    }

    private static int BuildTextGeometryKey(TMP_Text text) {
        text.ForceMeshUpdate();
        unchecked {
            int hash = 17;
            Rect rect = text.rectTransform.rect;
            hash = hash * 31 + rect.width.GetHashCode();
            hash = hash * 31 + rect.height.GetHashCode();
            hash = hash * 31 + text.textInfo.characterCount;
            for(int i = 0; i < text.textInfo.characterCount; i++) {
                var character = text.textInfo.characterInfo[i];
                if(!character.isVisible) continue;
                hash = hash * 31 + character.bottomLeft.GetHashCode();
                hash = hash * 31 + character.topRight.GetHashCode();
            }
            return hash;
        }
    }

    private static void ApplySyntaxHighlighting(
        TMP_Text text,
        string source,
        TagSyntaxSpan[] spans
    ) {
        TagSyntaxKind?[] kinds = source == null ? [] : new TagSyntaxKind?[source.Length];
        if(source != null) {
            foreach(var span in spans) {
                int start = Math.Clamp(span.Index, 0, kinds.Length);
                int end = Math.Clamp(start + span.Length, start, kinds.Length);
                for(int i = start; i < end; i++) kinds[i] = span.Kind;
            }
        }

        Color32 plain = text.color;
        var textInfo = text.textInfo;
        for(int i = 0; i < textInfo.characterCount; i++) {
            var character = textInfo.characterInfo[i];
            if(!character.isVisible) continue;

            Color32 color = character.index >= 0 && character.index < kinds.Length && kinds[character.index].HasValue
                ? SyntaxColor(kinds[character.index].Value)
                : plain;
            int material = character.materialReferenceIndex;
            int vertex = character.vertexIndex;
            var colors = textInfo.meshInfo[material].colors32;
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private static Color32 SyntaxColor(TagSyntaxKind kind) => kind switch {
        TagSyntaxKind.Delimiter => new Color32(166, 172, 205, 255),
        TagSyntaxKind.Tag => new Color32(150, 255, 145, 255),
        TagSyntaxKind.UnknownTag => new Color32(226, 103, 109, 255),
        TagSyntaxKind.Argument => new Color32(255, 213, 128, 255),
        TagSyntaxKind.Format => new Color32(130, 210, 206, 255),
        TagSyntaxKind.Separator => new Color32(137, 144, 179, 255),
        _ => new Color32(255, 255, 255, 255)
    };

    private static string SeverityColor(CompileSeverity severity) => severity switch {
        CompileSeverity.Error => "#E2676D",
        CompileSeverity.Warning => "#FFE591",
        _ => "#96B7FF"
    };

    private static Color SeverityUnityColor(CompileSeverity severity) => severity switch {
        CompileSeverity.Error => new Color(0.886f, 0.404f, 0.427f, 1f),
        CompileSeverity.Warning => new Color(1f, 0.898f, 0.569f, 1f),
        _ => new Color(0.588f, 0.718f, 1f, 1f)
    };

    private static string FormatDiagnostic(CompileDiagnostic diagnostic) {
        object[] data = diagnostic.Data ?? [];
        string Data(int index, string fallback = "?")
            => index < data.Length && data[index] != null ? data[index].ToString() : fallback;
        string ArgumentNumber() => data.Length > 0 && data[0] is int index
            ? (index + 1).ToString()
            : Data(0);

        return diagnostic.Id switch {
            DiagnosticId.TagNotFound => FormatMissingTag(diagnostic),
            DiagnosticId.ArgConvertFail => $"Argument {ArgumentNumber()} ('{Data(1)}') cannot convert to {Data(2)}",
            DiagnosticId.ArgTooFew => $"Expected at least {Data(0)} arguments; got {Data(1)}",
            DiagnosticId.ArgTooMany => $"Expected at most {Data(0)} arguments; got {Data(1)}",
            DiagnosticId.FormatFail => $"Invalid format '{Data(0)}'",
            DiagnosticId.AdvancedTagException => Data(0, "Advanced tag failed"),
            DiagnosticId.InternalError => "Internal compiler error",
            _ => diagnostic.Id.ToString()
        };
    }

    private static string FormatMissingTag(CompileDiagnostic diagnostic) {
        string name = diagnostic.Data?.Length > 0 && diagnostic.Data[0] != null
            ? diagnostic.Data[0].ToString()
            : diagnostic.Context.TagName;
        string suggestion = diagnostic.Data?.Length > 1 && diagnostic.Data[1] != null
            ? diagnostic.Data[1].ToString()
            : null;
        return suggestion == null
            ? $"Tag '{name}' not found"
            : $"Tag '{name}' not found. Did you mean '{suggestion}'?";
    }

    private static int GetLine(string source, int index) {
        source ??= string.Empty;
        int limit = Math.Clamp(index, 0, source.Length);
        int line = 0;
        for(int i = 0; i < limit; i++) {
            if(source[i] == '\n') line++;
        }
        return line;
    }

    private static string BuildDiagnosticsKey(
        TextEngineState state,
        CompileDiagnostic[] diagnostics,
        string source
    ) => $"{state}|{source?.GetHashCode() ?? 0}|{string.Join("|", diagnostics.Select(d => d.ToString()))}";

    private void Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format = "F2") {
        Slider(parent, label, defaultValue, min, max, value, changed, id, format, true);
    }

    private UISlider Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format, bool clamp) {
        var row = GenerateUI.Row(parent, 50f);
        var slider = GenerateUI.Slider(row, defaultValue, min, max, value, format, clamp, null, newValue => {
            changed(newValue);
            apply();
        }, _ => save(), label, id);
        Track(slider);
        return slider;
    }

    private void Toggle(Transform parent, string label, bool defaultValue, bool value, Action<bool> changed, string id) {
        var row = GenerateUI.Row(parent, 50f);
        var toggle = GenerateUI.Toggle(row, defaultValue, value, newValue => {
            changed(newValue);
            ApplyAndSave();
        }, label, id);
        Track(toggle);
    }

    private void EnumDropDown<T>(Transform parent, string label, T defaultValue, T value, Action<T> changed, string id, Action completed = null) where T : struct, Enum {
        var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.DropDown(row, defaultValue, value, values, option => $"{label}: {option}", newValue => {
            changed(newValue);
            if(completed == null) ApplyAndSave();
            else completed();
        }, id);
        Track(dropdown);
    }

    private void Vector2Sliders(Transform parent, string label, Vector2 defaults, float min, float max, Func<Vector2> get, Action<Vector2> set, string id, string format = "F2") {
        Slider(parent, $"{label} X", defaults.x, min, max, get().x, value => set(new Vector2(value, get().y)), id + "_x", format);
        Slider(parent, $"{label} Y", defaults.y, min, max, get().y, value => set(new Vector2(get().x, value)), id + "_y", format);
    }

    private void NumericPropertyRow(
        Transform parent,
        string label,
        (string Label, float Default, Func<float> Get, Action<float> Set, string Id)[] fields,
        string format
    ) {
        RectTransform row = CompactRow(parent, 44f, 6f);
        FixedLabel(row, label, 66f);
        foreach(var field in fields) {
            NumericField(row, field.Label, field.Default, field.Get, field.Set, field.Id, format);
        }
    }

    private (UISlider Field, Func<float> Get) NumericField(
        Transform parent,
        string label,
        float defaultValue,
        Func<float> get,
        Action<float> set,
        string id,
        string format
    ) {
        int decimals = 0;
        if(format.Length > 1 && (format[0] == 'F' || format[0] == 'f')) {
            int.TryParse(format[1..], out decimals);
        }
        var field = GenerateUI.Slider(
            parent,
            defaultValue,
            -1f,
            1f,
            get(),
            format,
            false,
            null,
            value => {
                set(value);
                apply();
            },
            _ => save(),
            label,
            id,
            showFill: false,
            dragStep: Mathf.Pow(10f, -decimals),
            blockHoverWhileDragging: true
        );
        var element = field.Rect.gameObject.AddComponent<LayoutElement>();
        element.minWidth = 100f;
        element.flexibleWidth = 1f;
        Track(field);
        return (field, get);
    }

    private static RectTransform CompactRow(Transform parent, float height, float spacing) {
        RectTransform row = GenerateUI.Row(parent, height);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return row;
    }

    private static RectTransform VerticalGroup(Transform parent, float spacing) {
        GameObject groupObject = new("VerticalGroup");
        groupObject.transform.SetParent(parent, false);
        RectTransform rect = groupObject.AddComponent<RectTransform>();
        var element = groupObject.AddComponent<LayoutElement>();
        element.minWidth = 220f;
        element.flexibleWidth = 1f;
        var layout = groupObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static TextMeshProUGUI FixedLabel(Transform parent, string text, float width) {
        TextMeshProUGUI label = GenerateUI.AddText(parent, true);
        label.text = text;
        label.fontSize = 14f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
        return label;
    }

    private Action BuildRectPositionFields(Transform parent, OvObject obj) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        bool StretchX() => !Mathf.Approximately(cfg.AnchorMin.x, cfg.AnchorMax.x);
        bool StretchY() => !Mathf.Approximately(cfg.AnchorMin.y, cfg.AnchorMax.y);
        bool DrivenX() => obj.Config.ContentSizeFitterConfig?.ComponentEnabled == true
            && obj.Config.ContentSizeFitterConfig.HorizontalFit != ContentSizeFitter.FitMode.Unconstrained;
        bool DrivenY() => obj.Config.ContentSizeFitterConfig?.ComponentEnabled == true
            && obj.Config.ContentSizeFitterConfig.VerticalFit != ContentSizeFitter.FitMode.Unconstrained;
        float PositionX() => DrivenX() ? obj.RectTransform.anchoredPosition.x : cfg.AnchoredPosition.x;
        float PositionY() => DrivenY() ? obj.RectTransform.anchoredPosition.y : cfg.AnchoredPosition.y;
        float SizeX() => DrivenX() ? obj.RectTransform.sizeDelta.x : cfg.SizeDelta.x;
        float SizeY() => DrivenY() ? obj.RectTransform.sizeDelta.y : cfg.SizeDelta.y;
        float Left() => DrivenX() ? obj.RectTransform.offsetMin.x : cfg.GetOffsetMin(0);
        float Right() => DrivenX() ? -obj.RectTransform.offsetMax.x : -cfg.GetOffsetMax(0);
        float Top() => DrivenY() ? -obj.RectTransform.offsetMax.y : -cfg.GetOffsetMax(1);
        float Bottom() => DrivenY() ? obj.RectTransform.offsetMin.y : cfg.GetOffsetMin(1);

        RectTransform fields = VerticalGroup(parent, 2f);
        var firstRow = CompactRow(fields, 44f, 6f);
        var secondRow = CompactRow(fields, 44f, 6f);
        var firstX = NumericField(firstRow, "", 0f, () => StretchX() ? Left() : PositionX(), value => {
            if(StretchX()) cfg.SetOffsetMin(0, value);
            else cfg.AnchoredPosition.x = value;
        }, "transform_rect_x1", "F1");
        var firstY = NumericField(firstRow, "", 0f, () => StretchY() ? Top() : PositionY(), value => {
            if(StretchY()) cfg.SetOffsetMax(1, -value);
            else cfg.AnchoredPosition.y = value;
        }, "transform_rect_y1", "F1");
        var secondX = NumericField(secondRow, "", 200f, () => StretchX() ? Right() : SizeX(), value => {
            if(StretchX()) cfg.SetOffsetMax(0, -value);
            else cfg.SizeDelta.x = value;
        }, "transform_rect_x2", "F1");
        var secondY = NumericField(secondRow, "", 200f, () => StretchY() ? Bottom() : SizeY(), value => {
            if(StretchY()) cfg.SetOffsetMin(1, value);
            else cfg.SizeDelta.y = value;
        }, "transform_rect_y2", "F1");

        void RefreshValues() {
            firstX.Field.Label.text = StretchX() ? "Left" : "Pos X";
            SetDisplayedValue(firstX.Field, firstX.Get());
            secondX.Field.Label.text = StretchX() ? "Right" : "Width";
            SetDisplayedValue(secondX.Field, secondX.Get());
            firstY.Field.Label.text = StretchY() ? "Top" : "Pos Y";
            SetDisplayedValue(firstY.Field, firstY.Get());
            secondY.Field.Label.text = StretchY() ? "Bottom" : "Height";
            SetDisplayedValue(secondY.Field, secondY.Get());
        }

        bool drivenX = DrivenX();
        bool drivenY = DrivenY();
        firstX.Field.SetBlocked(drivenX && StretchX(), true);
        secondX.Field.SetBlocked(drivenX, true);
        firstY.Field.SetBlocked(drivenY && StretchY(), true);
        secondY.Field.SetBlocked(drivenY, true);
        RefreshValues();

        if(drivenX || drivenY) {
            controls.Add(new UIWatcher("rect_transform_driven", fields, RefreshValues));
        }
        return RefreshValues;
    }

    private static void SetDisplayedValue(UISlider field, float value) {
        if(!Mathf.Approximately(field.Value, value)) field.Set(value, false);
    }

    private Action AnchorPresetControl(Transform parent, OvObject obj, Action positionFieldsChanged) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        RectTransform buttonRow = GenerateUI.Row(parent, 58f);
        var buttonLayout = buttonRow.GetComponent<LayoutElement>();
        buttonLayout.minWidth = 58f;
        buttonLayout.preferredWidth = 58f;
        buttonLayout.flexibleWidth = 0f;
        var summary = GenerateUI.Button(buttonRow, null, string.Empty, "transform_anchor_presets");
        summary.Label.gameObject.SetActive(false);
        summary.Rect.anchorMin = new Vector2(0f, 0.5f);
        summary.Rect.anchorMax = new Vector2(0f, 0.5f);
        summary.Rect.pivot = new Vector2(0f, 0.5f);
        summary.Rect.anchoredPosition = Vector2.zero;
        summary.Rect.sizeDelta = new Vector2(58f, 52f);
        controls.Add(summary);

        GameObject summaryGraphic = AddAnchorGraphic(summary.Rect, ModeForAxis(cfg, 0), ModeForAxis(cfg, 1), false, false, 42f);
        TextMeshProUGUI horizontalLabel = AddAnchorHeader(summary.Rect, true, ModeName(ModeForAxis(cfg, 0), false));
        TextMeshProUGUI verticalLabel = AddAnchorHeader(summary.Rect, false, ModeName(ModeForAxis(cfg, 1), true));

        RectTransform popup = CreateAnchorPopup(UICore.Canvas.transform);
        RectTransform blocker = CreatePopupBlocker(UICore.Canvas.transform);
        blocker.gameObject.SetActive(false);
        summary.OnDisposed += () => {
            if(popup != null) UnityEngine.Object.Destroy(popup.gameObject);
            if(blocker != null) UnityEngine.Object.Destroy(blocker.gameObject);
        };
        popup.gameObject.SetActive(false);
        bool open = false;
        float lastClickTime = -1f;

        void ClosePopup() {
            open = false;
            if(popup != null) popup.gameObject.SetActive(false);
            if(blocker != null) blocker.gameObject.SetActive(false);
        }

        GenerateUI.AddButton(blocker.gameObject, button => {
            if(button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) ClosePopup();
        });

        var selections = new List<(AnchorMode H, AnchorMode V, Image Image)>();
        var presetGraphics = new List<(RectTransform Parent, AnchorMode H, AnchorMode V, bool Header, GameObject Graphic)>();
        Transform table = popup.Find("Table");
        TextMeshProUGUI modifierHelp = popup.Find("ModifierHelp").GetComponent<TextMeshProUGUI>();
        AnchorMode[] horizontalModes = { AnchorMode.Custom, AnchorMode.Min, AnchorMode.Middle, AnchorMode.Max, AnchorMode.Stretch };
        AnchorMode[] verticalModes = { AnchorMode.Custom, AnchorMode.Max, AnchorMode.Middle, AnchorMode.Min, AnchorMode.Stretch };
        bool lastShift = false;
        bool lastAlt = false;

        void RefreshSummary() {
            UnityEngine.Object.Destroy(summaryGraphic);
            summaryGraphic = AddAnchorGraphic(summary.Rect, ModeForAxis(cfg, 0), ModeForAxis(cfg, 1), false, false, 42f);
            horizontalLabel.text = ModeName(ModeForAxis(cfg, 0), false);
            verticalLabel.text = ModeName(ModeForAxis(cfg, 1), true);
            foreach(var cell in selections) {
                bool selected = (cell.H == AnchorMode.Custom || cell.H == ModeForAxis(cfg, 0))
                    && (cell.V == AnchorMode.Custom || cell.V == ModeForAxis(cfg, 1));
                Color color = Color.white;
                color.a = selected ? (cell.H == AnchorMode.Custom || cell.V == AnchorMode.Custom ? 0.55f : 1f) : 0f;
                cell.Image.color = color;
            }
        }

        void RefreshModifierGraphics(bool force = false) {
            bool shift = OVC_Input.GetKey(KeyCode.LeftShift) || OVC_Input.GetKey(KeyCode.RightShift);
            bool alt = OVC_Input.GetKey(KeyCode.LeftAlt) || OVC_Input.GetKey(KeyCode.RightAlt);
            if(!force && shift == lastShift && alt == lastAlt) return;
            lastShift = shift;
            lastAlt = alt;

            for(int i = 0; i < presetGraphics.Count; i++) {
                var item = presetGraphics[i];
                UnityEngine.Object.Destroy(item.Graphic);
                GameObject graphic = AddAnchorGraphic(item.Parent, item.H, item.V, shift, alt, item.Header ? 34f : 40f);
                PositionAnchorGraphic(graphic, item.H, item.V);
                presetGraphics[i] = (item.Parent, item.H, item.V, item.Header, graphic);
            }

            modifierHelp.text = $"{(shift ? "<color=#FFCC44>" : "")}Shift: Also set pivot{(shift ? "</color>" : "")}     {(alt ? "<color=#FFCC44>" : "")}Alt: Also set position{(alt ? "</color>" : "")}";
        }

        for(int y = 0; y < verticalModes.Length; y++) {
            for(int x = 0; x < horizontalModes.Length; x++) {
                AnchorMode horizontal = horizontalModes[x];
                AnchorMode vertical = verticalModes[y];
                if(x == 0 && y == 0) {
                    CreateAnchorTableBlank(table);
                    continue;
                }

                var cell = GenerateUI.Button(table, null, string.Empty, $"transform_anchor_{x}_{y}");
                cell.Label.gameObject.SetActive(false);
                Track(cell);
                bool header = x == 0 || y == 0;
                cell.NormalColor = header
                    ? new Color(0.13f, 0.13f, 0.17f, 1f)
                    : new Color(0.09f, 0.09f, 0.12f, 1f);
                cell.Background.color = cell.NormalColor;
                Image selection = AddSelection(cell.Rect);
                selections.Add((horizontal, vertical, selection));
                GameObject graphic = AddAnchorGraphic(cell.Rect, horizontal, vertical, false, false, header ? 34f : 40f);
                PositionAnchorGraphic(graphic, horizontal, vertical);
                presetGraphics.Add((cell.Rect, horizontal, vertical, header, graphic));
                if(header) AddTableHeader(cell.Rect, x == 0, x == 0 ? ModeName(vertical, true) : ModeName(horizontal, false));
                cell.Rect.AddToolTip(AnchorCellName(horizontal, vertical));
                cell.OnClick = () => {
                    bool setPivot = OVC_Input.GetKey(KeyCode.LeftShift) || OVC_Input.GetKey(KeyCode.RightShift);
                    bool setPosition = OVC_Input.GetKey(KeyCode.LeftAlt) || OVC_Input.GetKey(KeyCode.RightAlt);
                    ApplyAnchorModes(obj, horizontal, vertical, setPivot, setPosition);
                    apply();
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(obj.RectTransform);
                    positionFieldsChanged();
                    save();
                    RefreshSummary();

                    float now = Time.unscaledTime;
                    if(now - lastClickTime < 0.35f) {
                        ClosePopup();
                    }
                    lastClickTime = now;
                };
            }
        }

        summary.OnClick = () => {
            open = !open;
            if(open) {
                RectTransform canvasRect = UICore.Canvas.GetComponent<RectTransform>();
                Vector3 corner = summary.Rect.TransformPoint(new Vector3(summary.Rect.rect.xMin, summary.Rect.rect.yMin, 0f));
                popup.anchoredPosition = canvasRect.InverseTransformPoint(corner);
                blocker.gameObject.SetActive(true);
                blocker.SetAsLastSibling();
                popup.SetAsLastSibling();
            } else {
                blocker.gameObject.SetActive(false);
            }
            popup.gameObject.SetActive(open);
            RefreshSummary();
            if(open) RefreshModifierGraphics(true);
        };
        summary.Rect.GetComponent<OventHandler>().OnDisabled = () => {
            ClosePopup();
        };
        popup.gameObject.GetComponent<OventHandler>().OnHoverUpdate = () => RefreshModifierGraphics();
        RefreshSummary();
        return RefreshSummary;
    }

    private static RectTransform CreatePopupBlocker(Transform parent) {
        GameObject blockerObject = new("AnchorPopupBlocker");
        blockerObject.transform.SetParent(parent, false);
        RectTransform blocker = blockerObject.AddComponent<RectTransform>();
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.offsetMin = Vector2.zero;
        blocker.offsetMax = Vector2.zero;
        Image image = blockerObject.AddComponent<Image>();
        image.color = Color.clear;
        return blocker;
    }

    private static RectTransform CreateAnchorPopup(Transform parent) {
        GameObject popupObject = new("AnchorPresetPopup");
        popupObject.transform.SetParent(parent, false);
        RectTransform popup = popupObject.AddComponent<RectTransform>();
        popup.anchorMin = new Vector2(0.5f, 0.5f);
        popup.anchorMax = new Vector2(0.5f, 0.5f);
        popup.pivot = new Vector2(0f, 1f);
        popup.anchoredPosition = Vector2.zero;
        popup.sizeDelta = new Vector2(320f, 352f);
        popupObject.AddComponent<OventHandler>();
        Image background = popup.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.98f);

        var vertical = popup.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(10, 10, 8, 8);
        vertical.spacing = 3f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        TextMeshProUGUI title = GenerateUI.AddText(popup, true);
        title.text = "Anchor Presets";
        title.fontSize = 18f;
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 23f;

        TextMeshProUGUI help = GenerateUI.AddText(popup, true);
        help.name = "ModifierHelp";
        help.text = "Shift: Also set pivot     Alt: Also set position";
        help.fontSize = 12f;
        help.color = new Color(1f, 1f, 1f, 0.55f);
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

        GameObject separator = new("Separator");
        separator.transform.SetParent(popup, false);
        separator.AddComponent<RectTransform>();
        separator.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
        separator.AddComponent<LayoutElement>().preferredHeight = 1f;

        GameObject table = new("Table");
        table.transform.SetParent(popup, false);
        table.AddComponent<RectTransform>();
        var grid = table.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(50f, 50f);
        grid.spacing = new Vector2(5f, 5f);
        grid.childAlignment = TextAnchor.UpperCenter;
        table.AddComponent<LayoutElement>().preferredHeight = 270f;

        return popup;
    }

    private static void CreateAnchorTableBlank(Transform parent) {
        GameObject blank = new("CurrentModeCorner");
        blank.transform.SetParent(parent, false);
        blank.AddComponent<RectTransform>();
        blank.AddComponent<LayoutElement>();
    }

    private static Image AddSelection(RectTransform parent) {
        GameObject selectionObject = new("Selection");
        selectionObject.transform.SetParent(parent, false);
        RectTransform rect = selectionObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);
        Image image = selectionObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256O64P2048);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    private static void PositionAnchorGraphic(GameObject graphic, AnchorMode horizontal, AnchorMode vertical) {
        RectTransform rect = graphic.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(
            horizontal == AnchorMode.Custom ? 7f : 0f,
            vertical == AnchorMode.Custom ? -6f : 0f
        );
    }

    private static GameObject AddAnchorGraphic(RectTransform parent, AnchorMode horizontal, AnchorMode vertical, bool showPivot, bool alignPosition, float size) {
        GameObject root = new("AnchorGraphic");
        root.transform.SetParent(parent, false);
        RectTransform frame = root.AddComponent<RectTransform>();
        frame.anchorMin = new Vector2(0.5f, 0.5f);
        frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(size, size);

        Color parentColor = new(1f, 1f, 1f, 0.7f);
        float edge = size * 0.5f;
        AddGraphicLine(frame, "Top", new Vector2(0f, edge), new Vector2(size, 1f), parentColor);
        AddGraphicLine(frame, "Bottom", new Vector2(0f, -edge), new Vector2(size, 1f), parentColor);
        AddGraphicLine(frame, "Left", new Vector2(-edge, 0f), new Vector2(1f, size), parentColor);
        AddGraphicLine(frame, "Right", new Vector2(edge, 0f), new Vector2(1f, size), parentColor);

        if(horizontal == AnchorMode.Custom && vertical == AnchorMode.Custom) return root;

        float innerSize = size * 0.5f;
        Vector2 objectSize = new(horizontal == AnchorMode.Stretch ? size - 4f : innerSize, vertical == AnchorMode.Stretch ? size - 4f : innerSize);
        Vector2 objectPosition = new(ModePosition(horizontal, size), ModePosition(vertical, size));
        if(!alignPosition) objectPosition = Vector2.zero;

        GameObject objectGraphic = new("Self");
        objectGraphic.transform.SetParent(frame, false);
        RectTransform objectRect = objectGraphic.AddComponent<RectTransform>();
        objectRect.anchorMin = new Vector2(0.5f, 0.5f);
        objectRect.anchorMax = new Vector2(0.5f, 0.5f);
        objectRect.anchoredPosition = objectPosition;
        objectRect.sizeDelta = objectSize;
        Color selfColor = new(0.9f, 0.9f, 0.9f, 0.95f);
        AddGraphicOutline(objectRect, selfColor);

        Color simpleColor = new(1f, 0.22f, 0.22f, 1f);
        Color stretchColor = new(0.1f, 0.85f, 1f, 1f);
        if(horizontal != AnchorMode.Custom) {
            float x = ModePosition(horizontal, size, true);
            if(horizontal == AnchorMode.Stretch) AddStretchArrow(frame, true, stretchColor, size);
            else AddGraphicLine(frame, "HorizontalAnchor", new Vector2(x, 0f), new Vector2(1f, size - 2f), simpleColor);
        }
        if(vertical != AnchorMode.Custom) {
            float y = ModePosition(vertical, size, true);
            if(vertical == AnchorMode.Stretch) AddStretchArrow(frame, false, stretchColor, size);
            else AddGraphicLine(frame, "VerticalAnchor", new Vector2(0f, y), new Vector2(size - 2f, 1f), simpleColor);
        }

        Color cornerColor = new(1f, 0.72f, 0.05f, 1f);
        if(horizontal != AnchorMode.Custom && vertical != AnchorMode.Custom) {
            foreach(float x in AnchorPositions(horizontal, size)) {
                foreach(float y in AnchorPositions(vertical, size)) {
                    AddGraphicLine(frame, "AnchorCorner", new Vector2(x, y), new Vector2(3f, 3f), cornerColor);
                }
            }
        }

        if(showPivot && horizontal != AnchorMode.Custom && vertical != AnchorMode.Custom) {
            Vector2 pivotPosition = objectPosition + new Vector2(
                PivotOffset(horizontal, objectSize.x),
                PivotOffset(vertical, objectSize.y)
            );
            AddGraphicLine(frame, "Pivot", pivotPosition, new Vector2(5f, 2f), stretchColor);
            AddGraphicLine(frame, "Pivot", pivotPosition, new Vector2(2f, 5f), stretchColor);
        }
        return root;
    }

    private static TextMeshProUGUI AddAnchorHeader(RectTransform parent, bool horizontal, string value) {
        TextMeshProUGUI label = GenerateUI.AddText(parent, true);
        label.text = value;
        label.fontSize = 11f;
        label.color = new Color(1f, 1f, 1f, 0.55f);
        label.alignment = horizontal ? TextAlignmentOptions.Bottom : TextAlignmentOptions.MidlineLeft;
        if(horizontal) label.rectTransform.offsetMin = new Vector2(0f, 2f);
        else label.rectTransform.offsetMin = new Vector2(6f, 0f);
        label.raycastTarget = false;
        return label;
    }

    private static void AddTableHeader(RectTransform parent, bool vertical, string value) {
        TextMeshProUGUI label = GenerateUI.AddText(parent, true);
        label.text = vertical ? value[0].ToString().ToUpperInvariant() : value;
        label.fontSize = 10f;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.alignment = vertical ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Top;
        label.rectTransform.offsetMin = new Vector2(3f, 3f);
        label.rectTransform.offsetMax = new Vector2(-3f, -3f);
        label.raycastTarget = false;
    }

    private static void AddGraphicOutline(RectTransform frame, Color color) {
        Vector2 size = frame.sizeDelta;
        AddGraphicLine(frame, "Top", new Vector2(0f, size.y * 0.5f), new Vector2(size.x, 1f), color);
        AddGraphicLine(frame, "Bottom", new Vector2(0f, size.y * -0.5f), new Vector2(size.x, 1f), color);
        AddGraphicLine(frame, "Left", new Vector2(size.x * -0.5f, 0f), new Vector2(1f, size.y), color);
        AddGraphicLine(frame, "Right", new Vector2(size.x * 0.5f, 0f), new Vector2(1f, size.y), color);
    }

    private static void AddStretchArrow(RectTransform parent, bool horizontal, Color color, float size) {
        Vector2 lineSize = horizontal ? new Vector2(size * 0.45f, 1f) : new Vector2(1f, size * 0.45f);
        AddGraphicLine(parent, "Stretch", Vector2.zero, lineSize, color);
        float end = size * 0.225f;
        if(horizontal) {
            AddGraphicLine(parent, "Arrow", new Vector2(-end, 0f), new Vector2(2f, 5f), color);
            AddGraphicLine(parent, "Arrow", new Vector2(end, 0f), new Vector2(2f, 5f), color);
        } else {
            AddGraphicLine(parent, "Arrow", new Vector2(0f, -end), new Vector2(5f, 2f), color);
            AddGraphicLine(parent, "Arrow", new Vector2(0f, end), new Vector2(5f, 2f), color);
        }
    }

    private static IEnumerable<float> AnchorPositions(AnchorMode mode, float size) {
        if(mode == AnchorMode.Stretch) return new[] { size * -0.5f, size * 0.5f };
        return new[] { ModePosition(mode, size, true) };
    }

    private static float ModePosition(AnchorMode mode, float size, bool edge = false) {
        float range = edge ? size * 0.5f : size * 0.25f;
        return mode switch { AnchorMode.Min => -range, AnchorMode.Max => range, _ => 0f };
    }

    private static float PivotOffset(AnchorMode mode, float size) {
        return mode switch { AnchorMode.Min => size * -0.5f, AnchorMode.Max => size * 0.5f, _ => 0f };
    }

    private static AnchorMode ModeForAxis(RectTransformSettings cfg, int axis) {
        float min = cfg.AnchorMin[axis];
        float max = cfg.AnchorMax[axis];
        if(Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f)) return AnchorMode.Min;
        if(Mathf.Approximately(min, 0.5f) && Mathf.Approximately(max, 0.5f)) return AnchorMode.Middle;
        if(Mathf.Approximately(min, 1f) && Mathf.Approximately(max, 1f)) return AnchorMode.Max;
        if(Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 1f)) return AnchorMode.Stretch;
        return AnchorMode.Custom;
    }

    private static string ModeName(AnchorMode mode, bool vertical) => mode switch {
        AnchorMode.Min => vertical ? "bottom" : "left",
        AnchorMode.Middle => vertical ? "middle" : "center",
        AnchorMode.Max => vertical ? "top" : "right",
        AnchorMode.Stretch => "stretch",
        _ => "custom"
    };

    private static string AnchorCellName(AnchorMode horizontal, AnchorMode vertical) {
        if(horizontal == AnchorMode.Custom) return $"Vertical: {ModeName(vertical, true)}";
        if(vertical == AnchorMode.Custom) return $"Horizontal: {ModeName(horizontal, false)}";
        return $"{ModeName(horizontal, false)} / {ModeName(vertical, true)}";
    }

    private static void ApplyAnchorModes(OvObject obj, AnchorMode horizontal, AnchorMode vertical, bool setPivot, bool setPosition) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        Vector2 parentSize = (obj.RectTransform.parent as RectTransform)?.rect.size ?? new Vector2(1920f, 1080f);
        Vector2 visibleSize = obj.RectTransform.rect.size;
        ApplyAnchorModeForAxis(cfg, 0, horizontal, parentSize.x, visibleSize.x, setPivot, setPosition);
        ApplyAnchorModeForAxis(cfg, 1, vertical, parentSize.y, visibleSize.y, setPivot, setPosition);
    }

    private static void ApplyAnchorModeForAxis(RectTransformSettings cfg, int axis, AnchorMode mode, float parentSize, float visibleSize, bool setPivot, bool setPosition) {
        if(mode == AnchorMode.Custom) return;

        float oldMin = cfg.AnchorMin[axis];
        float oldMax = cfg.AnchorMax[axis];
        float oldPivot = cfg.Pivot[axis];
        float newMin = mode == AnchorMode.Stretch ? 0f : mode switch { AnchorMode.Min => 0f, AnchorMode.Middle => 0.5f, _ => 1f };
        float newMax = mode == AnchorMode.Stretch ? 1f : newMin;
        float oldReference = Mathf.Lerp(oldMin, oldMax, oldPivot);
        float newReference = Mathf.Lerp(newMin, newMax, oldPivot);

        cfg.AnchoredPosition[axis] += (oldReference - newReference) * parentSize;
        cfg.SizeDelta[axis] += ((oldMax - oldMin) - (newMax - newMin)) * parentSize;
        cfg.AnchorMin[axis] = newMin;
        cfg.AnchorMax[axis] = newMax;

        if(setPivot) {
            float newPivot = mode switch { AnchorMode.Min => 0f, AnchorMode.Max => 1f, _ => 0.5f };
            float rectSize = parentSize * (newMax - newMin) + cfg.SizeDelta[axis];
            cfg.AnchoredPosition[axis] += (newPivot - oldPivot) * rectSize;
            cfg.Pivot[axis] = newPivot;
        }

        if(setPosition) {
            cfg.AnchoredPosition[axis] = 0f;
            cfg.SizeDelta[axis] = mode == AnchorMode.Stretch ? 0f : visibleSize;
        }
    }

    private static void AddGraphicLine(RectTransform parent, string name, Vector2 position, Vector2 size, Color color) {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(parent, false);
        RectTransform rect = lineObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = lineObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void SpriteDropDown(Transform parent, ImageSettings cfg) {
        const string none = "None";
        var options = UserResourceManager.Spr.Keys.OrderBy(key => key).ToList();
        if(!string.IsNullOrEmpty(cfg.SpriteKey) && !options.Contains(cfg.SpriteKey)) {
            options.Insert(0, cfg.SpriteKey);
        }
        options.Insert(0, none);

        string current = string.IsNullOrEmpty(cfg.SpriteKey) ? none : cfg.SpriteKey;
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.DropDown(row, none, current, options, option => $"Sprite: {option}", selected => {
            cfg.SpriteKey = selected == none ? null : selected;
            ApplyAndSave();
        }, "image_sprite");
        Track(dropdown);
    }

    private void ColorSliders(Transform parent, string label, Color defaults, Func<Color> get, Action<Color> set, string id) {
        RectTransform row = GenerateUI.Row(parent, 50f);
        UIColorPicker picker = GenerateUI.ColorPicker(row, defaults, get(), value => {
            set(value);
            apply();
        }, _ => save(), id);
        Track(picker);
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

    private void RefreshDrivenLayout(OvObject obj) {
        obj.ApplyConfig();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(obj.RectTransform);
        Canvas.ForceUpdateCanvases();
        save();
        rebuild();
    }

    private void RefreshComponents(OvObject obj) {
        obj.ApplyComponent();
        obj.ApplyConfig();
        save();
        rebuild();
    }
}
