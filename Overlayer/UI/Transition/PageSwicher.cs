using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Overlayer.Core;
using Overlayer.Tween;
using UnityEngine;

namespace Overlayer.UI.Transition;

public class PageSwicher {
    private static GTween pageSeq;

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

        if(pageSeq != null) {
            pageSeq.Complete();
            pageSeq.Kill();
        }

        fromPage.anchoredPosition = Vector2.zero;
        toPage.anchoredPosition = new Vector2(1100f, 0f);

        fromCg.alpha = 1f;
        toCg.alpha = 0f;

        fromCg.interactable = false;
        fromCg.blocksRaycasts = false;
        toCg.interactable = false;
        toCg.blocksRaycasts = false;

        pageSeq = GTweenSequenceBuilder.New()
            .Join(fromPage.GTAnchorPosX(-1100f, 0.45f).SetEasing(Easing.OutExpo))
            .Join(fromCg.GTFade(0f, 0.3f))
            .Join(toPage.GTAnchorPosX(0f, 0.45f).SetEasing(Easing.OutExpo))
            .Join(toCg.GTFade(1f, 0.3f))
            .Join(GTweenSequenceBuilder.New()
                .AppendTime(0.1f)
                .AppendCallback(() => {
                    toCg.interactable = true;
                    toCg.blocksRaycasts = true;
                }).Build()
            )
            .AppendCallback(() => {
                fromCg.interactable = false;
                fromCg.blocksRaycasts = false;
            }).Build();
        MainCore.TC.Play(pageSeq);

        return true;
    }
}