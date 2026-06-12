using System.Runtime.InteropServices;
using UnityEngine;

namespace Overlayer.Compat.OVC.OS;

public sealed class OVC_Mac : OVC_OSAPI {
    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint(double x, double y) {
        public double x = x;
        public double y = y;
    }

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern CGPoint CGEventSourceCreateMouseCursorPosition(int mouseStateSpace);

    public override void SetCursorPosition(int x, int y) {
        try {
            CGWarpMouseCursorPosition(new CGPoint(x, y));
        } catch { }
    }

    public override Vector2Int GetCursorPosition() {
        try {
            CGPoint p = CGEventSourceCreateMouseCursorPosition(0);
            return new Vector2Int(Mathf.RoundToInt((float)p.x), Mathf.RoundToInt((float)p.y));
        } catch { }

        return Vector2Int.zero;
    }
}