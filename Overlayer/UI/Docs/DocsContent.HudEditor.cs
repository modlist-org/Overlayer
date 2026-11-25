using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    // ------------------------------------------------------------------
    // HUD Editor & Canvas Inspector
    // ------------------------------------------------------------------
    private static DocChapter BuildHudEditor() => new(
        "hud",
        UIIcon.Expand,
        null,
        Tr("DOCS_HUD_TITLE", "HUD Editor"),
        TrKeys("DOCS_HUD_SUBTITLE",
            "Press {HudKey} to move, resize, rotate, align, and group HUD elements directly on screen, then refine them in the side panel or the full inspector"),
        DocSpecialKind.None,
        BuildHudManipulationSection(),
        BuildHudSnapSection(),
        BuildHudPanelsSection(),
        BuildHudShortcutsSection()
    );

    // 1. Direct manipulation, handles & toolbars
    private static DocSection BuildHudManipulationSection() => new(
        Tr("DOCS_HUD_S1_TITLE", "Direct Manipulation, Handles & Toolbars"),
        TrKeys("DOCS_HUD_S1_SUMMARY",
            "The editor opens with {HudKey} at any time, including during play. While it is open, HudEditorOpen() returns true, so HUDs shown only during play remain visible for editing"),
        new DocTable(
            Tr("DOCS_HUD_COL_FEATURE", "Feature"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_HANDLES_KEY", "Selection handles"),
             Tr("DOCS_HUD_HANDLES_DESC",
                "Handles appear only when exactly one object is selected. Edge handles are hidden when the object spans less than 28 px on screen, and the rotate ring sits just outside each corner (up to 22 px away). The cursor changes to a resize, rotate, or move shape to indicate the drag action")),
            (Tr("DOCS_HUD_HANDLES_SHIFT_KEY", "Shift + Drag handle"),
             Tr("DOCS_HUD_HANDLES_SHIFT_DESC",
                "On a corner or edge, locks the aspect ratio and shows a diagonal guide. On the rotate ring, snaps rotation to 15° steps")),
            (Tr("DOCS_HUD_HANDLES_ALT_KEY", "Alt + Drag handle"),
             Tr("DOCS_HUD_HANDLES_ALT_DESC",
                "Scales from the center instead of from the opposite corner or edge"))
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_TOP_TOOLBAR", "Top toolbar"),
            Tr("DOCS_HUD_COL_ACTION", "Action"),
            (Tr("DOCS_HUD_TB_LAYERS_KEY", "Canvas list"),
             Tr("DOCS_HUD_TB_LAYERS_DESC", "Shows or hides the Layers panel with the canvas and object tree")),
            (Tr("DOCS_HUD_TB_ADD_TEXT_KEY", "Add Text"),
             Tr("DOCS_HUD_TB_ADD_TEXT_DESC", "Adds a new text object at the current placement target")),
            (Tr("DOCS_HUD_TB_ADD_IMAGE_KEY", "Add Image"),
             Tr("DOCS_HUD_TB_ADD_IMAGE_DESC", "Adds a new image object at the current placement target")),
            (Tr("DOCS_HUD_TB_PRESETS_KEY", "Presets ★"),
             Tr("DOCS_HUD_TB_PRESETS_DESC", "Opens the preset picker (elements, layouts and other presets) and inserts the chosen preset")),
            (Tr("DOCS_HUD_TB_PLACEMENT_KEY", "Placement"),
             Tr("DOCS_HUD_TB_PLACEMENT_DESC", "Cycles where new objects go: Main canvas, Next to selected (as a sibling) or Inside selected (as a child)")),
            (Tr("DOCS_HUD_TB_GRID_KEY", "Grid"),
             Tr("DOCS_HUD_TB_GRID_DESC", "Toggles the center-anchored snapping grid. The grid size is set in Settings")),
            (Tr("DOCS_HUD_TB_RULERS_KEY", "Rulers & Guides"),
             Tr("DOCS_HUD_TB_RULERS_DESC", "Toggles the rulers and user guides. Guides participate in snapping only while the rulers are shown")),
            (Tr("DOCS_HUD_TB_AUTO_ANCHOR_KEY", "Auto Anchor"),
             Tr("DOCS_HUD_TB_AUTO_ANCHOR_DESC", "Toggles automatic Anchor and Pivot pinning during object moves")),
            (Tr("DOCS_HUD_TB_DUPLICATE_KEY", "Duplicate"),
             Tr("DOCS_HUD_TB_DUPLICATE_DESC", "Duplicates the selection, same as Ctrl+D")),
            (Tr("DOCS_HUD_TB_DELETE_KEY", "Delete"),
             Tr("DOCS_HUD_TB_DELETE_DESC", "Hold to delete the selection. Shift + Click deletes immediately without holding")),
            (Tr("DOCS_HUD_TB_UNDO_KEY", "Undo"),
             Tr("DOCS_HUD_TB_UNDO_DESC", "Undoes the last edit, same as Ctrl+Z")),
            (Tr("DOCS_HUD_TB_REDO_KEY", "Redo"),
             Tr("DOCS_HUD_TB_REDO_DESC", "Redoes the last undone edit, same as Ctrl+Y")),
            (Tr("DOCS_HUD_TB_DONE_KEY", "Done ✓"),
             Tr("DOCS_HUD_TB_DONE_DESC", "Saves and closes the HUD Editor"))
        ),
        new DocCallout(
            DocCalloutKind.Tip,
            Tr("DOCS_HUD_FADE_TITLE", "Toolbars yield during edits"),
            Tr("DOCS_HUD_FADE_TEXT",
                "While an object is dragged, moved, resized, or rotated, the toolbar, rulers, and side panels fade to 8% opacity and stop blocking the mouse, allowing HUD elements to be placed directly at the screen edges")
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_BOTTOM_BAR", "Bottom bar"),
            Tr("DOCS_HUD_COL_ACTION", "Action"),
            (Tr("DOCS_HUD_BB_ALIGN_KEY", "Align"),
             Tr("DOCS_HUD_BB_ALIGN_DESC",
                "Six modes: left, center, right, top, middle, and bottom. Requires at least one movable object: a single object aligns to its parent (or the screen); multiple objects align to each other")),
            (Tr("DOCS_HUD_BB_DISTRIBUTE_KEY", "Distribute"),
             Tr("DOCS_HUD_BB_DISTRIBUTE_DESC", "Spaces objects evenly, horizontally or vertically. Requires at least three movable objects")),
            (Tr("DOCS_HUD_BB_GROUP_KEY", "Group / Ungroup"),
             Tr("DOCS_HUD_BB_GROUP_DESC", "Wraps the selection in a new group, or moves a group's children out and removes it")),
            (Tr("DOCS_HUD_BB_ORDER_KEY", "Front / Back"),
             Tr("DOCS_HUD_BB_ORDER_DESC", "Brings the object to the front or sends it to the back. Single selection only")),
            (Tr("DOCS_HUD_BB_LOCK_KEY", "Lock / Hide"),
             Tr("DOCS_HUD_BB_LOCK_DESC", "Locks the selection so it cannot be picked on screen, or hides it")),
            (Tr("DOCS_HUD_BB_KEYS_KEY", "Show Keys"),
             Tr("DOCS_HUD_BB_KEYS_DESC", "Opens a card listing the editor shortcuts"))
        ),
        new DocParagraph(Tr("DOCS_HUD_BB_GREYED",
            "Tools inapplicable to the current selection are greyed out"))
    );

    // 2. Snap, measure, rulers, guides & auto anchor
    private static DocSection BuildHudSnapSection() => new(
        Tr("DOCS_HUD_S2_TITLE", "Snap, Measure, Rulers, Guides & Auto Anchor"),
        Tr("DOCS_HUD_S2_SUMMARY",
            "Precision tools for alignment during drags, and for retaining HUD positions when the screen size changes"),
        new DocTable(
            Tr("DOCS_HUD_COL_FEATURE", "Feature"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_SNAP_KEY", "Smart Snap"),
             Tr("DOCS_HUD_SNAP_DESC",
                "While dragging, edges and centers snap in the following order: equal-spacing guides (when nearer), user guides (only while rulers are shown), other objects, screen edges and center, and finally the center-anchored grid. Hold Alt while dragging to disable snapping")),
            (Tr("DOCS_HUD_MEASURE_KEY", "Alt + Hover measure"),
             Tr("DOCS_HUD_MEASURE_DESC",
                "Hold Alt and point at another object or empty space to display the gaps from the selection to its neighbors and to the screen edges, in HUD units. Containment and overlap are also measured")),
            (Tr("DOCS_HUD_GUIDES_KEY", "Rulers & guides"),
             Tr("DOCS_HUD_GUIDES_DESC",
                "Drag out from a ruler to create a guide; drag it back onto the ruler to delete it. Shift + Drag snaps a guide to 10 px steps. Guides are stored as screen fractions (such as x0.5 or y0.25), so they persist across resolution changes, and guide edits are undoable")),
            (Tr("DOCS_HUD_AUTO_ANCHOR_KEY", "Auto Anchor"),
             Tr("DOCS_HUD_AUTO_ANCHOR_DESC",
                "While an object is moved, its Anchor and Pivot are pinned to the nearest of 9 zones of the parent rect (or the screen for top-level objects). Hysteresis thresholds at 0.125 / 0.375 / 0.625 / 0.875 prevent flickering near zone borders, and an animated gizmo indicates the active zone"))
        ),
        new DocCallout(
            DocCalloutKind.Info,
            Tr("DOCS_HUD_AUTO_ANCHOR_FX_TITLE", "Auto Anchor and Fx"),
            Tr("DOCS_HUD_AUTO_ANCHOR_FX_TEXT",
                "Auto Anchor skips any object whose Anchor, Pivot, or Size is driven by an Fx expression, and therefore never overwrites expressions")
        )
    );

    // 3. Quick panel, layers & main-window inspector
    private static DocSection BuildHudPanelsSection() => new(
        Tr("DOCS_HUD_S3_TITLE", "Quick Panel, Layers & Main-Window Inspector"),
        Tr("DOCS_HUD_S3_SUMMARY",
            "The side panel edits the selection in place, the Layers panel manages the tree, and the main window provides the full canvas inspector"),
        new DocTable(
            Tr("DOCS_HUD_COL_QUICK", "Quick panel section"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_QP_NAME_KEY", "Name"),
             Tr("DOCS_HUD_QP_NAME_DESC", "Renames the selected object")),
            (Tr("DOCS_HUD_QP_POS_KEY", "Position and size"),
             Tr("DOCS_HUD_QP_POS_DESC",
                "X, Y, W, H, rotation and scale; a 3×3 Pin to picker that changes Anchor and Pivot without moving the object on screen; reset buttons for position, rotation and scale; Fit to text for text objects; and Ungroup")),
            (Tr("DOCS_HUD_QP_LAYOUT_KEY", "Auto layout"),
             Tr("DOCS_HUD_QP_LAYOUT_DESC", "Off, Row or Column, with Gap, Padding and cross-axis alignment")),
            (Tr("DOCS_HUD_QP_LOOK_KEY", "Look"),
             Tr("DOCS_HUD_QP_LOOK_DESC", "Opacity, text (tag editor, font size, alignment and color) and image color")),
            (Tr("DOCS_HUD_QP_ALL_KEY", "All properties…"),
             Tr("DOCS_HUD_QP_ALL_DESC", "Opens the full component inspector for the selected object")),
            (Tr("DOCS_HUD_QP_MULTI_KEY", "Multiple selection"),
             Tr("DOCS_HUD_QP_MULTI_DESC",
                "Align, Distribute, Group, and Group as row or column. Property edits apply to every selected object; fields whose values differ show — or Mixed. Fields driven by Fx are locked and show a notice"))
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_PANEL", "Panel control"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_PN_PIN_KEY", "Pinned / Auto"),
             Tr("DOCS_HUD_PN_PIN_DESC", "Pinned keeps the panel open; Auto shows it only while something is selected")),
            (Tr("DOCS_HUD_PN_SIDE_KEY", "← / →"),
             Tr("DOCS_HUD_PN_SIDE_DESC", "Moves the panel to the opposite side of the screen. The panel also moves aside automatically when it would cover the selection")),
            (Tr("DOCS_HUD_PN_RESIZE_KEY", "Resize grips"),
             Tr("DOCS_HUD_PN_RESIZE_DESC", "Drag to change the panel width or height. Double-click the height grip to expand to full height")),
            (Tr("DOCS_HUD_PN_TAB_KEY", "Pull tab"),
             Tr("DOCS_HUD_PN_TAB_DESC", "While the panel is hidden, a tab on the right edge restores it")),
            (Tr("DOCS_HUD_PN_DOCK_KEY", "Tear-off docks"),
             Tr("DOCS_HUD_PN_DOCK_DESC",
                "Drag a Quick section header or an All properties component card out of the panel to float it; drag it back to redock. Floating docks persist across restarts (HudDetached)"))
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_LAYERS", "Layers panel"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_LY_SELECT_KEY", "Selection"),
             Tr("DOCS_HUD_LY_SELECT_DESC", "Click to select, Shift + Click to select a range, Ctrl + Click to add or remove one row")),
            (Tr("DOCS_HUD_LY_DROP_KEY", "Drag & drop"),
             Tr("DOCS_HUD_LY_DROP_DESC",
                "The drop position on a row determines the result: the top third places before it, the middle third inside it, and the bottom third after it")),
            (Tr("DOCS_HUD_LY_CURRENT_KEY", "current marker"),
             Tr("DOCS_HUD_LY_CURRENT_DESC", "Marks the main canvas (HudMainCanvas), where new elements and presets are added")),
            (Tr("DOCS_HUD_LY_HIDDEN_KEY", "Hidden objects"),
             Tr("DOCS_HUD_LY_HIDDEN_DESC", "Hidden objects stay in the list and can be shown again from their context menu")),
            (Tr("DOCS_HUD_LY_LOCK_KEY", "Locks"),
             Tr("DOCS_HUD_LY_LOCK_DESC",
                "Locked objects cannot be picked on screen, only from the list. Locks persist across restarts (HudLocked) and are excluded from undo"))
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_MAIN_EDITOR", "Main-window canvas editor"),
            Tr("DOCS_HUD_COL_DESC", "Description"),
            (Tr("DOCS_HUD_ME_TABS_KEY", "Canvas tabs"),
             Tr("DOCS_HUD_ME_TABS_DESC", "Drag the canvas tabs to reorder them")),
            (Tr("DOCS_HUD_ME_MULTI_KEY", "Shift + Click"),
             Tr("DOCS_HUD_ME_MULTI_DESC",
                "Adds or removes objects in the hierarchy multi-selection. A changed field is applied to every selected object; their other fields remain unchanged")),
            ("Ctrl+C / Ctrl+V",
             Tr("DOCS_HUD_ME_CLIPBOARD_DESC",
                "Copies and pastes the component card under the mouse, or the whole object when no card is hovered")),
            (Tr("DOCS_HUD_ME_CHANGE_KEY", "Change bar"),
             Tr("DOCS_HUD_ME_CHANGE_DESC", "Fields changed elsewhere, such as in the HUD Editor, are highlighted live with a change bar")),
            (Tr("DOCS_HUD_ME_PROPS_KEY", "Canvas Props"),
             Tr("DOCS_HUD_ME_PROPS_DESC",
                "Edits the .o5cp package metadata of a canvas: thumbnail, author, description, version, license and extra JS files"))
        ),
        new DocCallout(
            DocCalloutKind.Info,
            Tr("DOCS_HUD_UNDO_TITLE", "Undo & clipboard"),
            Tr("DOCS_HUD_UNDO_TEXT",
                "The UndoStack retains up to 200 steps, and edits are auto-saved after a 0.5 s pause. Lock is not undoable. Copy and paste use the system clipboard as overlayer-hud: JSON, which allows pasting across game sessions")
        )
    );

    // 4. Shortcuts & mouse gestures
    private static DocSection BuildHudShortcutsSection() => new(
        Tr("DOCS_HUD_S4_TITLE", "Complete HUD Editor Shortcuts & Mouse Gestures"),
        Tr("DOCS_HUD_S4_SUMMARY",
            "Every keyboard shortcut and mouse gesture in the HUD Editor. Keyboard shortcuts are ignored while a text field has input focus"),
        new DocCallout(
            DocCalloutKind.Info,
            Tr("DOCS_HUD_PICK_TITLE", "How picking works"),
            Tr("DOCS_HUD_PICK_TEXT",
                "A click on the canvas always picks the deepest object under the mouse directly. Each double-click descends one level in the hierarchy (there is no separate group-entering mode); upon reaching a text object with nothing below it, the double-click starts inline text editing. Ctrl + Click on the canvas does not deep-select: Ctrl only modifies box selection (leaf objects only) and toggles rows in the Layers panel")
        ),
        new DocTable(
            Tr("DOCS_HUD_COL_SHORTCUT", "Shortcut"),
            Tr("DOCS_HUD_COL_ACTION", "Action"),
            (Tr("DOCS_HUD_K_CLICK", "Click"),
             Tr("DOCS_HUD_K_CLICK_DESC", "Selects the deepest object under the mouse and allows immediate dragging. Grabbing any member of a multi-selection moves the whole selection")),
            (Tr("DOCS_HUD_K_DBLCLICK", "Double Click"),
             Tr("DOCS_HUD_K_DBLCLICK_DESC", "Steps one hierarchy level deeper per double-click; at a leaf text object, starts inline text editing")),
            (Tr("DOCS_HUD_K_SHIFT_CLICK", "Shift + Click"),
             Tr("DOCS_HUD_K_SHIFT_CLICK_DESC", "Adds an object to the selection or removes it")),
            (Tr("DOCS_HUD_K_DRAG_EMPTY", "Drag Empty"),
             Tr("DOCS_HUD_K_DRAG_EMPTY_DESC", "Draws a selection box")),
            (Tr("DOCS_HUD_K_SHIFT_DRAG_EMPTY", "Shift + Drag Empty"),
             Tr("DOCS_HUD_K_SHIFT_DRAG_EMPTY_DESC", "Adds the boxed objects to the current selection")),
            (Tr("DOCS_HUD_K_CTRL_DRAG_EMPTY", "Ctrl + Drag Empty"),
             Tr("DOCS_HUD_K_CTRL_DRAG_EMPTY_DESC", "Box-selects leaf objects only (objects without children)")),
            (Tr("DOCS_HUD_K_SHIFT_DRAG", "Shift + Drag handle / guide"),
             Tr("DOCS_HUD_K_SHIFT_DRAG_DESC", "Locks the aspect ratio on corners and edges, snaps rotation to 15°, or snaps a guide to 10 px")),
            (Tr("DOCS_HUD_K_ALT_DRAG_HANDLE", "Alt + Drag handle"),
             Tr("DOCS_HUD_K_ALT_DRAG_HANDLE_DESC", "Scales from the center")),
            (Tr("DOCS_HUD_K_ALT_DRAG", "Alt + Drag object"),
             Tr("DOCS_HUD_K_ALT_DRAG_DESC", "Moves without snapping")),
            (Tr("DOCS_HUD_K_ALT_HOVER", "Alt + Hover"),
             Tr("DOCS_HUD_K_ALT_HOVER_DESC", "Measures gaps to neighbors and screen edges")),
            (Tr("DOCS_HUD_K_RIGHT_CLICK", "Right Click"),
             Tr("DOCS_HUD_K_RIGHT_CLICK_DESC", "Opens the context menu, listing every layer under the mouse, which also allows selecting parent containers")),
            (Tr("DOCS_HUD_K_ARROWS", "Arrows / Shift + Arrows"),
             Tr("DOCS_HUD_K_ARROWS_DESC", "Nudges the selection by 1 px or 10 px")),
            ("Ctrl+A",
             Tr("DOCS_HUD_K_SELECT_ALL_DESC", "Selects all unlocked top-level objects")),
            ("Ctrl+X",
             Tr("DOCS_HUD_K_CUT_DESC", "Cuts the selection")),
            ("Ctrl+C / Ctrl+V / Ctrl+Shift+V",
             Tr("DOCS_HUD_K_CLIPBOARD_DESC", "Copies, pastes, or pastes in place")),
            ("Ctrl+Alt+C / Ctrl+Alt+V",
             Tr("DOCS_HUD_K_STYLE_DESC", "Copies or pastes only the visual style (font, color, outline and so on)")),
            ("Ctrl+D / Del",
             Tr("DOCS_HUD_K_DUP_DEL_DESC", "Duplicates or deletes the selection")),
            ("Ctrl+G / Ctrl+Shift+G",
             Tr("DOCS_HUD_K_GROUP_DESC", "Groups or ungroups the selection")),
            ("Ctrl+] / Ctrl+[",
             Tr("DOCS_HUD_K_ORDER_STEP_DESC", "Brings forward or sends backward by one step")),
            ("Ctrl+Shift+] / Ctrl+Shift+[",
             Tr("DOCS_HUD_K_ORDER_END_DESC", "Brings to front or sends to back")),
            ("Ctrl+Shift+L / Ctrl+Shift+H",
             Tr("DOCS_HUD_K_LOCK_HIDE_DESC", "Locks or hides the selection")),
            ("Ctrl+Z",
             Tr("DOCS_HUD_K_UNDO_DESC", "Undoes the last edit")),
            ("Ctrl+Y / Ctrl+Shift+Z",
             Tr("DOCS_HUD_K_REDO_DESC", "Redoes the last undone edit")),
            ("Esc",
             Tr("DOCS_HUD_K_ESC_DESC",
                "Performs the first applicable action: close the context menu → close the shortcut card → cancel the current drag → clear the selection → close the HUD Editor"))
        )
    );
}
