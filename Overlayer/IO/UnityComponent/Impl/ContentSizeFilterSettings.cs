using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.UnityComponent.Impl;

public class ContentSizeFilterSettings : UnityComponentSettingsBase, ICopyable<ContentSizeFilterSettings> {
    public ContentSizeFitter.FitMode HorizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    public ContentSizeFitter.FitMode VerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<ContentSizeFitter>();
        if(com == null) {
            return false;
        }

        com.horizontalFit = HorizontalFit;
        com.verticalFit = VerticalFit;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<ContentSizeFitter>();
        if(com == null) {
            return false;
        }

        HorizontalFit = com.horizontalFit;
        VerticalFit = com.verticalFit;

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(HorizontalFit)] = IOUtils.WriteEnum(HorizontalFit),
            [nameof(VerticalFit)] = IOUtils.WriteEnum(VerticalFit)
        };
    }

    public override void Deserialize(JToken token) {
        if(token == null) {
            return;
        }

        HorizontalFit = IOUtils.ReadEnum(token, nameof(HorizontalFit), HorizontalFit);
        VerticalFit = IOUtils.ReadEnum(token, nameof(VerticalFit), VerticalFit);
    }

    public ContentSizeFilterSettings Copy() {
        return new ContentSizeFilterSettings {
            HorizontalFit = HorizontalFit,
            VerticalFit = VerticalFit
        };
    }
}