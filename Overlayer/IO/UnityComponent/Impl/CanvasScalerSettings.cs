using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.CanvasScaler;

namespace Overlayer.IO.UnityComponent.Impl;

public class CanvasScalerSettings : UnityComponentSettingsBase, ICopyable<CanvasScalerSettings> {
    public FxValue<CanvasScaler.ScaleMode> UiScaleMode = new(CanvasScaler.ScaleMode.ScaleWithScreenSize);
    public FxValue<Vector2> ReferenceResolution = new(new Vector2(1920, 1080));
    public FxValue<float> MatchWidthOrHeight = new(0.5f);
    public FxValue<ScreenMatchMode> ScreenMatchMode = new(CanvasScaler.ScreenMatchMode.Expand);
    public FxValue<float> ScaleFactor = new(1f);
    public FxValue<float> DynamicPixelsPerUnit = new(1f);

    private CanvasScaler.ScaleMode _lastUiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    private Vector2 _lastReferenceResolution = new(1920, 1080);
    private float _lastMatchWidthOrHeight = 0.5f;
    private ScreenMatchMode _lastScreenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    private float _lastScaleFactor = 1f;
    private float _lastDynamicPixelsPerUnit = 1f;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(UiScaleMode)
        || FxUtil.HasFx(ReferenceResolution)
        || FxUtil.HasFx(MatchWidthOrHeight)
        || FxUtil.HasFx(ScreenMatchMode)
        || FxUtil.HasFx(ScaleFactor)
        || FxUtil.HasFx(DynamicPixelsPerUnit);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<CanvasScaler>();
        if(com == null) {
            return false;
        }

        _lastUiScaleMode = UiScaleMode.Value;
        _lastReferenceResolution = ReferenceResolution.Value;
        _lastMatchWidthOrHeight = MatchWidthOrHeight.Value;
        _lastScreenMatchMode = ScreenMatchMode.Value;
        _lastScaleFactor = ScaleFactor.Value;
        _lastDynamicPixelsPerUnit = DynamicPixelsPerUnit.Value;
        com.uiScaleMode = _lastUiScaleMode;
        com.referenceResolution = _lastReferenceResolution;
        com.matchWidthOrHeight = _lastMatchWidthOrHeight;
        com.screenMatchMode = _lastScreenMatchMode;
        com.scaleFactor = _lastScaleFactor;
        com.dynamicPixelsPerUnit = _lastDynamicPixelsPerUnit;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<CanvasScaler>();
        if(com == null) {
            return false;
        }

        UiScaleMode.Value = com.uiScaleMode;
        ReferenceResolution.Value = com.referenceResolution;
        MatchWidthOrHeight.Value = com.matchWidthOrHeight;
        ScreenMatchMode.Value = com.screenMatchMode;
        ScaleFactor.Value = com.scaleFactor;
        DynamicPixelsPerUnit.Value = com.dynamicPixelsPerUnit;
        _lastUiScaleMode = com.uiScaleMode;
        _lastReferenceResolution = com.referenceResolution;
        _lastMatchWidthOrHeight = com.matchWidthOrHeight;
        _lastScreenMatchMode = com.screenMatchMode;
        _lastScaleFactor = com.scaleFactor;
        _lastDynamicPixelsPerUnit = com.dynamicPixelsPerUnit;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<CanvasScaler>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastUiScaleMode, UiScaleMode.Value, v => com.uiScaleMode = v);
        FxUtil.ApplyIfChanged(ref _lastReferenceResolution, ReferenceResolution.Value, v => com.referenceResolution = v);
        FxUtil.ApplyIfChanged(ref _lastMatchWidthOrHeight, MatchWidthOrHeight.Value, v => com.matchWidthOrHeight = v);
        FxUtil.ApplyIfChanged(ref _lastScreenMatchMode, ScreenMatchMode.Value, v => com.screenMatchMode = v);
        FxUtil.ApplyIfChanged(ref _lastScaleFactor, ScaleFactor.Value, v => com.scaleFactor = v);
        FxUtil.ApplyIfChanged(ref _lastDynamicPixelsPerUnit, DynamicPixelsPerUnit.Value, v => com.dynamicPixelsPerUnit = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(UiScaleMode)] = IOUtils.WriteFx(UiScaleMode),
            [nameof(ReferenceResolution)] = IOUtils.WriteFx(ReferenceResolution),
            [nameof(MatchWidthOrHeight)] = IOUtils.WriteFx(MatchWidthOrHeight),
            [nameof(ScreenMatchMode)] = IOUtils.WriteFx(ScreenMatchMode),
            [nameof(ScaleFactor)] = IOUtils.WriteFx(ScaleFactor),
            [nameof(DynamicPixelsPerUnit)] = IOUtils.WriteFx(DynamicPixelsPerUnit)
        };
    }

    public override void Deserialize(JToken token) {
        UiScaleMode = IOUtils.ReadFx(token, nameof(UiScaleMode), UiScaleMode);
        ReferenceResolution = IOUtils.ReadFx(token, nameof(ReferenceResolution), ReferenceResolution);
        MatchWidthOrHeight = IOUtils.ReadFx(token, nameof(MatchWidthOrHeight), MatchWidthOrHeight);
        ScreenMatchMode = IOUtils.ReadFx(token, nameof(ScreenMatchMode), ScreenMatchMode);
        ScaleFactor = IOUtils.ReadFx(token, nameof(ScaleFactor), ScaleFactor);
        DynamicPixelsPerUnit = IOUtils.ReadFx(token, nameof(DynamicPixelsPerUnit), DynamicPixelsPerUnit);
    }

    public CanvasScalerSettings Copy() {
        return new CanvasScalerSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            UiScaleMode = UiScaleMode?.Copy(),
            ReferenceResolution = ReferenceResolution?.Copy(),
            MatchWidthOrHeight = MatchWidthOrHeight?.Copy(),
            ScreenMatchMode = ScreenMatchMode?.Copy(),
            ScaleFactor = ScaleFactor?.Copy(),
            DynamicPixelsPerUnit = DynamicPixelsPerUnit?.Copy()
        };
    }
}
