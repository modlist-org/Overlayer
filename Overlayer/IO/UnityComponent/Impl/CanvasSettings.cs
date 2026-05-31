using Newtonsoft.Json.Linq;
using Overlayer.IO.UnityComponent;
using UnityEngine;

namespace Overlayer.IO.Overlay;

public class CanvasSettings : UnityComponentSettingsBase {
    public RenderMode RenderMode = RenderMode.ScreenSpaceOverlay;
    public int SortingOrder = 32760;
    public bool PixelPerfect = false;

    public override void ToUnity(GameObject target) {
        var com = target.GetComponent<Canvas>();
        com.renderMode = RenderMode;
        com.sortingOrder = SortingOrder;
        com.pixelPerfect = PixelPerfect;
    }

    public override void FromUnity(GameObject source) {
        var com = source.GetComponent<Canvas>();
        RenderMode = com.renderMode;
        SortingOrder = com.sortingOrder;
        PixelPerfect = com.pixelPerfect;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(RenderMode)] = IOUtils.WriteEnum(RenderMode),
            [nameof(SortingOrder)] = SortingOrder,
            [nameof(PixelPerfect)] = PixelPerfect,
        };
    }

    public override void Deserialize(JToken token) {
        RenderMode = IOUtils.ReadEnum(token, nameof(RenderMode), RenderMode);
        SortingOrder = IOUtils.Read(token, nameof(SortingOrder), SortingOrder);
        PixelPerfect = IOUtils.Read(token, nameof(PixelPerfect), PixelPerfect);
    }
}