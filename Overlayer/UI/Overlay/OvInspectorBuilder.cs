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
using Overlayer.Tween;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
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
    private string componentKey;

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

        BuildCanvasRectTransform(canvas);
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
        if(obj.Config.MovingManConfig != null) {
            BuildMovingMan(obj, obj.Config.MovingManConfig);
        }
        if(obj.Config.ColorRangeConfig != null) {
            BuildColorRange(obj, obj.Config.ColorRangeConfig);
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
            componentKey = "RECT_MASK_2D";
            var (_, rectMask) = GenerateUI.ComponentCard(content, InspectorLabel("Rect Mask 2D"), obj.Config.RectMask2DEnabled, value => {
                obj.Config.RectMask2DEnabled = value;
                ApplyAndSave();
            }, () => {
                obj.Config.HasRectMask2D = false;
                obj.Config.RectMask2DEnabled = true;
                RefreshComponents(obj);
            });
            Label(rectMask, InspectorText(
                "COMPONANT_RECT_MASK_2D_DESCRIPTION",
                InspectorText("INSPECTOR_RECT_MASK_DESCRIPTION", "Clips child graphics to this object's rectangle.")));
        }
#if !IL2CPP
        if(obj.Config.BoxCollider2DConfig != null) {
            BuildBoxCollider2D(obj, obj.Config.BoxCollider2DConfig);
        }
        if(obj.Config.Rigidbody2DConfig != null) {
            BuildRigidbody2D(obj, obj.Config.Rigidbody2DConfig);
        }
#endif

        BuildAddComponent(obj);
    }

    private void BuildTransform(OvObject obj) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        var (_, basic) = Card("Rect Transform", false);
        Action refreshPositionFields = null;
        Action refreshPivotFields = null;
        RectTransform rectLayout = CompactRow(basic, 92f, 6f);
        Action refreshAnchor = AnchorPresetControl(rectLayout, obj, () => {
            refreshPositionFields?.Invoke();
            refreshPivotFields?.Invoke();
        });
        refreshPositionFields = BuildRectPositionFields(rectLayout, obj);

        NumericPropertyRow(basic, "Position", [
            ("Z", 0f, () => cfg.AnchoredPositionZ, value => cfg.AnchoredPositionZ = value, "rect_position_z")
        ], "F1");
        NumericPropertyRow(basic, "Rotation", [
            ("X", 0f, () => cfg.RotationXY.x, value => cfg.RotationXY.x = value, "rect_rotation_x"),
            ("Y", 0f, () => cfg.RotationXY.y, value => cfg.RotationXY.y = value, "rect_rotation_y"),
            ("Z", 0f, () => cfg.Rotation, value => cfg.Rotation = value, "rect_rotation_z")
        ], "F1");
        NumericPropertyRow(basic, "Scale", [
            ("X", 1f, () => cfg.Scale.x, value => cfg.Scale.x = value, "rect_scale_x"),
            ("Y", 1f, () => cfg.Scale.y, value => cfg.Scale.y = value, "rect_scale_y"),
            ("Z", 1f, () => cfg.Scale.z, value => cfg.Scale.z = value, "rect_scale_z")
        ], "F2");
        refreshPivotFields = NumericPropertyRow(basic, "Pivot", [
            ("X", 0.5f, () => cfg.Pivot.x, value => cfg.Pivot.x = value, "rect_pivot_x"),
            ("Y", 0.5f, () => cfg.Pivot.y, value => cfg.Pivot.y = value, "rect_pivot_y")
        ], "F2");

        var (_, anchors) = Card("Anchors", false);
        NumericPropertyRow(anchors, "Min", [
            ("X", 0f, () => cfg.AnchorMin.x, value => { cfg.AnchorMin.x = value; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_min_x"),
            ("Y", 0f, () => cfg.AnchorMin.y, value => { cfg.AnchorMin.y = value; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_min_y")
        ], "F2");
        NumericPropertyRow(anchors, "Max", [
            ("X", 1f, () => cfg.AnchorMax.x, value => { cfg.AnchorMax.x = value; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_max_x"),
            ("Y", 1f, () => cfg.AnchorMax.y, value => { cfg.AnchorMax.y = value; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_max_y")
        ], "F2");
    }

    private void BuildCanvasRectTransform(OvCanvas canvas) {
        RectTransformSettings cfg = canvas.Config.RectTransformConfig;
        var (_, basic) = Card("Rect Transform", false);
        Action refreshPositionFields = null;
        Action refreshPivotFields = null;
        RectTransform rectLayout = CompactRow(basic, 92f, 6f);
        Action refreshAnchor = AnchorPresetControl(rectLayout, cfg, canvas.RectTransform, () => {
            refreshPositionFields?.Invoke();
            refreshPivotFields?.Invoke();
        });
        refreshPositionFields = BuildRectPositionFields(rectLayout, cfg);

        NumericPropertyRow(basic, "Position", [
            ("Z", 0f, () => cfg.AnchoredPositionZ, value => cfg.AnchoredPositionZ = value, "canvas_rect_position_z")
        ], "F1");
        NumericPropertyRow(basic, "Rotation", [
            ("X", 0f, () => cfg.RotationXY.x, value => cfg.RotationXY.x = value, "canvas_rect_rotation_x"),
            ("Y", 0f, () => cfg.RotationXY.y, value => cfg.RotationXY.y = value, "canvas_rect_rotation_y"),
            ("Z", 0f, () => cfg.Rotation, value => cfg.Rotation = value, "canvas_rect_rotation_z")
        ], "F1");
        NumericPropertyRow(basic, "Scale", [
            ("X", 1f, () => cfg.Scale.x, value => cfg.Scale.x = value, "canvas_rect_scale_x"),
            ("Y", 1f, () => cfg.Scale.y, value => cfg.Scale.y = value, "canvas_rect_scale_y"),
            ("Z", 1f, () => cfg.Scale.z, value => cfg.Scale.z = value, "canvas_rect_scale_z")
        ], "F2");
        refreshPivotFields = NumericPropertyRow(basic, "Pivot", [
            ("X", 0.5f, () => cfg.Pivot.x, value => cfg.Pivot.x = value, "canvas_rect_pivot_x"),
            ("Y", 0.5f, () => cfg.Pivot.y, value => cfg.Pivot.y = value, "canvas_rect_pivot_y")
        ], "F2");

        var (_, anchors) = Card("Anchors", false);
        NumericPropertyRow(anchors, "Min", [
            ("X", 0f, () => cfg.AnchorMin.x, value => { cfg.AnchorMin.x = value; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_min_x"),
            ("Y", 0f, () => cfg.AnchorMin.y, value => { cfg.AnchorMin.y = value; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_min_y")
        ], "F2");
        NumericPropertyRow(anchors, "Max", [
            ("X", 1f, () => cfg.AnchorMax.x, value => { cfg.AnchorMax.x = value; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_max_x"),
            ("Y", 1f, () => cfg.AnchorMax.y, value => { cfg.AnchorMax.y = value; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_max_y")
        ], "F2");
    }

    private void BuildText(OvObject obj, TextMeshProUGUISettings cfg) {
        OvTextSettings textCfg = obj.Config.TextEngineConfig ??= OvTextSettings.FromLegacy(cfg.Text);
        var (_, card) = ComponentCard("Text", cfg, () => {
            obj.Config.TextConfig = null;
            obj.Config.TextEngineConfig = null;
            obj.Config.ColorRangeConfig = null;
            RefreshComponents(obj);
        });
        CodeEditor(card, "Playing Text", "text_playing", textCfg.PlayingText, value => {
            textCfg.PlayingText = value;
            cfg.Text = value;
            apply();
        }, () => obj.TextUpdater?.PlayingEngine);
        CodeEditor(card, "Not Playing Text", "text_not_playing", textCfg.NotPlayingText, value => {
            textCfg.NotPlayingText = value;
            apply();
        }, () => obj.TextUpdater?.NotPlayingEngine);
        FontDropDown(card, cfg);
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
        GameObject textGradientControls = null;
        Toggle(card, "Text Gradient", false, !cfg.Color.SolidColor, value => {
            GradientColor color = cfg.Color;
            color.SolidColor = !value;
            cfg.Color = color;
            textGradientControls?.SetActive(value);
        }, "text_gradient");
        textGradientControls = GradientColorSliders(card, () => cfg.Color, value => cfg.Color = value, "text_gradient", Color.white);
        textGradientControls.SetActive(!cfg.Color.SolidColor);
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
        RectTransform preserveAspectRow = null;
        RectTransform useSpriteMeshRow = null;
        RectTransform fillCenterRow = null;
        RectTransform pixelsPerUnitRow = null;
        RectTransform fillMethodRow = null;
        RectTransform fillAmountRow = null;
        RectTransform fillOriginRow = null;
        RectTransform fillClockwiseRow = null;
        EnumDropDown(card, "Image Type", Image.Type.Simple, cfg.Type, value => {
            cfg.Type = value;
            RefreshImageTypeOptions(value);
        }, "image_type");
        preserveAspectRow = Toggle(card, "Preserve Aspect", false, cfg.PreserveAspect, value => cfg.PreserveAspect = value, "image_aspect");
        useSpriteMeshRow = Toggle(card, "Use Sprite Mesh", false, cfg.UseSpriteMesh, value => cfg.UseSpriteMesh = value, "image_sprite_mesh");
        fillCenterRow = Toggle(card, "Fill Center", true, cfg.FillCenter, value => cfg.FillCenter = value, "image_fill_center");
        pixelsPerUnitRow = Slider(
            card,
            "Pixels Per Unit",
            1f,
            0f,
            10f,
            cfg.PixelsPerUnitMultiplier,
            value => cfg.PixelsPerUnitMultiplier = value,
            "image_pixels_per_unit",
            "F2",
            false,
            value => Mathf.Max(0f, value)
        );
        fillMethodRow = EnumDropDown(card, "Fill Method", Image.FillMethod.Horizontal, cfg.FillMethod, value => {
            cfg.FillMethod = value;
            cfg.FillOrigin = 0;
        }, "image_fill_method", () => {
            ApplyAndSave();
            rebuild();
        });
        fillAmountRow = Slider(card, "Fill Amount", 1f, 0f, 1f, cfg.FillAmount, value => cfg.FillAmount = value, "image_fill_amount");
        cfg.FillOrigin = Mathf.Clamp(cfg.FillOrigin, 0, cfg.FillMethod is Image.FillMethod.Horizontal or Image.FillMethod.Vertical ? 1 : 3);
        fillOriginRow = CreateFillOriginRow();
        fillClockwiseRow = Toggle(card, "Fill Clockwise", true, cfg.FillClockwise, value => cfg.FillClockwise = value, "image_fill_clockwise");

        RectTransform CreateFillOriginRow() => cfg.FillMethod switch {
            Image.FillMethod.Horizontal => EnumDropDown(
                card, "Fill Origin", Image.OriginHorizontal.Left, (Image.OriginHorizontal)cfg.FillOrigin,
                value => cfg.FillOrigin = (int)value, "image_fill_origin"
            ),
            Image.FillMethod.Vertical => EnumDropDown(
                card, "Fill Origin", Image.OriginVertical.Bottom, (Image.OriginVertical)cfg.FillOrigin,
                value => cfg.FillOrigin = (int)value, "image_fill_origin"
            ),
            Image.FillMethod.Radial90 => EnumDropDown(
                card, "Fill Origin", Image.Origin90.BottomLeft, (Image.Origin90)cfg.FillOrigin,
                value => cfg.FillOrigin = (int)value, "image_fill_origin"
            ),
            Image.FillMethod.Radial180 => EnumDropDown(
                card, "Fill Origin", Image.Origin180.Bottom, (Image.Origin180)cfg.FillOrigin,
                value => cfg.FillOrigin = (int)value, "image_fill_origin"
            ),
            _ => EnumDropDown(
                card, "Fill Origin", Image.Origin360.Bottom, (Image.Origin360)cfg.FillOrigin,
                value => cfg.FillOrigin = (int)value, "image_fill_origin"
            )
        };

        void RefreshImageTypeOptions(Image.Type type) {
            bool simple = type == Image.Type.Simple;
            bool slicedOrTiled = type is Image.Type.Sliced or Image.Type.Tiled;
            bool filled = type == Image.Type.Filled;

            preserveAspectRow.gameObject.SetActive(simple || filled);
            useSpriteMeshRow.gameObject.SetActive(simple);
            fillCenterRow.gameObject.SetActive(slicedOrTiled);
            pixelsPerUnitRow.gameObject.SetActive(slicedOrTiled);
            fillMethodRow.gameObject.SetActive(filled);
            fillAmountRow.gameObject.SetActive(filled);
            fillOriginRow.gameObject.SetActive(filled);
            fillClockwiseRow.gameObject.SetActive(filled && cfg.FillMethod is not Image.FillMethod.Horizontal and not Image.FillMethod.Vertical);
        }

        RefreshImageTypeOptions(cfg.Type);
    }

    private void BuildMovingMan(OvObject obj, MovingManSettings cfg) {
        var (_, card) = ComponentCard("Moving Man", cfg, () => {
            obj.Config.MovingManConfig = null;
            RefreshComponents(obj);
        });

        Input(card, "Target Tag", null, cfg.TagName, value => cfg.TagName = value, "moving_man_tag");
        MovingManTargets(card, cfg);
        Slider(card, "Start Value", 30f, -10000f, 10000f, (float)cfg.StartSize, value => cfg.StartSize = value, "moving_man_start", "F1", false);
        Slider(card, "End Value", 80f, -10000f, 10000f, (float)cfg.EndSize, value => cfg.EndSize = value, "moving_man_end", "F1", false);
        Slider(card, "Default Value", 30f, -10000f, 10000f, (float)cfg.DefaultSize, value => cfg.DefaultSize = value, "moving_man_default", "F1", false);
        Slider(card, "Speed", 800f, 0f, 10000f, (float)cfg.Speed, value => cfg.Speed = value, "moving_man_speed", "F0");
        Toggle(card, "Invert", false, cfg.Invert, value => cfg.Invert = value, "moving_man_invert");
        EnumDropDown(card, "Ease", Easing.OutExpo, cfg.Ease, value => cfg.Ease = value, "moving_man_ease");
    }

    private void MovingManTargets(Transform parent, MovingManSettings cfg) {
        string label = InspectorLabel("Target");
        var values = Enum.GetValues(typeof(MovingManTarget))
            .Cast<MovingManTarget>()
            .Where(value => value != MovingManTarget.None)
            .ToArray();
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.MultiDropDown(
            row,
            MovingManTarget.TextSize,
            cfg.Target,
            values,
            value => $"{label}: {value}",
            value => $"{label}: {MovingManTargetSummary(value, values)}",
            newValue => {
                cfg.Target = newValue;
                ApplyAndSave();
            },
            "moving_man_target"
        );
        Track(dropdown);
    }

    private static string MovingManTargetSummary(MovingManTarget value, IReadOnlyList<MovingManTarget> values) {
        if(value == MovingManTarget.None) {
            return "None";
        }

        var selected = values
            .Where(option => value.HasFlag(option))
            .Select(option => option.ToString())
            .ToArray();
        return selected.Length <= 2
            ? string.Join(", ", selected)
            : $"{selected.Length} selected";
    }

    private void BuildColorRange(OvObject obj, ColorRangeSettings cfg) {
        var (_, card) = ComponentCard("Color Range", cfg, () => {
            obj.Config.ColorRangeConfig = null;
            RefreshComponents(obj);
        });

        Input(card, "Target Tag", null, cfg.TagName, value => cfg.TagName = value, "color_range_tag");
        Slider(card, "Minimum", 0f, -10000f, 10000f, (float)cfg.Minimum, value => cfg.Minimum = value, "color_range_min", "F2", false);
        Slider(card, "Maximum", 100f, -10000f, 10000f, (float)cfg.Maximum, value => cfg.Maximum = value, "color_range_max", "F2", false);
        GameObject minimumGradientControls = null;
        Toggle(card, "Minimum Gradient", false, !cfg.MinimumColor.SolidColor, value => {
            GradientColor color = cfg.MinimumColor;
            color.SolidColor = !value;
            cfg.MinimumColor = color;
            minimumGradientControls?.SetActive(value);
        }, "color_range_min_gradient");
        minimumGradientControls = GradientColorSliders(card, () => cfg.MinimumColor, value => cfg.MinimumColor = value, "color_range_min_gradient", Color.black);
        minimumGradientControls.SetActive(!cfg.MinimumColor.SolidColor);
        GameObject maximumGradientControls = null;
        Toggle(card, "Maximum Gradient", false, !cfg.MaximumColor.SolidColor, value => {
            GradientColor color = cfg.MaximumColor;
            color.SolidColor = !value;
            cfg.MaximumColor = color;
            maximumGradientControls?.SetActive(value);
        }, "color_range_max_gradient");
        maximumGradientControls = GradientColorSliders(card, () => cfg.MaximumColor, value => cfg.MaximumColor = value, "color_range_max_gradient", Color.white);
        maximumGradientControls.SetActive(!cfg.MaximumColor.SolidColor);
        EnumDropDown(card, "Ease", Easing.Linear, cfg.Ease, value => cfg.Ease = value, "color_range_ease");
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

#if !IL2CPP
    private void BuildBoxCollider2D(OvObject obj, BoxCollider2DSettings cfg) {
        var (_, card) = ComponentCard("Box Collider 2D", cfg, () => {
            obj.Config.BoxCollider2DConfig = null;
            RefreshComponents(obj);
        });

        Vector2Sliders(card, "Size", Vector2.one, 0f, 1024f, () => cfg.Size, value => cfg.Size = value, "box_collider_size", "F1");
        Vector2Sliders(card, "Offset", Vector2.zero, -1024f, 1024f, () => cfg.Offset, value => cfg.Offset = value, "box_collider_offset", "F1");
        Toggle(card, "Is Trigger", false, cfg.IsTrigger, value => cfg.IsTrigger = value, "box_collider_trigger");
        Toggle(card, "Used By Effector", false, cfg.UsedByEffector, value => cfg.UsedByEffector = value, "box_collider_effector");
        EnumDropDown(card, "Composite Operation", Collider2D.CompositeOperation.None, cfg.CompositeOperation, value => cfg.CompositeOperation = value, "box_collider_composite");
        Slider(card, "Edge Radius", 0f, 0f, 100f, cfg.EdgeRadius, value => cfg.EdgeRadius = value, "box_collider_edge_radius", "F2");
    }

    private void BuildRigidbody2D(OvObject obj, Rigidbody2DSettings cfg) {
        var (_, card) = ComponentCard("Rigidbody 2D", cfg, () => {
            obj.Config.Rigidbody2DConfig = null;
            RefreshComponents(obj);
        });

        EnumDropDown(card, "Body Type", RigidbodyType2D.Dynamic, cfg.BodyType, value => cfg.BodyType = value, "rigidbody2d_body_type");
        Toggle(card, "Simulated", true, cfg.Simulated, value => cfg.Simulated = value, "rigidbody2d_simulated");
        Toggle(card, "Use Auto Mass", false, cfg.UseAutoMass, value => cfg.UseAutoMass = value, "rigidbody2d_auto_mass");
        Slider(card, "Mass", 1f, 0.01f, 1000f, cfg.Mass, value => cfg.Mass = value, "rigidbody2d_mass", "F2");
        Slider(card, "Linear Damping", 0f, 0f, 100f, cfg.LinearDamping, value => cfg.LinearDamping = value, "rigidbody2d_linear_damping", "F2");
        Slider(card, "Angular Damping", 0.05f, 0f, 100f, cfg.AngularDamping, value => cfg.AngularDamping = value, "rigidbody2d_angular_damping", "F2");
        Slider(card, "Gravity Scale", 1f, -100f, 100f, cfg.GravityScale, value => cfg.GravityScale = value, "rigidbody2d_gravity_scale", "F2");
        EnumDropDown(card, "Collision Detection", CollisionDetectionMode2D.Discrete, cfg.CollisionDetectionMode, value => cfg.CollisionDetectionMode = value, "rigidbody2d_collision");
        EnumDropDown(card, "Sleep Mode", RigidbodySleepMode2D.StartAwake, cfg.SleepMode, value => cfg.SleepMode = value, "rigidbody2d_sleep_mode");
        EnumDropDown(card, "Interpolation", RigidbodyInterpolation2D.None, cfg.Interpolation, value => cfg.Interpolation = value, "rigidbody2d_interpolation");
        EnumDropDown(card, "Constraints", RigidbodyConstraints2D.None, cfg.Constraints, value => cfg.Constraints = value, "rigidbody2d_constraints");
        Toggle(card, "Freeze Rotation", false, cfg.FreezeRotation, value => cfg.FreezeRotation = value, "rigidbody2d_freeze_rotation");
    }
#endif

    private void BuildAddComponent(OvObject obj) {
        var options = new List<string> { "Add Component..." };
        if(obj.Config.TextConfig == null && obj.Config.ImageConfig == null) {
            options.Add("Text");
            options.Add("Image");
        }
        if(obj.Config.MovingManConfig == null) {
            options.Add("Moving Man");
        }

        if(obj.Config.TextConfig != null && obj.Config.ColorRangeConfig == null) {
            options.Add("Color Range");
        }
        
        if(obj.Config.ShadowConfig == null) {
            options.Add("Shadow");
        }

        if(obj.Config.OutlineConfig == null) {
            options.Add("Outline");
        }

        if(obj.Config.MaskConfig == null) {
            options.Add("Mask");
        }

        if(obj.Config.ContentSizeFitterConfig == null) {
            options.Add("Content Size Fitter");
        }

        if(!obj.Config.HasRectMask2D) {
            options.Add("Rect Mask 2D");
        }
#if !IL2CPP
        if(obj.Config.BoxCollider2DConfig == null) {
            options.Add("Box Collider 2D");
        }

        if(obj.Config.Rigidbody2DConfig == null) {
            options.Add("Rigidbody 2D");
        }
#endif
        if(options.Count == 1) {
            return;
        }

        var row = GenerateUI.Row(content, 50f);
        var dropdown = GenerateUI.DropDown(row, options[0], options[0], options, InspectorLabel, selected => {
            switch(selected) {
                case "Text":
                    obj.Config.TextConfig = new TextMeshProUGUISettings();
                    obj.Config.TextEngineConfig = new OvTextSettings();
                    break;
                case "Image":
                    obj.Config.ImageConfig = new ImageSettings();
                    break;
                case "Moving Man":
                    obj.Config.MovingManConfig = new MovingManSettings();
                    break;
                case "Color Range":
                    obj.Config.ColorRangeConfig = new ColorRangeSettings();
                    break;
                case "Shadow":
                    obj.Config.ShadowConfig = new ShadowSettings();
                    break;
                case "Outline":
                    obj.Config.OutlineConfig = new OutlineSettings();
                    break;
                case "Mask":
                    obj.Config.MaskConfig = new MaskSettings();
                    break;
                case "Content Size Fitter":
                    obj.Config.ContentSizeFitterConfig = new ContentSizeFitterSettings();
                    break;
                case "Rect Mask 2D":
                    obj.Config.HasRectMask2D = true;
                    obj.Config.RectMask2DEnabled = true;
                    break;
#if !IL2CPP
                case "Box Collider 2D":
                    obj.Config.BoxCollider2DConfig = new BoxCollider2DSettings();
                    break;
                case "Rigidbody 2D":
                    obj.Config.Rigidbody2DConfig = new Rigidbody2DSettings();
                    break;
#endif
                default:
                    return;
            }
            RefreshComponents(obj);
        }, "add_component");
        Track(dropdown);
    }

    private static string InspectorText(string key, string fallback) => MainCore.Tr.Get(key, fallback);

    private string ComponentText(string key, string fallback) =>
        InspectorText($"COMPONANT_{componentKey}_{key}", InspectorText($"INSPECTOR_{key}", fallback));

    private string InspectorLabel(string label) {
        if(string.IsNullOrEmpty(label)) {
            return label;
        }

        string key = label
            .Replace(" / ", "_")
            .Replace(" ", "_")
            .Replace(".", string.Empty)
            .ToUpperInvariant();
        return componentKey == null
            ? InspectorText($"INSPECTOR_{key}", label)
            : ComponentText(key, label);
    }

    private (RectTransform Card, RectTransform Content) Card(string title, bool removable, Action remove = null) {
        componentKey = null;
        return GenerateUI.ComponentCard(content, InspectorLabel(title), true, null, remove, removable, showActiveToggle: false);
    }

    private (RectTransform Card, RectTransform Content) ComponentCard(
        string title,
        UnityComponentSettingsBase settings,
        Action remove,
        Action enabledChanged = null
    ) {
        componentKey = title.Replace(" ", "_").ToUpperInvariant();
        return GenerateUI.ComponentCard(content, InspectorText($"COMPONANT_{componentKey}", InspectorText($"INSPECTOR_{componentKey}", title)), settings.ComponentEnabled, value => {
            settings.ComponentEnabled = value;
            if(enabledChanged == null) {
                ApplyAndSave();
            } else {
                enabledChanged();
            }
        }, remove);
    }

    private void Input(Transform parent, string label, string defaultValue, string value, Action<string> changed, string id, Action finished = null) {
        label = InspectorLabel(label);
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
            InspectorLabel($"{label} / tag expression"),
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

        var completionPopup = new TagCompletionPopup(codeInput, text);
        codeInput.HandleKey = completionPopup.HandleKey;

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
            completionPopup.Refresh(composing);
        };

        void SetDiagnosticsHeight(int diagnosticCount) {
            int lines = Math.Max(1, diagnosticCount);
            diagnosticsHeight = (lines * diagnosticsLineHeight) + 8f;
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
            var diagnostics = state is TextEngineState.Ready or TextEngineState.Error
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
            label.text = InspectorText("INSPECTOR_CHECKING", "Checking...");
            label.color = new Color(1f, 1f, 1f, 0.42f);
            return;
        }

        if(diagnostics.Length == 0) {
            label.text = InspectorText("INSPECTOR_NO_PROBLEMS", "No problems");
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
                .Select(d => $"{InspectorText("INSPECTOR_LINE", "Line")} {GetLine(source, d.Context.Index) + 1} [{d.Severity}] {FormatDiagnostic(d)}"));
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
            hash = (hash * 31) + rect.width.GetHashCode();
            hash = (hash * 31) + rect.height.GetHashCode();
            hash = (hash * 31) + text.textInfo.characterCount;
            for(int i = 0; i < text.textInfo.characterCount; i++) {
                var character = text.textInfo.characterInfo[i];
                if(!character.isVisible) {
                    continue;
                }

                hash = (hash * 31) + character.bottomLeft.GetHashCode();
                hash = (hash * 31) + character.topRight.GetHashCode();
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
                for(int i = start; i < end; i++) {
                    kinds[i] = span.Kind;
                }
            }
        }

        Color32 plain = text.color;
        var textInfo = text.textInfo;
        for(int i = 0; i < textInfo.characterCount; i++) {
            var character = textInfo.characterInfo[i];
            if(!character.isVisible) {
                continue;
            }

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
            DiagnosticId.ArgConvertFail => string.Format(InspectorText("INSPECTOR_DIAG_ARG_CONVERT", "Argument {0} ('{1}') cannot convert to {2}"), ArgumentNumber(), Data(1), Data(2)),
            DiagnosticId.ArgTooFew => string.Format(InspectorText("INSPECTOR_DIAG_ARG_TOO_FEW", "Expected at least {0} arguments; got {1}"), Data(0), Data(1)),
            DiagnosticId.ArgTooMany => string.Format(InspectorText("INSPECTOR_DIAG_ARG_TOO_MANY", "Expected at most {0} arguments; got {1}"), Data(0), Data(1)),
            DiagnosticId.FormatFail => string.Format(InspectorText("INSPECTOR_DIAG_FORMAT", "Invalid format '{0}'"), Data(0)),
            DiagnosticId.AdvancedTagException => Data(0, InspectorText("INSPECTOR_DIAG_ADVANCED_TAG", "Advanced tag failed")),
            DiagnosticId.InternalError => InspectorText("INSPECTOR_DIAG_INTERNAL", "Internal compiler error"),
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
            ? string.Format(InspectorText("INSPECTOR_DIAG_TAG_NOT_FOUND", "Tag '{0}' not found"), name)
            : string.Format(InspectorText("INSPECTOR_DIAG_TAG_SUGGESTION", "Tag '{0}' not found. Did you mean '{1}'?"), name, suggestion);
    }

    private static int GetLine(string source, int index) {
        source ??= string.Empty;
        int limit = Math.Clamp(index, 0, source.Length);
        int line = 0;
        for(int i = 0; i < limit; i++) {
            if(source[i] == '\n') {
                line++;
            }
        }
        return line;
    }

    private static string BuildDiagnosticsKey(
        TextEngineState state,
        CompileDiagnostic[] diagnostics,
        string source
    ) => $"{state}|{source?.GetHashCode() ?? 0}|{string.Join("|", diagnostics.Select(d => d.ToString()))}";

    private RectTransform Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format = "F2") => Slider(parent, label, defaultValue, min, max, value, changed, id, format, true, null);

    private RectTransform Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format, bool clamp, Func<float, float> filter = null) {
        label = InspectorLabel(label);
        var row = GenerateUI.Row(parent, 50f);
        var slider = GenerateUI.Slider(row, defaultValue, min, max, value, format, clamp, filter, newValue => {
            changed(newValue);
            apply();
        }, _ => save(), label, id);
        Track(slider);
        return row;
    }

    private RectTransform Toggle(Transform parent, string label, bool defaultValue, bool value, Action<bool> changed, string id) {
        label = InspectorLabel(label);
        var row = GenerateUI.Row(parent, 50f);
        var toggle = GenerateUI.Toggle(row, defaultValue, value, newValue => {
            changed(newValue);
            ApplyAndSave();
        }, label, id);
        Track(toggle);
        return row;
    }

    private RectTransform EnumDropDown<T>(Transform parent, string label, T defaultValue, T value, Action<T> changed, string id, Action completed = null) where T : struct, Enum {
        label = InspectorLabel(label);
        var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.DropDown(row, defaultValue, value, values, option => $"{label}: {option}", newValue => {
            changed(newValue);
            if(completed == null) {
                ApplyAndSave();
            } else {
                completed();
            }
        }, id);
        Track(dropdown);
        return row;
    }

    private void Vector2Sliders(Transform parent, string label, Vector2 defaults, float min, float max, Func<Vector2> get, Action<Vector2> set, string id, string format = "F2") {
        Slider(parent, $"{label} X", defaults.x, min, max, get().x, value => set(new Vector2(value, get().y)), id + "_x", format);
        Slider(parent, $"{label} Y", defaults.y, min, max, get().y, value => set(new Vector2(get().x, value)), id + "_y", format);
    }

    private Action NumericPropertyRow(
        Transform parent,
        string label,
        (string Label, float Default, Func<float> Get, Action<float> Set, string Id)[] fields,
        string format
    ) {
        RectTransform row = CompactRow(parent, 44f, 6f);
        FixedLabel(row, InspectorLabel(label), 66f);
        var numericFields = new List<(UISlider Field, Func<float> Get)>();
        foreach(var field in fields) {
            numericFields.Add(NumericField(row, field.Label, field.Default, field.Get, field.Set, field.Id, format));
        }

        void RefreshValues() {
            foreach(var field in numericFields) {
                SetDisplayedValue(field.Field, field.Get());
            }
        }

        RefreshValues();
        return RefreshValues;
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
        label = InspectorLabel(label);
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
        var (Field, Get) = NumericField(firstRow, "", 0f, () => StretchX() ? Left() : PositionX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMin(0, value);
            } else {
                cfg.AnchoredPosition.x = value;
            }
        }, "transform_rect_x1", "F1");
        var firstY = NumericField(firstRow, "", 0f, () => StretchY() ? Top() : PositionY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMax(1, -value);
            } else {
                cfg.AnchoredPosition.y = value;
            }
        }, "transform_rect_y1", "F1");
        var secondX = NumericField(secondRow, "", 200f, () => StretchX() ? Right() : SizeX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMax(0, -value);
            } else {
                cfg.SizeDelta.x = value;
            }
        }, "transform_rect_x2", "F1");
        var secondY = NumericField(secondRow, "", 200f, () => StretchY() ? Bottom() : SizeY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMin(1, value);
            } else {
                cfg.SizeDelta.y = value;
            }
        }, "transform_rect_y2", "F1");

        void RefreshValues() {
            Field.Label.text = InspectorLabel(StretchX() ? "Left" : "Pos X");
            SetDisplayedValue(Field, Get());
            secondX.Field.Label.text = InspectorLabel(StretchX() ? "Right" : "Width");
            SetDisplayedValue(secondX.Field, secondX.Get());
            firstY.Field.Label.text = InspectorLabel(StretchY() ? "Top" : "Pos Y");
            SetDisplayedValue(firstY.Field, firstY.Get());
            secondY.Field.Label.text = InspectorLabel(StretchY() ? "Bottom" : "Height");
            SetDisplayedValue(secondY.Field, secondY.Get());
        }

        bool drivenX = DrivenX();
        bool drivenY = DrivenY();
        Field.SetBlocked(drivenX && StretchX(), true);
        secondX.Field.SetBlocked(drivenX, true);
        firstY.Field.SetBlocked(drivenY && StretchY(), true);
        secondY.Field.SetBlocked(drivenY, true);
        RefreshValues();

        if(drivenX || drivenY) {
            controls.Add(new UIWatcher("rect_transform_driven", fields, RefreshValues));
        }
        return RefreshValues;
    }

    private Action BuildRectPositionFields(Transform parent, RectTransformSettings cfg) {
        bool StretchX() => !Mathf.Approximately(cfg.AnchorMin.x, cfg.AnchorMax.x);
        bool StretchY() => !Mathf.Approximately(cfg.AnchorMin.y, cfg.AnchorMax.y);
        float PositionX() => cfg.AnchoredPosition.x;
        float PositionY() => cfg.AnchoredPosition.y;
        float SizeX() => cfg.SizeDelta.x;
        float SizeY() => cfg.SizeDelta.y;
        float Left() => cfg.GetOffsetMin(0);
        float Right() => -cfg.GetOffsetMax(0);
        float Top() => -cfg.GetOffsetMax(1);
        float Bottom() => cfg.GetOffsetMin(1);

        RectTransform fields = VerticalGroup(parent, 2f);
        var firstRow = CompactRow(fields, 44f, 6f);
        var secondRow = CompactRow(fields, 44f, 6f);
        var (Field, Get) = NumericField(firstRow, "", 0f, () => StretchX() ? Left() : PositionX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMin(0, value);
            } else {
                cfg.AnchoredPosition.x = value;
            }
        }, "transform_rect_x1", "F1");
        var firstY = NumericField(firstRow, "", 0f, () => StretchY() ? Top() : PositionY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMax(1, -value);
            } else {
                cfg.AnchoredPosition.y = value;
            }
        }, "transform_rect_y1", "F1");
        var secondX = NumericField(secondRow, "", 200f, () => StretchX() ? Right() : SizeX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMax(0, -value);
            } else {
                cfg.SizeDelta.x = value;
            }
        }, "transform_rect_x2", "F1");
        var secondY = NumericField(secondRow, "", 200f, () => StretchY() ? Bottom() : SizeY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMin(1, value);
            } else {
                cfg.SizeDelta.y = value;
            }
        }, "transform_rect_y2", "F1");

        void RefreshValues() {
            Field.Label.text = InspectorLabel(StretchX() ? "Left" : "Pos X");
            SetDisplayedValue(Field, Get());
            secondX.Field.Label.text = InspectorLabel(StretchX() ? "Right" : "Width");
            SetDisplayedValue(secondX.Field, secondX.Get());
            firstY.Field.Label.text = InspectorLabel(StretchY() ? "Top" : "Pos Y");
            SetDisplayedValue(firstY.Field, firstY.Get());
            secondY.Field.Label.text = InspectorLabel(StretchY() ? "Bottom" : "Height");
            SetDisplayedValue(secondY.Field, secondY.Get());
        }

        RefreshValues();
        return RefreshValues;
    }

    private static void SetDisplayedValue(UISlider field, float value) {
        if(!Mathf.Approximately(field.Value, value)) {
            field.Set(value, false);
        }
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
        CanvasGroup popupCanvas = popup.gameObject.AddComponent<CanvasGroup>();
        blocker.gameObject.SetActive(false);
        GTween popupTween = null;
        summary.OnDisposed += () => {
            popupTween?.Kill();
            if(popup != null) {
                UnityEngine.Object.Destroy(popup.gameObject);
            }

            if(blocker != null) {
                UnityEngine.Object.Destroy(blocker.gameObject);
            }
        };
        popup.gameObject.SetActive(false);
        bool open = false;
        float lastClickTime = -1f;

        void ClosePopup() {
            if(!open) {
                return;
            }

            open = false;
            popupTween?.Kill();
            popupCanvas.interactable = false;
            popupCanvas.blocksRaycasts = false;
            popupTween = PlayPopupAnimation(popup, popupCanvas, false).OnComplete(() => {
                if(open) {
                    return;
                }

                popup.gameObject.SetActive(false);
                blocker.gameObject.SetActive(false);
            });
        }

        void OpenPopup() {
            open = true;
            popupTween?.Kill();
            popup.gameObject.SetActive(true);
            popup.localScale = new Vector3(0.96f, 0.96f, 1f);
            popupCanvas.alpha = 0f;
            popupCanvas.interactable = true;
            popupCanvas.blocksRaycasts = true;
            blocker.gameObject.SetActive(true);
            blocker.SetAsLastSibling();
            popup.SetAsLastSibling();
            popupTween = PlayPopupAnimation(popup, popupCanvas, true);
        }

        void RefreshPopupPosition() {
            if(!open) {
                return;
            }

            RectTransform canvasRect = UICore.Canvas.GetComponent<RectTransform>();
            Vector3 corner = summary.Rect.TransformPoint(new Vector3(summary.Rect.rect.xMin, summary.Rect.rect.yMin, 0f));
            Vector2 position = canvasRect.InverseTransformPoint(corner);
            float minX = canvasRect.rect.xMin + 8f;
            float maxX = canvasRect.rect.xMax - popup.rect.width - 8f;
            float minY = canvasRect.rect.yMin + popup.rect.height + 8f;
            float maxY = canvasRect.rect.yMax - 8f;
            position.x = maxX >= minX ? Mathf.Clamp(position.x, minX, maxX) : minX;
            position.y = maxY >= minY ? Mathf.Clamp(position.y, minY, maxY) : maxY;
            popup.anchoredPosition = position;
        }

        controls.Add(new UIWatcher("transform_anchor_popup", summary.Rect, RefreshPopupPosition));

        GenerateUI.AddButton(blocker.gameObject, button => {
            if(button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) {
                ClosePopup();
            }
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
            if(!force && shift == lastShift && alt == lastAlt) {
                return;
            }

            lastShift = shift;
            lastAlt = alt;

            for(int i = 0; i < presetGraphics.Count; i++) {
                var item = presetGraphics[i];
                UnityEngine.Object.Destroy(item.Graphic);
                GameObject graphic = AddAnchorGraphic(item.Parent, item.H, item.V, shift, alt, item.Header ? 34f : 40f);
                PositionAnchorGraphic(graphic, item.H, item.V);
                presetGraphics[i] = (item.Parent, item.H, item.V, item.Header, graphic);
            }

            modifierHelp.text = AnchorModifierHelp(shift, alt);
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
                if(header) {
                    AddTableHeader(cell.Rect, x == 0, x == 0 ? ModeName(vertical, true) : ModeName(horizontal, false));
                }

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
            if(open) {
                ClosePopup();
            } else {
                OpenPopup();
                RefreshPopupPosition();
            }
            RefreshSummary();
            if(open) {
                RefreshModifierGraphics(true);
            }
        };
        summary.Rect.GetComponent<OventHandler>().OnDisabled = ClosePopup;
        popup.gameObject.GetComponent<OventHandler>().OnDisabled = () => {
            if(open) {
                ClosePopup();
            }
        };
        popup.gameObject.GetComponent<OventHandler>().OnHoverUpdate = () => RefreshModifierGraphics();
        RefreshSummary();
        return RefreshSummary;
    }

    private Action AnchorPresetControl(Transform parent, RectTransformSettings cfg, RectTransform targetTransform, Action positionFieldsChanged) {
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
        CanvasGroup popupCanvas = popup.gameObject.AddComponent<CanvasGroup>();
        blocker.gameObject.SetActive(false);
        GTween popupTween = null;
        summary.OnDisposed += () => {
            popupTween?.Kill();
            if(popup != null) {
                UnityEngine.Object.Destroy(popup.gameObject);
            }

            if(blocker != null) {
                UnityEngine.Object.Destroy(blocker.gameObject);
            }
        };
        popup.gameObject.SetActive(false);
        bool open = false;
        float lastClickTime = -1f;

        void ClosePopup() {
            if(!open) {
                return;
            }

            open = false;
            popupTween?.Kill();
            popupCanvas.interactable = false;
            popupCanvas.blocksRaycasts = false;
            popupTween = PlayPopupAnimation(popup, popupCanvas, false).OnComplete(() => {
                if(open) {
                    return;
                }

                popup.gameObject.SetActive(false);
                blocker.gameObject.SetActive(false);
            });
        }

        void OpenPopup() {
            open = true;
            popupTween?.Kill();
            popup.gameObject.SetActive(true);
            popup.localScale = new Vector3(0.96f, 0.96f, 1f);
            popupCanvas.alpha = 0f;
            popupCanvas.interactable = true;
            popupCanvas.blocksRaycasts = true;
            blocker.gameObject.SetActive(true);
            blocker.SetAsLastSibling();
            popup.SetAsLastSibling();
            popupTween = PlayPopupAnimation(popup, popupCanvas, true);
        }

        void RefreshPopupPosition() {
            if(!open) {
                return;
            }

            RectTransform canvasRect = UICore.Canvas.GetComponent<RectTransform>();
            Vector3 corner = summary.Rect.TransformPoint(new Vector3(summary.Rect.rect.xMin, summary.Rect.rect.yMin, 0f));
            Vector2 position = canvasRect.InverseTransformPoint(corner);
            float minX = canvasRect.rect.xMin + 8f;
            float maxX = canvasRect.rect.xMax - popup.rect.width - 8f;
            float minY = canvasRect.rect.yMin + popup.rect.height + 8f;
            float maxY = canvasRect.rect.yMax - 8f;
            position.x = maxX >= minX ? Mathf.Clamp(position.x, minX, maxX) : minX;
            position.y = maxY >= minY ? Mathf.Clamp(position.y, minY, maxY) : maxY;
            popup.anchoredPosition = position;
        }

        controls.Add(new UIWatcher("transform_anchor_popup", summary.Rect, RefreshPopupPosition));

        GenerateUI.AddButton(blocker.gameObject, button => {
            if(button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) {
                ClosePopup();
            }
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
            if(!force && shift == lastShift && alt == lastAlt) {
                return;
            }

            lastShift = shift;
            lastAlt = alt;

            for(int i = 0; i < presetGraphics.Count; i++) {
                var item = presetGraphics[i];
                UnityEngine.Object.Destroy(item.Graphic);
                GameObject graphic = AddAnchorGraphic(item.Parent, item.H, item.V, shift, alt, item.Header ? 34f : 40f);
                PositionAnchorGraphic(graphic, item.H, item.V);
                presetGraphics[i] = (item.Parent, item.H, item.V, item.Header, graphic);
            }

            modifierHelp.text = AnchorModifierHelp(shift, alt);
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
                if(header) {
                    AddTableHeader(cell.Rect, x == 0, x == 0 ? ModeName(vertical, true) : ModeName(horizontal, false));
                }

                cell.Rect.AddToolTip(AnchorCellName(horizontal, vertical));
                cell.OnClick = () => {
                    bool setPivot = OVC_Input.GetKey(KeyCode.LeftShift) || OVC_Input.GetKey(KeyCode.RightShift);
                    bool setPosition = OVC_Input.GetKey(KeyCode.LeftAlt) || OVC_Input.GetKey(KeyCode.RightAlt);
                    ApplyAnchorModes(cfg, targetTransform, horizontal, vertical, setPivot, setPosition);
                    apply();
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(targetTransform);
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
            if(open) {
                ClosePopup();
            } else {
                OpenPopup();
                RefreshPopupPosition();
            }
            RefreshSummary();
            if(open) {
                RefreshModifierGraphics(true);
            }
        };
        summary.Rect.GetComponent<OventHandler>().OnDisabled = ClosePopup;
        popup.gameObject.GetComponent<OventHandler>().OnDisabled = () => {
            if(open) {
                ClosePopup();
            }
        };
        popup.gameObject.GetComponent<OventHandler>().OnHoverUpdate = () => RefreshModifierGraphics();
        RefreshSummary();
        return RefreshSummary;
    }

    private static void ApplyAnchorModes(RectTransformSettings cfg, RectTransform targetTransform, AnchorMode horizontal, AnchorMode vertical, bool setPivot, bool setPosition) {
        Vector2 parentSize = (targetTransform.parent as RectTransform)?.rect.size ?? new Vector2(1920f, 1080f);
        Vector2 visibleSize = targetTransform.rect.size;
        ApplyAnchorModeForAxis(cfg, 0, horizontal, parentSize.x, visibleSize.x, setPivot, setPosition);
        ApplyAnchorModeForAxis(cfg, 1, vertical, parentSize.y, visibleSize.y, setPivot, setPosition);
    }

    private static GTween PlayPopupAnimation(RectTransform popup, CanvasGroup canvas, bool opening) {
        GTween sequence = GTweenSequenceBuilder.New()
            .Join(popup.GTScale(opening ? Vector3.one : new Vector3(0.96f, 0.96f, 1f), 0.2f).SetEasing(Easing.OutBack))
            .Join(canvas.GTFade(opening ? 1f : 0f, 0.16f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(sequence);
        return sequence;
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
        vertical.padding = new RectOffset { left = 10, right = 10, top = 8, bottom = 8 };
        vertical.spacing = 3f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        TextMeshProUGUI title = GenerateUI.AddText(popup, true);
        title.text = InspectorText("INSPECTOR_ANCHOR_PRESETS", "Anchor Presets");
        title.fontSize = 18f;
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 23f;

        TextMeshProUGUI help = GenerateUI.AddText(popup, true);
        help.name = "ModifierHelp";
        help.text = AnchorModifierHelp(false, false);
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

        if(horizontal == AnchorMode.Custom && vertical == AnchorMode.Custom) {
            return root;
        }

        float innerSize = size * 0.5f;
        Vector2 objectSize = new(horizontal == AnchorMode.Stretch ? size - 4f : innerSize, vertical == AnchorMode.Stretch ? size - 4f : innerSize);
        Vector2 objectPosition = new(ModePosition(horizontal, size), ModePosition(vertical, size));
        if(!alignPosition) {
            objectPosition = Vector2.zero;
        }

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
            if(horizontal == AnchorMode.Stretch) {
                AddStretchArrow(frame, true, stretchColor, size);
            } else {
                AddGraphicLine(frame, "HorizontalAnchor", new Vector2(x, 0f), new Vector2(1f, size - 2f), simpleColor);
            }
        }
        if(vertical != AnchorMode.Custom) {
            float y = ModePosition(vertical, size, true);
            if(vertical == AnchorMode.Stretch) {
                AddStretchArrow(frame, false, stretchColor, size);
            } else {
                AddGraphicLine(frame, "VerticalAnchor", new Vector2(0f, y), new Vector2(size - 2f, 1f), simpleColor);
            }
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
        if(horizontal) {
            label.rectTransform.offsetMin = new Vector2(0f, 2f);
        } else {
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
        }

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
        if(mode == AnchorMode.Stretch) {
            return new[] { size * -0.5f, size * 0.5f };
        }

        return new[] { ModePosition(mode, size, true) };
    }

    private static float ModePosition(AnchorMode mode, float size, bool edge = false) {
        float range = edge ? size * 0.5f : size * 0.25f;
        return mode switch { AnchorMode.Min => -range, AnchorMode.Max => range, _ => 0f };
    }

    private static float PivotOffset(AnchorMode mode, float size) => mode switch { AnchorMode.Min => size * -0.5f, AnchorMode.Max => size * 0.5f, _ => 0f };

    private static AnchorMode ModeForAxis(RectTransformSettings cfg, int axis) {
        float min = cfg.AnchorMin[axis];
        float max = cfg.AnchorMax[axis];
        if(Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f)) {
            return AnchorMode.Min;
        }

        if(Mathf.Approximately(min, 0.5f) && Mathf.Approximately(max, 0.5f)) {
            return AnchorMode.Middle;
        }

        if(Mathf.Approximately(min, 1f) && Mathf.Approximately(max, 1f)) {
            return AnchorMode.Max;
        }

        if(Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 1f)) {
            return AnchorMode.Stretch;
        }

        return AnchorMode.Custom;
    }

    private static string ModeName(AnchorMode mode, bool vertical) => mode switch {
        AnchorMode.Min => InspectorText(vertical ? "INSPECTOR_ANCHOR_BOTTOM" : "INSPECTOR_ANCHOR_LEFT", vertical ? "bottom" : "left"),
        AnchorMode.Middle => InspectorText("INSPECTOR_ANCHOR_MIDDLE", "middle"),
        AnchorMode.Max => InspectorText(vertical ? "INSPECTOR_ANCHOR_TOP" : "INSPECTOR_ANCHOR_RIGHT", vertical ? "top" : "right"),
        AnchorMode.Stretch => InspectorText("INSPECTOR_ANCHOR_STRETCH", "stretch"),
        _ => InspectorText("INSPECTOR_ANCHOR_CUSTOM", "custom")
    };

    private static string AnchorCellName(AnchorMode horizontal, AnchorMode vertical) {
        if(horizontal == AnchorMode.Custom) {
            return $"{InspectorText("INSPECTOR_ANCHOR_VERTICAL", "Vertical")}: {ModeName(vertical, true)}";
        }

        if(vertical == AnchorMode.Custom) {
            return $"{InspectorText("INSPECTOR_ANCHOR_HORIZONTAL", "Horizontal")}: {ModeName(horizontal, false)}";
        }

        return $"{ModeName(horizontal, false)} / {ModeName(vertical, true)}";
    }

    private static string AnchorModifierHelp(bool shift, bool alt) {
        string shiftText = InspectorText("INSPECTOR_ANCHOR_SHIFT", "Shift: Also set pivot");
        string altText = InspectorText("INSPECTOR_ANCHOR_ALT", "Alt: Also set position");
        return $"{(shift ? "<color=#FFCC44>" : "")}{shiftText}{(shift ? "</color>" : "")}     {(alt ? "<color=#FFCC44>" : "")}{altText}{(alt ? "</color>" : "")}";
    }

    private static void ApplyAnchorModes(OvObject obj, AnchorMode horizontal, AnchorMode vertical, bool setPivot, bool setPosition) {
        RectTransformSettings cfg = obj.Config.RectTransformConfig;
        Vector2 parentSize = (obj.RectTransform.parent as RectTransform)?.rect.size ?? new Vector2(1920f, 1080f);
        Vector2 visibleSize = obj.RectTransform.rect.size;
        ApplyAnchorModeForAxis(cfg, 0, horizontal, parentSize.x, visibleSize.x, setPivot, setPosition);
        ApplyAnchorModeForAxis(cfg, 1, vertical, parentSize.y, visibleSize.y, setPivot, setPosition);
    }

    private static void ApplyAnchorModeForAxis(RectTransformSettings cfg, int axis, AnchorMode mode, float parentSize, float visibleSize, bool setPivot, bool setPosition) {
        if(mode == AnchorMode.Custom) {
            return;
        }

        float oldMin = cfg.AnchorMin[axis];
        float oldMax = cfg.AnchorMax[axis];
        float oldPivot = cfg.Pivot[axis];
        float newMin = mode == AnchorMode.Stretch ? 0f : mode switch { AnchorMode.Min => 0f, AnchorMode.Middle => 0.5f, _ => 1f };
        float newMax = mode == AnchorMode.Stretch ? 1f : newMin;
        float oldReference = Mathf.Lerp(oldMin, oldMax, oldPivot);
        float newReference = Mathf.Lerp(newMin, newMax, oldPivot);

        cfg.AnchoredPosition[axis] += (oldReference - newReference) * parentSize;
        cfg.SizeDelta[axis] += (oldMax - oldMin - (newMax - newMin)) * parentSize;
        cfg.AnchorMin[axis] = newMin;
        cfg.AnchorMax[axis] = newMax;

        if(setPivot) {
            float newPivot = mode switch { AnchorMode.Min => 0f, AnchorMode.Max => 1f, _ => 0.5f };
            float rectSize = (parentSize * (newMax - newMin)) + cfg.SizeDelta[axis];
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
        var dropdown = GenerateUI.DropDown(row, none, current, options, option => $"{InspectorLabel("Sprite")}: {InspectorLabel(option)}", selected => {
            cfg.SpriteKey = selected == none ? null : selected;
            ApplyAndSave();
        }, "image_sprite");
        Track(dropdown);
    }

    private void FontDropDown(Transform parent, TextMeshProUGUISettings cfg) {
        const string none = "Default";
        var options = UserResourceManager.Fnt.Keys.OrderBy(key => key).ToList();
        if(!string.IsNullOrEmpty(cfg.FontKey) && !options.Contains(cfg.FontKey)) {
            options.Insert(0, cfg.FontKey);
        }
        options.Insert(0, none);

        string current = string.IsNullOrEmpty(cfg.FontKey) ? none : cfg.FontKey;
        var row = GenerateUI.Row(parent, 50f);
        var dropdown = GenerateUI.DropDown(row, none, current, options, option => $"{InspectorLabel("Font")}: {InspectorLabel(option)}", selected => {
            cfg.FontKey = selected == none ? null : selected;
            ApplyAndSave();
        }, "text_font");
        Track(dropdown);
    }

    private void ColorSliders(Transform parent, string label, Color defaults, Func<Color> get, Action<Color> set, string id) {
        RectTransform row = GenerateUI.Row(parent, 50f);
        UIColorPicker picker = GenerateUI.ColorPicker(row, defaults, get(), value => {
            set(value);
            apply();
        }, _ => save(), id, InspectorLabel(label));
        Track(picker);
    }

    private GameObject GradientColorSliders(
        Transform parent,
        Func<GradientColor> get,
        Action<GradientColor> set,
        string idPrefix,
        Color defaults
    ) {
        GameObject gridObject = new("GradientCorners");
        gridObject.transform.SetParent(parent, false);
        RectTransform grid = gridObject.AddComponent<RectTransform>();
        var gridLayout = gridObject.AddComponent<VerticalLayoutGroup>();
        gridLayout.spacing = 6f;
        gridLayout.childControlWidth = true;
        gridLayout.childControlHeight = true;
        gridLayout.childForceExpandWidth = true;
        gridLayout.childForceExpandHeight = false;

        GradientColorRow(grid, defaults, "Top Left", () => get().TL, value => {
            GradientColor color = get();
            color.TL = value;
            set(color);
        }, idPrefix + "_top_left", "Top Right", () => get().TR, value => {
            GradientColor color = get();
            color.TR = value;
            set(color);
        }, idPrefix + "_top_right");
        GradientColorRow(grid, defaults, "Bottom Left", () => get().BL, value => {
            GradientColor color = get();
            color.BL = value;
            set(color);
        }, idPrefix + "_bottom_left", "Bottom Right", () => get().BR, value => {
            GradientColor color = get();
            color.BR = value;
            set(color);
        }, idPrefix + "_bottom_right");
        return gridObject;
    }

    private void GradientColorRow(
        Transform parent,
        Color defaults,
        string leftLabel,
        Func<Color> leftGet,
        Action<Color> leftSet,
        string leftId,
        string rightLabel,
        Func<Color> rightGet,
        Action<Color> rightSet,
        string rightId
    ) {
        GameObject rowObject = new("TextGradientRow");
        rowObject.transform.SetParent(parent, false);
        RectTransform row = rowObject.AddComponent<RectTransform>();
        var rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        ColorSliders(row, leftLabel, defaults, leftGet, leftSet, leftId);
        ColorSliders(row, rightLabel, defaults, rightGet, rightSet, rightId);
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
