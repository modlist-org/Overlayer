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
    private bool _isHovered;

    void Awake() => _rectTransform = GetComponent<RectTransform>();

    void Start() {
        var trigger = GetComponent<EventTrigger>();
        if(trigger == null) {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => _isHovered = true),
            (EventTriggerType.PointerExit, () => _isHovered = false)
        );
    }

    void OnDisable() {
        _isHovered = false;
    }

    void Update() {
        if(!_isHovered) {
            return;
        }

        for(int i = 0; i < 3; i++) {
            if(OVC_Input.GetMouseButtonDown(i)) {
                OnClick?.Invoke((PointerEventData.InputButton)i);
            }
        }
    }
}