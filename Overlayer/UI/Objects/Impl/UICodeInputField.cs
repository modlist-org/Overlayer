using UnityEngine;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class UICodeInputField
#if ML && IL2CPP
    (IntPtr ptr) : TMP_InputField(ptr)
#else
    : TMP_InputField
#endif
{
    public Action<TMP_Text, bool> AfterLabelUpdate;

    protected override void LateUpdate() {
        base.LateUpdate();
        if(textComponent != null) {
            AfterLabelUpdate?.Invoke(textComponent, isFocused && !string.IsNullOrEmpty(Input.compositionString));
        }
    }
}
