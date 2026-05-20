using DG.Tweening;
using Overlayer.Core;
using UnityEngine;

namespace Overlayer.UI.Objects;

public abstract class UIObject {
    public string Id { get; }
    public RectTransform Rect { get; }

    private bool _onlyModOn;
    public bool OnlyModOn {
        get => _onlyModOn;
        set {
            if(_onlyModOn == value) {
                return;
            }

            _onlyModOn = value;
            if(_onlyModOn) {
                SetBlocked(!MainCore.IsModEnabled, true);
                MainCore.OnModEnabledChanged += ApplyStateForAction;
            } else {
                MainCore.OnModEnabledChanged -= ApplyStateForAction;
            }
        }
    }

    private CanvasGroup _canvasGroup;
    protected CanvasGroup CanvasGroup {
        get {
            _canvasGroup ??= Rect.GetComponent<CanvasGroup>() ?? Rect.gameObject.AddComponent<CanvasGroup>();
            return _canvasGroup;
        }
    }
    private Sequence blockSeq;

    protected UIObject(string id, RectTransform rect) {
        Id = id;
        Rect = rect;
    }

    private void ApplyStateForAction(bool enabled, bool isDispose) {
        if(!_onlyModOn || isDispose) {
            return;
        }

        SetBlocked(!enabled);
    }

    public virtual void SetBlocked(bool blocked, bool noAnimate = false) {
        blockSeq?.Kill();

        float targetAlpha = blocked ? 0.4f : 1f;

        CanvasGroup.interactable = !blocked;
        CanvasGroup.blocksRaycasts = !blocked;

        if(noAnimate) {
            CanvasGroup.alpha = targetAlpha;
            return;
        }

        blockSeq = DOTween.Sequence().SetUpdate(true)
            .Join(DOTween.To(
                () => CanvasGroup.alpha,
                x => CanvasGroup.alpha = x,
                targetAlpha,
                0.2f
            ).SetEase(Ease.OutSine));
    }
}
