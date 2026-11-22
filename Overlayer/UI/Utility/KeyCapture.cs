using Overlayer.Core;
using UnityEngine;

namespace Overlayer.UI.Utility;

// Waits for the next key press for a key-bind button, with whatever Ctrl / Shift / Alt is held.
// Modifier keys alone don't finish the capture. Esc cancels.
internal static class KeyCapture {
    private static readonly KeyCode[] Keys = [.. Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
        .Where(k => k != KeyCode.None && !(k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) && k < KeyCode.JoystickButton0
            && !KeyBind.IsModifierKey(k))
        .Distinct()];

    private static Action<KeyBind?> pending;
    private static int startFrame;
    private static int lastCaptureFrame = -1;

    public static bool Active => pending != null;
    public static bool SuppressShortcuts => pending != null || Time.frameCount == lastCaptureFrame;

    /// <summary><paramref name="onDone"/> gets the bind, or null when cancelled.</summary>
    public static void Begin(Action onStart, Action<KeyBind?> onDone) {
        Cancel();
        pending = onDone;
        startFrame = Time.frameCount;
        onStart?.Invoke();
    }

    public static void Cancel() {
        var done = pending;
        pending = null;
        done?.Invoke(null);
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
            lastCaptureFrame = Time.frameCount;
            try {
                done(new KeyBind(key, KeyBind.HeldModifiers()));
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(KeyCapture)}] Key bind handler failed: {e.Message}");
            }
            return;
        }
    }
}
