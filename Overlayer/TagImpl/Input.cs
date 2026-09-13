using Overlayer.Compat.OVC;
using Overlayer.Tag.Core;
using UnityEngine;

namespace Overlayer.TagImpl;

public static class Input {
    [Tag(Desc = "[Unity] Current mouse X position in screen coordinates")]
    public static float MouseX => OVC_Input.MousePosition.x;

    [Tag(Desc = "[Unity] Current mouse Y position in screen coordinates")]
    public static float MouseY => OVC_Input.MousePosition.y;

    [Tag(Desc = "[Unity] Returns true while the specified key is held down\nEx) {IsKeyHeld:A}, {IsKeyHeld:Space}")]
    public static bool IsKeyHeld(KeyCode key) => OVC_Input.GetKey(key);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified key was pressed\nEx) {IsKeyDown:A}")]
    public static bool IsKeyDown(KeyCode key) => OVC_Input.GetKeyDown(key);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified key was released\nEx) {IsKeyUp:A}")]
    public static bool IsKeyUp(KeyCode key) => OVC_Input.GetKeyUp(key);

    [Tag(Desc = "[Unity] Returns true while the specified mouse button is held down\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseHeld:0}")]
    public static bool IsMouseHeld(int button = 0) => OVC_Input.GetMouseButton(button);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified mouse button was pressed\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseDown:0}")]
    public static bool IsMouseDown(int button = 0) => OVC_Input.GetMouseButtonDown(button);

    [Tag(Desc = "[Unity] Returns true only on the frame the specified mouse button was released\n0: Left, 1: Right, 2: Middle\nEx) {IsMouseUp:0}")]
    public static bool IsMouseUp(int button = 0) => OVC_Input.GetMouseButtonUp(button);

    [Tag(Desc = "[Unity] Returns true while any mouse button (Left/Right/Middle) is held down")]
    public static bool IsAnyMouseHeld
        => OVC_Input.GetMouseButton(0) || OVC_Input.GetMouseButton(1) || OVC_Input.GetMouseButton(2);

    [Tag(Desc = "[Unity] Returns true only on the frame any mouse button (Left/Right/Middle) was pressed")]
    public static bool IsAnyMouseDown
        => OVC_Input.GetMouseButtonDown(0) || OVC_Input.GetMouseButtonDown(1) || OVC_Input.GetMouseButtonDown(2);

    [Tag(Desc = "[Unity] Returns true only on the frame any mouse button (Left/Right/Middle) was released")]
    public static bool IsAnyMouseUp
        => OVC_Input.GetMouseButtonUp(0) || OVC_Input.GetMouseButtonUp(1) || OVC_Input.GetMouseButtonUp(2);

    [Tag(Desc = "[Unity] Mouse scroll wheel delta on the X axis")]
    public static float MouseScrollX => OVC_Input.MouseScrollDelta.x;

    [Tag(Desc = "[Unity] Mouse scroll wheel delta on the Y axis")]
    public static float MouseScrollY => OVC_Input.MouseScrollDelta.y;

    [Tag(Desc = "[Unity] Mouse movement delta on the X axis since the last frame")]
    public static float MouseDeltaX => OVC_Input.MouseDelta.x;

    [Tag(Desc = "[Unity] Mouse movement delta on the Y axis since the last frame")]
    public static float MouseDeltaY => OVC_Input.MouseDelta.y;

    [Tag(Desc = "[Unity] Returns true while any key or mouse button is held down")]
    public static bool IsAnyKeyHeld => UnityEngine.Input.anyKey;

    [Tag(Desc = "[Unity] Returns true only on the frame any key or mouse button was pressed")]
    public static bool IsAnyKeyDown => UnityEngine.Input.anyKeyDown;
}
