using DG.Tweening;
using UnityEngine;

namespace Overlayer.UI.Transition;

public class PageSwicher {
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

        if(fromCg == null || toCg == null) {
            return false;
        }

        pageSeq?.Kill(true);

        fromPage.anchoredPosition = Vector2.zero;
        toPage.anchoredPosition = new Vector2(600f, 0f);

        fromCg.alpha = 1f;
        toCg.alpha = 0f;

        fromCg.interactable = false;
        fromCg.blocksRaycasts = false;

        toCg.interactable = false;
        toCg.blocksRaycasts = false;

        pageSeq = DOTween.Sequence().SetUpdate(true);

        pageSeq.Join(fromPage.DOAnchorPosX(-600f, 0.35f).SetEase(Ease.OutCubic));
        pageSeq.Join(fromCg.DOFade(0f, 0.3f));

        pageSeq.Join(toPage.DOAnchorPosX(0f, 0.45f).SetEase(Ease.OutExpo).SetDelay(0.05f));
        pageSeq.Join(toCg.DOFade(1f, 0.3f));

        pageSeq.Insert(0.1f, DOTween.To(
            () => 0,
            _ => {
                toCg.interactable = true;
                toCg.blocksRaycasts = true;
            },
            1,
            0f
        ));

        return true;
    }
}