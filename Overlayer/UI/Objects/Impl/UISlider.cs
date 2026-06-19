using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Overlayer.Core;
using GTweens.Builders;
using GTweens.Easings;
using Overlayer.Utility.Math;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
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
    public TextMeshProUGUI PreviewLabel { get; }
    public Image ChangedImage { get; }
    public Image ChangedUpImage { get; }
    public Image OutlineImage { get; }
    public float? LastValidValue { get; private set; }
    private bool isUpdatingFromCode;

    private GTween fillSeq, changeSeq, stateSeq;

    public UISlider(
        string id,
        RectTransform rect,
        RectTransform fillRect,
        Image fillImage,
        TextMeshProUGUI label,
        TMP_InputField valueInputField,
        TextMeshProUGUI previewLabel,
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
        InputCore = new UIInputCore(valueInputField, null, value.ToString(format),
            (val) => {
                if(isUpdatingFromCode) {
                    return;
                }

                var (result, state) = useInputClamp
                    ? Evaluator<float>.Evaluate(val, Value, Min, Max)
                    : Evaluator<float>.Evaluate(val, Value);

                MainCore.Log.Msg($"State: {state}, Color: {MathVisuals.GetStateColor(state)}");

                LastValidValue = state != EvalState.Error ? result : null;

                bool isCalc = state != EvalState.Error;

                if(isCalc) {
                    string valStr = (Filter?.Invoke(result) ?? result).ToString();
                    string symbol = state switch {
                        EvalState.OverRange => "<",
                        EvalState.UnderRange => ">",
                        _ => "="
                    };

                    PreviewLabel.text = $"{valStr} {symbol} <color=#00000000>{val}</color>";
                } else {
                    PreviewLabel.text = "";
                }

                SetStateVisuals(MathVisuals.GetStateColor(state), true);
            },
            (val) => {
                if(LastValidValue == null) {
                    InputCore.SetValue(Value.ToString(Format), false);
                } else {
                    Set(LastValidValue.Value, true);
                    OnComplete?.Invoke(Value);
                }

                PreviewLabel.text = "";
                SetStateVisuals(UIColors.ObjectActive, false);
            }
        );
        valueInputField.onSelect.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<string>>(new Action<string>(
#endif
                (_) => valueInputField.text = Value.ToString()
#if ML && IL2CPP
            ))
#endif
        );
        PreviewLabel = previewLabel;
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

        isUpdatingFromCode = true;
        InputCore.SetValue(Value.ToString(Format), false);
        isUpdatingFromCode = false;

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
    private void SetStateVisuals(Color targetColor, bool isCalculating) {
        stateSeq?.Kill();

        float targetAlpha = isCalculating ? 0f : 1f;

        Color startOutline = OutlineImage.color;
        Color startFill = FillImage.color;
        Color startChanged = ChangedImage.color;
        Color startCaret = InputCore.InputField.caretColor;

        stateSeq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(() => 0f, x => {
                OutlineImage.color = Color.Lerp(startOutline, targetColor, x);
                FillImage.color = Color.Lerp(startFill, new(targetColor.r, targetColor.g, targetColor.b, targetAlpha), x);
                ChangedImage.color = Color.Lerp(startChanged, new(targetColor.r, targetColor.g, targetColor.b, ChangedImage.color.a), x);
                InputCore.InputField.caretColor = Color.Lerp(startCaret, new(targetColor.r, targetColor.g, targetColor.b, InputCore.InputField.caretColor.a), x);
            }, 1f, 0.2f).SetEasing(Easing.OutSine)).Build();

        MainCore.TC.Play(stateSeq);
    }

    public override void Dispose() {
        base.Dispose();
        InputCore.Dispose();
        fillSeq?.Kill();
        changeSeq?.Kill();
    }
}