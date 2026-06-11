using Overlayer.Compat.OVC;
using UnityEngine;
using UnityEngine.EventSystems;

#if ML && IL2CPP
using MelonLoader;
using Il2CppInterop.Runtime;
#endif

namespace Overlayer.UI.Utility;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class DragHandler
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    private RectTransform rect;
    private Vector2 offset;

    private void Awake() {
        rect = transform.parent?.GetComponent<RectTransform>();
        SetupEvents();
    }

    private void SetupEvents() {
        var trigger = gameObject.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>(
#endif
                (_) => OnPointerDownInternal()
#if ML && IL2CPP 
                ))
#endif
            );
        trigger.triggers.Add(downEntry);

        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>(
#endif
                (_) => OnDragInternal()
#if ML && IL2CPP
                ))
#endif
            );
        trigger.triggers.Add(dragEntry);

    }

    private void OnPointerDownInternal() {
        if(rect == null) {
            rect = transform.parent?.GetComponent<RectTransform>();
        }

        if(rect == null) {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            OVC_Input.MousePosition,
            null,
            out Vector2 localPoint
        );
        offset = rect.anchoredPosition - localPoint;
    }

    private void OnDragInternal() {
        if(rect == null) {
            rect = transform.parent?.GetComponent<RectTransform>();
        }

        if(rect == null) {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            OVC_Input.MousePosition,
            null,
            out Vector2 localPoint
        );
        rect.anchoredPosition = localPoint + offset;
    }
}