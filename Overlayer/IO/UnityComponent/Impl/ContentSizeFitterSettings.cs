using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ContentSizeFitterSettings : UnityComponentSettingsBase, ICopyable<ContentSizeFitterSettings> {
    public FxValue<ContentSizeFitter.FitMode> HorizontalFit = new(ContentSizeFitter.FitMode.PreferredSize);
    public FxValue<ContentSizeFitter.FitMode> VerticalFit = new(ContentSizeFitter.FitMode.PreferredSize);

    private ContentSizeFitter.FitMode _lastHorizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    private ContentSizeFitter.FitMode _lastVerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(HorizontalFit)
        || FxUtil.HasFx(VerticalFit);

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<ContentSizeFitter>();
        if(component == null) {
            return false;
        }

        _lastHorizontalFit = HorizontalFit.Value;
        _lastVerticalFit = VerticalFit.Value;
        component.horizontalFit = _lastHorizontalFit;
        component.verticalFit = _lastVerticalFit;
        ToUnity(component);
        LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<ContentSizeFitter>();
        if(component == null) {
            return false;
        }

        HorizontalFit.Value = component.horizontalFit;
        VerticalFit.Value = component.verticalFit;
        _lastHorizontalFit = component.horizontalFit;
        _lastVerticalFit = component.verticalFit;
        FromUnity(component);
        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var component = target.GetComponent<ContentSizeFitter>();
        if(component == null) {
            return;
        }

        RefreshEnabled(component);
        bool changed = false;
        changed |= FxUtil.ApplyIfChanged(ref _lastHorizontalFit, HorizontalFit.Value, v => component.horizontalFit = v);
        changed |= FxUtil.ApplyIfChanged(ref _lastVerticalFit, VerticalFit.Value, v => component.verticalFit = v);
        if(changed) {
            LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        }
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(HorizontalFit)] = IOUtils.WriteFx(HorizontalFit),
            [nameof(VerticalFit)] = IOUtils.WriteFx(VerticalFit)
        });
    }

    public override void Deserialize(JToken token) {
        if(token == null) {
            return;
        }

        DeserializeComponent(token);
        HorizontalFit = IOUtils.ReadFx(token, nameof(HorizontalFit), HorizontalFit);
        VerticalFit = IOUtils.ReadFx(token, nameof(VerticalFit), VerticalFit);
    }

    public ContentSizeFitterSettings Copy() {
        return new ContentSizeFitterSettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            HorizontalFit = HorizontalFit?.Copy(),
            VerticalFit = VerticalFit?.Copy()
        };
    }
}
