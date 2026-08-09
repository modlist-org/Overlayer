using Overlayer.Core;
using Overlayer.Compat.OVC;
using Overlayer.Tween;
using Overlayer.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public sealed class UIColorPicker : UIObject {
    private const int TextureSize = 256;
    private const float RingInner = 0.37f;
    private const float RingOuter = 0.49f;
    private const float TriangleRadius = 0.32f;

    public Color DefaultValue { get; }
    public Color Value { get; private set; }
    public bool Expanded { get; private set; }

    private readonly RectTransform hostRow;
    private readonly LayoutElement hostLayout;
    private readonly GameObject body;
    private readonly RectTransform bodyRect;
    private readonly CanvasGroup bodyCanvas;
    private readonly Image preview;
    private readonly Image triangle;
    private readonly RectTransform triangleRect;
    private readonly RectTransform wheelRect;
    private readonly RectTransform hueHandle;
    private readonly RectTransform colorHandle;
    private readonly UIInput hexInput;
    private readonly Image hexOutline;
    private readonly UISlider[] sliders;
    private readonly Image rgbModeBackground;
    private readonly Image hsvModeBackground;
    private readonly TextMeshProUGUI rgbModeLabel;
    private readonly TextMeshProUGUI hsvModeLabel;
    private readonly Action<Color> onChanged;
    private readonly Action<Color> onComplete;
    private readonly Texture2D texture;
    private readonly Sprite textureSprite;

    private float hue;
    private float saturation;
    private float brightness;
    private float renderedHue = -1f;
    private bool suppressHex;
    private Color? pendingHexColor;
    private bool hsvMode;
    private DragTarget dragTarget;
    private GTween layoutTween, validationTween;

    private enum DragTarget { None, Hue, Triangle }

    public UIColorPicker(
        string id,
        RectTransform rect,
        RectTransform hostRow,
        GameObject body,
        CanvasGroup bodyCanvas,
        Image preview,
        Image triangle,
        RectTransform triangleRect,
        RectTransform wheelRect,
        RectTransform hueHandle,
        RectTransform colorHandle,
        UIInput hexInput,
        Image sharedOutline,
        UISlider[] sliders,
        Image rgbModeBackground,
        TextMeshProUGUI rgbModeLabel,
        Image hsvModeBackground,
        TextMeshProUGUI hsvModeLabel,
        Color defaultValue,
        Color value,
        Action<Color> onChanged,
        Action<Color> onComplete
    ) : base(id, rect) {
        this.hostRow = hostRow;
        hostLayout = hostRow.GetComponent<LayoutElement>();
        this.body = body;
        bodyRect = body.GetComponent<RectTransform>();
        this.bodyCanvas = bodyCanvas;
        this.preview = preview;
        this.triangle = triangle;
        this.triangleRect = triangleRect;
        this.wheelRect = wheelRect;
        this.hueHandle = hueHandle;
        this.colorHandle = colorHandle;
        this.sliders = sliders;
        this.rgbModeBackground = rgbModeBackground;
        this.rgbModeLabel = rgbModeLabel;
        this.hsvModeBackground = hsvModeBackground;
        this.hsvModeLabel = hsvModeLabel;
        this.hexInput = hexInput;
        hexOutline = sharedOutline;
        this.onChanged = onChanged;
        this.onComplete = onComplete;
        DefaultValue = defaultValue;

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) {
            name = $"ColorPicker_{id}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        textureSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
        wheelRect.GetComponent<Image>().sprite = textureSprite;

        Set(value, false);
        SetMode(false);
        SetExpanded(false, true);
    }

    public void ToggleExpanded() => SetExpanded(!Expanded);

    public void SetExpanded(bool expanded, bool noAnimate = false) {
        if(IsDisposed) {
            return;
        }

        layoutTween?.Kill();
        Expanded = expanded;
        if(expanded) {
            body.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);
            UpdateHandles();
        }
        bodyCanvas.interactable = expanded;
        bodyCanvas.blocksRaycasts = expanded;

        const float collapsedHeight = 50f;
        const float expandedHeight = 578f;
        float targetHeight = expanded ? expandedHeight : collapsedHeight;
        float targetAlpha = expanded ? 1f : 0f;
        Vector3 targetRotation = expanded ? new Vector3(0f, 0f, 180f) : Vector3.zero;

        void Rebuild(float height) {
            if(hostLayout) {
                hostLayout.preferredHeight = height;
            }

            if(hostRow) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(hostRow);
            }

            if(hostRow && hostRow.parent is RectTransform parent) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }
        }

        if(noAnimate) {
            Rebuild(targetHeight);
            bodyCanvas.alpha = targetAlpha;
            triangleRect.localRotation = Quaternion.Euler(targetRotation);
            body.SetActive(expanded);
            return;
        }

        layoutTween = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(
                () => hostLayout ? hostLayout.preferredHeight : targetHeight,
                Rebuild,
                targetHeight,
                0.22f
            ).SetEasing(Easing.OutBack))
            .Join(GTweenExtensions.Tween(
                () => bodyCanvas.alpha,
                value => bodyCanvas.alpha = value,
                targetAlpha,
                0.16f
            ).SetEasing(Easing.OutSine))
            .Join(triangleRect.GTRotate(targetRotation, 0.4f).SetEasing(Easing.OutBack))
            .Build()
            .OnComplete(() => {
                if(!Expanded) {
                    body.SetActive(false);
                }
            });
        MainCore.TC.Play(layoutTween);
    }

    public void Reset() {
        Set(DefaultValue);
        onComplete?.Invoke(Value);
    }

    public void Set(Color value, bool invoke = true) {
        if(IsDisposed) {
            return;
        }

        value.r = Mathf.Clamp01(value.r);
        value.g = Mathf.Clamp01(value.g);
        value.b = Mathf.Clamp01(value.b);
        value.a = Mathf.Clamp01(value.a);
        Value = value;
        Color.RGBToHSV(value, out hue, out saturation, out brightness);
        UpdateVisuals();
        if(invoke) {
            onChanged?.Invoke(Value);
        }
    }

    public void SetMode(bool useHsv) {
        if(IsDisposed) {
            return;
        }

        hsvMode = useHsv;
        rgbModeBackground.color = useHsv ? Color.clear : UIColors.ObjectActive;
        hsvModeBackground.color = useHsv ? UIColors.ObjectActive : Color.clear;
        rgbModeLabel.color = useHsv ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
        hsvModeLabel.color = useHsv ? Color.white : new Color(1f, 1f, 1f, 0.55f);

        string[] labels = useHsv ? ["H", "S", "V", "A"] : ["R", "G", "B", "A"];
        Color.RGBToHSV(DefaultValue, out float defaultHue, out float defaultSaturation, out float defaultBrightness);
        float[] defaults = useHsv
            ? [defaultHue, defaultSaturation, defaultBrightness, DefaultValue.a]
            : [DefaultValue.r, DefaultValue.g, DefaultValue.b, DefaultValue.a];
        Color[] colors = useHsv
            ? [
                Color.HSVToRGB(hue, 1f, 1f),
                new Color(0.38f, 0.78f, 1f, 1f),
                new Color(1f, 0.82f, 0.35f, 1f),
                new Color(0.45f, 0.45f, 0.45f, 1f)
            ]
            : [
                new Color(1f, 0.42f, 0.44f, 1f),
                new Color(0.48f, 0.82f, 0.48f, 1f),
                new Color(0.56f, 0.56f, 0.9f, 1f),
                new Color(0.45f, 0.45f, 0.45f, 1f)
            ];
        for(int i = 0; i < sliders.Length; i++) {
            sliders[i].Label.text = labels[i];
            sliders[i].FillImage.color = colors[i];
            sliders[i].SetDefaultValue(defaults[i], true);
        }
        UpdateSliderValues();
    }

    public void SetChannel(int channel, float value) {
        value = Mathf.Clamp01(value);
        if(!hsvMode) {
            Color color = Value;
            color[channel] = value;
            Set(color);
            return;
        }

        switch(channel) {
            case 0:
                hue = value;
                break;
            case 1:
                saturation = value;
                break;
            case 2:
                brightness = value;
                break;
            case 3:
                Value = new Color(Value.r, Value.g, Value.b, value);
                UpdateVisuals();
                onChanged?.Invoke(Value);
                return;
        }
        Color rgb = Color.HSVToRGB(hue, saturation, brightness);
        rgb.a = Value.a;
        Value = rgb;
        UpdateVisuals();
        onChanged?.Invoke(Value);
    }

    public void BeginPointer(BaseEventData data) {
        if(!TryGetPointerPosition(data, out Vector2 normalized)) {
            return;
        }

        float distance = normalized.magnitude;
        if(distance > 0.56f) {
            return;
        }

        dragTarget = distance >= RingInner - 0.03f ? DragTarget.Hue : DragTarget.Triangle;
        ApplyPointer(normalized);
    }

    public void DragPointer(BaseEventData data) {
        if(dragTarget == DragTarget.None || !TryGetPointerPosition(data, out Vector2 normalized)) {
            return;
        }

        ApplyPointer(normalized);
    }

    public void EndPointer(BaseEventData data) {
        if(dragTarget == DragTarget.None) {
            return;
        }

        dragTarget = DragTarget.None;
        onComplete?.Invoke(Value);
    }

    private bool TryGetPointerPosition(BaseEventData data, out Vector2 normalized) {
#pragma warning disable IDE0019
        PointerEventData pointer =
#pragma warning restore IDE0019
#if ML && IL2CPP
            data.TryCast<PointerEventData>();
#else
            data as PointerEventData;
#endif
        normalized = default;
        if(pointer == null || pointer.button != PointerEventData.InputButton.Left) {
            return false;
        }

        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRect, pointer.position, pointer.pressEventCamera, out Vector2 local
        )) {
            return false;
        }

        float radius = Mathf.Min(wheelRect.rect.width, wheelRect.rect.height);
        if(radius <= 0f) {
            return false;
        }

        normalized = local / radius;
        return true;
    }

    private void ApplyPointer(Vector2 normalized) {
        if(dragTarget == DragTarget.Hue) {
            hue = Mathf.Repeat(Mathf.Atan2(normalized.y, normalized.x) / (Mathf.PI * 2f), 1f);
            Color rgb = Color.HSVToRGB(hue, saturation, brightness);
            rgb.a = Value.a;
            Value = rgb;
            UpdateVisuals();
            onChanged?.Invoke(Value);
            return;
        }

        Vector2 huePoint = Direction(hue) * TriangleRadius;
        Vector2 whitePoint = Direction(hue + (1f / 3f)) * TriangleRadius;
        Vector2 blackPoint = Direction(hue - (1f / 3f)) * TriangleRadius;
        Vector2 point = ClosestPointOnTriangle(normalized, huePoint, whitePoint, blackPoint);
        if(!Barycentric(point, huePoint, whitePoint, blackPoint, out Vector3 weights)) {
            return;
        }

        saturation = weights.x / Mathf.Max(weights.x + weights.y, 0.0001f);
        brightness = Mathf.Clamp01(weights.x + weights.y);
        Color picked = Color.HSVToRGB(hue, saturation, brightness);
        picked.a = Value.a;
        Value = picked;
        UpdateVisuals();
        onChanged?.Invoke(Value);
    }

    public void ValidateHex(string text) {
        if(suppressHex) {
            return;
        }

        string candidate = text.StartsWith("#") ? text : "#" + text;
        bool validLength = candidate.Length is 7 or 9;
        Color parsed = default;
        bool valid = validLength && ColorUtility.TryParseHtmlString(candidate, out parsed);
        pendingHexColor = valid ? parsed : null;
        preview.color = valid ? parsed : Value;

        Color stateColor = valid
            ? UIColors.ObjectActiveMathOk
            : IsPartialHex(text)
                ? UIColors.ObjectActiveMathWarn
                : UIColors.ObjectActiveMathErr;
        SetHexValidationColor(stateColor);
    }

    public void CompleteHex(string text) {
        ValidateHex(text);
        if(pendingHexColor.HasValue) {
            Set(pendingHexColor.Value);
            onComplete?.Invoke(Value);
        } else {
            SetHexText();
        }
        pendingHexColor = null;
        SetHexValidationColor(UIColors.ObjectActive, true);
    }

    private void UpdateVisuals() {
        preview.color = Value;
        triangle.color = Value;
        UpdateSliderValues();
        SetHexText();
        UpdateTexture();
        UpdateHandles();
    }

    private void UpdateSliderValues() {
        float[] values = hsvMode
            ? [hue, saturation, brightness, Value.a]
            : [Value.r, Value.g, Value.b, Value.a];
        for(int i = 0; i < sliders.Length; i++) {
            sliders[i].Set(values[i], false);
        }

        if(hsvMode) {
            sliders[0].FillImage.color = Color.HSVToRGB(hue, 1f, 1f);
        }
    }

    private void SetHexText() {
        string value = ColorUtils.ToHtmlStringRGBA(Value);
        suppressHex = true;
        hexInput.Set(value, false);
        suppressHex = false;
    }

    private void SetHexValidationColor(Color color, bool resetText = false) {
        if(hexOutline) {
            validationTween?.Kill();
            float alpha = 1f;
            if(resetText) {
                RectTransform header = hexOutline.rectTransform.parent as RectTransform;
                Camera camera = UICore.Canvas ? UICore.Canvas.worldCamera : null;
                alpha = header && RectTransformUtility.RectangleContainsScreenPoint(
                    header, OVC_Input.MousePosition, camera
                ) ? 1f : 0f;
            }
            validationTween = hexOutline
                .GTColor(new Color(color.r, color.g, color.b, alpha), 0.2f)
                .SetEasing(Easing.OutSine);
            MainCore.TC.Play(validationTween);
        }
        hexInput.InputField.textComponent.color = Color.white;
    }

    private static bool IsPartialHex(string text) {
        if(string.IsNullOrEmpty(text)) {
            return true;
        }

        int start = text[0] == '#' ? 1 : 0;
        int length = text.Length - start;
        if(length > 8) {
            return false;
        }

        for(int i = start; i < text.Length; i++) {
            char c = text[i];
            bool isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if(!isHex) {
                return false;
            }
        }
        return true;
    }

    private void UpdateHandles() {
        float size = Mathf.Min(wheelRect.rect.width, wheelRect.rect.height);
        hueHandle.anchoredPosition = Direction(hue) * (size * 0.43f);

        float hueWeight = saturation * brightness;
        float whiteWeight = (1f - saturation) * brightness;
        float blackWeight = 1f - brightness;
        Vector2 position = Direction(hue) * hueWeight;
        position += Direction(hue + (1f / 3f)) * whiteWeight;
        position += Direction(hue - (1f / 3f)) * blackWeight;
        colorHandle.anchoredPosition = position * (size * TriangleRadius);
    }

    private void UpdateTexture() {
        if(renderedHue >= 0f && Mathf.Abs(Mathf.DeltaAngle(renderedHue * 360f, hue * 360f)) < 0.5f) {
            return;
        }

        renderedHue = hue;
        Color hueColor = Color.HSVToRGB(hue, 1f, 1f);
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        Vector2 huePoint = Direction(hue) * TriangleRadius;
        Vector2 whitePoint = Direction(hue + (1f / 3f)) * TriangleRadius;
        Vector2 blackPoint = Direction(hue - (1f / 3f)) * TriangleRadius;

        for(int y = 0; y < TextureSize; y++) {
            for(int x = 0; x < TextureSize; x++) {
                Vector2 point = new(
                    ((x + 0.5f) / TextureSize) - 0.5f,
                    ((y + 0.5f) / TextureSize) - 0.5f
                );
                float distance = point.magnitude;
                Color color = Color.clear;
                if(distance is >= RingInner and <= RingOuter) {
                    float angle = Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2f), 1f);
                    color = Color.HSVToRGB(angle, 1f, 1f);
                } else if(Barycentric(point, huePoint, whitePoint, blackPoint, out Vector3 weights)) {
                    color = (hueColor * weights.x) + (Color.white * weights.y) + (Color.black * weights.z);
                    color.a = 1f;
                }
                pixels[(y * TextureSize) + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static Vector2 Direction(float turns) {
        float radians = turns * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static bool Barycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights) {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = point - a;
        float denominator = (v0.x * v1.y) - (v1.x * v0.y);
        if(Mathf.Abs(denominator) < 0.00001f) {
            weights = default;
            return false;
        }
        float y = ((v2.x * v1.y) - (v1.x * v2.y)) / denominator;
        float z = ((v0.x * v2.y) - (v2.x * v0.y)) / denominator;
        float x = 1f - y - z;
        weights = new Vector3(x, y, z);
        return x >= 0f && y >= 0f && z >= 0f;
    }

    private static Vector2 ClosestPointOnTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c) {
        if(Barycentric(point, a, b, c, out _)) {
            return point;
        }

        Vector2 ab = ClosestPointOnSegment(point, a, b);
        Vector2 bc = ClosestPointOnSegment(point, b, c);
        Vector2 ca = ClosestPointOnSegment(point, c, a);
        float abDistance = (point - ab).sqrMagnitude;
        float bcDistance = (point - bc).sqrMagnitude;
        float caDistance = (point - ca).sqrMagnitude;
        if(abDistance <= bcDistance && abDistance <= caDistance) {
            return ab;
        }

        return bcDistance <= caDistance ? bc : ca;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b) {
        Vector2 segment = b - a;
        float length = segment.sqrMagnitude;
        if(length <= 0.00001f) {
            return a;
        }

        return a + (segment * Mathf.Clamp01(Vector2.Dot(point - a, segment) / length));
    }

    public override void Dispose() {
        if(IsDisposed) {
            return;
        }

        layoutTween?.Kill();
        validationTween?.Kill();
        layoutTween = null;
        validationTween = null;
        foreach(UISlider slider in sliders) {
            slider.Dispose();
        }

        hexInput.Dispose();
        UnityEngine.Object.Destroy(textureSprite);
        UnityEngine.Object.Destroy(texture);
        base.Dispose();
    }
}
