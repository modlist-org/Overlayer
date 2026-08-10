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
    public Action<string> OnChanged { get; set; }
    public Action<string> OnEndEdit { get; set; }
    public TMP_InputField InputField { get; }
    public TextMeshProUGUI Placeholder { get; }

    private GTween caretTween, placeholderTween;
    private bool caretLooping, hasFocused;
    private bool suppressChanged;

    public UIInputCore(TMP_InputField inputField, TextMeshProUGUI placeholder, string value, Action<string> onChanged, Action<string> onEndEdit, bool multiline = false) {
        InputField = inputField;
        Placeholder = placeholder;
        Value = value;
        OnChanged = onChanged;
        OnEndEdit = onEndEdit;

        SetupInputField(multiline);

        if(InputField.text != (value ?? string.Empty)) {
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
        UIInputBlocker.SetFocused(focused, focused ? InputField.gameObject : null);

        UpdateCaretAnimation(focused);
        UpdatePlaceholder(focused);
    }

    public void SetValue(string value, bool invoke = true) {
        Value = value ?? string.Empty;
        if(InputField.text != Value) {
            suppressChanged = true;
            InputField.text = Value;
            suppressChanged = false;
        }

        if(invoke) {
            OnChanged?.Invoke(Value);
        }
    }

    private void SetupInputField(bool multiline) {
        InputField.lineType = multiline
            ? TMP_InputField.LineType.MultiLineNewline
            : TMP_InputField.LineType.SingleLine;
        InputField.lineLimit = 0;
        InputField.richText = false;
        InputField.customCaretColor = true;
        InputField.caretColor = UIColors.ObjectActive;
        InputField.caretBlinkRate = 0f;
        InputField.caretWidth = 2;
        InputField.selectionColor = UIColors.MenuHover;
    }

    private void OnValueChanged(string value) {
        Value = value;
        UpdateCaretAnimation(InputField.isFocused);
        if(!suppressChanged) {
            OnChanged?.Invoke(value);
        }
    }

    private void OnValueEndEdit(string value) => OnEndEdit?.Invoke(value);

    private void UpdateCaretAnimation(bool focused) {
        if(focused) {
            if(caretLooping) {
                return;
            }

            caretLooping = true;
            caretTween?.Kill();
            InputField.caretColor = UIColors.ObjectActive;
            caretTween = CreateCaretLoop();
            MainCore.TC.Play(caretTween);
            return;
        }

        caretLooping = false;
        caretTween?.Kill();
        InputField.caretColor = UIColors.ObjectActive;
    }

    private GTween CreateCaretLoop() {
        return GTweenSequenceBuilder.New()
            .Append(GTweenExtensions.Tween(
                () => InputField.caretColor.a,
                x => {
                    var color = UIColors.ObjectActive;
                    color.a = x;
                    InputField.caretColor = color;
                },
                0.35f,
                0.55f
            ).SetEasing(Easing.InOutSine))
            .Append(GTweenExtensions.Tween(
                () => InputField.caretColor.a,
                x => {
                    var color = UIColors.ObjectActive;
                    color.a = x;
                    InputField.caretColor = color;
                },
                1f,
                0.12f
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
        if(hasFocused) {
            hasFocused = false;
            UIInputBlocker.SetFocused(false);
        }
        caretTween?.Kill();
        placeholderTween?.Kill();
    }
}
