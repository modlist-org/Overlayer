using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class CanvasSettings : UnityComponentSettingsBase, ICopyable<CanvasSettings> {
    public FxValue<RenderMode> RenderMode = new(UnityEngine.RenderMode.ScreenSpaceOverlay);
    public FxValue<int> SortingOrder = new(32760);
    public FxValue<bool> PixelPerfect = new(false);
    public FxValue<bool> OverrideSorting = new(true);

    private RenderMode _lastRenderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
    private int _lastSortingOrder = 32760;
    private bool _lastPixelPerfect;
    private bool _lastOverrideSorting = true;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(RenderMode)
        || FxUtil.HasFx(SortingOrder)
        || FxUtil.HasFx(PixelPerfect)
        || FxUtil.HasFx(OverrideSorting);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Canvas>();
        if(com == null) {
            return false;
        }

        _lastRenderMode = RenderMode.Value;
        _lastSortingOrder = SortingOrder.Value;
        _lastPixelPerfect = PixelPerfect.Value;
        _lastOverrideSorting = OverrideSorting.Value;
        com.renderMode = _lastRenderMode;
        com.sortingOrder = _lastSortingOrder;
        com.pixelPerfect = _lastPixelPerfect;
        com.overrideSorting = _lastOverrideSorting;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Canvas>();
        if(com == null) {
            return false;
        }

        RenderMode.Value = com.renderMode;
        SortingOrder.Value = com.sortingOrder;
        PixelPerfect.Value = com.pixelPerfect;
        OverrideSorting.Value = com.overrideSorting;
        _lastRenderMode = com.renderMode;
        _lastSortingOrder = com.sortingOrder;
        _lastPixelPerfect = com.pixelPerfect;
        _lastOverrideSorting = com.overrideSorting;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<Canvas>();
        if(com == null) {
            return;
        }

        FxUtil.ApplyIfChanged(ref _lastRenderMode, RenderMode.Value, v => com.renderMode = v);
        FxUtil.ApplyIfChanged(ref _lastSortingOrder, SortingOrder.Value, v => com.sortingOrder = v);
        FxUtil.ApplyIfChanged(ref _lastPixelPerfect, PixelPerfect.Value, v => com.pixelPerfect = v);
        FxUtil.ApplyIfChanged(ref _lastOverrideSorting, OverrideSorting.Value, v => com.overrideSorting = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(RenderMode)] = IOUtils.WriteFx(RenderMode),
            [nameof(SortingOrder)] = IOUtils.WriteFx(SortingOrder),
            [nameof(PixelPerfect)] = IOUtils.WriteFx(PixelPerfect),
            [nameof(OverrideSorting)] = IOUtils.WriteFx(OverrideSorting),
        };
    }

    public override void Deserialize(JToken token) {
        RenderMode = IOUtils.ReadFx(token, nameof(RenderMode), RenderMode);
        SortingOrder = IOUtils.ReadFx(token, nameof(SortingOrder), SortingOrder);
        PixelPerfect = IOUtils.ReadFx(token, nameof(PixelPerfect), PixelPerfect);
        OverrideSorting = IOUtils.ReadFx(token, nameof(OverrideSorting), OverrideSorting);
    }

    public CanvasSettings Copy() {
        return new CanvasSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            RenderMode = RenderMode?.Copy(),
            SortingOrder = SortingOrder?.Copy(),
            PixelPerfect = PixelPerfect?.Copy(),
            OverrideSorting = OverrideSorting?.Copy(),
        };
    }
}
