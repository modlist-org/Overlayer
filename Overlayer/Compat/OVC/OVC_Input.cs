using System.Reflection;
using UnityEngine;

namespace Overlayer.Compat.OVC;

/*
 * This file contains components derived from UniverseLib.
 * * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 2.1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

public static class OVC_Input {
    private static Type t_Keyboard, t_Key, t_ButtonControl, t_Mouse, t_Pointer;
    private static PropertyInfo p_kbCurrent, p_kbIndexer, p_btnIsPressed, p_btnWasPressed, p_btnWasReleased;
    private static PropertyInfo p_mouseCurrent, p_mousePosition, p_mouseScroll;
    private static PropertyInfo p_leftBtn, p_rightBtn, p_middleBtn;
    private static MethodInfo m_ReadV2;
    private static bool _initialized;

    private static void EnsureInitialized() {
        if(_initialized) {
            return;
        }

        t_Keyboard = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
        t_Key = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
        t_ButtonControl = Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem");
        t_Mouse = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
        t_Pointer = Type.GetType("UnityEngine.InputSystem.Pointer, Unity.InputSystem");

        if(t_Keyboard != null) {
            p_kbCurrent = t_Keyboard.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            p_kbIndexer = t_Keyboard.GetProperty("Item", [t_Key]);
            p_btnIsPressed = t_ButtonControl.GetProperty("isPressed");
            p_btnWasPressed = t_ButtonControl.GetProperty("wasPressedThisFrame");
            p_btnWasReleased = t_ButtonControl.GetProperty("wasReleasedThisFrame");
        }

        if(t_Mouse != null) {
            p_mouseCurrent = t_Mouse.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            p_leftBtn = t_Mouse.GetProperty("leftButton");
            p_rightBtn = t_Mouse.GetProperty("rightButton");
            p_middleBtn = t_Mouse.GetProperty("middleButton");
            p_mouseScroll = t_Mouse.GetProperty("scroll");
            p_mousePosition = t_Pointer.GetProperty("position");
            m_ReadV2 = t_Pointer.Assembly.GetType("UnityEngine.InputSystem.InputControl`1")
                .MakeGenericType(typeof(Vector2)).GetMethod("ReadValue");
        }
        _initialized = true;
    }

    public static bool GetKey(KeyCode key) {
        EnsureInitialized();
        return t_Keyboard != null ? TryInvoke(p_btnIsPressed, GetKeyControl(key)) : Input.GetKey(key);
    }

    public static bool GetKeyDown(KeyCode key) {
        EnsureInitialized();
        return t_Keyboard != null ? TryInvoke(p_btnWasPressed, GetKeyControl(key)) : Input.GetKeyDown(key);
    }

    public static bool GetKeyUp(KeyCode key) {
        EnsureInitialized();
        return t_Keyboard != null ? TryInvoke(p_btnWasReleased, GetKeyControl(key)) : Input.GetKeyUp(key);
    }

    public static bool GetMouseButton(int btn) {
        EnsureInitialized();
        return t_Mouse != null ? TryInvoke(p_btnIsPressed, GetMouseBtnControl(btn)) : Input.GetMouseButton(btn);
    }

    public static bool GetMouseButtonDown(int btn) {
        EnsureInitialized();
        return t_Mouse != null ? TryInvoke(p_btnWasPressed, GetMouseBtnControl(btn)) : Input.GetMouseButtonDown(btn);
    }

    public static bool GetMouseButtonUp(int btn) {
        EnsureInitialized();
        return t_Mouse != null ? TryInvoke(p_btnWasReleased, GetMouseBtnControl(btn)) : Input.GetMouseButtonUp(btn);
    }

    public static Vector2 MousePosition {
        get {
            EnsureInitialized();
            if(t_Mouse != null) {
                try { return (Vector2)m_ReadV2.Invoke(p_mousePosition.GetValue(p_mouseCurrent.GetValue(null)), null); } catch { return Vector2.zero; }
            }
            return Input.mousePosition;
        }
    }

    public static Vector2 MouseScrollDelta {
        get {
            EnsureInitialized();
            if(t_Mouse != null) {
                try { return (Vector2)m_ReadV2.Invoke(p_mouseScroll.GetValue(p_mouseCurrent.GetValue(null)), null); } catch { return Vector2.zero; }
            }
            return Input.mouseScrollDelta;
        }
    }

    private static object GetKeyControl(KeyCode key) {
        EnsureInitialized();
        string s = key.ToString();
        if(s == "BackQuote") {
            s = "Backquote";
        }

        s = s.Replace("Alpha", "Digit").Replace("Control", "Ctrl").Replace("Return", "Enter");
        try { return p_kbIndexer.GetValue(p_kbCurrent.GetValue(null), [Enum.Parse(t_Key, s)]); } catch { return null; }
    }

    private static object GetMouseBtnControl(int btn) {
        EnsureInitialized();
        var mouse = p_mouseCurrent.GetValue(null);
        return btn switch {
            0 => p_leftBtn.GetValue(mouse),
            1 => p_rightBtn.GetValue(mouse),
            2 => p_middleBtn.GetValue(mouse),
            _ => null
        };
    }

    private static bool TryInvoke(PropertyInfo prop, object target) {
        try {
            return target != null && (bool)prop.GetValue(target);
        } catch {
            return false;
        }
    }
}