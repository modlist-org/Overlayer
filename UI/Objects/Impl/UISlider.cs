using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Objects.Impl;

public class UISlider : UIObject {
    public float DefaultValue { get; }
    public float Min { get; }
    public float Max { get; }
    public float Value { get; private set; }
    public string Format;

    public Action<float> OnChanged;
    public Action<float> OnComplete;

    public RectTransform FillRect { get; }
    public Image FillImage { get; }

    public TextMeshProUGUI Label { get; }
    public TextMeshProUGUI ValueText { get; }

    private Sequence fillSeq;

    public UISlider(
        string id,
        RectTransform rect,
        RectTransform fillRect,
        Image fillImage,
        TextMeshProUGUI label,
        TextMeshProUGUI valueText,
        float defaultValue,
        float min,
        float max,
        float value,
        Action<float> onChanged,
        Action<float> onComplete,
        string format = "0.##"
    ) : base(id, rect) {
        FillRect = fillRect;
        FillImage = fillImage;

        Label = label;
        ValueText = valueText;

        DefaultValue = defaultValue;
        Min = min;
        Max = max;
        Value = Mathf.Clamp(value, min, max);

        OnChanged = onChanged;
        OnComplete = onComplete;
        
        Format = format;

        UpdateVisual(false);
    }

    public void Set(float value, bool invoke = true) {
        Value = Mathf.Clamp(value, Min, Max);

        if (invoke) {
            OnChanged?.Invoke(Value);
        }

        UpdateVisual();
    }

    public void SetOnlyValue(float value) {
        Value = Mathf.Clamp(value, Min, Max);
        UpdateVisual(false);
    }

    public float Normalize() {
        return Mathf.InverseLerp(Min, Max, Value);
    }

    public void SetNormalized(float t, bool invoke = true) {
        Set(Mathf.Lerp(Min, Max, t), invoke);
    }

    public void UpdateVisual(bool animate = true) {
        float t = Normalize();

        if (ValueText != null) {
            ValueText.text = Value.ToString(Format);
        }

        fillSeq?.Kill();

        if (animate) {
            fillSeq = DOTween.Sequence().Join(
                DOTween.To(
                    () => FillRect.anchorMax.x,
                    x => {
                        Vector2 max = FillRect.anchorMax;
                        max.x = x;
                        FillRect.anchorMax = max;
                    },
                    t,
                    0.6f
                )
                .SetEase(Ease.OutExpo)
                .SetUpdate(true)
            );
        } else {
            Vector2 max = FillRect.anchorMax;
            max.x = t;
            FillRect.anchorMax = max;
        }

        FillImage.color = UIColors.ObjectActive;
    }

    public void OnDrag(float normalizedValue) {
        SetNormalized(normalizedValue, true);
    }
}