using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ContentSizeFitterSettings : UnityComponentSettingsBase, ICopyable<ContentSizeFitterSettings> {
    public ContentSizeFitter.FitMode HorizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    public ContentSizeFitter.FitMode VerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    public override bool ToUnity(GameObject target) {
        var component = target.GetComponent<ContentSizeFitter>();
        if(component == null) return false;

        component.horizontalFit = HorizontalFit;
        component.verticalFit = VerticalFit;
        LayoutRebuilder.MarkLayoutForRebuild(target.GetComponent<RectTransform>());
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var component = source.GetComponent<ContentSizeFitter>();
        if(component == null) return false;

        HorizontalFit = component.horizontalFit;
        VerticalFit = component.verticalFit;
        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(HorizontalFit)] = IOUtils.WriteEnum(HorizontalFit),
            [nameof(VerticalFit)] = IOUtils.WriteEnum(VerticalFit)
        };
    }

    public override void Deserialize(JToken token) {
        if(token == null) return;
        HorizontalFit = IOUtils.ReadEnum(token, nameof(HorizontalFit), HorizontalFit);
        VerticalFit = IOUtils.ReadEnum(token, nameof(VerticalFit), VerticalFit);
    }

    public ContentSizeFitterSettings Copy() {
        return new ContentSizeFitterSettings {
            HorizontalFit = HorizontalFit,
            VerticalFit = VerticalFit
        };
    }
}
