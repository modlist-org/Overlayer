using UnityEngine;

namespace Overlayer.UI.Objects.Impl;

internal interface ICodeCompletion {
    bool HandleKey(KeyCode key);
    void Refresh(bool composing);
}
