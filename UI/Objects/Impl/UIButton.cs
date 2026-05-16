using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI.Objects.Impl;

public class UIButton : UIObject {
    public Action OnClick { get; set; }
    public TextMeshProUGUI Text { get; }
    public Image Background { get; }

    private Sequence bgSeq;

    public UIButton(
        string id,
        RectTransform rect,
        TextMeshProUGUI text,
        Image background,
        Action onClick
    ) : base(id, rect) {
        Text = text;
        Background = background;
        OnClick = onClick;

        UpdateVisual();
    }

    public void Click(bool invoke = true) {
        if(invoke) {
            OnClick?.Invoke();
        }

        UpdateVisual();
    }

    public void UpdateVisual() {
        bgSeq?.Kill();

        Background.color = UIColors.ObjectActiveBright;

        bgSeq = DOTween.Sequence().Append(
            Background.DOColor(
                UIColors.ObjectButton,
                0.2f
            ).SetEase(Ease.OutSine)
        );
    }
}