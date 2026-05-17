using UnityEngine;

namespace Overlayer.UI.Objects;

using DG.Tweening;
using UnityEngine;

public abstract class UIObject(string id, RectTransform rect) {
    public string Id { get; set; } = id;
    public RectTransform Rect { get; private set; } = rect;

    private CanvasGroup canvasGroup =
        rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();
    private Sequence blockSeq;

    public virtual void SetBlocked(bool blocked, bool noAnimate = false) {
        if(canvasGroup == null) {
            return;
        }

        float targetAlpha = blocked ? 0.4f : 1f;

        canvasGroup.interactable = !blocked;
        canvasGroup.blocksRaycasts = !blocked;

        if(noAnimate) {
            blockSeq?.Kill();
            canvasGroup.alpha = targetAlpha;
            return;
        }

        blockSeq?.Kill();

        blockSeq = DOTween.Sequence()
            .Join(DOTween.To(
                () => canvasGroup.alpha,
                x => canvasGroup.alpha = x,
                targetAlpha,
                0.15f
            ).SetEase(Ease.OutSine)).SetUpdate(true);
    }
}