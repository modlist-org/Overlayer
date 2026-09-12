using Overlayer.IO.Fx;
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
        Input(identity, "Canvas Name", "", canvas.Config.Name.Value, value => {
            canvas.Config.Name.Value = value;
            canvas.ApplyConfig();
            nameChanged(value);
        }, "canvas_name", hierarchyChanged);

        var group = canvas.Config.CanvasGroupConfig;
        FxSlider(identity, "Opacity", group.Alpha, 1f, 0f, 1f, "canvas_alpha");
        FxToggle(identity, "Interactable", group.Interactable, false, "canvas_interactable");
        FxToggle(identity, "Blocks Raycasts", group.BlocksRaycasts, true, "canvas_raycast");
        FxToggle(identity, "Ignore Parent Groups", group.IgnoreParentGroups, false, "canvas_ignore_parent");

        var (_, rendering) = Card("Rendering", false);
        var canvasCfg = canvas.Config.CanvasConfig;
        FxEnum(rendering, "Render Mode", canvasCfg.RenderMode, RenderMode.ScreenSpaceOverlay, "canvas_render_mode");
        FxIntSlider(rendering, "Sorting Order", canvasCfg.SortingOrder, 32760, -32768f, 32767f, "canvas_sort");
        FxToggle(rendering, "Override Sorting", canvasCfg.OverrideSorting, true, "canvas_override_sorting");
        FxToggle(rendering, "Pixel Perfect", canvasCfg.PixelPerfect, false, "canvas_pixel_perfect");
        FxToggle(rendering, "Graphic Raycaster", canvas.Config.GraphicRaycasterConfig.Enabled, true, "canvas_graphic_raycast");

        var (_, scaling) = Card("Scaling", false);
        var scale = canvas.Config.CanvasScalerConfig;
        FxEnum(scaling, "Scale Mode", scale.UiScaleMode, CanvasScaler.ScaleMode.ScaleWithScreenSize, "canvas_scale_mode");
        FxVector2(scaling, "Reference", scale.ReferenceResolution, new Vector2(1920, 1080), 1f, 8192f, "canvas_reference", "F0");
        FxSlider(scaling, "Match Width / Height", scale.MatchWidthOrHeight, 0.5f, 0f, 1f, "canvas_match");
        FxSlider(scaling, "Scale Factor", scale.ScaleFactor, 1f, 0.01f, 10f, "canvas_scale_factor");

        BuildCanvasRectTransform(canvas);
    }

    public void BuildObject(OvObject obj) {
        var (_, identity) = Card("Object", false);
        Input(identity, "Object Name", "OvObject", obj.Config.Name.Value, value => {
            obj.Config.Name.Value = value;
            apply();
        }, "obj_name", hierarchyChanged);
        FxToggle(identity, "Visible", obj.Config.Enabled, true, "obj_visible");

        var group = obj.Config.CanvasGroupConfig;
        FxSlider(identity, "Opacity", group.Alpha, 1f, 0f, 1f, "obj_alpha");
        FxToggle(identity, "Interactable", group.Interactable, false, "obj_interactable");
        FxToggle(identity, "Blocks Raycasts", group.BlocksRaycasts, false, "obj_raycast");

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
        if(obj.Config.HasRectMask2D.Value) {
            componentKey = "RECT_MASK_2D";
            var (_, rectMask) = GenerateUI.ComponentCard(content, InspectorLabel("Rect Mask 2D"), obj.Config.RectMask2DEnabled.Value, value => {
                obj.Config.RectMask2DEnabled.Value = value;
                ApplyAndSave();
            }, () => {
                obj.Config.HasRectMask2D.Value = false;
                obj.Config.RectMask2DEnabled.Value = true;
                RefreshComponents(obj);
            });
            Label(rectMask, InspectorText(
                "COMPONENT_RECT_MASK_2D_DESCRIPTION",
                InspectorText("INSPECTOR_RECT_MASK_DESCRIPTION", "Clips child graphics to this object's rectangle.")));
            FxToggle(rectMask, "Enabled Fx", obj.Config.RectMask2DEnabled, true, "rect_mask_enabled");
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
        refreshPositionFields = () => { };
        FxBlock(rectLayout, "Position", cfg.AnchoredPosition,
            group => refreshPositionFields = BuildRectPositionFields(group, obj), "rect_position_xy");
        FxFloatRow(basic, "Position", cfg.AnchoredPositionZ, 0f, "Z", "rect_position_z", "F1");
        FxNumericRow(basic, "Rotation XY", cfg.RotationXY, [
            ("X", 0f, () => cfg.RotationXY.Value.x, value => { var v = cfg.RotationXY.Value; v.x = value; cfg.RotationXY.Value = v; }, "rect_rotation_x"),
            ("Y", 0f, () => cfg.RotationXY.Value.y, value => { var v = cfg.RotationXY.Value; v.y = value; cfg.RotationXY.Value = v; }, "rect_rotation_y")
        ], "F1", "rect_rotation_xy");
        FxFloatRow(basic, "Rotation Z", cfg.Rotation, 0f, "Z", "rect_rotation_z", "F1");
        FxNumericRow3(basic, "Scale", cfg.Scale, [
            ("X", 1f, () => cfg.Scale.Value.x, value => { var v = cfg.Scale.Value; v.x = value; cfg.Scale.Value = v; }, "rect_scale_x"),
            ("Y", 1f, () => cfg.Scale.Value.y, value => { var v = cfg.Scale.Value; v.y = value; cfg.Scale.Value = v; }, "rect_scale_y"),
            ("Z", 1f, () => cfg.Scale.Value.z, value => { var v = cfg.Scale.Value; v.z = value; cfg.Scale.Value = v; }, "rect_scale_z")
        ], "F2", "rect_scale");
        refreshPivotFields = FxNumericRow(basic, "Pivot", cfg.Pivot, [
            ("X", 0.5f, () => cfg.Pivot.Value.x, value => { var v = cfg.Pivot.Value; v.x = value; cfg.Pivot.Value = v; }, "rect_pivot_x"),
            ("Y", 0.5f, () => cfg.Pivot.Value.y, value => { var v = cfg.Pivot.Value; v.y = value; cfg.Pivot.Value = v; }, "rect_pivot_y")
        ], "F2", "rect_pivot");

        var (_, anchors) = Card("Anchors", false);
        FxNumericRow(anchors, "Min", cfg.AnchorMin, [
            ("X", 0f, () => cfg.AnchorMin.Value.x, value => { var v = cfg.AnchorMin.Value; v.x = value; cfg.AnchorMin.Value = v; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_min_x"),
            ("Y", 0f, () => cfg.AnchorMin.Value.y, value => { var v = cfg.AnchorMin.Value; v.y = value; cfg.AnchorMin.Value = v; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_min_y")
        ], "F2", "transform_anchor_min");
        FxNumericRow(anchors, "Max", cfg.AnchorMax, [
            ("X", 1f, () => cfg.AnchorMax.Value.x, value => { var v = cfg.AnchorMax.Value; v.x = value; cfg.AnchorMax.Value = v; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_max_x"),
            ("Y", 1f, () => cfg.AnchorMax.Value.y, value => { var v = cfg.AnchorMax.Value; v.y = value; cfg.AnchorMax.Value = v; refreshAnchor(); refreshPositionFields(); }, "transform_anchor_max_y")
        ], "F2", "transform_anchor_max");
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
        refreshPositionFields = () => { };
        FxBlock(rectLayout, "Position", cfg.AnchoredPosition,
            group => refreshPositionFields = BuildRectPositionFields(group, cfg), "canvas_rect_position_xy");
        FxFloatRow(basic, "Position", cfg.AnchoredPositionZ, 0f, "Z", "canvas_rect_position_z", "F1");
        FxNumericRow(basic, "Rotation XY", cfg.RotationXY, [
            ("X", 0f, () => cfg.RotationXY.Value.x, value => { var v = cfg.RotationXY.Value; v.x = value; cfg.RotationXY.Value = v; }, "canvas_rect_rotation_x"),
            ("Y", 0f, () => cfg.RotationXY.Value.y, value => { var v = cfg.RotationXY.Value; v.y = value; cfg.RotationXY.Value = v; }, "canvas_rect_rotation_y")
        ], "F1", "canvas_rect_rotation_xy");
        FxFloatRow(basic, "Rotation Z", cfg.Rotation, 0f, "Z", "canvas_rect_rotation_z", "F1");
        FxNumericRow3(basic, "Scale", cfg.Scale, [
            ("X", 1f, () => cfg.Scale.Value.x, value => { var v = cfg.Scale.Value; v.x = value; cfg.Scale.Value = v; }, "canvas_rect_scale_x"),
            ("Y", 1f, () => cfg.Scale.Value.y, value => { var v = cfg.Scale.Value; v.y = value; cfg.Scale.Value = v; }, "canvas_rect_scale_y"),
            ("Z", 1f, () => cfg.Scale.Value.z, value => { var v = cfg.Scale.Value; v.z = value; cfg.Scale.Value = v; }, "canvas_rect_scale_z")
        ], "F2", "canvas_rect_scale");
        refreshPivotFields = FxNumericRow(basic, "Pivot", cfg.Pivot, [
            ("X", 0.5f, () => cfg.Pivot.Value.x, value => { var v = cfg.Pivot.Value; v.x = value; cfg.Pivot.Value = v; }, "canvas_rect_pivot_x"),
            ("Y", 0.5f, () => cfg.Pivot.Value.y, value => { var v = cfg.Pivot.Value; v.y = value; cfg.Pivot.Value = v; }, "canvas_rect_pivot_y")
        ], "F2", "canvas_rect_pivot");

        var (_, anchors) = Card("Anchors", false);
        FxNumericRow(anchors, "Min", cfg.AnchorMin, [
            ("X", 0f, () => cfg.AnchorMin.Value.x, value => { var v = cfg.AnchorMin.Value; v.x = value; cfg.AnchorMin.Value = v; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_min_x"),
            ("Y", 0f, () => cfg.AnchorMin.Value.y, value => { var v = cfg.AnchorMin.Value; v.y = value; cfg.AnchorMin.Value = v; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_min_y")
        ], "F2", "canvas_transform_anchor_min");
        FxNumericRow(anchors, "Max", cfg.AnchorMax, [
            ("X", 1f, () => cfg.AnchorMax.Value.x, value => { var v = cfg.AnchorMax.Value; v.x = value; cfg.AnchorMax.Value = v; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_max_x"),
            ("Y", 1f, () => cfg.AnchorMax.Value.y, value => { var v = cfg.AnchorMax.Value; v.y = value; cfg.AnchorMax.Value = v; refreshAnchor(); refreshPositionFields(); }, "canvas_transform_anchor_max_y")
        ], "F2", "canvas_transform_anchor_max");
    }

    private void BuildText(OvObject obj, TextMeshProUGUISettings cfg) {
        OvTextSettings textCfg = obj.Config.TextEngineConfig ??= OvTextSettings.FromLegacy(cfg.Text.Value);
        var (_, card) = ComponentCard("Text", cfg, () => {
            obj.Config.TextConfig = null;
            obj.Config.TextEngineConfig = null;
            obj.Config.ColorRangeConfig = null;
            RefreshComponents(obj);
        });
        CodeEditor(card, "Playing Text", "text_playing", textCfg.PlayingText.Value, value => {
            textCfg.PlayingText.Value = value;
            cfg.Text.Value = value;
            apply();
        }, () => obj.TextUpdater?.PlayingEngine);
        CodeEditor(card, "Not Playing Text", "text_not_playing", textCfg.NotPlayingText.Value, value => {
            textCfg.NotPlayingText.Value = value;
            apply();
        }, () => obj.TextUpdater?.NotPlayingEngine);
        FontDropDown(card, cfg);
        FxSlider(card, "Font Size", cfg.FontSize, 48f, 1f, 512f, "text_size", "F1");
        FxToggle(card, "Rich Text", cfg.RichText, true, "text_rich");
        FxToggle(card, "Auto Size", cfg.AutoSize, false, "text_auto_size");
        FxVector2(card, "Font Range", cfg.FontSizeRange, new Vector2(16, 64), 1f, 512f, "text_font_range", "F1");
        FxEnum(card, "Alignment", cfg.Alignment, TextAlignmentOptions.Center, "text_alignment");
        FxEnum(card, "Wrapping", cfg.TextWrappingMode, TextWrappingModes.Normal, "text_wrapping");
        FxEnum(card, "Overflow", cfg.OverFlowMode, TextOverflowModes.Overflow, "text_overflow");
        FxSlider(card, "Line Spacing", cfg.LineSpacing, 0f, -100f, 100f, "text_line_spacing", "F1");
        FxSlider(card, "Character Spacing", cfg.CharacterSpacing, 0f, -100f, 100f, "text_char_spacing", "F1");
        FxSlider(card, "Word Spacing", cfg.WordSpacing, 0f, -100f, 100f, "text_word_spacing", "F1");
        FxGradient(card, cfg.Color, Color.white, "text_color_fx");
        FxToggle(card, "Material Outline", cfg.EnableOutline, false, "text_outline");
        FxSlider(card, "Outline Width", cfg.OutlineWidth, 0.05f, 0f, 0.25f, "text_outline_width");
        FxSlider(card, "Outline Softness", cfg.OutlineSoftness, 0f, 0f, 1f, "text_outline_softness");
        FxSlider(card, "Face Dilate", cfg.FaceDilate, 0f, -1f, 1f, "text_face_dilate");
        FxColor(card, "Outline", cfg.OutlineColor, Color.black, "text_outline_color");
        FxToggle(card, "Material Shadow", cfg.EnableShadow, true, "text_shadow");
        FxVector2(card, "Shadow Offset", cfg.ShadowOffset, new Vector2(0.75f, -0.75f), -1f, 1f, "text_shadow_offset", "F2");
        FxSlider(card, "Shadow Dilate", cfg.ShadowDilate, 1f, 0f, 1f, "text_shadow_dilate");
        FxSlider(card, "Shadow Softness", cfg.ShadowSoftness, 0.5f, 0f, 1f, "text_shadow_softness");
        FxColor(card, "Shadow", cfg.ShadowColor, new Color(0f, 0f, 0f, 0.5f), "text_shadow_color");
    }

    private void BuildImage(OvObject obj, ImageSettings cfg) {
        var (_, card) = ComponentCard("Image", cfg, () => {
            obj.Config.ImageConfig = null;
            RefreshComponents(obj);
        });

        SpriteDropDown(card, cfg);
        FxColor(card, "Color", cfg.Color, Color.white, "image_color");
        FxToggle(card, "Raycast Target", cfg.RaycastTarget, true, "image_raycast");
        RectTransform preserveAspectRow = null;
        RectTransform useSpriteMeshRow = null;
        RectTransform fillCenterRow = null;
        RectTransform pixelsPerUnitRow = null;
        RectTransform fillMethodRow = null;
        RectTransform fillAmountRow = null;
        RectTransform fillOriginRow = null;
        RectTransform fillClockwiseRow = null;
        FxEnum(card, "Image Type", cfg.Type, Image.Type.Simple, "image_type", () => {
            RefreshImageTypeOptions(cfg.Type.Value);
            ApplyAndSave();
        });
        preserveAspectRow = FxToggle(card, "Preserve Aspect", cfg.PreserveAspect, false, "image_aspect");
        useSpriteMeshRow = FxToggle(card, "Use Sprite Mesh", cfg.UseSpriteMesh, false, "image_sprite_mesh");
        fillCenterRow = FxToggle(card, "Fill Center", cfg.FillCenter, true, "image_fill_center");
        pixelsPerUnitRow = FxSlider(card, "Pixels Per Unit", cfg.PixelsPerUnitMultiplier, 1f, 0f, 10f, "image_pixels_per_unit", "F2", ClampMode.Slider, value => Mathf.Max(0f, value));
        fillMethodRow = FxEnum(card, "Fill Method", cfg.FillMethod, Image.FillMethod.Horizontal, "image_fill_method", () => {
            cfg.FillOrigin.Value = 0;
            ApplyAndSave();
            rebuild();
        });
        fillAmountRow = FxSlider(card, "Fill Amount", cfg.FillAmount, 1f, 0f, 1f, "image_fill_amount");
        cfg.FillOrigin.Value = Mathf.Clamp(cfg.FillOrigin.Value, 0, cfg.FillMethod.Value is Image.FillMethod.Horizontal or Image.FillMethod.Vertical ? 1 : 3);
        fillOriginRow = CreateFillOriginRow();
        fillClockwiseRow = FxToggle(card, "Fill Clockwise", cfg.FillClockwise, true, "image_fill_clockwise");

        RectTransform CreateFillOriginRow() => cfg.FillMethod.Value switch {
            Image.FillMethod.Horizontal => FxEnumMapped(
                card, "Fill Origin", cfg.FillOrigin, (int)Image.OriginHorizontal.Left, "image_fill_origin",
                v => (Image.OriginHorizontal)v, v => (int)v
            ),
            Image.FillMethod.Vertical => FxEnumMapped(
                card, "Fill Origin", cfg.FillOrigin, (int)Image.OriginVertical.Bottom, "image_fill_origin",
                v => (Image.OriginVertical)v, v => (int)v
            ),
            Image.FillMethod.Radial90 => FxEnumMapped(
                card, "Fill Origin", cfg.FillOrigin, (int)Image.Origin90.BottomLeft, "image_fill_origin",
                v => (Image.Origin90)v, v => (int)v
            ),
            Image.FillMethod.Radial180 => FxEnumMapped(
                card, "Fill Origin", cfg.FillOrigin, (int)Image.Origin180.Bottom, "image_fill_origin",
                v => (Image.Origin180)v, v => (int)v
            ),
            _ => FxEnumMapped(
                card, "Fill Origin", cfg.FillOrigin, (int)Image.Origin360.Bottom, "image_fill_origin",
                v => (Image.Origin360)v, v => (int)v
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
            fillClockwiseRow.gameObject.SetActive(filled && cfg.FillMethod.Value is not Image.FillMethod.Horizontal and not Image.FillMethod.Vertical);
        }

        RefreshImageTypeOptions(cfg.Type.Value);
    }

    private void BuildMovingMan(OvObject obj, MovingManSettings cfg) {
        var (_, card) = ComponentCard("Moving Man", cfg, () => {
            obj.Config.MovingManConfig = null;
            RefreshComponents(obj);
        });

        FxInput(card, "Target Tag", cfg.TagName, null, "moving_man_tag");
        MovingManTargets(card, cfg);
        FxDoubleSlider(card, "Start Value", cfg.StartSize, 30, -10000f, 10000f, "moving_man_start", "F1");
        FxDoubleSlider(card, "End Value", cfg.EndSize, 80, -10000f, 10000f, "moving_man_end", "F1");
        FxDoubleSlider(card, "Default Value", cfg.DefaultSize, 30, -10000f, 10000f, "moving_man_default", "F1");
        FxDoubleSlider(card, "Speed", cfg.Speed, 800, 0f, 10000f, "moving_man_speed", "F0");
        FxToggle(card, "Invert", cfg.Invert, false, "moving_man_invert");
        FxEnum(card, "Ease", cfg.Ease, Easing.OutExpo, "moving_man_ease");
    }

    private void MovingManTargets(Transform parent, MovingManSettings cfg) {
        FxBlock(parent, "Target", cfg.Target, group => {
        string label = InspectorLabel("Target");
        var values = Enum.GetValues(typeof(MovingManTarget))
            .Cast<MovingManTarget>()
            .Where(value => value != MovingManTarget.None)
            .ToArray();
        var row = GenerateUI.Row(group, 50f);
        var dropdown = GenerateUI.MultiDropDown(
            row,
            MovingManTarget.TextSize,
            cfg.Target.Value,
            values,
            value => $"{label}: {value}",
            value => $"{label}: {MovingManTargetSummary(value, values)}",
            newValue => {
                cfg.Target.Value = newValue;
                ApplyAndSave();
            },
            "moving_man_target"
        );
        Track(dropdown);
        }, "moving_man_target", dropdown: true);
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

        FxInput(card, "Target Tag", cfg.TagName, null, "color_range_tag");
        FxDoubleSlider(card, "Minimum", cfg.Minimum, 0, -10000f, 10000f, "color_range_min", "F2");
        FxDoubleSlider(card, "Maximum", cfg.Maximum, 100, -10000f, 10000f, "color_range_max", "F2");
        FxGradient(card, cfg.MinimumColor, Color.black, "color_range_min_fx");
        FxGradient(card, cfg.MaximumColor, Color.white, "color_range_max_fx");
        FxEnum(card, "Ease", cfg.Ease, Easing.Linear, "color_range_ease");
    }

    private void BuildContentSizeFitter(OvObject obj, ContentSizeFitterSettings cfg) {
        var (_, card) = ComponentCard("Content Size Fitter", cfg, () => {
            obj.Config.ContentSizeFitterConfig = null;
            RefreshComponents(obj);
        }, () => RefreshDrivenLayout(obj));
        FxEnum(card, "Horizontal Fit", cfg.HorizontalFit, ContentSizeFitter.FitMode.PreferredSize, "content_size_horizontal", () => RefreshDrivenLayout(obj));
        FxEnum(card, "Vertical Fit", cfg.VerticalFit, ContentSizeFitter.FitMode.PreferredSize, "content_size_vertical", () => RefreshDrivenLayout(obj));
    }

    private void BuildShadow(OvObject obj, ShadowSettings cfg) {
        var (_, card) = ComponentCard("Shadow", cfg, () => {
            obj.Config.ShadowConfig = null;
            RefreshComponents(obj);
        });
        FxVector2(card, "Distance", cfg.EffectDistance, new Vector2(6, -6), -100f, 100f, "shadow_distance", "F1");
        FxColor(card, "Color", cfg.EffectColor, Color.black, "shadow_color");
        FxToggle(card, "Use Graphic Alpha", cfg.UseGraphicAlpha, true, "shadow_alpha");
    }

    private void BuildOutline(OvObject obj, OutlineSettings cfg) {
        var (_, card) = ComponentCard("Outline", cfg, () => {
            obj.Config.OutlineConfig = null;
            RefreshComponents(obj);
        });
        FxVector2(card, "Distance", cfg.EffectDistance, new Vector2(1, -1), -100f, 100f, "outline_distance", "F1");
        FxColor(card, "Color", cfg.EffectColor, Color.red, "outline_color");
        FxToggle(card, "Use Graphic Alpha", cfg.UseGraphicAlpha, true, "outline_alpha");
    }
    private void BuildMask(OvObject obj, MaskSettings cfg) {
        var (_, card) = ComponentCard("Mask", cfg, () => {
            obj.Config.MaskConfig = null;
            RefreshComponents(obj);
        });
        FxToggle(card, "Show Mask Graphic", cfg.ShowMaskGraphic, true, "mask_graphic");
    }

#if !IL2CPP
    private void BuildBoxCollider2D(OvObject obj, BoxCollider2DSettings cfg) {
        var (_, card) = ComponentCard("Box Collider 2D", cfg, () => {
            obj.Config.BoxCollider2DConfig = null;
            RefreshComponents(obj);
        });

        FxVector2(card, "Size", cfg.Size, Vector2.one, 0f, 1024f, "box_collider_size", "F1");
        FxVector2(card, "Offset", cfg.Offset, Vector2.zero, -1024f, 1024f, "box_collider_offset", "F1");
        FxToggle(card, "Is Trigger", cfg.IsTrigger, false, "box_collider_trigger");
        FxToggle(card, "Used By Effector", cfg.UsedByEffector, false, "box_collider_effector");
        FxEnum(card, "Composite Operation", cfg.CompositeOperation, Collider2D.CompositeOperation.None, "box_collider_composite");
        FxSlider(card, "Edge Radius", cfg.EdgeRadius, 0f, 0f, 100f, "box_collider_edge_radius", "F2");
    }

    private void BuildRigidbody2D(OvObject obj, Rigidbody2DSettings cfg) {
        var (_, card) = ComponentCard("Rigidbody 2D", cfg, () => {
            obj.Config.Rigidbody2DConfig = null;
            RefreshComponents(obj);
        });

        FxEnum(card, "Body Type", cfg.BodyType, RigidbodyType2D.Dynamic, "rigidbody2d_body_type");
        FxToggle(card, "Simulated", cfg.Simulated, true, "rigidbody2d_simulated");
        FxToggle(card, "Use Auto Mass", cfg.UseAutoMass, false, "rigidbody2d_auto_mass");
        FxSlider(card, "Mass", cfg.Mass, 1f, 0.01f, 1000f, "rigidbody2d_mass", "F2");
        FxSlider(card, "Linear Damping", cfg.LinearDamping, 0f, 0f, 100f, "rigidbody2d_linear_damping", "F2");
        FxSlider(card, "Angular Damping", cfg.AngularDamping, 0.05f, 0f, 100f, "rigidbody2d_angular_damping", "F2");
        FxSlider(card, "Gravity Scale", cfg.GravityScale, 1f, -100f, 100f, "rigidbody2d_gravity_scale", "F2");
        FxEnum(card, "Collision Detection", cfg.CollisionDetectionMode, CollisionDetectionMode2D.Discrete, "rigidbody2d_collision");
        FxEnum(card, "Sleep Mode", cfg.SleepMode, RigidbodySleepMode2D.StartAwake, "rigidbody2d_sleep_mode");
        FxEnum(card, "Interpolation", cfg.Interpolation, RigidbodyInterpolation2D.None, "rigidbody2d_interpolation");
        FxEnum(card, "Constraints", cfg.Constraints, RigidbodyConstraints2D.None, "rigidbody2d_constraints");
        FxToggle(card, "Freeze Rotation", cfg.FreezeRotation, false, "rigidbody2d_freeze_rotation");
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

        if(!obj.Config.HasRectMask2D.Value) {
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
                    obj.Config.HasRectMask2D.Value = true;
                    obj.Config.RectMask2DEnabled.Value = true;
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
        InspectorText($"COMPONENT_{componentKey}_{key}", InspectorText($"INSPECTOR_{key}", fallback));

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
        var built = GenerateUI.ComponentCard(content, InspectorText($"COMPONENT_{componentKey}", InspectorText($"INSPECTOR_{componentKey}", title)), settings.ComponentEnabled.Value, value => {
            settings.ComponentEnabled.Value = value;
            if(enabledChanged == null) {
                ApplyAndSave();
            } else {
                enabledChanged();
            }
        }, remove);
        var header = built.cardRect.Find("Header");
        var enabledFx = settings.ComponentEnabled;
        var fxButton = GenerateUI.Button(header, () => {
            enabledFx.UseFx = !enabledFx.UseFx;
            if(enabledFx.UseFx) enabledFx.EnsureEngine();
            ApplyAndSave();
            rebuild();
        }, MainCore.Spr.Get(UISprite.F128), "comp_enabled_" + componentKey, 5f);
        fxButton.Rect.SetSiblingIndex(1);
        var fxLayout = fxButton.Rect.gameObject.AddComponent<LayoutElement>();
        fxLayout.preferredWidth = 26f;
        fxLayout.preferredHeight = 30f;
        fxButton.NormalColor = enabledFx.UseFx ? UIColors.FxOn : UIColors.FxOff;
        fxButton.UpdateVisual(true);
        controls.Add(fxButton);
        if(enabledFx.UseFx) {
            var expressionRow = GenerateUI.Row(header, 30f);
            expressionRow.SetSiblingIndex(2);
            var expressionLayout = expressionRow.GetComponent<LayoutElement>();
            expressionLayout.preferredWidth = 160f;
            expressionLayout.flexibleWidth = 1f;
            var input = GenerateUI.Input(expressionRow, "", enabledFx.Expression, value => {
                enabledFx.Expression = value;
                apply();
            }, "Enabled", null, "comp_enabled_expr_" + componentKey, _ => save(), monospace: true);
            Track(input);
        }
        return built;
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

    private RectTransform Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format = "F2") => Slider(parent, label, defaultValue, min, max, value, changed, id, format, ClampMode.All, null);

    private RectTransform Slider(Transform parent, string label, float defaultValue, float min, float max, float value, Action<float> changed, string id, string format, ClampMode clampMode, Func<float, float> filter = null) {
        label = InspectorLabel(label);
        var row = GenerateUI.Row(parent, 50f);
        var slider = GenerateUI.Slider(row, defaultValue, min, max, value, format, clampMode, filter, newValue => {
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

    private static string transitioningFxId;

    private RectTransform FxBlock<T>(Transform parent, string label, FxValue<T> fx, Action<Transform> staticUI, string id, bool dropdown = false) {
        var group = CompactRow(parent, 50f, -10f);
        group.GetComponent<LayoutElement>().flexibleWidth = 1f;
        group.GetComponent<LayoutElement>().minWidth = 0f;
        group.GetComponent<HorizontalLayoutGroup>().reverseArrangement = true;
        group.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
        group.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
        if(dropdown) group.GetComponent<HorizontalLayoutGroup>().padding.right = 230;
        group.GetComponent<LayoutElement>().preferredHeight = -1f;
        var editor = VerticalGroup(group, 0f);
        editor.GetComponent<LayoutElement>().minWidth = 0f;
        if(fx.UseFx) {
            var row = GenerateUI.Row(editor, 50f);
            var input = GenerateUI.Input(row, "", fx.Expression, value => {
                fx.Expression = value;
                apply();
            }, InspectorLabel(label), null, id + "_fx", _ => save(), monospace: true);
            Track(input);
        } else {
            staticUI(editor);
        }
        if(dropdown && !fx.UseFx) {
            foreach(var rect in editor.GetComponentsInChildren<RectTransform>(true)) {
                if(rect.name != "Bg" || (rect.parent.name != "Dropdown" && rect.parent.name != "MultiDropdown")) continue;
                rect.sizeDelta = new Vector2(0f, 50f);
                rect.anchoredPosition = Vector2.zero;
            }
        }
        foreach(var image in editor.GetComponentsInChildren<Image>(true)) {
            if(image.color.Equals(UIColors.ObjectBG)) image.color = UIColors.FxField;
        }
        var fade = group.gameObject.AddComponent<CanvasGroup>();
        GTween transition = null;
        bool switching = false;
        if(transitioningFxId == id) {
            transitioningFxId = null;
            fade.alpha = 0f;
            transition = fade.GTFade(1f, 0.12f).SetEasing(Easing.OutSine);
            MainCore.TC.Play(transition);
        }
        var buttonSlot = GenerateUI.Row(group, 50f);
        buttonSlot.SetAsFirstSibling();
        var slotLayout = buttonSlot.GetComponent<LayoutElement>();
        slotLayout.minWidth = 30f;
        slotLayout.preferredWidth = 30f;
        slotLayout.flexibleWidth = 0f;
        slotLayout.flexibleHeight = 0f;
        var button = GenerateUI.Button(buttonSlot, () => {
            if(switching) return;
            switching = true;
            fade.interactable = false;
            transition?.Kill();
            transition = fade.GTFade(0f, 0.08f).SetEasing(Easing.OutSine).OnComplete(() => {
                fx.UseFx = !fx.UseFx;
                if(fx.UseFx) fx.EnsureEngine();
                ApplyAndSave();
                transitioningFxId = id;
                group.gameObject.SetActive(false);
                rebuild();
            });
            MainCore.TC.Play(transition);
        }, MainCore.Spr.Get(UISprite.F128), id + "_use_fx", 4f);
        button.Rect.anchorMin = new Vector2(0f, 1f);
        button.Rect.anchorMax = new Vector2(1f, 1f);
        button.Rect.pivot = new Vector2(0.5f, 1f);
        button.Rect.anchoredPosition = Vector2.zero;
        button.Rect.sizeDelta = new Vector2(0f, 50f);
        var width = button.Rect.gameObject.AddComponent<LayoutElement>();
        width.minWidth = 30f;
        width.preferredWidth = 30f;
        width.flexibleWidth = 0f;
        width.minHeight = 50f;
        width.preferredHeight = 50f;
        button.NormalColor = fx.UseFx ? UIColors.FxOn : UIColors.FxOff;
        button.Icon.color = Color.white;
        button.Icon.rectTransform.offsetMin = new Vector2(12f, 10f);
        button.Icon.rectTransform.offsetMax = new Vector2(-2f, -10f);
        button.UpdateVisual(true);
        button.OnDisposed += () => {
            transition?.Kill();
        };
        controls.Add(button);
        return group;
    }

    private RectTransform FxSlider(Transform parent, string label, FxValue<float> fx, float defaultValue, float min, float max, string id, string format = "F2") {
        return FxBlock(parent, label, fx, g => Slider(g, label, defaultValue, min, max, fx.Value, value => fx.Value = value, id, format), id);
    }

    private RectTransform FxSlider(Transform parent, string label, FxValue<float> fx, float defaultValue, float min, float max, string id, string format, ClampMode clampMode, Func<float, float> filter = null) {
        return FxBlock(parent, label, fx, g => Slider(g, label, defaultValue, min, max, fx.Value, value => fx.Value = value, id, format, clampMode, filter), id);
    }

    private RectTransform FxIntSlider(Transform parent, string label, FxValue<int> fx, int defaultValue, float min, float max, string id, string format = "F0") {
        return FxBlock(parent, label, fx, g => Slider(g, label, defaultValue, min, max, fx.Value, value => fx.Value = Mathf.RoundToInt(value), id, format), id);
    }

    private RectTransform FxDoubleSlider(Transform parent, string label, FxValue<double> fx, double defaultValue, float min, float max, string id, string format = "F1") {
        return FxBlock(parent, label, fx, g => Slider(g, label, (float)defaultValue, min, max, (float)fx.Value, value => fx.Value = value, id, format, ClampMode.Slider), id);
    }

    private RectTransform FxToggle(Transform parent, string label, FxValue<bool> fx, bool defaultValue, string id) {
        return FxBlock(parent, label, fx, g => Toggle(g, label, defaultValue, fx.Value, value => fx.Value = value, id), id);
    }

    private RectTransform FxInput(Transform parent, string label, FxValue<string> fx, string defaultValue, string id, Action<string> onChanged = null, Action finished = null) {
        return FxBlock(parent, label, fx, g => Input(g, label, defaultValue, fx.Value, value => {
            fx.Value = value;
            onChanged?.Invoke(value);
        }, id, finished), id);
    }

    private RectTransform FxEnum<T>(Transform parent, string label, FxValue<T> fx, T defaultValue, string id, Action completed = null) where T : struct, Enum {
        return FxBlock(parent, label, fx, g => EnumDropDown(g, label, defaultValue, fx.Value, value => fx.Value = value, id, completed), id, dropdown: true);
    }

    private RectTransform FxEnumMapped<TEnum>(Transform parent, string label, FxValue<int> fx, int defaultValue, string id, Func<int, TEnum> toEnum, Func<TEnum, int> fromEnum, Action completed = null) where TEnum : struct, Enum {
        return FxBlock(parent, label, fx, g => EnumDropDown(g, label, toEnum(defaultValue), toEnum(fx.Value), value => fx.Value = fromEnum(value), id, completed), id, dropdown: true);
    }

    private RectTransform FxVector2(Transform parent, string label, FxValue<Vector2> fx, Vector2 defaults, float min, float max, string id, string format = "F2") {
        return FxBlock(parent, label, fx, g => NumericPropertyRow(g, label, [
            ("X", defaults.x, () => fx.StaticValue.x, value => {
                var vector = fx.StaticValue;
                vector.x = Mathf.Clamp(value, min, max);
                fx.StaticValue = vector;
            }, id + "_x"),
            ("Y", defaults.y, () => fx.StaticValue.y, value => {
                var vector = fx.StaticValue;
                vector.y = Mathf.Clamp(value, min, max);
                fx.StaticValue = vector;
            }, id + "_y")
        ], format), id);
    }

    private RectTransform FxColor(Transform parent, string label, FxValue<Color> fx, Color defaults, string id) {
        return FxBlock(parent, label, fx, g => ColorSliders(g, label, defaults, () => fx.Value, value => fx.Value = value, id), id);
    }

    private RectTransform FxGradient(Transform parent, FxValue<GradientColor> fx, Color defaults, string idPrefix) {
        return FxBlock(parent, "Gradient", fx, g => {
            Toggle(g, "Gradient", false, !fx.Value.SolidColor, value => {
                var color = fx.Value;
                color.SolidColor = !value;
                fx.Value = color;
                ApplyAndSave();
                rebuild();
            }, idPrefix);
            if(fx.Value.SolidColor) {
                ColorSliders(g, "Color", defaults, () => fx.Value.TL, value => {
                    var color = fx.Value;
                    color.TL = value;
                    fx.Value = color;
                }, idPrefix + "_solid");
            } else {
                GradientColorSliders(g, () => fx.Value, value => fx.Value = value, idPrefix, defaults);
            }
        }, idPrefix);
    }

    private void FxSwitch<T>(Transform parent, string label, FxValue<T> fx, string id) {
        FxBlock(parent, label, fx, group => Label(group, InspectorLabel(label)), id);
    }

    private Action FxNumericRow(
        Transform parent,
        string label,
        FxValue<Vector2> fx,
        (string Label, float Default, Func<float> Get, Action<float> Set, string Id)[] fields,
        string format,
        string id
    ) {
        Action refresh = () => { };
        FxBlock(parent, label, fx, group => refresh = NumericPropertyRow(group, label, fields, format), id);
        return refresh;
    }

    private Action FxNumericRow3(
        Transform parent,
        string label,
        FxValue<Vector3> fx,
        (string Label, float Default, Func<float> Get, Action<float> Set, string Id)[] fields,
        string format,
        string id
    ) {
        Action refresh = () => { };
        FxBlock(parent, label, fx, group => refresh = NumericPropertyRow(group, label, fields, format), id);
        return refresh;
    }

    private void FxFloatRow(Transform parent, string label, FxValue<float> fx, float defaultValue, string fieldLabel, string id, string format) {
        FxBlock(parent, label, fx, group => {
            NumericPropertyRow(group, label, [
                (fieldLabel, defaultValue, () => fx.Value, value => fx.Value = value, id)
            ], format);
        }, id);
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
            ClampMode.None,
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
        bool StretchX() => !Mathf.Approximately(cfg.AnchorMin.Value.x, cfg.AnchorMax.Value.x);
        bool StretchY() => !Mathf.Approximately(cfg.AnchorMin.Value.y, cfg.AnchorMax.Value.y);
        bool DrivenX() => obj.Config.ContentSizeFitterConfig?.ComponentEnabled.Value == true
            && obj.Config.ContentSizeFitterConfig.HorizontalFit.Value != ContentSizeFitter.FitMode.Unconstrained;
        bool DrivenY() => obj.Config.ContentSizeFitterConfig?.ComponentEnabled.Value == true
            && obj.Config.ContentSizeFitterConfig.VerticalFit.Value != ContentSizeFitter.FitMode.Unconstrained;
        float PositionX() => DrivenX() ? obj.RectTransform.anchoredPosition.x : cfg.AnchoredPosition.Value.x;
        float PositionY() => DrivenY() ? obj.RectTransform.anchoredPosition.y : cfg.AnchoredPosition.Value.y;
        float SizeX() => DrivenX() ? obj.RectTransform.sizeDelta.x : cfg.SizeDelta.Value.x;
        float SizeY() => DrivenY() ? obj.RectTransform.sizeDelta.y : cfg.SizeDelta.Value.y;
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
                var v = cfg.AnchoredPosition.Value;
                v.x = value;
                cfg.AnchoredPosition.Value = v;
            }
        }, "transform_rect_x1", "F1");
        var firstY = NumericField(firstRow, "", 0f, () => StretchY() ? Top() : PositionY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMax(1, -value);
            } else {
                var v = cfg.AnchoredPosition.Value;
                v.y = value;
                cfg.AnchoredPosition.Value = v;
            }
        }, "transform_rect_y1", "F1");
        var secondX = NumericField(secondRow, "", 200f, () => StretchX() ? Right() : SizeX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMax(0, -value);
            } else {
                var v = cfg.SizeDelta.Value;
                v.x = value;
                cfg.SizeDelta.Value = v;
            }
        }, "transform_rect_x2", "F1");
        var secondY = NumericField(secondRow, "", 200f, () => StretchY() ? Bottom() : SizeY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMin(1, value);
            } else {
                var v = cfg.SizeDelta.Value;
                v.y = value;
                cfg.SizeDelta.Value = v;
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
        bool posFx = cfg.AnchoredPosition.UseFx;
        bool sizeFx = cfg.SizeDelta.UseFx;
        Field.SetBlocked((drivenX && StretchX()) || posFx || (StretchX() && sizeFx), true);
        secondX.Field.SetBlocked(drivenX || sizeFx || (StretchX() && posFx), true);
        firstY.Field.SetBlocked((drivenY && StretchY()) || posFx || (StretchY() && sizeFx), true);
        secondY.Field.SetBlocked(drivenY || sizeFx || (StretchY() && posFx), true);
        RefreshValues();

        if(drivenX || drivenY || posFx) {
            controls.Add(new UIWatcher("rect_transform_driven", fields, RefreshValues));
        }
        return RefreshValues;
    }

    private Action BuildRectPositionFields(Transform parent, RectTransformSettings cfg) {
        bool StretchX() => !Mathf.Approximately(cfg.AnchorMin.Value.x, cfg.AnchorMax.Value.x);
        bool StretchY() => !Mathf.Approximately(cfg.AnchorMin.Value.y, cfg.AnchorMax.Value.y);
        float PositionX() => cfg.AnchoredPosition.Value.x;
        float PositionY() => cfg.AnchoredPosition.Value.y;
        float SizeX() => cfg.SizeDelta.Value.x;
        float SizeY() => cfg.SizeDelta.Value.y;
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
                var v = cfg.AnchoredPosition.Value;
                v.x = value;
                cfg.AnchoredPosition.Value = v;
            }
        }, "transform_rect_x1", "F1");
        var firstY = NumericField(firstRow, "", 0f, () => StretchY() ? Top() : PositionY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMax(1, -value);
            } else {
                var v = cfg.AnchoredPosition.Value;
                v.y = value;
                cfg.AnchoredPosition.Value = v;
            }
        }, "transform_rect_y1", "F1");
        var secondX = NumericField(secondRow, "", 200f, () => StretchX() ? Right() : SizeX(), value => {
            if(StretchX()) {
                cfg.SetOffsetMax(0, -value);
            } else {
                var v = cfg.SizeDelta.Value;
                v.x = value;
                cfg.SizeDelta.Value = v;
            }
        }, "transform_rect_x2", "F1");
        var secondY = NumericField(secondRow, "", 200f, () => StretchY() ? Bottom() : SizeY(), value => {
            if(StretchY()) {
                cfg.SetOffsetMin(1, value);
            } else {
                var v = cfg.SizeDelta.Value;
                v.y = value;
                cfg.SizeDelta.Value = v;
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

        bool posFx = cfg.AnchoredPosition.UseFx;
        bool sizeFx = cfg.SizeDelta.UseFx;
        Field.SetBlocked(posFx || (StretchX() && sizeFx), true);
        secondX.Field.SetBlocked(sizeFx || (StretchX() && posFx), true);
        firstY.Field.SetBlocked(posFx || (StretchY() && sizeFx), true);
        secondY.Field.SetBlocked(sizeFx || (StretchY() && posFx), true);
        RefreshValues();
        if(posFx) {
            controls.Add(new UIWatcher("rect_transform_driven", fields, RefreshValues));
        }
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
        float min = cfg.AnchorMin.Value[axis];
        float max = cfg.AnchorMax.Value[axis];
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

        float oldMin = cfg.AnchorMin.Value[axis];
        float oldMax = cfg.AnchorMax.Value[axis];
        float oldPivot = cfg.Pivot.Value[axis];
        float newMin = mode == AnchorMode.Stretch ? 0f : mode switch { AnchorMode.Min => 0f, AnchorMode.Middle => 0.5f, _ => 1f };
        float newMax = mode == AnchorMode.Stretch ? 1f : newMin;
        float oldReference = Mathf.Lerp(oldMin, oldMax, oldPivot);
        float newReference = Mathf.Lerp(newMin, newMax, oldPivot);

        var anchoredPosition = cfg.AnchoredPosition.Value;
        anchoredPosition[axis] += (oldReference - newReference) * parentSize;
        cfg.AnchoredPosition.Value = anchoredPosition;
        var sizeDelta = cfg.SizeDelta.Value;
        sizeDelta[axis] += (oldMax - oldMin - (newMax - newMin)) * parentSize;
        cfg.SizeDelta.Value = sizeDelta;
        var anchorMin = cfg.AnchorMin.Value;
        anchorMin[axis] = newMin;
        cfg.AnchorMin.Value = anchorMin;
        var anchorMax = cfg.AnchorMax.Value;
        anchorMax[axis] = newMax;
        cfg.AnchorMax.Value = anchorMax;

        if(setPivot) {
            float newPivot = mode switch { AnchorMode.Min => 0f, AnchorMode.Max => 1f, _ => 0.5f };
            float rectSize = (parentSize * (newMax - newMin)) + cfg.SizeDelta.Value[axis];
            anchoredPosition = cfg.AnchoredPosition.Value;
            anchoredPosition[axis] += (newPivot - oldPivot) * rectSize;
            cfg.AnchoredPosition.Value = anchoredPosition;
            var pivot = cfg.Pivot.Value;
            pivot[axis] = newPivot;
            cfg.Pivot.Value = pivot;
        }

        if(setPosition) {
            anchoredPosition = cfg.AnchoredPosition.Value;
            anchoredPosition[axis] = 0f;
            cfg.AnchoredPosition.Value = anchoredPosition;
            sizeDelta = cfg.SizeDelta.Value;
            sizeDelta[axis] = mode == AnchorMode.Stretch ? 0f : visibleSize;
            cfg.SizeDelta.Value = sizeDelta;
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
        if(!string.IsNullOrEmpty(cfg.SpriteKey.Value) && !options.Contains(cfg.SpriteKey.Value)) {
            options.Insert(0, cfg.SpriteKey.Value);
        }
        options.Insert(0, none);

        string current = string.IsNullOrEmpty(cfg.SpriteKey.Value) ? none : cfg.SpriteKey.Value;
        FxBlock(parent, "Sprite", cfg.SpriteKey, group => {
        var row = GenerateUI.Row(group, 50f);
        var dropdown = GenerateUI.DropDown(row, none, current, options, option => $"{InspectorLabel("Sprite")}: {InspectorLabel(option)}", selected => {
            cfg.SpriteKey.Value = selected == none ? null : selected;
            ApplyAndSave();
        }, "image_sprite");
        Track(dropdown);
        }, "image_sprite", dropdown: true);
    }

    private void FontDropDown(Transform parent, TextMeshProUGUISettings cfg) {
        const string none = "Default";
        var options = UserResourceManager.Fnt.Keys.OrderBy(key => key).ToList();
        if(!string.IsNullOrEmpty(cfg.FontKey.Value) && !options.Contains(cfg.FontKey.Value)) {
            options.Insert(0, cfg.FontKey.Value);
        }
        options.Insert(0, none);

        string current = string.IsNullOrEmpty(cfg.FontKey.Value) ? none : cfg.FontKey.Value;
        FxBlock(parent, "Font", cfg.FontKey, group => {
        var row = GenerateUI.Row(group, 50f);
        var dropdown = GenerateUI.DropDown(row, none, current, options, option => $"{InspectorLabel("Font")}: {InspectorLabel(option)}", selected => {
            cfg.FontKey.Value = selected == none ? null : selected;
            ApplyAndSave();
        }, "text_font");
        Track(dropdown);
        }, "text_font", dropdown: true);
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
