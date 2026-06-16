using UnityEngine;
using UnityEngine.UI;
using GTweens.Tweens;
using Overlayer.Tween;
using GTweens.Easings;
using Overlayer.Core;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Label { get; }
    public Image Background { get; }

    private GTween hoverTween;

    public UIButton(
        string id,
        RectTransform rect,
        TextMeshProUGUI label,
        Image background,
        Action onClick
    ) : base(id, rect) {
        Label = label;
        Background = background;
        OnClick = onClick;

        UpdateVisual(true);
    }

    public void OnHoverEnter() {
        hoverTween?.Kill();

        hoverTween = Background
            .GTColor(UIColors.ObjectActiveLightBright, 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }

    public void OnHoverExit() {
        hoverTween?.Kill();

        hoverTween = Background
            .GTColor(UIColors.ObjectButton, 0.12f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }

    public void Click(bool invoke = true) {
        if(invoke) {
            OnClick?.Invoke();
        }

        UpdateVisual();
    }

    public void UpdateVisual(bool noAnimate = false) {
        hoverTween?.Kill();

        if(noAnimate) {
            Background.color = UIColors.ObjectButton;
            return;
        }

        hoverTween = Background
            .GTColor(UIColors.ObjectButton, 0.2f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(hoverTween);
    }

    public override void Dispose() {
        base.Dispose();
        hoverTween?.Kill();
    }
}