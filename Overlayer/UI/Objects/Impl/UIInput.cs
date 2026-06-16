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
    public UIInputCore Core { get; }
    public string DefaultValue { get; }
    public string Value => Core.Value;
    public Action<string> OnChanged => Core.OnChanged;
    public TMP_InputField InputField => Core.InputField;
    public TextMeshProUGUI Placeholder => Core.Placeholder;
    public Image IconImage { get; }
    public Image ChangedImage { get; }

    private GTween changeTween, iconTween;

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
        DefaultValue = defaultValue;
        ChangedImage = changedImage;
        IconImage = iconImage;

        Core = new UIInputCore(inputField, placeholder, value, val => {
            UpdateVisual();
            onChanged?.Invoke(val);
        }, null);

        RegisterTick();
        UpdateVisual(true);
    }

    public void Set(string value, bool invoke = true) {
        Core.SetValue(value);
        if(invoke) {
            Core.OnChanged?.Invoke(value);
        }

        UpdateVisual();
    }

    public void Reset() {
        if(DefaultValue != null) {
            Set(DefaultValue);
        }
    }

    public void UpdateVisual(bool noAnimate = false) {
        changeTween?.Kill();
        float target = (DefaultValue != null && DefaultValue != Core.Value) ? 1f : 0f;
        if(noAnimate) {
            Color c = ChangedImage.color;
            c.a = target;
            ChangedImage.color = c;
            return;
        }
        changeTween = GTweenExtensions.Tween(() => ChangedImage.color.a, x => { Color c = ChangedImage.color; c.a = x; ChangedImage.color = c; }, target, 0.2f).SetEasing(Easing.OutSine);
        MainCore.TC.Play(changeTween);
    }

    private void UpdateIconImage(bool focused) {
        if(IconImage == null || !IconImage.enabled || IconImage.sprite == null) {
            return;
        }
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

    public override void Tick() {
        Core.OnTick();
        UpdateIconImage(InputField.isFocused);
    }

    public override void Dispose() {
        base.Dispose();
        Core.Dispose();
        changeTween?.Kill();
        iconTween?.Kill();
    }
}