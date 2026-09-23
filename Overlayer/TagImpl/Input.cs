using O5Kit.Input;
using Overlayer.Tag.Core;
using UnityEngine;

namespace Overlayer.TagImpl;

public static class Input {
    [Tag(Desc = "[Unity] Current mouse X position in screen coordinates")]
    public static float MouseX => O5Input.MousePosition.x;

    [Tag(Desc = "[Unity] Current mouse Y position in screen coordinates")]
    public static float MouseY => O5Input.MousePosition.y;

    [Tag(Desc = "[Unity] Returns true while the specified key is held down\nEx) {IsKeyHeld:A}, {IsKeyHeld:Space}")]
    public static bool IsKeyHeld(KeyCode key) => O5Input.GetKey(key);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified key was pressed\nEx) {IsKeyDown:A}")]
    public static bool IsKeyDown(KeyCode key) => O5Input.GetKeyDown(key);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified key was released\nEx) {IsKeyUp:A}")]
    public static bool IsKeyUp(KeyCode key) => O5Input.GetKeyUp(key);

    [Tag(Desc = "[Unity] Returns true while the specified mouse button is held down\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseHeld:0}")]
    public static bool IsMouseHeld(int button = 0) => O5Input.GetMouseButton(button);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified mouse button was pressed\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseDown:0}")]
    public static bool IsMouseDown(int button = 0) => O5Input.GetMouseButtonDown(button);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified mouse button was released\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseUp:0}")]
    public static bool IsMouseUp(int button = 0) => O5Input.GetMouseButtonUp(button);

    [Tag(Desc = "[Unity] Returns true while any mouse button (Left/Right/Middle) is held down")]
    public static bool IsAnyMouseHeld
        => O5Input.GetMouseButton(0) || O5Input.GetMouseButton(1) || O5Input.GetMouseButton(2);

    [Tag(Desc = "[Unity] Returns true only on the frame any mouse button (Left/Right/Middle) was pressed")]
    public static bool IsAnyMouseDown
        => O5Input.GetMouseButtonDown(0) || O5Input.GetMouseButtonDown(1) || O5Input.GetMouseButtonDown(2);

    [Tag(Desc = "[Unity] Returns true only on the frame any mouse button (Left/Right/Middle) was released")]
    public static bool IsAnyMouseUp
        => O5Input.GetMouseButtonUp(0) || O5Input.GetMouseButtonUp(1) || O5Input.GetMouseButtonUp(2);

    [Tag(Desc = "[Unity] Mouse scroll wheel delta on the X axis")]
    public static float MouseScrollX => O5Input.MouseScrollDelta.x;

    [Tag(Desc = "[Unity] Mouse scroll wheel delta on the Y axis")]
    public static float MouseScrollY => O5Input.MouseScrollDelta.y;

    [Tag(Desc = "[Unity] Mouse movement delta on the X axis since the last frame")]
    public static float MouseDeltaX => O5Input.MouseDelta.x;

    [Tag(Desc = "[Unity] Mouse movement delta on the Y axis since the last frame")]
    public static float MouseDeltaY => O5Input.MouseDelta.y;

    [Tag(Desc = "[Unity] Returns true while any key or mouse button is held down")]
    public static bool IsAnyKeyHeld => UnityEngine.Input.anyKey;

    [Tag(Desc = "[Unity] Returns true only on the frame any key or mouse button was pressed")]
    public static bool IsAnyKeyDown => UnityEngine.Input.anyKeyDown;
}
