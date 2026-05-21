using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Utility;

public enum ResizeHandleType {
    Top,
    Left,
    Right,
    Bottom,

    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public class ResizeHandle
    : MonoBehaviour,
      IPointerDownHandler,
      IDragHandler {
    public ResizeHandleType Type;

    public RectTransform Panel;

    private Vector2 startMouse;
    private Vector2 startSize;
    private Vector2 startPos;

    public const float MIN_WIDTH = 900f;
    public const float MIN_HEIGHT = 500f;

    public void OnPointerDown(PointerEventData eventData) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Panel.parent as RectTransform,
            eventData.position,
            null,
            out startMouse
        );

        startSize = Panel.sizeDelta;
        startPos = Panel.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Panel.parent as RectTransform,
            eventData.position,
            null,
            out Vector2 currentMouse
        );

        Vector2 delta = currentMouse - startMouse;

        float width = startSize.x;
        float height = startSize.y;

        Vector2 pos = startPos;

        // RIGHT
        if(Type is ResizeHandleType.Right
            or ResizeHandleType.TopRight
            or ResizeHandleType.BottomRight) {
            float newWidth = Mathf.Max(
                MIN_WIDTH / MainCore.Config.UIScale,
                startSize.x + delta.x
            );

            float applied = newWidth - startSize.x;

            width = newWidth;
            pos.x += applied * 0.5f;
        }

        // LEFT
        if(Type is ResizeHandleType.Left
            or ResizeHandleType.TopLeft
            or ResizeHandleType.BottomLeft) {
            float newWidth = Mathf.Max(
                MIN_WIDTH / MainCore.Config.UIScale,
                startSize.x - delta.x
            );

            float applied = newWidth - startSize.x;

            width = newWidth;
            pos.x -= applied * 0.5f;
        }

        // TOP
        if(Type is ResizeHandleType.Top
            or ResizeHandleType.TopLeft
            or ResizeHandleType.TopRight) {
            float newHeight = Mathf.Max(
                MIN_HEIGHT / MainCore.Config.UIScale,
                startSize.y + delta.y
            );

            float applied = newHeight - startSize.y;

            height = newHeight;
            pos.y += applied * 0.5f;
        }

        // BOTTOM
        if(Type is ResizeHandleType.Bottom
            or ResizeHandleType.BottomLeft
            or ResizeHandleType.BottomRight) {
            float newHeight = Mathf.Max(
                MIN_HEIGHT / MainCore.Config.UIScale,
                startSize.y - delta.y
            );

            float applied = newHeight - startSize.y;

            height = newHeight;
            pos.y -= applied * 0.5f;
        }

        Panel.sizeDelta = new(width, height);
        Panel.anchoredPosition = pos;
    }

    private static readonly ResizeHandleType[] HandleOrder = {
        ResizeHandleType.TopLeft,
        ResizeHandleType.Top,
        ResizeHandleType.TopRight,

        ResizeHandleType.Left,
        ResizeHandleType.Right,

        ResizeHandleType.BottomLeft,
        ResizeHandleType.Bottom,
        ResizeHandleType.BottomRight
    };

    private const float HANDLE_CORNER = 32f;
    private const float HANDLE_SIDE = 10f;

    public static void CreateResizeHandles(RectTransform parent) {
        foreach(ResizeHandleType type in HandleOrder) {
            GameObject handle = new($"Resize_{type}");
            handle.transform.SetParent(parent, false);

            RectTransform rect =
                handle.AddComponent<RectTransform>();

            bool isCorner =
                type is ResizeHandleType.TopLeft
                or ResizeHandleType.TopRight
                or ResizeHandleType.BottomLeft
                or ResizeHandleType.BottomRight;

            if(isCorner) {
                rect.sizeDelta = new(
                    HANDLE_CORNER,
                    HANDLE_CORNER
                );
            }

            switch(type) {
                // Top
                case ResizeHandleType.Top:
                    rect.anchorMin = new(0, 1);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);

                    rect.offsetMin = new(
                        HANDLE_SIDE,
                        -HANDLE_SIDE
                    );

                    rect.offsetMax = new(
                        -HANDLE_SIDE,
                        HANDLE_SIDE
                    );
                    break;

                // Bottom
                case ResizeHandleType.Bottom:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(1, 0);
                    rect.pivot = new(0.5f, 0.5f);

                    rect.offsetMin = new(
                        HANDLE_SIDE,
                        -HANDLE_SIDE
                    );

                    rect.offsetMax = new(
                        -HANDLE_SIDE,
                        HANDLE_SIDE
                    );
                    break;

                // Left
                case ResizeHandleType.Left:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(0, 1);
                    rect.pivot = new(0.5f, 0.5f);

                    rect.offsetMin = new(
                        -HANDLE_SIDE,
                        HANDLE_SIDE
                    );

                    rect.offsetMax = new(
                        HANDLE_SIDE,
                        -HANDLE_SIDE
                    );
                    break;

                // Right
                case ResizeHandleType.Right:
                    rect.anchorMin = new(1, 0);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);

                    rect.offsetMin = new(
                        -HANDLE_SIDE,
                        HANDLE_SIDE
                    );

                    rect.offsetMax = new(
                        HANDLE_SIDE,
                        -HANDLE_SIDE
                    );
                    break;

                // Top Left
                case ResizeHandleType.TopLeft:
                    rect.anchorMin = new(0, 1);
                    rect.anchorMax = new(0, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    break;

                // Top Right
                case ResizeHandleType.TopRight:
                    rect.anchorMin = new(1, 1);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    break;

                // Bottom Left
                case ResizeHandleType.BottomLeft:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(0, 0);
                    rect.pivot = new(0.5f, 0.5f);
                    break;

                // Bottom Right
                case ResizeHandleType.BottomRight:
                    rect.anchorMin = new(1, 0);
                    rect.anchorMax = new(1, 0);
                    rect.pivot = new(0.5f, 0.5f);
                    break;
            }

            rect.anchoredPosition = Vector2.zero;

            Image image = handle.AddComponent<Image>();
            image.sprite = MainCore.Spr.Get(UISprite.Circle256);
            image.color = Color.clear;

            ResizeHandle resize = handle.AddComponent<ResizeHandle>();

            resize.Type = type;
            resize.Panel = parent;

            EventTrigger trigger = handle.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerEnter
            };

            enter.callback.AddListener(_ => {

            });

            var exit = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerExit
            };

            exit.callback.AddListener(_ => {

            });

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }
    }
}