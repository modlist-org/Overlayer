using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Tween;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// The bar along the bottom: arranging what's selected (align, distribute, group, order,
// lock / hide) plus a pop-up shortcut card. Tools that can't do anything with the current
// selection are greyed out.
internal static partial class HudEditor {
    private static RectTransform bottomArea, bottomBar, keysBubble, keysTail;
    private static CanvasGroup bottomGroup, keysGroup;
    private static O5Button keysButton;
    private static ITweenHandle keysTween;
    private static bool keysOpen;
    private static readonly List<(O5Button Button, Func<bool> Enabled)> bottomTools = [];

    private static bool KeysBubbleOpen => keysOpen;

    private static void BuildBottomBar() {
        bottomTools.Clear();
        var area = bottomArea = NewRect("BottomBarArea", root);
        area.anchorMin = Vector2.zero;
        area.anchorMax = new Vector2(1f, 0f);
        area.offsetMin = Vector2.zero;
        area.offsetMax = new Vector2(0f, 1f);
        var bar = bottomBar = UiStyle.Rounded("BottomBar", area, UiStyle.Surface).rectTransform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, Margin);
        bar.sizeDelta = new Vector2(0f, ToolbarHeight);
        bottomGroup = bar.gameObject.AddComponent<CanvasGroup>();
        UiStyle.Horizontal(bar.gameObject, 4f, new RectOffset(6, 6, 6, 6));
        bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // One thing lines up with its parent (or the screen), several with each other.
        bool CanAlign() => selection.Any(e => e.IsAlive && e.CanMove);
        bool CanDistribute() => selection.Count(e => e.IsAlive && e.CanMove) >= 3;
        MarkTool(bar, "align_l", AlignPicker.Mark.Left, Tr("HUD_ALIGN_LEFT", "Align left"), () => Align(AlignMode.Left), CanAlign);
        MarkTool(bar, "align_c", AlignPicker.Mark.Center, Tr("HUD_ALIGN_CENTER", "Align center"), () => Align(AlignMode.CenterX), CanAlign);
        MarkTool(bar, "align_r", AlignPicker.Mark.Right, Tr("HUD_ALIGN_RIGHT", "Align right"), () => Align(AlignMode.Right), CanAlign);
        MarkTool(bar, "align_t", AlignPicker.Mark.Top, Tr("HUD_ALIGN_TOP", "Align top"), () => Align(AlignMode.Top), CanAlign);
        MarkTool(bar, "align_m", AlignPicker.Mark.Middle, Tr("HUD_ALIGN_MIDDLE", "Align middle"), () => Align(AlignMode.CenterY), CanAlign);
        MarkTool(bar, "align_b", AlignPicker.Mark.Bottom, Tr("HUD_ALIGN_BOTTOM", "Align bottom"), () => Align(AlignMode.Bottom), CanAlign);
        UiStyle.Separator(bar, true);
        MarkTool(bar, "dist_x", AlignPicker.Mark.DistributeX, Tr("HUD_DIST_X_TIP", "Distribute horizontally"), () => Align(AlignMode.DistributeX), CanDistribute);
        MarkTool(bar, "dist_y", AlignPicker.Mark.DistributeY, Tr("HUD_DIST_Y_TIP", "Distribute vertically"), () => Align(AlignMode.DistributeY), CanDistribute);
        UiStyle.Separator(bar, true);
        TextTool(bar, "group", Tr("HUD_GROUP", "Group"), 70f, Tr("HUD_GROUP", "Group"), GroupSelection, () => selection.Any(e => e.IsAlive && !e.Locked));
        TextTool(bar, "ungroup", Tr("HUD_UNGROUP", "Ungroup"), 84f, Tr("HUD_UNGROUP", "Ungroup"), UngroupSelection, () => selection.Any(e => e.IsAlive && !e.Locked && e.Object.Children.Count > 0));
        UiStyle.Separator(bar, true);
        bool CanOrder() => selection.Count == 1 && Primary is { IsAlive: true } p && (!p.Locked || p.LayoutDriven);
        TextTool(bar, "front", Tr("HUD_FRONT", "Front"), 68f, Tr("HUD_BRING_FRONT", "Bring to front"), () => MoveToEnd(true), CanOrder);
        TextTool(bar, "back", Tr("HUD_BACK", "Back"), 68f, Tr("HUD_SEND_BACK", "Send to back"), () => MoveToEnd(false), CanOrder);
        UiStyle.Separator(bar, true);
        TextTool(bar, "lock", Tr("HUD_LOCK", "Lock"), 60f, Tr("HUD_LOCK", "Lock"), ToggleLockSelection, () => selection.Any(e => e.IsAlive));
        TextTool(bar, "hide", Tr("HUD_HIDE", "Hide"), 60f, Tr("HUD_HIDE", "Hide"), HideSelection, () => selection.Any(e => e.IsAlive && !e.Locked));
        UiStyle.Separator(bar, true);
        keysButton = Button(Cell(bar, 108f), "keys", Tr("HUD_SHOW_KEYS", "Show Keys"), ToggleKeysBubble, Tr("HUD_SHOW_KEYS_TIP", "Shows the shortcut list."));
        if (keysButton.Label != null) {
            TmpCompat.SetNoWrap(keysButton.Label);
        }
        UpdateBottomBar();
    }

    private static void MarkTool(RectTransform bar, string id, AlignPicker.Mark mark, string tooltip, Action run, Func<bool> enabled) {
        var button = Button(Cell(bar, ToolbarHeight - 12f), id, string.Empty, run, tooltip);
        var icon = NewRect("Icon", button.Rect);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(18f, 18f);
        AlignPicker.DrawIcon(icon, mark);
        bottomTools.Add((button, enabled));
    }

    private static void TextTool(RectTransform bar, string id, string text, float width, string tooltip, Action run, Func<bool> enabled)
        => bottomTools.Add((Button(Cell(bar, width), id, text, run, tooltip), enabled));

    // HUD pieces along the screen edges can sit under the bars, rulers or side panels: while
    // dragging, fade them nearly see-through and pass pointer raycasts through so placement stays clear.
    private static void FadeBottomBarWhileDragging() {
        bool dragging = drag != DragMode.None;
        float step = Time.unscaledDeltaTime * 8f;
        if (bottomGroup != null) {
            bottomGroup.alpha = Mathf.MoveTowards(bottomGroup.alpha, dragging ? 0.12f : 1f, step);
            bottomGroup.blocksRaycasts = !dragging;
        }
        bool draggingObj = dragChanged && drag is DragMode.Move or DragMode.Scale or DragMode.Rotate or DragMode.Resize;
        float panelAlpha = draggingObj ? 0.08f : 1f;
        if (toolbarGroup != null) {
            toolbarGroup.alpha = Mathf.MoveTowards(toolbarGroup.alpha, panelAlpha, step);
            toolbarGroup.blocksRaycasts = !draggingObj;
        }
        if (rulerBarsGroup != null) {
            rulerBarsGroup.alpha = Mathf.MoveTowards(rulerBarsGroup.alpha, panelAlpha, step);
            rulerBarsGroup.blocksRaycasts = !draggingObj;
        }
        if (panelGroup != null) {
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, panelAlpha, step);
            panelGroup.blocksRaycasts = !draggingObj;
        }
        if (layersGroup != null) {
            layersGroup.alpha = Mathf.MoveTowards(layersGroup.alpha, panelAlpha, step);
            layersGroup.blocksRaycasts = !draggingObj;
        }
    }

    // After the selection changed.
    private static void UpdateBottomBar() {
        foreach (var (button, enabled) in bottomTools) {
            if (button != null) {
                button.SetBlocked(!enabled(), true);
            }
        }
        bool canEditSel = selection.Any(e => e.IsAlive && !e.Locked);
        duplicateButton?.SetBlocked(!canEditSel, true);
        deleteButton?.SetBlocked(!canEditSel, true);
    }

    private static void BuildKeysBubble() {
        keysBubble = UiStyle.Rounded("KeysBubble", root, UiStyle.Surface).rectTransform;
        keysGroup = keysBubble.gameObject.AddComponent<CanvasGroup>();
        keysBubble.anchorMin = keysBubble.anchorMax = new Vector2(0.5f, 0.5f);
        keysBubble.sizeDelta = new Vector2(490f, 0f);
        UiStyle.Vertical(keysBubble.gameObject, 6f, new RectOffset(16, 16, 12, 14));
        keysBubble.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        keysTail = NewImage("Tail", keysBubble, UiStyle.Surface).rectTransform;
        keysTail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        keysTail.anchorMin = keysTail.anchorMax = new Vector2(0.5f, 0f);
        keysTail.pivot = new Vector2(0.5f, 0.5f);
        keysTail.sizeDelta = new Vector2(12f, 12f);
        keysTail.localEulerAngles = new Vector3(0f, 0f, 45f);
        keysTail.SetAsFirstSibling();

        UiStyle.Section(keysBubble, Tr("HUD_SHORTCUTS", "Shortcuts"));
        var body = O5Factory.ControlText(O5KitAdapters.Ctx, keysBubble, 13.5f, false);
        body.raycastTarget = false;
        TmpCompat.SetNoWrap(body);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 6f;
        static string Row(string keyLabel, string descKey, string descFallback)
            => $"<b>{keyLabel}</b><pos=178><alpha=#AA>{Tr(descKey, descFallback)}";
        body.text = string.Join("\n",
            Row(Tr("HUD_KEY_DBLCLICK", "Double-click"), "HUD_KEY_DBLCLICK_DESC", "Edit text / rename in canvas list"),
            Row(Tr("HUD_KEY_SHIFT_CLICK", "Shift + click"), "HUD_KEY_SHIFT_CLICK_DESC", "Add or remove selection"),
            Row(Tr("HUD_KEY_DRAG_EMPTY", "Drag empty"), "HUD_KEY_DRAG_EMPTY_DESC", "Box select  (Ctrl: leaves only)"),
            Row(Tr("HUD_KEY_RIGHT_CLICK", "Right-click"), "HUD_KEY_RIGHT_CLICK_DESC", "Context menu & layers under cursor"),
            Row(Tr("HUD_KEY_SHIFT_DRAG", "Shift + drag"), "HUD_KEY_SHIFT_DRAG_DESC", "Lock aspect / snap rotation 15°"),
            Row(Tr("HUD_KEY_HOLD_ALT", "Hold Alt"), "HUD_KEY_HOLD_ALT_DESC", "Measure / disable snap / scale from center"),
            Row(Tr("HUD_KEY_ARROWS", "Arrows / Shift"), "HUD_KEY_ARROWS_DESC", "Nudge 1px / 10px"),
            Row("Ctrl + A", "HUD_KEY_SELECT_ALL_DESC", "Select all"),
            Row("Ctrl + C / X / V", "HUD_KEY_CLIPBOARD_DESC", "Copy / cut / paste  (Shift+V: in place)"),
            Row("Ctrl + Alt + C / V", "HUD_KEY_STYLE_DESC", "Copy / paste style only"),
            Row("Ctrl + D", "HUD_KEY_DUPLICATE_DESC", "Duplicate"),
            Row("Ctrl + G / Shift+G", "HUD_KEY_GROUP_DESC", "Group / ungroup"),
            Row("Ctrl + ] / [", "HUD_KEY_ORDER_STEP_DESC", "Bring forward / send backward"),
            Row("Ctrl + Shift + ] / [", "HUD_KEY_ORDER_END_DESC", "Bring to front / send to back"),
            Row("Ctrl + Shift + L / H", "HUD_KEY_LOCK_HIDE_DESC", "Lock / hide selection"),
            Row("Ctrl + Z", "HUD_KEY_UNDO_DESC", "Undo"),
            Row("Ctrl + Y / Shift+Z", "HUD_KEY_REDO_DESC", "Redo"),
            Row("Del", "HUD_KEY_DELETE_DESC", "Delete  (Shift+click X skips hold)"),
            Row("Esc", "HUD_KEY_ESC_DESC", "Cancel / deselect / close")
        );
    }

    private static void PositionKeysBubble() {
        if (keysBubble == null || keysButton?.Rect == null || root == null) {
            return;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(keysBubble);
        var corners4 = new Vector3[4];
        keysButton.Rect.GetWorldCorners(corners4);
        Vector2 btnLocal = ToLocal((corners4[1] + corners4[2]) * 0.5f);
        Vector2 size = keysBubble.rect.size;
        float halfW = size.x * 0.5f;
        float maxX = Mathf.Max(0f, root.rect.width * 0.5f - Margin - halfW);
        float centerX = Mathf.Clamp(btnLocal.x, -maxX, maxX);
        float tailX = Mathf.Clamp(btnLocal.x - centerX, -halfW + 18f, halfW - 18f);
        keysTail.anchoredPosition = new Vector2(tailX, 0f);
        keysBubble.pivot = new Vector2(0.5f + tailX / Mathf.Max(size.x, 1f), 0f);
        keysBubble.anchoredPosition = new Vector2(centerX + tailX, btnLocal.y + 10f);
    }

    private static void ToggleKeysBubble() {
        if (keysOpen) {
            CloseKeysBubble();
        } else {
            OpenKeysBubble();
        }
    }

    private static void OpenKeysBubble() {
        if (root == null) {
            return;
        }
        if (keysBubble == null) {
            BuildKeysBubble();
        }
        bool wasActive = keysBubble.gameObject.activeSelf;
        keysBubble.gameObject.SetActive(true);
        keysBubble.SetAsLastSibling();
        PositionKeysBubble();
        keysOpen = true;
        UpdateKeysButton();
        keysTween?.Kill();
        if (!wasActive) {
            keysBubble.localScale = Vector3.zero;
            keysGroup.alpha = 0f;
        }
        keysGroup.blocksRaycasts = true;
        keysTween = O5Seq.New()
            .Join(d => keysBubble.TScale(Vector3.one, 0.28f, O5Ease.OutBack, d))
            .Join(d => keysGroup.TFade(1f, 0.16f, O5Ease.OutSine, d))
            .Play();
    }

    private static void CloseKeysBubble(bool instant = false) {
        if (!keysOpen && !instant) {
            return;
        }
        keysOpen = false;
        UpdateKeysButton();
        keysTween?.Kill();
        keysTween = null;
        if (keysBubble == null) {
            return;
        }
        if (instant || !keysBubble.gameObject.activeSelf) {
            keysBubble.gameObject.SetActive(false);
            keysBubble.localScale = Vector3.one;
            if (keysGroup != null) {
                keysGroup.alpha = 0f;
                keysGroup.blocksRaycasts = false;
            }
            return;
        }
        keysGroup.blocksRaycasts = false;
        keysTween = O5Seq.New()
            .Join(d => keysBubble.TScale(Vector3.zero, 0.18f, O5Ease.InBack, d))
            .Join(d => keysGroup.TFade(0f, 0.14f, O5Ease.InSine, d))
            .OnComplete(() => {
                if (!keysOpen && keysBubble != null) {
                    keysBubble.gameObject.SetActive(false);
                }
            })
            .Play();
    }

    private static void UpdateKeysButton() {
        if (keysButton == null) {
            return;
        }
        keysButton.NormalColor = keysOpen ? O5KitAdapters.Ctx.Theme.ObjectButton : UiStyle.Ghost;
        keysButton.UpdateVisual(true);
    }

    private static void DestroyKeysBubble() {
        keysTween?.Kill();
        keysTween = null;
        keysOpen = false;
        keysButton = null;
        keysBubble = keysTail = null;
        keysGroup = null;
    }
}
