using DG.Tweening;
using Overlayer.UI;
using Overlayer.UI.Objects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Label { get; }
    public Image Background { get; }

    private Tween hoverTween;

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
            .DOColor(UIColors.ObjectActiveLightBright, 0.12f)
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
    }

    public void OnHoverExit() {
        hoverTween?.Kill();

        hoverTween = Background
            .DOColor(UIColors.ObjectButton, 0.12f)
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
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
            .DOColor(UIColors.ObjectButton, 0.2f)
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
    }
}