using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Overlayer.Core;
using UnityEngine;

namespace Overlayer.UI.Objects;

public abstract class UIObject {
    private static readonly List<UIObject> _tickables = [];

    public string Id { get; }
    public RectTransform Rect { get; }
    public Action OnDisposed;
    public bool IsDisposed { get; private set; }

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
    private GTween blockSeq;

    protected UIObject(string id, RectTransform rect) {
        Id = id;
        Rect = rect;
    }

    private void ApplyStateForAction(bool enabled, bool isDispose) {
        if(IsDisposed || !OnlyModOn || isDispose) {
            return;
        }

        SetBlocked(!enabled);
    }

    public virtual void SetBlocked(bool blocked, bool noAnimate = false) {
        if(IsDisposed) {
            return;
        }

        blockSeq?.Kill();

        float targetAlpha = blocked ? 0.4f : 1f;

        CanvasGroup.interactable = !blocked;
        CanvasGroup.blocksRaycasts = !blocked;

        if(noAnimate) {
            CanvasGroup.alpha = targetAlpha;
            return;
        }

        blockSeq = GTweenSequenceBuilder.New()
            .Join(
                GTweenExtensions.Tween(
                    () => CanvasGroup.alpha,
                    x => CanvasGroup.alpha = x,
                    targetAlpha,
                    0.2f
                ).SetEasing(Easing.OutSine)
            ).Build();
        MainCore.TC.Play(blockSeq);
    }

    public virtual void Dispose() {
        if(IsDisposed) {
            return;
        }

        IsDisposed = true;
        blockSeq?.Kill();
        blockSeq = null;
        if(OnlyModOn) {
            MainCore.OnModEnabledChanged -= ApplyStateForAction;
        }
        UnregisterTick();
        OnDisposed?.Invoke();
        OnDisposed = null;
    }

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
