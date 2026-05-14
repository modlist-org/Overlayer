using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Overlayer.UI;

public class NonRaycastButton : MonoBehaviour, IPointerClickHandler {
    public UnityAction onClick;

    public void OnPointerClick(PointerEventData eventData) {
        if(!eventData.dragging) {
            onClick?.Invoke();
        }
    }
}