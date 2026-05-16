using Steamworks;
using UnityEngine;

namespace Overlayer.UI;

public static class UIColors {
    public static readonly Color PanelBG = new(0.165f, 0.161f, 0.196f, 1f);

    public static readonly Color TopBar = new(0.255f, 0.259f, 0.333f, 1f);
    public static readonly Color TopBarCloseCircle = new(0.886f, 0.404f, 0.427f, 0f);

    public static readonly Color MenuBG = new(0.42f, 0.431f, 0.545f, 1f);
    public static readonly Color MenuNormal = new(0.635f, 0.655f, 0.878f, 0f);
    public static readonly Color MenuHover = new(0.635f, 0.655f, 0.878f, 0.4f);
    public static readonly Color MenuSelected = new(0.635f, 0.655f, 0.878f, 1f);
    public static readonly Color MenuHighlight = new(0.824f, 0.835f, 0.965f, 1f);

    public static readonly Color ObjectBG = new(0.235f, 0.227f, 0.294f, 1f);
    public static readonly Color ObjectActive = new(0.569f, 0.604f, 1f, 1f);
    public static readonly Color ObjectInactive = new(0.569f, 0.604f, 1f, 0.4f);
    public static readonly Color ObjectActiveBright = new(0.812f, 0.827f, 1f, 1f);
    public static readonly Color ObjectButton = new(0.478f, 0.514f, 0.875f, 1f);
}
