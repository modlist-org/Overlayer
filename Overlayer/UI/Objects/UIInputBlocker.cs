using UnityEngine;
using UnityEngine.EventSystems;

namespace Overlayer.UI.Objects;

public static class UIInputBlocker {
    private static int focusedInputCount;

    public static bool IsEditing => focusedInputCount > 0;

    public static void SetFocused(bool focused, GameObject inputObject = null) {
        focusedInputCount = Math.Max(0, focusedInputCount + (focused ? 1 : -1));

        if(focused && inputObject != null && EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(inputObject);
        }

    }
}
