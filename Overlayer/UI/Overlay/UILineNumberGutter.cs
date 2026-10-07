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

        // Only write on change so an idle editor doesn't dirty the canvas every frame.
        Vector2 position = LineNumbers.anchoredPosition;
        float y = Source.anchoredPosition.y;
        if(position.y != y) {
            position.y = y;
            LineNumbers.anchoredPosition = position;
        }

        Vector2 size = LineNumbers.sizeDelta;
        float height = Source.sizeDelta.y;
        if(size.y != height) {
            size.y = height;
            LineNumbers.sizeDelta = size;
        }
    }
}
