using UnityEngine;

namespace Overlayer.Compat.OVC.OS;

public abstract class OVC_OSAPI {
    public abstract void SetCursorPosition(int x, int y);
    public abstract Vector2Int GetCursorPosition();
}