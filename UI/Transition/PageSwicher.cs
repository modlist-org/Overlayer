using DG.Tweening;
using UnityEngine;

namespace Overlayer.UI.Transition;

internal class PageSwicher {
    private static Sequence pageSeq;
    public static void SwitchPage(MenuState from, MenuState to) {
        if(from == to) {
            return;
        }

        pageSeq?.Kill(true);

        RectTransform fromPage = UICore.Pages[from];
        RectTransform toPage = UICore.Pages[to];

        CanvasGroup fromCg = fromPage.GetComponent<CanvasGroup>();
        CanvasGroup toCg = toPage.GetComponent<CanvasGroup>();

        toPage.anchoredPosition = new Vector2(600, 0);
        toCg.alpha = 0f;
        toCg.interactable = false;
        toCg.blocksRaycasts = false;

        pageSeq = DOTween.Sequence();

        pageSeq.Join(fromPage.DOAnchorPosX(-420f, 0.35f).SetEase(Ease.OutCubic));
        pageSeq.Join(fromCg.DOFade(0f, 0.3f));

        pageSeq.Join(toPage.DOAnchorPosX(0f, 0.45f).SetEase(Ease.OutExpo).SetDelay(0.05f));
        pageSeq.Join(toCg.DOFade(1f, 0.3f));

        pageSeq.OnComplete(() => {
            fromCg.interactable = false;
            fromCg.blocksRaycasts = false;

            toCg.interactable = true;
            toCg.blocksRaycasts = true;
        });

        pageSeq.SetUpdate(true);
    }
}
