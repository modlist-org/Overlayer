using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.HudEdit;

// Read-only views into the editor for the tutorial: what to point at and what the user did.
internal static partial class HudEditor {
    /// <summary>A piece of the editor by its path under the editor's root ("Panel", "Layers", ...).</summary>
    internal static RectTransform Part(string path) => root != null ? root.Find(path) as RectTransform : null;

    /// <summary>A toolbar / bottom bar / panel button by the id it was made with ("add_text", "grid", "align_l" ...).</summary>
    internal static RectTransform Control(string id)
        => controls.FirstOrDefault(c => c != null && c.Id == "plus_hud_" + id)?.Rect;

    internal static OvObject SelectedObject => Primary?.Object;

    internal static int SelectedCount => selection.Count;

    internal static bool PresetMenuOpen => contextMenu != null && contextMenu.name == "PresetMenu";

    internal static bool AnyContextMenuOpen => contextMenu != null;

    internal static bool AnyDetached => docks.Any(d => d.Window != null);
}
