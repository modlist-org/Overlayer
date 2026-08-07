using UnityEngine;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Overlay;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
internal sealed class UILineNumberGutter
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public RectTransform Source;
    public RectTransform LineNumbers;

    private void LateUpdate() {
        if(Source == null || LineNumbers == null) {
            return;
        }

        Vector2 position = LineNumbers.anchoredPosition;
        position.y = Source.anchoredPosition.y;
        LineNumbers.anchoredPosition = position;

        Vector2 size = LineNumbers.sizeDelta;
        size.y = Source.sizeDelta.y;
        LineNumbers.sizeDelta = size;
    }
}
