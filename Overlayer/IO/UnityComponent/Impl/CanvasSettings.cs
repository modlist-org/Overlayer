using Newtonsoft.Json.Linq;
using Overlayer.IO.UnityComponent;
using UnityEngine;

namespace Overlayer.IO.Overlay;

public class CanvasSettings : UnityComponentSettingsBase {
    public RenderMode RenderMode = RenderMode.ScreenSpaceOverlay;
    public int SortingOrder = 32760;
    public bool PixelPerfect = false;

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<Canvas>();
        if (com == null) {
            return false;
        }
        
        com.renderMode = RenderMode;
        com.sortingOrder = SortingOrder;
        com.pixelPerfect = PixelPerfect;
        
        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<Canvas>();
        if (com == null) {
            return false;
        }
        
        RenderMode = com.renderMode;
        SortingOrder = com.sortingOrder;
        PixelPerfect = com.pixelPerfect;
        
        return true;
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