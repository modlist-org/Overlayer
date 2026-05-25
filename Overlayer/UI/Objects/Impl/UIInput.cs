using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private Tween changeTween;
    private Tween caretTween;
    private Tween placeholderTween;
    private Tween iconTween;

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
        Value = value ?? string.Empty;
        OnChanged = onChanged;

        RegisterTick();

        SetupInputField();

        InputField.onValueChanged.AddListener(OnValueChanged);

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
        changeTween?.Kill(true);

        float target = (DefaultValue != null && DefaultValue != Value) ? 1f : 0f;

        if(noAnimate) {
            Color c = ChangedImage.color;
            c.a = target;
            ChangedImage.color = c;
            return;
        }

        changeTween = DOTween.To(
            () => ChangedImage.color.a,
            x => {
                Color c = ChangedImage.color;
                c.a = x;
                ChangedImage.color = c;
            },
            target,
            0.2f
        )
        .SetEase(Ease.OutSine)
        .SetUpdate(true);
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
                caretTween = CreateCaretLoop(UIColors.ObjectActive);
                return;
            }

            caretLooping = true;
            caretTween = DOTween.Sequence()
                .SetUpdate(true)
                .Append(DOTween.To(
                    () => InputField.caretColor.a,
                    x => {
                        var c = UIColors.ObjectActive;
                        c.a = x;
                        InputField.caretColor = c;
                    },
                    1f,
                    0.2f
                ).SetEase(Ease.OutSine))
                .AppendCallback(() => {
                    caretTween?.Kill();
                    caretTween = CreateCaretLoop(UIColors.ObjectActive);
                });

            return;
        }

        if(!caretLooping) {
            return;
        }

        caretLooping = false;

        caretTween?.Kill();
        caretTween = DOTween.To(
            () => InputField.caretColor.a,
            x => {
                var c = InputField.caretColor;
                c.a = x;
                InputField.caretColor = c;
            },
            0f,
            0.3f
        ).SetEase(Ease.OutSine);
    }

    private Tween CreateCaretLoop(Color baseColor) {
        return DOTween.Sequence()
            .SetUpdate(true)
            .SetLoops(-1, LoopType.Restart)

            .Append(DOTween.To(
                () => InputField.caretColor.a,
                x => {
                    var c = InputField.caretColor;
                    c.a = x;
                    InputField.caretColor = c;
                },
                1f,
                0.02f
            ).SetEase(Ease.OutQuad))

            .Append(DOTween.To(
                () => InputField.caretColor.a,
                x => {
                    var c = InputField.caretColor;
                    c.a = x;
                    InputField.caretColor = c;
                },
                0.4f,
                0.62f
            ).SetEase(Ease.OutQuad));
    }

    private void UpdatePlaceholder(bool focused) {
        placeholderTween?.Kill();

        float target = focused ? 0f : 0.2f;
        float duration = focused ? 0.2f : 0.3f;

        placeholderTween = DOTween.To(
            () => Placeholder.color.a,
            x => {
                Color c = Placeholder.color;
                c.a = x;
                Placeholder.color = c;
            },
            target,
            duration
        )
        .SetEase(Ease.OutQuad)
        .SetUpdate(true);
    }

    private void UpdateIconImage(bool focused) {
        iconTween?.Kill();
        iconTween = DOTween.To(
            () => IconImage.color.a,
            x => {
                Color c = IconImage.color;
                c.a = x;
                IconImage.color = c;
            },
            focused ? 0f : 0.2f,
            focused ? 0.2f : 0.3f
        )
        .SetEase(Ease.OutQuad)
        .SetUpdate(true);
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