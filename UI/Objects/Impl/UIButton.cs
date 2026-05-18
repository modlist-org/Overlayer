using DG.Tweening;
using Overlayer;
using Overlayer.UI;
using Overlayer.UI.Objects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Label { get; }
    public Image Background { get; }

    private Sequence hoverSeq;

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

        UpdateVisual();
    }

    public void OnHoverEnter() {
        hoverSeq?.Kill();
        hoverSeq = DOTween.Sequence()
            .Join(Background.DOColor(UIColors.ObjectActiveLightBright, 0.12f)
                .SetEase(Ease.OutSine)).SetUpdate(true);
    }

    public void OnHoverExit() {
        hoverSeq?.Kill();

        hoverSeq = DOTween.Sequence()
            .Join(Background.DOColor(UIColors.ObjectButton, 0.12f)
                .SetEase(Ease.OutSine)).SetUpdate(true);
    }

    public void Click(bool invoke = true) {
        if(invoke) {
            OnClick?.Invoke();
        }

        UpdateVisual();
    }

    public void UpdateVisual() {
        Background.color = UIColors.ObjectActiveBright;

        hoverSeq?.Kill();
        hoverSeq = DOTween.Sequence().Append(
            Background.DOColor(UIColors.ObjectButton, 0.2f)
                .SetEase(Ease.OutSine).SetUpdate(true)
        );
    }
}