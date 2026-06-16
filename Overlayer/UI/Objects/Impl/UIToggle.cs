using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Overlayer.Tween;
using GTweens.Builders;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public class UIToggle : UIObject {
    public bool DefaultValue { get; }
    public bool Value { get; private set; }
    public Action<bool> OnChanged { get; }
    public TextMeshProUGUI Label { get; }
    public Image CircleImage { get; }
    public Image ChangedImage { get; }
    public RectTransform CircleRect { get; }

    private GTween circleSeq, changeSeq;

    public UIToggle(
        string id,
        RectTransform rect,
        TextMeshProUGUI label,
        Image circleImage,
        RectTransform circleRect,
        Image changedImage,
        bool defaultValue,
        bool value,
        Action<bool> onChanged
    ) : base(id, rect) {
        Label = label;

        CircleImage = circleImage;
        CircleRect = circleRect;
        ChangedImage = changedImage;
        DefaultValue = defaultValue;
        Value = value;
        OnChanged = onChanged;

        UpdateVisual(true);
    }

    public void Set(bool value, bool invoke = true) {
        Value = value;

        if(invoke) {
            OnChanged?.Invoke(value);
        }

        UpdateVisual();
    }

    public void Toggle() => Set(!Value);

    public void Reset() => Set(DefaultValue);

    public void UpdateVisual(bool noAnimate = false) {
        circleSeq?.Kill();
        changeSeq?.Kill();

        CircleImage.sprite = MainCore.Spr.Get(
            Value ? UISprite.Circle256 : UISprite.ToggleCircle128
        );

        CircleRect.sizeDelta = new(30f, 30f);

        float target = DefaultValue != Value ? 1f : 0f;

        if(noAnimate) {
            CircleRect.sizeDelta = new(26f, 26f);
            CircleImage.color = Value ? UIColors.ObjectActive : UIColors.ObjectInactive;

            Color c = ChangedImage.color;
            c.a = target;
            ChangedImage.color = c;

            return;
        }

        circleSeq = GTweenSequenceBuilder.New()
            .Join(
                GTweenExtensions.Tween(
                    () => CircleRect.sizeDelta.x,
                    x => CircleRect.sizeDelta = new Vector2(x, x),
                    26f,
                    0.3f
                ).SetEasing(Easing.OutQuad)
            )
            .Join(
                CircleImage.GTColor(Value ? UIColors.ObjectActive : UIColors.ObjectInactive, 0.15f)
                    .SetEasing(Easing.OutQuad)
            ).Build();
        MainCore.TC.Play(circleSeq);

        changeSeq = GTweenSequenceBuilder.New()
            .Append(
                GTweenExtensions.Tween(
                    () => ChangedImage.color.a,
                    x => {
                        Color c = ChangedImage.color;
                        c.a = x;
                        ChangedImage.color = c;
                    },
                    target,
                    0.2f
                ).SetEasing(Easing.OutSine)
            ).Build();
        MainCore.TC.Play(changeSeq);
    }

    public override void Dispose() {
        base.Dispose();
        circleSeq?.Kill();
        changeSeq?.Kill();
    }
}