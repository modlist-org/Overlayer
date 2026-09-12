using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
namespace Overlayer.IO.UnityComponent.Impl;

public class CanvasGroupSettings : UnityComponentSettingsBase, ICopyable<CanvasGroupSettings> {
    public FxValue<float> Alpha = new(1f);
    public FxValue<bool> Interactable = new(false);
    public FxValue<bool> BlocksRaycasts = new(false);
    public FxValue<bool> IgnoreParentGroups = new(false);

    private float _lastAlpha = 1f;
    private bool _lastInteractable;
    private bool _lastBlocksRaycasts;
    private bool _lastIgnoreParentGroups;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Alpha)
        || FxUtil.HasFx(Interactable)
        || FxUtil.HasFx(BlocksRaycasts)
        || FxUtil.HasFx(IgnoreParentGroups);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<CanvasGroup>();
        if(com == null) {
            return false;
        }

        _lastAlpha = Alpha.Value;
        _lastInteractable = Interactable.Value;
        _lastBlocksRaycasts = BlocksRaycasts.Value;
        _lastIgnoreParentGroups = IgnoreParentGroups.Value;
        com.alpha = _lastAlpha;
        com.interactable = _lastInteractable;
        com.blocksRaycasts = _lastBlocksRaycasts;
        com.ignoreParentGroups = _lastIgnoreParentGroups;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<CanvasGroup>();
        if(com == null) {
            return false;
        }

        Alpha.Value = com.alpha;
        Interactable.Value = com.interactable;
        BlocksRaycasts.Value = com.blocksRaycasts;
        IgnoreParentGroups.Value = com.ignoreParentGroups;
        _lastAlpha = com.alpha;
        _lastInteractable = com.interactable;
        _lastBlocksRaycasts = com.blocksRaycasts;
        _lastIgnoreParentGroups = com.ignoreParentGroups;

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<CanvasGroup>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        FxUtil.ApplyIfChanged(ref _lastAlpha, Alpha.Value, v => com.alpha = v);
        FxUtil.ApplyIfChanged(ref _lastInteractable, Interactable.Value, v => com.interactable = v);
        FxUtil.ApplyIfChanged(ref _lastBlocksRaycasts, BlocksRaycasts.Value, v => com.blocksRaycasts = v);
        FxUtil.ApplyIfChanged(ref _lastIgnoreParentGroups, IgnoreParentGroups.Value, v => com.ignoreParentGroups = v);
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Alpha)] = IOUtils.WriteFx(Alpha),
            [nameof(Interactable)] = IOUtils.WriteFx(Interactable),
            [nameof(BlocksRaycasts)] = IOUtils.WriteFx(BlocksRaycasts),
            [nameof(IgnoreParentGroups)] = IOUtils.WriteFx(IgnoreParentGroups)
        };
    }

    public override void Deserialize(JToken token) {
        Alpha = IOUtils.ReadFx(token, nameof(Alpha), Alpha);
        Interactable = IOUtils.ReadFx(token, nameof(Interactable), Interactable);
        BlocksRaycasts = IOUtils.ReadFx(token, nameof(BlocksRaycasts), BlocksRaycasts);
        IgnoreParentGroups = IOUtils.ReadFx(token, nameof(IgnoreParentGroups), IgnoreParentGroups);
    }

    public CanvasGroupSettings Copy() {
        return new CanvasGroupSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Alpha = Alpha?.Copy(),
            Interactable = Interactable?.Copy(),
            BlocksRaycasts = BlocksRaycasts?.Copy(),
            IgnoreParentGroups = IgnoreParentGroups?.Copy()
        };
    }
}
