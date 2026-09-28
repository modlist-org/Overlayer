using UnityEngine;
using O5Kit.Core;

namespace Overlayer.UI.Objects.Impl;

internal sealed class UIWatcher : O5Object {
    private Action tick;

    public UIWatcher(O5Context ctx, string id, RectTransform rect, Action tick) : base(ctx, id, rect) {
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
