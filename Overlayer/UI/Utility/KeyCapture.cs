using Overlayer.Core;
using UnityEngine;

namespace Overlayer.UI.Utility;

// Waits for the next key press for a key-bind button. Esc cancels.
internal static class KeyCapture {
    private static readonly KeyCode[] Keys = [.. Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
        .Where(k => k != KeyCode.None && !(k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) && k < KeyCode.JoystickButton0)
        .Distinct()];

    private static Action<KeyCode> pending;
    private static int startFrame;

    public static bool Active => pending != null;

    public static void Begin(Action onStart, Action<KeyCode> onDone) {
        Cancel();
        pending = onDone;
        startFrame = Time.frameCount;
        onStart?.Invoke();
    }

    public static void Cancel() {
        var done = pending;
        pending = null;
        done?.Invoke(KeyCode.None);
    }

    public static void Tick() {
        // Skip the frame of the click that started capture.
        if (pending == null || Time.frameCount == startFrame) {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Cancel();
            return;
        }
        foreach (var key in Keys) {
            if (!Input.GetKeyDown(key)) {
                continue;
            }
            var done = pending;
            pending = null;
            try {
                done(key);
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(KeyCapture)}] Key bind handler failed: {e.Message}");
            }
            return;
        }
    }
}
