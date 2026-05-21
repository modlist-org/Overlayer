using DG.Tweening;
using UnityEngine;

namespace Overlayer.UI.Transition;

internal class PageSwicher {
    private static Sequence pageSeq;
    public static bool SwitchPage(int from, int to) {
        if(from == to) {
            return false;
        }

        if(!UICore.Pages.TryGetValue(from, out RectTransform fromPage)) {
            return false;
        }

        if(!UICore.Pages.TryGetValue(to, out RectTransform toPage)) {
            return false;
        }

        CanvasGroup fromCg = fromPage.GetComponent<CanvasGroup>();
        CanvasGroup toCg = toPage.GetComponent<CanvasGroup>();

        pageSeq?.Kill(true);

        toPage.anchoredPosition = new Vector2(600, 0);
        toCg.alpha = 0f;
        toCg.interactable = false;
        toCg.blocksRaycasts = false;

        pageSeq = DOTween.Sequence().SetUpdate(true);

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

        return true;
    }
}
