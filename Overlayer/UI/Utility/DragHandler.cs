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
    private Canvas canvas;
    private Vector2 offset;

    private void Awake() {
        rect = transform.parent.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        var trigger = gameObject.AddComponent<EventTrigger>();

        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((e) =>
#else
            (e) => 
#endif
            { 
                if(e is PointerEventData ped) {
                    OnPointerDownInternal(ped);
                }
            }
#if ML && IL2CPP
            ))
#endif
        );
        trigger.triggers.Add(downEntry);

        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((e) =>
#else
            (e) => 
#endif
            { 
                if(e is PointerEventData ped) {
                    OnDragInternal(ped);
                }
            }
#if ML && IL2CPP
            ))
#endif
        );
        trigger.triggers.Add(dragEntry);
    }

    private void OnPointerDownInternal(PointerEventData eventData) {
        if(rect == null || canvas == null) {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );
        offset = rect.anchoredPosition - localPoint;
    }

    private void OnDragInternal(PointerEventData eventData) {
        if(rect == null || canvas == null) {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );
        rect.anchoredPosition = localPoint + offset;
    }
}