using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.IO.Overlay;

public sealed class OvCanvas : ISettingsFile {
    public int SortingOrder = 32760;
    public bool PixelPerfect = false;

    public CanvasScaler.ScaleMode UiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    public Vector2 ReferenceResolution = new(1920, 1080);
    public float MatchWidthOrHeight = 0.5f;
    public CanvasScaler.ScreenMatchMode ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    public float ScaleFactor = 1f;
    public float DynamicPixelsPerUnit = 1f;

    public JToken Serialize() {
        return new JObject {
            [nameof(SortingOrder)] = SortingOrder,
            [nameof(PixelPerfect)] = PixelPerfect,

            [nameof(UiScaleMode)] = IOUtils.WriteEnum(UiScaleMode),
            [nameof(ReferenceResolution)] = IOUtils.Write(ReferenceResolution),
            [nameof(MatchWidthOrHeight)] = MatchWidthOrHeight,
            [nameof(ScreenMatchMode)] = (int)ScreenMatchMode,
            [nameof(ScaleFactor)] = ScaleFactor,
            [nameof(DynamicPixelsPerUnit)] = DynamicPixelsPerUnit
        };
    }

    public void Deserialize(JToken token) {
        SortingOrder = IOUtils.Read(token, nameof(SortingOrder), SortingOrder);
        PixelPerfect = IOUtils.Read(token, nameof(PixelPerfect), PixelPerfect);

        UiScaleMode = IOUtils.ReadEnum(token, nameof(UiScaleMode), UiScaleMode);
        ReferenceResolution = IOUtils.Read(token, nameof(ReferenceResolution), ReferenceResolution);
        MatchWidthOrHeight = IOUtils.Read(token, nameof(MatchWidthOrHeight), MatchWidthOrHeight);
        ScreenMatchMode = IOUtils.ReadEnum(token, nameof(ScreenMatchMode), ScreenMatchMode);
        ScaleFactor = IOUtils.Read(token, nameof(ScaleFactor), ScaleFactor);
        DynamicPixelsPerUnit = IOUtils.Read(token, nameof(DynamicPixelsPerUnit), DynamicPixelsPerUnit);
    }

    public void ToUnity(Canvas canvas, CanvasScaler scaler) {
        canvas.sortingOrder = SortingOrder;
        canvas.pixelPerfect = PixelPerfect;

        scaler.uiScaleMode = UiScaleMode;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;
        scaler.screenMatchMode = ScreenMatchMode;
        scaler.scaleFactor = ScaleFactor;
        scaler.dynamicPixelsPerUnit = DynamicPixelsPerUnit;
    }

    public void FromUnity(Canvas canvas, CanvasScaler scaler) {
        SortingOrder = canvas.sortingOrder;
        PixelPerfect = canvas.pixelPerfect;

        UiScaleMode = scaler.uiScaleMode;
        ReferenceResolution = scaler.referenceResolution;
        MatchWidthOrHeight = scaler.matchWidthOrHeight;
        ScreenMatchMode = scaler.screenMatchMode;
        ScaleFactor = scaler.scaleFactor;
        DynamicPixelsPerUnit = scaler.dynamicPixelsPerUnit;
    }
}