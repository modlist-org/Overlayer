using UnityEngine;
using UnityEngine.EventSystems;

public class DragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler {
    private RectTransform rect;
    private Canvas canvas;

    private Vector2 offset;

    private void Awake() {
        rect = transform.parent.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );

        offset = rect.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );

        rect.anchoredPosition = localPoint + offset;
    }
}