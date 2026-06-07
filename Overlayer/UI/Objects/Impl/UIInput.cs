using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using GTweens.Extensions;
using GTweens.Easings;
using GTweens.Builders;
using Overlayer.Core;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public sealed class UIInput : UIObject {
    public string DefaultValue { get; }
    public string Value { get; private set; }

    public Action<string> OnChanged { get; }

    public TMP_InputField InputField { get; }
    public TextMeshProUGUI Placeholder { get; }

    public Image IconImage { get; }
    public Image ChangedImage { get; }

    private readonly Queue<TextMeshProUGUI> glyphPool = [];

    private readonly List<GlyphSnapshot> glyphCache = [];

    private GTween changeTween;
    private GTween caretTween;
    private GTween placeholderTween;
    private GTween iconTween;

    public UIInput(
        string id,
        RectTransform rect,
        TMP_InputField inputField,
        TextMeshProUGUI placeholder,
        Image iconImage,
        Image changedImage,
        string defaultValue,
        string value,
        Action<string> onChanged
    ) : base(id, rect) {
        InputField = inputField;
        Placeholder = placeholder;
        IconImage = iconImage;
        ChangedImage = changedImage;
        DefaultValue = defaultValue;
        Value = value;
        OnChanged = onChanged;

        RegisterTick();

        SetupInputField();

        InputField.onValueChanged.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<string>>(new Action<string>(
#endif
            OnValueChanged
#if ML && IL2CPP
            ))
#endif
        );

        UpdateVisual(true);
    }

    public void Set(string value, bool invoke = true) {
        value ??= string.Empty;

        Value = value;

        if(InputField.text != value) {
            InputField.text = value;
        }

        if(invoke) {
            OnChanged?.Invoke(value);
        }

        UpdateVisual();
    }

    public void Reset() {
        if(DefaultValue != null) {
            Set(DefaultValue);
        }
    }

    private void OnValueChanged(string value) {
        Value = value;

        UpdateVisual();
        UpdateCaretAnimation(InputField.isFocused);

        OnChanged?.Invoke(value);
    }

    public void UpdateVisual(bool noAnimate = false) {
        if(changeTween != null) {
            changeTween.Complete();
            changeTween.Kill();
        }

        float target = (DefaultValue != null && DefaultValue != Value) ? 1f : 0f;

        if(noAnimate) {
            Color c = ChangedImage.color;
            c.a = target;
            ChangedImage.color = c;
            return;
        }

        changeTween = GTweenExtensions.Tween(
            () => ChangedImage.color.a,
            x => {
                Color c = ChangedImage.color;
                c.a = x;
                ChangedImage.color = c;
            },
            target,
            0.2f
        )
        .SetEasing(Easing.OutSine);
        MainCore.TC.Play(changeTween);
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

    private bool caretLooping;

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
                    var c = UIColors.ObjectActive;
                    c.a = x;
                    InputField.caretColor = c;
                },
                1f,
                0.02f
            ).SetEasing(Easing.OutSine))
            .Append(GTweenExtensions.Tween(
                () => InputField.caretColor.a,
                x => {
                    var c = UIColors.ObjectActive;
                    c.a = x;
                    InputField.caretColor = c;
                },
                0.4f,
                0.62f
            ).SetEasing(Easing.OutSine))
            .Build().SetMaxLoops();
    }

    private void UpdatePlaceholder(bool focused) {
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

    private void UpdateIconImage(bool focused) {
        iconTween?.Kill();
        iconTween = GTweenSequenceBuilder.New()
            .Append(GTweenExtensions.Tween(
                () => IconImage.color.a,
                x => {
                    Color c = IconImage.color;
                    c.a = x;
                    IconImage.color = c;
                },
                focused ? 0f : 0.2f,
                focused ? 0.2f : 0.3f
            )
            .SetEasing(Easing.OutQuad)).Build();
        MainCore.TC.Play(iconTween);
    }

    private readonly struct GlyphSnapshot(char character, Vector2 position) {
        public readonly char Character = character;
        public readonly Vector2 Position = position;
    }

    bool hasFocused = false;
    public override void Tick() {
        bool focused = InputField.isFocused;

        if(focused == hasFocused) {
            return;
        }

        hasFocused = focused;

        UpdateCaretAnimation(focused);
        UpdatePlaceholder(focused);
        UpdateIconImage(focused);
    }

    public override void Dispose() {
        base.Dispose();

        caretTween?.Kill();
        changeTween?.Kill();
    }
}