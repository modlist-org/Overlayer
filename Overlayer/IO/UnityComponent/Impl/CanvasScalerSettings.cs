using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.CanvasScaler;

namespace Overlayer.IO.UnityComponent.Impl;

public class CanvasScalerSettings : UnityComponentSettingsBase {
    public CanvasScaler.ScaleMode UiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    public Vector2 ReferenceResolution = new(1920, 1080);
    public float MatchWidthOrHeight = 0.5f;
    public ScreenMatchMode ScreenMatchMode = ScreenMatchMode.Expand;
    public float ScaleFactor = 1f;
    public float DynamicPixelsPerUnit = 1f;

    public override void ToUnity(GameObject target) {
        var com = target.GetComponent<CanvasScaler>();
        com.uiScaleMode = UiScaleMode;
        com.referenceResolution = ReferenceResolution;
        com.matchWidthOrHeight = MatchWidthOrHeight;
        com.screenMatchMode = ScreenMatchMode;
        com.scaleFactor = ScaleFactor;
        com.dynamicPixelsPerUnit = DynamicPixelsPerUnit;
    }

    public override void FromUnity(GameObject source) {
        var com = source.GetComponent<CanvasScaler>();
        UiScaleMode = com.uiScaleMode;
        ReferenceResolution = com.referenceResolution;
        MatchWidthOrHeight = com.matchWidthOrHeight;
        ScreenMatchMode = com.screenMatchMode;
        ScaleFactor = com.scaleFactor;
        DynamicPixelsPerUnit = com.dynamicPixelsPerUnit;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(UiScaleMode)] = IOUtils.WriteEnum(UiScaleMode),
            [nameof(ReferenceResolution)] = IOUtils.Write(ReferenceResolution),
            [nameof(MatchWidthOrHeight)] = MatchWidthOrHeight,
            [nameof(ScreenMatchMode)] = (int)ScreenMatchMode,
            [nameof(ScaleFactor)] = ScaleFactor,
            [nameof(DynamicPixelsPerUnit)] = DynamicPixelsPerUnit
        };
    }

    public override void Deserialize(JToken token) {
        UiScaleMode = IOUtils.ReadEnum(token, nameof(UiScaleMode), UiScaleMode);
        ReferenceResolution = IOUtils.Read(token, nameof(ReferenceResolution), ReferenceResolution);
        MatchWidthOrHeight = IOUtils.Read(token, nameof(MatchWidthOrHeight), MatchWidthOrHeight);
        ScreenMatchMode = IOUtils.ReadEnum(token, nameof(ScreenMatchMode), ScreenMatchMode);
        ScaleFactor = IOUtils.Read(token, nameof(ScaleFactor), ScaleFactor);
        DynamicPixelsPerUnit = IOUtils.Read(token, nameof(DynamicPixelsPerUnit), DynamicPixelsPerUnit);
    }
}