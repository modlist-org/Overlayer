using DG.Tweening;
using UnityEngine;

namespace Overlayer.UI.Objects;

public abstract class UIObject {
    public string Id { get; }
    public RectTransform Rect { get; }

    public bool onlyModOn;
    private readonly CanvasGroup canvasGroup;
    private Sequence blockSeq;

    protected UIObject(string id, RectTransform rect, bool onlyModOn = false) {
        Id = id;
        Rect = rect;
        this.onlyModOn = onlyModOn;

        canvasGroup = rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();

        Core.OnModEnabledChanged += HandleModState;
    }

    private void HandleModState(bool enabled) {
        if(onlyModOn) {
            SetBlocked(!enabled);
            return;
        }

        SetBlocked(false);
    }

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
            ).SetEase(Ease.OutSine))
            .SetUpdate(true);
    }
}