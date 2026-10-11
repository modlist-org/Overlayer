using System.Reflection;
using UnityEngine;

namespace Overlayer.Compat;

/// <summary>
/// Compatibility shim for <c>Canvas.worldCamera</c> / <c>Canvas.pixelPerfect</c>
/// setters, which stripped IL2CPP metadata may lack (only the getters ship).
/// Resolved by name; sets are skipped when the setter is absent.
/// </summary>
internal static class CanvasCompat {
    private static readonly PropertyInfo WorldCameraProp =
        typeof(Canvas).GetProperty("worldCamera");

    private static readonly PropertyInfo PixelPerfectProp =
        typeof(Canvas).GetProperty("pixelPerfect");

    public static void SetWorldCamera(Canvas canvas, Camera camera) {
        if (canvas == null || WorldCameraProp == null || !WorldCameraProp.CanWrite) {
            return;
        }
        try {
            WorldCameraProp.SetValue(canvas, camera, null);
        } catch {
        }
    }

    public static void SetPixelPerfect(Canvas canvas, bool pixelPerfect) {
        if (canvas == null || PixelPerfectProp == null || !PixelPerfectProp.CanWrite) {
            return;
        }
        try {
            PixelPerfectProp.SetValue(canvas, pixelPerfect, null);
        } catch {
        }
    }
}
