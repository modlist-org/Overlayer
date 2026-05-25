using DG.Tweening;
using Overlayer.Core;
using UnityEngine;

namespace Overlayer.UI.Objects;

public abstract class UIObject {
    private static readonly List<UIObject> _tickables = [];

    public string Id { get; }
    public RectTransform Rect { get; }

    public bool OnlyModOn {
        get;
        set {
            if(field == value) {
                return;
            }

            field = value;
            if(field) {
                SetBlocked(!MainCore.IsModEnabled, true);
                MainCore.OnModEnabledChanged += ApplyStateForAction;
            } else {
                MainCore.OnModEnabledChanged -= ApplyStateForAction;
            }
        }
    }

    protected CanvasGroup CanvasGroup {
        get {
            field ??= Rect.GetComponent<CanvasGroup>() ?? Rect.gameObject.AddComponent<CanvasGroup>();
            return field;
        }
    }
    private Sequence blockSeq;

    protected UIObject(string id, RectTransform rect) {
        Id = id;
        Rect = rect;
    }

    private void ApplyStateForAction(bool enabled, bool isDispose) {
        if(!OnlyModOn || isDispose) {
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

    public virtual void Dispose() => UnregisterTick();

    protected void RegisterTick() => _tickables.Add(this);

    protected void UnregisterTick() => _tickables.Remove(this);

    public virtual void Tick() {
    }

    public static void TickAll() {
        for(int i = 0; i < _tickables.Count; i++) {
            _tickables[i].Tick();
        }
    }
}
