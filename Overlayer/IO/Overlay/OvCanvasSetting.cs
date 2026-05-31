using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;

namespace Overlayer.IO.Overlay;

public sealed class OvCanvasSettings : ISettingsFile {
    public string Name = "OvCanvas";
    public RectTransformSettings RectTransformConfig = new();
    public CanvasSettings CanvasConfig = new();
    public CanvasScalerSettings CanvasScalerConfig = new();
    public GraphicRaycasterSettings GraphicRaycasterConfig = new();

    public List<OvObjectSettings> OvObjectConfigs = [];

    public JToken Serialize() {
        return new JObject {
            [nameof(Name)] = Name,
            [nameof(RectTransformConfig)] = RectTransformConfig.Serialize(),
            [nameof(CanvasConfig)] = CanvasConfig.Serialize(),
            [nameof(CanvasScalerConfig)] = CanvasScalerConfig.Serialize(),
            [nameof(GraphicRaycasterConfig)] = GraphicRaycasterConfig.Serialize(),
            [nameof(OvObjectConfigs)] = new JArray(OvObjectConfigs.Select(x => x.Serialize()))
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

        OvObjectConfigs.Clear();
        if(obj[nameof(OvObjectConfigs)] is JArray arr) {
            foreach(var item in arr) {
                var o = new OvObjectSettings();
                o.Deserialize(item);
                OvObjectConfigs.Add(o);
            }
        }
    }
}