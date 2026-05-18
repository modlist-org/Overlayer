using DG.Tweening;
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
            ApplyState(Core.IsModEnabled);
        }
    }

    private CanvasGroup _canvasGroup;
    protected CanvasGroup canvasGroup {
        get {
            if (_canvasGroup == null) {
                _canvasGroup = Rect.GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = Rect.gameObject.AddComponent<CanvasGroup>();
                }
            }
            return _canvasGroup;
        }
    }
    private Sequence blockSeq;

    protected UIObject(string id, RectTransform rect, bool onlyModOn = false) {
        Id = id;
        Rect = rect;
        _onlyModOn = onlyModOn;

        Core.OnModEnabledChanged += ApplyState;

        ApplyState(Core.IsModEnabled);
    }

    private void ApplyState(bool enabled) {
        if(_onlyModOn) {
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
