using Overlayer.Compat.OVC;
using UnityEngine;
using UnityEngine.EventSystems;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Utility;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class OventHandler
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    private RectTransform _rectTransform;
    public Action<PointerEventData.InputButton> OnClick;

    void Awake() => _rectTransform = GetComponent<RectTransform>();

    void Update() {
        for(int i = 0; i < 3; i++) {
            if(OVC_Input.GetMouseButtonDown(i)) {
                if(IsMouseOver()) {
                    OnClick?.Invoke((PointerEventData.InputButton)i);
                }
            }
        }
    }

    private bool IsMouseOver() {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            OVC_Input.MousePosition,
            null,
            out localPoint
        );

        return _rectTransform.rect.Contains(localPoint);
    }
}