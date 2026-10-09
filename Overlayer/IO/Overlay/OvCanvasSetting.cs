using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
using UnityEngine;

namespace Overlayer.IO.Overlay;

public sealed class OvCanvasSettings : ISettingsFile, ICopyable<OvCanvasSettings> {
    public FxValue<string> Name = FxValue<string>.FromValue("OvCanvas");
    public FxValue<bool> Enabled = new(true);
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

    public bool HasAnyFx => FxUtil.HasFx(Name)
        || FxUtil.HasFx(Enabled)
        || (RectTransformConfig?.HasAnyFx ?? false)
        || (CanvasGroupConfig?.HasAnyFx ?? false)
        || (CanvasConfig?.HasAnyFx ?? false)
        || (CanvasScalerConfig?.HasAnyFx ?? false)
        || (GraphicRaycasterConfig?.HasAnyFx ?? false);

    public JToken Serialize() {
        return new JObject {
            [nameof(Name)] = IOUtils.WriteFx(Name),
            [nameof(Enabled)] = IOUtils.WriteFx(Enabled),
            [nameof(RectTransformConfig)] = RectTransformConfig.Serialize(),
            [nameof(CanvasGroupConfig)] = CanvasGroupConfig.Serialize(),
            [nameof(CanvasConfig)] = CanvasConfig.Serialize(),
            [nameof(CanvasScalerConfig)] = CanvasScalerConfig.Serialize(),
            [nameof(GraphicRaycasterConfig)] = GraphicRaycasterConfig.Serialize(),
        };
    }

    public void Deserialize(JToken token) {
        if (token is not JObject obj) {
            return;
        }

        Name = IOUtils.ReadFx(obj, nameof(Name), Name);
        Enabled = IOUtils.ReadFx(obj, nameof(Enabled), Enabled);
        RectTransformConfig.Deserialize(obj[nameof(RectTransformConfig)]);
        if (obj[nameof(CanvasGroupConfig)] != null) {
            CanvasGroupConfig.Deserialize(obj[nameof(CanvasGroupConfig)]);
        }
        CanvasConfig.Deserialize(obj[nameof(CanvasConfig)]);
        CanvasScalerConfig.Deserialize(obj[nameof(CanvasScalerConfig)]);
        GraphicRaycasterConfig.Deserialize(obj[nameof(GraphicRaycasterConfig)]);
    }

    public OvCanvasSettings Copy() {
        return new OvCanvasSettings {
            Name = Name?.Copy(),
            Enabled = Enabled?.Copy(),
            RectTransformConfig = RectTransformConfig?.Copy(),
            CanvasGroupConfig = CanvasGroupConfig?.Copy(),
            CanvasConfig = CanvasConfig?.Copy(),
            CanvasScalerConfig = CanvasScalerConfig?.Copy(),
            GraphicRaycasterConfig = GraphicRaycasterConfig?.Copy()
        };
    }
}
