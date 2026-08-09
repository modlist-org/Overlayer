using UnityEngine;

namespace Overlayer.UI.Objects.Impl;

internal sealed class UIWatcher : UIObject {
    private Action tick;

    public UIWatcher(string id, RectTransform rect, Action tick) : base(id, rect) {
        this.tick = tick;
        RegisterTick();
    }

    public override void Tick() {
        if(!IsDisposed) {
            tick?.Invoke();
        }
    }

    public override void Dispose() {
        if(IsDisposed) {
            return;
        }

        tick = null;
        base.Dispose();
    }
}
