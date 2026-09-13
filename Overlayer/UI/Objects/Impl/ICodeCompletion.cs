using UnityEngine;

namespace Overlayer.UI.Objects.Impl;

internal interface ICodeCompletion {
    bool HandleKey(KeyCode key);
    void Refresh(bool composing);
}

internal sealed class CompositeCompletion(params ICodeCompletion[] popups) : ICodeCompletion {
    private readonly ICodeCompletion[] popups = popups;

    public bool HandleKey(KeyCode key) {
        foreach (var popup in popups) {
            if (popup != null && popup.HandleKey(key)) {
                return true;
            }
        }

        return false;
    }

    public void Refresh(bool composing) {
        foreach (var popup in popups) {
            popup?.Refresh(composing);
        }
    }
}
