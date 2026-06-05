using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
using UnityEngine;

namespace Overlayer.IO.Overlay;

public sealed class OvCanvasSettings : ISettingsFile, ICopyable<OvCanvasSettings> {
    public string Name = "OvCanvas";
    public RectTransformSettings RectTransformConfig = new() {
        AnchorMin = Vector2.zero,
        AnchorMax = Vector2.one,
        OffsetMin = Vector2.zero,
        OffsetMax = Vector2.zero,
        Pivot = new Vector2(0.5f, 0.5f)
    };
    public CanvasGroupSettings CanvasGroupConfig = new() {
        BlocksRaycasts = true,
    };
    public CanvasSettings CanvasConfig = new();
    public CanvasScalerSettings CanvasScalerConfig = new();
    public GraphicRaycasterSettings GraphicRaycasterConfig = new();

    public JToken Serialize() {
        return new JObject {
            [nameof(Name)] = Name,
            [nameof(RectTransformConfig)] = RectTransformConfig.Serialize(),
            [nameof(CanvasConfig)] = CanvasConfig.Serialize(),
            [nameof(CanvasScalerConfig)] = CanvasScalerConfig.Serialize(),
            [nameof(GraphicRaycasterConfig)] = GraphicRaycasterConfig.Serialize(),
        };
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            return;
        }

        Name = IOUtils.Read(obj, nameof(Name), Name);
        RectTransformConfig.Deserialize(obj[nameof(RectTransformConfig)]);
        CanvasConfig.Deserialize(obj[nameof(CanvasConfig)]);
        CanvasScalerConfig.Deserialize(obj[nameof(CanvasScalerConfig)]);
        GraphicRaycasterConfig.Deserialize(obj[nameof(GraphicRaycasterConfig)]);
    }

    public OvCanvasSettings Copy() {
        return new OvCanvasSettings {
            Name = Name,
            RectTransformConfig = RectTransformConfig?.Copy(),
            CanvasConfig = CanvasConfig?.Copy(),
            CanvasScalerConfig = CanvasScalerConfig?.Copy(),
            GraphicRaycasterConfig = GraphicRaycasterConfig?.Copy()
        };
    }
}