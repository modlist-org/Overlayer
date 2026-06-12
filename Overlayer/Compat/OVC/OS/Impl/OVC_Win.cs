using System.Runtime.InteropServices;
using UnityEngine;

namespace Overlayer.Compat.OVC.OS.Impl;

public sealed class OVC_Win : OVC_OSAPI {
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    public override void SetCursorPosition(int x, int y) {
        try { SetCursorPos(x, y); } catch { }
    }

    public override Vector2Int GetCursorPosition() {
        try {
            if(GetCursorPos(out POINT p)) {
                return new Vector2Int(p.X, p.Y);
            }
        } catch { }
        return Vector2Int.zero;
    }
}