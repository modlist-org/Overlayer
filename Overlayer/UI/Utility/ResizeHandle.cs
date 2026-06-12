using Overlayer.Compat.OVC;
using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using MelonLoader;
using Il2CppInterop.Runtime;
#endif

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

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class ResizeHandle

#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    public ResizeHandleType Type;
    public RectTransform Panel;
    public RectTransform PanelParent;

    private Vector2 startMouse;
    private Vector2 startSize;
    private Vector2 startPos;

    public const float MIN_WIDTH = 900f;
    public const float MIN_HEIGHT = 500f;

    private void Awake() {
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
        startSize = Panel.sizeDelta;
        startPos = Panel.anchoredPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PanelParent,
            OVC_Input.MousePosition,
            null,
            out startMouse
        );
    }

    public void OnDragInternal() {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PanelParent,
            OVC_Input.MousePosition,
            null,
            out Vector2 currentMouse
        );

        Vector2 delta = currentMouse - startMouse;
        Vector2 newSize = startSize;
        Vector2 newPos = startPos;

        float minW = MIN_WIDTH / MainCore.Conf.UIScale;
        float minH = MIN_HEIGHT / MainCore.Conf.UIScale;

        Vector2 pivot = Panel.pivot;

        if(Type is ResizeHandleType.Right or ResizeHandleType.TopRight or ResizeHandleType.BottomRight) {
            newSize.x = Math.Max(minW, startSize.x + delta.x);
        } else if(Type is ResizeHandleType.Left or ResizeHandleType.TopLeft or ResizeHandleType.BottomLeft) {
            newSize.x = Math.Max(minW, startSize.x - delta.x);
        }

        if(Type is ResizeHandleType.Top or ResizeHandleType.TopLeft or ResizeHandleType.TopRight) {
            newSize.y = Math.Max(minH, startSize.y + delta.y);
        } else if(Type is ResizeHandleType.Bottom or ResizeHandleType.BottomLeft or ResizeHandleType.BottomRight) {
            newSize.y = Math.Max(minH, startSize.y - delta.y);
        }

        Vector2 sizeDiff = newSize - startSize;

        if(Type is ResizeHandleType.Right or ResizeHandleType.TopRight or ResizeHandleType.BottomRight) {
            newPos.x = startPos.x + (sizeDiff.x * (1f - pivot.x));
        } else if(Type is ResizeHandleType.Left or ResizeHandleType.TopLeft or ResizeHandleType.BottomLeft) {
            newPos.x = startPos.x - (sizeDiff.x * pivot.x);
        }

        if(Type is ResizeHandleType.Top or ResizeHandleType.TopLeft or ResizeHandleType.TopRight) {
            newPos.y = startPos.y + (sizeDiff.y * (1f - pivot.y));
        } else if(Type is ResizeHandleType.Bottom or ResizeHandleType.BottomLeft or ResizeHandleType.BottomRight) {
            newPos.y = startPos.y - (sizeDiff.y * pivot.y);
        }

        Panel.sizeDelta = newSize;
        Panel.anchoredPosition = newPos;
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

    private const float HANDLE_CORNER = 26f;
    private const float HANDLE_SIDE = 12f;
    public static void CreateResizeHandles(RectTransform panel, RectTransform panelParent) {
        foreach(ResizeHandleType type in HandleOrder) {
            GameObject handle = new($"Resize_{type}");
            handle.transform.SetParent(panel, false);

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
                case ResizeHandleType.Top:
                    rect.anchorMin = new(0, 1);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.offsetMin = new(HANDLE_SIDE, -HANDLE_SIDE);
                    rect.offsetMax = new(-HANDLE_SIDE, HANDLE_SIDE);
                    rect.anchoredPosition = Vector2.zero;
                    break;

                case ResizeHandleType.Bottom:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(1, 0);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.offsetMin = new(HANDLE_SIDE, -HANDLE_SIDE);
                    rect.offsetMax = new(-HANDLE_SIDE, HANDLE_SIDE);
                    rect.anchoredPosition = Vector2.zero;
                    break;

                case ResizeHandleType.Left:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(0, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.offsetMin = new(-HANDLE_SIDE, HANDLE_SIDE);
                    rect.offsetMax = new(HANDLE_SIDE, -HANDLE_SIDE);
                    rect.anchoredPosition = Vector2.zero;
                    break;

                case ResizeHandleType.Right:
                    rect.anchorMin = new(1, 0);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.offsetMin = new(-HANDLE_SIDE, HANDLE_SIDE);
                    rect.offsetMax = new(HANDLE_SIDE, -HANDLE_SIDE);
                    rect.anchoredPosition = Vector2.zero;
                    break;

                case ResizeHandleType.TopLeft:
                    rect.anchorMin = new(0, 1);
                    rect.anchorMax = new(0, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.anchoredPosition = new(-HANDLE_CORNER * 0.5f, HANDLE_CORNER * 0.5f);
                    break;

                case ResizeHandleType.TopRight:
                    rect.anchorMin = new(1, 1);
                    rect.anchorMax = new(1, 1);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.anchoredPosition = new(HANDLE_CORNER * 0.5f, HANDLE_CORNER * 0.5f);
                    break;

                case ResizeHandleType.BottomLeft:
                    rect.anchorMin = new(0, 0);
                    rect.anchorMax = new(0, 0);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.anchoredPosition = new(-HANDLE_CORNER * 0.5f, -HANDLE_CORNER * 0.5f);
                    break;

                case ResizeHandleType.BottomRight:
                    rect.anchorMin = new(1, 0);
                    rect.anchorMax = new(1, 0);
                    rect.pivot = new(0.5f, 0.5f);
                    rect.anchoredPosition = new(HANDLE_CORNER * 0.5f, -HANDLE_CORNER * 0.5f);
                    break;
            }

            rect.anchoredPosition = Vector2.zero;

            Image image = handle.AddComponent<Image>();
            image.sprite = MainCore.Spr.Get(UISprite.Circle256);
            image.color = Color.clear;

            ResizeHandle resize = handle.AddComponent<ResizeHandle>();

            resize.Type = type;
            resize.Panel = panel;
            resize.PanelParent = panelParent;
        }
    }
}