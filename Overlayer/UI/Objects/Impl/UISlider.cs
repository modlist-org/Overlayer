using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Objects.Impl;

public class UISlider : UIObject {
    public float DefaultValue { get; }
    public float Min;
    public float Max;
    public float Value { get; private set; }

    public string Format {
        get;
        set {
            field = value;
            UpdateValueText();
        }
    }

    public Action<float> OnChanged;
    public Action<float> OnComplete;

    public Func<float, float> Filter;

    public RectTransform FillRect { get; }
    public Image FillImage { get; }

    public TextMeshProUGUI Label { get; }
    public TextMeshProUGUI ValueText { get; }

    public Image ChangedImage { get; }
    public Image ChangedUpImage { get; }

    private Sequence fillSeq;
    private Sequence changeSeq;

    public UISlider(
        string id,
        RectTransform rect,
        RectTransform fillRect,
        Image fillImage,
        TextMeshProUGUI label,
        TextMeshProUGUI valueText,
        Image changedImage,
        Image changedUpImage,
        float defaultValue,
        float min,
        float max,
        float value,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string format = "0.##"
    ) : base(id, rect) {
        FillRect = fillRect;
        FillImage = fillImage;
        FillImage.color = UIColors.ObjectActive;

        Label = label;
        ValueText = valueText;

        ChangedImage = changedImage;
        ChangedUpImage = changedUpImage;
        ChangedUpImage.color = UIColors.ObjectBG;

        DefaultValue = defaultValue;
        Min = min;
        Max = max;

        OnChanged = onChanged;
        OnComplete = onComplete;

        Format = format;
        Filter = filter;

        Value = ApplyFilter(value);
        Value = Mathf.Clamp(Value, Min, Max);

        UpdateVisual(true);
    }

    public void Set(float value, bool invoke = true) {
        value = ApplyFilter(value);

        Value = Mathf.Clamp(value, Min, Max);

        if(invoke) {
            OnChanged?.Invoke(Value);
        }

        UpdateVisual();
    }

    public void SetOnlyValue(float value) {
        Value = Mathf.Clamp(ApplyFilter(value), Min, Max);
        UpdateVisual(false);
    }

    public float Normalize() => Mathf.InverseLerp(Min, Max, Value);

    public void SetNormalized(float t, bool invoke = true)
        => Set(Mathf.Lerp(Min, Max, t), invoke);

    private float ApplyFilter(float v) => Filter?.Invoke(v) ?? v;

    private void UpdateValueText() => ValueText?.text = Value.ToString(Format);

    public void UpdateVisual(bool noAnimate = false) {
        fillSeq?.Kill();
        changeSeq?.Kill();
        UpdateValueText();

        float t = Normalize();
        Vector2 max = FillRect.anchorMax;
        float changeAlpha = DefaultValue == Value ? 0f : 1f;

        if(noAnimate) {
            Vector2 fra = FillRect.anchorMax;
            fra.x = t;
            FillRect.anchorMax = fra;

            Color ci = ChangedImage.color;
            ci.a = changeAlpha;
            ChangedImage.color = ci;

            Color cui = ChangedUpImage.color;
            cui.a = changeAlpha;
            ChangedUpImage.color = cui;

            return;
        }

        fillSeq = DOTween.Sequence().SetUpdate(true).Join(
             DOTween.To(
                 () => FillRect.anchorMax.x,
                 x => {
                     Vector2 anchor = FillRect.anchorMax;
                     anchor.x = x;
                     FillRect.anchorMax = anchor;
                 },
                 t,
                 0.6f
             ).SetEase(Ease.OutExpo)
         );

        changeSeq = DOTween.Sequence().SetUpdate(true)
            .Join(DOTween.To(
                () => ChangedImage.color.a,
                x => {
                    Color c = ChangedImage.color;
                    c.a = x;
                    ChangedImage.color = c;
                },
                changeAlpha,
                0.2f
            ).SetEase(Ease.OutSine))
            .Join(DOTween.To(
                () => ChangedUpImage.color.a,
                x => {
                    Color c = ChangedUpImage.color;
                    c.a = x;
                    ChangedUpImage.color = c;
                },
                changeAlpha,
                0.2f
            ).SetEase(Ease.OutSine)
        );
    }

    public void OnDrag(float normalizedValue) => SetNormalized(normalizedValue, true);
}