using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public class UIInputCore {
    public string Value { get; private set; }
    public Action<string> OnChanged { get; }
    public Action<string> OnEndEdit { get; }
    public TMP_InputField InputField { get; }
    public TextMeshProUGUI Placeholder { get; }

    private GTween caretTween, placeholderTween;
    private bool caretLooping, hasFocused;

    public UIInputCore(TMP_InputField inputField, TextMeshProUGUI placeholder, string value, Action<string> onChanged, Action<string> onEndEdit) {
        InputField = inputField;
        Placeholder = placeholder;
        Value = value;
        OnChanged = onChanged;
        OnEndEdit = onEndEdit;

        SetupInputField();
        
        if (InputField.text != (value ?? string.Empty)) {
            InputField.text = value ?? string.Empty;
        }

        InputField.onValueChanged.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<string>>(new Action<string>(
#endif
                OnValueChanged
#if ML && IL2CPP
            ))
#endif
        );

        InputField.onEndEdit.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<string>>(new Action<string>(
#endif
                OnValueEndEdit
#if ML && IL2CPP
            ))
#endif
        );
    }

    public void OnTick() {
        bool focused = InputField.isFocused;
        if(focused == hasFocused) {
            return;
        }

        hasFocused = focused;

        UpdateCaretAnimation(focused);
        UpdatePlaceholder(focused);
    }

    public void SetValue(string value, bool invoke = true) {
        Value = value ?? string.Empty;
        if(InputField.text != Value) {
            InputField.text = Value;
        }

        if(invoke) {
            OnChanged?.Invoke(Value);
        }
    }

    private void SetupInputField() {
        InputField.lineType = TMP_InputField.LineType.SingleLine;
        InputField.richText = false;
        InputField.customCaretColor = true;
        InputField.caretColor = Color.clear;
        InputField.caretBlinkRate = 0f;
        InputField.caretWidth = 2;
        InputField.selectionColor = UIColors.MenuHover;
    }

    private void OnValueChanged(string value) {
        Value = value;
        UpdateCaretAnimation(InputField.isFocused);
        OnChanged?.Invoke(value);
    }

    private void OnValueEndEdit(string value) {
        OnChanged?.Invoke(value);
        OnEndEdit?.Invoke(value);
    }

    private void UpdateCaretAnimation(bool focused) {
        if(focused) {
            caretTween?.Kill();
            if(caretLooping) {
                caretTween = CreateCaretLoop();
                MainCore.TC.Play(caretTween);
                return;
            }

            caretLooping = true;
            caretTween = GTweenSequenceBuilder.New()
                .Append(GTweenExtensions.Tween(
                    () => InputField.caretColor.a,
                    x => {
                        var c = UIColors.ObjectActive;
                        c.a = x;
                        InputField.caretColor = c;
                    },
                    1f,
                    0.2f
                ).SetEasing(Easing.OutSine))
                .AppendCallback(() => {
                    caretTween?.Kill();
                    caretTween = CreateCaretLoop();
                    MainCore.TC.Play(caretTween);
                }).Build();
            MainCore.TC.Play(caretTween);
            return;
        }

        if(!caretLooping) {
            return;
        }

        caretLooping = false;

        caretTween?.Kill();
        caretTween = GTweenExtensions.Tween(
            () => InputField.caretColor.a,
            x => {
                var c = UIColors.ObjectActive;
                c.a = x;
                InputField.caretColor = c;
            },
            0f,
            0.3f
        ).SetEasing(Easing.OutSine);
        MainCore.TC.Play(caretTween);
    }

    private GTween CreateCaretLoop() {
        return GTweenSequenceBuilder.New()
            .Append(GTweenExtensions.Tween(
                () => InputField.caretColor.a,
                x => {
                    var c = InputField.caretColor;
                    c.a = x;
                    InputField.caretColor = c;
                },
                1f,
                0.02f
            ).SetEasing(Easing.OutSine))
            .Append(GTweenExtensions.Tween(
                () => InputField.caretColor.a,
                x => {
                    var c = InputField.caretColor;
                    c.a = x;
                    InputField.caretColor = c;
                },
                0.4f,
                0.62f
            ).SetEasing(Easing.OutSine))
            .Build().SetMaxLoops();
    }

    private void UpdatePlaceholder(bool focused) {
        if(Placeholder == null) {
            return;
        }

        placeholderTween?.Kill();

        float target = focused ? 0f : 0.2f;
        float duration = focused ? 0.2f : 0.3f;

        placeholderTween = GTweenSequenceBuilder.New()
            .Append(GTweenExtensions.Tween(
                () => Placeholder.color.a,
                x => {
                    Color c = Placeholder.color;
                    c.a = x;
                    Placeholder.color = c;
                },
            target,
            duration
        )
        .SetEasing(Easing.OutQuad)).Build();
        MainCore.TC.Play(placeholderTween);
    }

    public void Dispose() {
        caretTween?.Kill();
        placeholderTween?.Kill();
    }
}