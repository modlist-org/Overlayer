using O5Kit.Control;
using Overlayer.UI.Utility;
using UnityEngine;

namespace Overlayer.HudEdit;

// The bar along the bottom: arranging what's selected (align, distribute, group, order,
// lock / hide). The top bar keeps making things and the view. Tools that can't do anything
// with the current selection are greyed out.
internal static partial class HudEditor {
    // Room the shortcut hints keep above the bottom bar.
    private const float HintBottom = Margin * 2f + ToolbarHeight + 4f;

    private static RectTransform bottomArea;
    private static CanvasGroup bottomGroup;
    private static readonly List<(O5Button Button, Func<bool> Enabled)> bottomTools = [];

    private static void BuildBottomBar() {
        bottomTools.Clear();
        var area = bottomArea = NewRect("BottomBarArea", root);
        area.anchorMin = Vector2.zero;
        area.anchorMax = new Vector2(1f, 0f);
        area.offsetMin = Vector2.zero;
        area.offsetMax = new Vector2(0f, 1f);
        var bar = UiStyle.Rounded("BottomBar", area, UiStyle.Surface).rectTransform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, Margin);
        bar.sizeDelta = new Vector2(0f, ToolbarHeight);
        bottomGroup = bar.gameObject.AddComponent<CanvasGroup>();
        UiStyle.Horizontal(bar.gameObject, 4f, new RectOffset(6, 6, 6, 6));
        bar.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        bool CanAlign() => selection.Count(e => e.IsAlive && e.CanMove) >= 2;
        bool CanDistribute() => selection.Count(e => e.IsAlive && e.CanMove) >= 3;
        MarkTool(bar, "align_l", AlignPicker.Mark.Left, "Align left", () => Align(AlignMode.Left), CanAlign);
        MarkTool(bar, "align_c", AlignPicker.Mark.Center, "Align centers (horizontally)", () => Align(AlignMode.CenterX), CanAlign);
        MarkTool(bar, "align_r", AlignPicker.Mark.Right, "Align right", () => Align(AlignMode.Right), CanAlign);
        MarkTool(bar, "align_t", AlignPicker.Mark.Top, "Align top", () => Align(AlignMode.Top), CanAlign);
        MarkTool(bar, "align_m", AlignPicker.Mark.Middle, "Align middles (vertically)", () => Align(AlignMode.CenterY), CanAlign);
        MarkTool(bar, "align_b", AlignPicker.Mark.Bottom, "Align bottom", () => Align(AlignMode.Bottom), CanAlign);
        UiStyle.Separator(bar, true);
        MarkTool(bar, "dist_x", AlignPicker.Mark.DistributeX, "Even gaps across (3 or more)", () => Align(AlignMode.DistributeX), CanDistribute);
        MarkTool(bar, "dist_y", AlignPicker.Mark.DistributeY, "Even gaps down (3 or more)", () => Align(AlignMode.DistributeY), CanDistribute);
        UiStyle.Separator(bar, true);
        TextTool(bar, "group", "Group", 70f, "Ctrl+G", GroupSelection, () => selection.Any(e => e.IsAlive && !e.Locked));
        TextTool(bar, "ungroup", "Ungroup", 84f, "Ctrl+Shift+G", UngroupSelection, () => selection.Any(e => e.IsAlive && !e.Locked && e.Object.Children.Count > 0));
        UiStyle.Separator(bar, true);
        bool CanOrder() => selection.Count == 1 && Primary is { IsAlive: true } p && (!p.Locked || p.LayoutDriven);
        TextTool(bar, "front", "Front", 64f, "Bring to front  Ctrl+Shift+]", () => MoveToEnd(true), CanOrder);
        TextTool(bar, "back", "Back", 60f, "Send to back  Ctrl+Shift+[", () => MoveToEnd(false), CanOrder);
        UiStyle.Separator(bar, true);
        TextTool(bar, "lock", "Lock", 60f, "Lock / unlock: locked things can't be picked on screen  Ctrl+Shift+L", ToggleLockSelection, () => selection.Any(e => e.IsAlive));
        TextTool(bar, "hide", "Hide", 60f, "Ctrl+Shift+H", HideSelection, () => selection.Any(e => e.IsAlive && !e.Locked));
        UpdateBottomBar();
    }

    private static void MarkTool(RectTransform bar, string id, AlignPicker.Mark mark, string tooltip, Action run, Func<bool> enabled) {
        var button = Button(Cell(bar, ToolbarHeight - 12f), id, string.Empty, run, tooltip);
        var icon = NewRect("Icon", button.Rect);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(18f, 16f);
        AlignPicker.DrawIcon(icon, mark);
        bottomTools.Add((button, enabled));
    }

    private static void TextTool(RectTransform bar, string id, string text, float width, string tooltip, Action run, Func<bool> enabled)
        => bottomTools.Add((Button(Cell(bar, width), id, text, run, tooltip), enabled));

    // HUD pieces along the bottom edge sit under the bar: while dragging, it steps aside
    // (nearly see-through, the pointer goes through) so things can be moved under it.
    private static void FadeBottomBarWhileDragging() {
        if (bottomGroup == null) {
            return;
        }
        bool dragging = drag != DragMode.None;
        bottomGroup.alpha = Mathf.MoveTowards(bottomGroup.alpha, dragging ? 0.12f : 1f, Time.unscaledDeltaTime * 8f);
        bottomGroup.blocksRaycasts = !dragging;
    }

    // After the selection changed.
    private static void UpdateBottomBar() {
        foreach (var (button, enabled) in bottomTools) {
            if (button != null) {
                button.SetBlocked(!enabled(), true);
            }
        }
    }
}
