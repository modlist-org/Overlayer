using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class VerticalLayoutGroupSettings : UnityComponentSettingsBase, ICopyable<VerticalLayoutGroupSettings> {
    public FxValue<float> Spacing = new(0f);
    public FxValue<Vector2> PaddingH = new(Vector2.zero);
    public FxValue<Vector2> PaddingV = new(Vector2.zero);
    public FxValue<TextAnchor> ChildAlignment = new(TextAnchor.UpperLeft);
    public FxValue<bool> ChildControlWidth = new(true);
    public FxValue<bool> ChildControlHeight = new(true);
    public FxValue<bool> ChildForceExpandWidth = new(true);
    public FxValue<bool> ChildForceExpandHeight = new(true);
    public FxValue<bool> ChildScaleWidth = new(false);
    public FxValue<bool> ChildScaleHeight = new(false);
    public FxValue<bool> ReverseArrangement = new(false);

    private float _lastSpacing;
    private Vector2 _lastPaddingH;
    private Vector2 _lastPaddingV;
    private TextAnchor _lastChildAlignment = TextAnchor.UpperLeft;
    private bool _lastChildControlWidth = true;
    private bool _lastChildControlHeight = true;
    private bool _lastChildForceExpandWidth = true;
    private bool _lastChildForceExpandHeight = true;
    private bool _lastChildScaleWidth;
    private bool _lastChildScaleHeight;
    private bool _lastReverseArrangement;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Spacing)
        || FxUtil.HasFx(PaddingH)
        || FxUtil.HasFx(PaddingV)
        || FxUtil.HasFx(ChildAlignment)
        || FxUtil.HasFx(ChildControlWidth)
        || FxUtil.HasFx(ChildControlHeight)
        || FxUtil.HasFx(ChildForceExpandWidth)
        || FxUtil.HasFx(ChildForceExpandHeight)
        || FxUtil.HasFx(ChildScaleWidth)
        || FxUtil.HasFx(ChildScaleHeight)
        || FxUtil.HasFx(ReverseArrangement);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<VerticalLayoutGroup>();
        if (component == null) {
            return false;
        }

        _lastSpacing = Spacing.Value;
        _lastPaddingH = PaddingH.Value;
        _lastPaddingV = PaddingV.Value;
        _lastChildAlignment = ChildAlignment.Value;
        _lastChildControlWidth = ChildControlWidth.Value;
        _lastChildControlHeight = ChildControlHeight.Value;
        _lastChildForceExpandWidth = ChildForceExpandWidth.Value;
        _lastChildForceExpandHeight = ChildForceExpandHeight.Value;
        _lastChildScaleWidth = ChildScaleWidth.Value;
        _lastChildScaleHeight = ChildScaleHeight.Value;
        _lastReverseArrangement = ReverseArrangement.Value;
        ApplyValues(component);
        ToUnity(component);
        LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        return true;
    }

    private void ApplyValues(VerticalLayoutGroup component) {
        component.spacing = _lastSpacing;
        component.padding.left = Mathf.RoundToInt(_lastPaddingH.x);
        component.padding.right = Mathf.RoundToInt(_lastPaddingH.y);
        component.padding.top = Mathf.RoundToInt(_lastPaddingV.x);
        component.padding.bottom = Mathf.RoundToInt(_lastPaddingV.y);
        component.childAlignment = _lastChildAlignment;
        component.childControlWidth = _lastChildControlWidth;
        component.childControlHeight = _lastChildControlHeight;
        component.childForceExpandWidth = _lastChildForceExpandWidth;
        component.childForceExpandHeight = _lastChildForceExpandHeight;
        component.childScaleWidth = _lastChildScaleWidth;
        component.childScaleHeight = _lastChildScaleHeight;
        component.reverseArrangement = _lastReverseArrangement;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<VerticalLayoutGroup>();
        if (component == null) {
            return false;
        }

        Spacing.Value = component.spacing;
        PaddingH.Value = new Vector2(component.padding.left, component.padding.right);
        PaddingV.Value = new Vector2(component.padding.top, component.padding.bottom);
        ChildAlignment.Value = component.childAlignment;
        ChildControlWidth.Value = component.childControlWidth;
        ChildControlHeight.Value = component.childControlHeight;
        ChildForceExpandWidth.Value = component.childForceExpandWidth;
        ChildForceExpandHeight.Value = component.childForceExpandHeight;
        ChildScaleWidth.Value = component.childScaleWidth;
        ChildScaleHeight.Value = component.childScaleHeight;
        ReverseArrangement.Value = component.reverseArrangement;
        _lastSpacing = Spacing.Value;
        _lastPaddingH = PaddingH.Value;
        _lastPaddingV = PaddingV.Value;
        _lastChildAlignment = ChildAlignment.Value;
        _lastChildControlWidth = ChildControlWidth.Value;
        _lastChildControlHeight = ChildControlHeight.Value;
        _lastChildForceExpandWidth = ChildForceExpandWidth.Value;
        _lastChildForceExpandHeight = ChildForceExpandHeight.Value;
        _lastChildScaleWidth = ChildScaleWidth.Value;
        _lastChildScaleHeight = ChildScaleHeight.Value;
        _lastReverseArrangement = ReverseArrangement.Value;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if (!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<VerticalLayoutGroup>();
        if (component == null) {
            return;
        }

        RefreshEnabled(component);
        bool changed = false;
        changed |= FxUtil.ApplyIfChanged(ref _lastSpacing, Spacing.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastPaddingH, PaddingH.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastPaddingV, PaddingV.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildAlignment, ChildAlignment.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildControlWidth, ChildControlWidth.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildControlHeight, ChildControlHeight.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildForceExpandWidth, ChildForceExpandWidth.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildForceExpandHeight, ChildForceExpandHeight.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildScaleWidth, ChildScaleWidth.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastChildScaleHeight, ChildScaleHeight.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastReverseArrangement, ReverseArrangement.Value, _ => { });
        if (changed) {
            ApplyValues(component);
            LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        }
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Spacing)] = IOUtils.WriteFx(Spacing),
            [nameof(PaddingH)] = IOUtils.WriteFx(PaddingH),
            [nameof(PaddingV)] = IOUtils.WriteFx(PaddingV),
            [nameof(ChildAlignment)] = IOUtils.WriteFx(ChildAlignment),
            [nameof(ChildControlWidth)] = IOUtils.WriteFx(ChildControlWidth),
            [nameof(ChildControlHeight)] = IOUtils.WriteFx(ChildControlHeight),
            [nameof(ChildForceExpandWidth)] = IOUtils.WriteFx(ChildForceExpandWidth),
            [nameof(ChildForceExpandHeight)] = IOUtils.WriteFx(ChildForceExpandHeight),
            [nameof(ChildScaleWidth)] = IOUtils.WriteFx(ChildScaleWidth),
            [nameof(ChildScaleHeight)] = IOUtils.WriteFx(ChildScaleHeight),
            [nameof(ReverseArrangement)] = IOUtils.WriteFx(ReverseArrangement)
        });
    }

    public override void Deserialize(JToken token) {
        if (token == null) {
            return;
        }

        DeserializeComponent(token);
        Spacing = IOUtils.ReadFx(token, nameof(Spacing), Spacing);
        PaddingH = IOUtils.ReadFx(token, nameof(PaddingH), PaddingH);
        PaddingV = IOUtils.ReadFx(token, nameof(PaddingV), PaddingV);
        ChildAlignment = IOUtils.ReadFx(token, nameof(ChildAlignment), ChildAlignment);
        ChildControlWidth = IOUtils.ReadFx(token, nameof(ChildControlWidth), ChildControlWidth);
        ChildControlHeight = IOUtils.ReadFx(token, nameof(ChildControlHeight), ChildControlHeight);
        ChildForceExpandWidth = IOUtils.ReadFx(token, nameof(ChildForceExpandWidth), ChildForceExpandWidth);
        ChildForceExpandHeight = IOUtils.ReadFx(token, nameof(ChildForceExpandHeight), ChildForceExpandHeight);
        ChildScaleWidth = IOUtils.ReadFx(token, nameof(ChildScaleWidth), ChildScaleWidth);
        ChildScaleHeight = IOUtils.ReadFx(token, nameof(ChildScaleHeight), ChildScaleHeight);
        ReverseArrangement = IOUtils.ReadFx(token, nameof(ReverseArrangement), ReverseArrangement);
    }

    public VerticalLayoutGroupSettings Copy() {
        return new VerticalLayoutGroupSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Spacing = Spacing?.Copy(),
            PaddingH = PaddingH?.Copy(),
            PaddingV = PaddingV?.Copy(),
            ChildAlignment = ChildAlignment?.Copy(),
            ChildControlWidth = ChildControlWidth?.Copy(),
            ChildControlHeight = ChildControlHeight?.Copy(),
            ChildForceExpandWidth = ChildForceExpandWidth?.Copy(),
            ChildForceExpandHeight = ChildForceExpandHeight?.Copy(),
            ChildScaleWidth = ChildScaleWidth?.Copy(),
            ChildScaleHeight = ChildScaleHeight?.Copy(),
            ReverseArrangement = ReverseArrangement?.Copy()
        };
    }
}
