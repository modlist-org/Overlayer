using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Overlayer.Core;
using GTweens.Extensions;
using GTweens.Builders;
using GTweens.Easings;
using NCalc;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public class UISlider : UIObject {
    public float DefaultValue { get; }
    public float Min;
    public float Max;
    public float Value { get; private set; }
    public string Format { get; set; }
    public bool UseInputClamp { get; set; }

    public Action<float> OnChanged;
    public Action<float> OnComplete;
    public Func<float, float> Filter;
    public RectTransform FillRect { get; }
    public Image FillImage { get; }
    public TextMeshProUGUI Label { get; }
    public UIInputCore InputCore { get; }
    public Image ChangedImage { get; }
    public Image ChangedUpImage { get; }
    public Image OutlineImage { get; }

    private GTween fillSeq, changeSeq;

    public UISlider(
        string id,
        RectTransform rect,
        RectTransform fillRect,
        Image fillImage,
        TextMeshProUGUI label,
        TMP_InputField valueInputField,
        Image changedImage,
        Image changedUpImage,
        Image outlineImage,
        float defaultValue,
        float min,
        float max,
        float value,
        string format,
        bool useInputClamp,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete
    ) : base(id, rect) {
        FillRect = fillRect;
        FillImage = fillImage;
        FillImage.color = UIColors.ObjectActive;
        Label = label;
        InputCore = new UIInputCore(valueInputField, null, value.ToString(format), null,
            (val) => {
                try {
                    Expression e = new(val);
                    object result = e.Evaluate();
                    float valResult = Convert.ToSingle(result);

                    Set(valResult, true);
                    OnComplete?.Invoke(Value);
                } catch {
                    InputCore.SetValue(Value.ToString(Format), false);
                }
            }
        );
        valueInputField.onSelect.AddListener((_) => {
            valueInputField.text = Value.ToString();
        });
        ChangedImage = changedImage;
        ChangedUpImage = changedUpImage;
        ChangedUpImage.color = UIColors.ObjectBG;
        OutlineImage = outlineImage;
        DefaultValue = defaultValue;
        Min = min;
        Max = max;
        OnChanged = onChanged;
        OnComplete = onComplete;
        Format = format;
        UseInputClamp = useInputClamp;
        Filter = filter;
        Value = ApplyFilter(value);
        Value = Math.Clamp(Value, Min, Max);

        RegisterTick();
        UpdateVisual(true);
    }

    public override void Tick() => InputCore.OnTick();

    public void Set(float value, bool invoke = true) {
        if(float.IsNaN(value)) {
            return;
        }

        value = ApplyFilter(value);

        Value = ClampSafe(value, Min, Max);

        if(invoke) {
            OnChanged?.Invoke(Value);
        }

        InputCore.SetValue(Value.ToString(Format), false);
        UpdateVisual();
    }

    private float ClampSafe(float value, float min, float max) {
        if(float.IsNaN(value)) {
            return Value;
        }

        if(!UseInputClamp) {
            return value;
        }

        if(value < min) {
            return min;
        }

        if(value > max) {
            return max;
        }

        return value;
    }

    public float Normalize() => Mathf.InverseLerp(Min, Max, Value);

    public void SetNormalized(float t, bool invoke = true) => Set(Mathf.Lerp(Min, Max, t), invoke);

    private float ApplyFilter(float v) => Filter?.Invoke(v) ?? v;

    public void UpdateVisual(bool noAnimate = false) {
        fillSeq?.Kill();
        changeSeq?.Kill();

        float t = Normalize();
        float changeAlpha = Math.Abs(DefaultValue - Value) > 0.001f ? 1f : 0f;

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

        fillSeq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(() => FillRect.anchorMax.x, x => {
                Vector2 anchor = FillRect.anchorMax;
                anchor.x = x;
                FillRect.anchorMax = anchor;
            }, t, 0.6f).SetEasing(Easing.OutExpo)).Build();
        MainCore.TC.Play(fillSeq);

        changeSeq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(() => ChangedImage.color.a, x => {
                Color c = ChangedImage.color;
                c.a = x;
                ChangedImage.color = c;
            }, changeAlpha, 0.2f).SetEasing(Easing.OutSine))
            .Join(GTweenExtensions.Tween(() => ChangedUpImage.color.a, x => {
                Color c = ChangedUpImage.color;
                c.a = x;
                ChangedUpImage.color = c;
            }, changeAlpha, 0.2f).SetEasing(Easing.OutSine)).Build();
        MainCore.TC.Play(changeSeq);
    }

    public void OnDrag(float normalizedValue) => SetNormalized(normalizedValue, true);

    public override void Dispose() {
        base.Dispose();
        InputCore.Dispose();
        fillSeq?.Kill();
        changeSeq?.Kill();
    }
}