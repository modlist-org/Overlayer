using DG.Tweening;
using Overlayer.UI.SpriteManage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Objects.Impl;

public class UIToggle : UIObject {
    public bool DefaultValue { get; }
    public bool Value { get; private set; }
    public Action<bool> OnChanged { get; }
    public TextMeshProUGUI Text { get; }
    public Image CircleImage { get; }
    public Image ChangedImage { get; }
    public RectTransform CircleRect { get; }

    private Sequence circleSeq;
    private Sequence changeSeq;

    public UIToggle(
        string id,
        RectTransform rect,
        TextMeshProUGUI text,
        Image circleImage,
        RectTransform circleRect,
        Image changedImage,
        bool defaultValue,
        bool value,
        Action<bool> onChanged
    ) : base(id, rect) {
        Text = text;

        CircleImage = circleImage;
        CircleRect = circleRect;
        ChangedImage = changedImage;
        DefaultValue = defaultValue;
        Value = value;
        OnChanged = onChanged;

        UpdateVisual();
    }

    public void Set(bool value, bool invoke = true) {
        Value = value;

        if(invoke) {
            OnChanged?.Invoke(value);
        }

        UpdateVisual();
    }

    public void Toggle() {
        Set(!Value);
    }

    public void Reset() {
        Set(DefaultValue);
    }

    public void UpdateVisual() {
        CircleImage.sprite = SpriteDatabase.Get(
            Value ? UISprite.Circle256 : UISprite.ToggleCircle128
        );

        circleSeq?.Kill();

        CircleRect.sizeDelta = new(30f, 30f);

        circleSeq = DOTween.Sequence()
            .Join(
                DOTween.To(
                    () => CircleRect.sizeDelta.x,
                    x => CircleRect.sizeDelta = new(x, x),
                    26f,
                    0.3f
                ).SetEase(Ease.OutQuad)
            )
            .Join(
                CircleImage.DOColor(
                    Value
                        ? UIColors.ObjectActive
                        : UIColors.ObjectInactive,
                    0.15f
                ).SetEase(Ease.OutQuad)
            );

        changeSeq?.Kill();

        float target = DefaultValue != Value
            ? 1f
            : 0f;

        changeSeq = DOTween.Sequence().Append(
            DOTween.To(
                () => ChangedImage.color.a,
                x => {
                    Color c = ChangedImage.color;
                    c.a = x;
                    ChangedImage.color = c;
                },
                target,
                0.2f
            ).SetEase(Ease.OutSine)
        );
    }
}