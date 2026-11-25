using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    // ------------------------------------------------------------------
    // 1. Overview
    // ------------------------------------------------------------------
    private static DocChapter BuildOverview() => new(
        "overview",
        null,
        UISprite.OV5LogoOutline128,
        Tr("DOCS_OV_TITLE", "Overview"),
        Tr("DOCS_OV_SUBTITLE",
            "An overlay mod for building live text, image, graph, and script HUDs on top of Unity games"),
        DocSpecialKind.Overview,
        new DocSection(
            Tr("DOCS_OV_PRE_TITLE", "Prerequisites"),
            Tr("DOCS_OV_PRE_SUMMARY",
                "Overlayer exposes Unity's UI hierarchy and runtime scripting directly. Building custom HUDs requires substantial technical background"),
            new DocParagraph(Tr("DOCS_OV_PRE_P1",
                "Rather than offering a fixed set of preset widgets, Overlayer lets you assemble real Unity Canvas, RectTransform, and component hierarchies at runtime. Familiarity with Unity UI concepts makes layout, scaling, and debugging significantly faster")),
            new DocTable(
                Tr("DOCS_OV_PRE_COL_AREA", "Area"),
                Tr("DOCS_OV_PRE_COL_USE", "Where it helps"),
                (Tr("DOCS_OV_PRE_UNITY_K", "Unity UI"), Tr("DOCS_OV_PRE_UNITY_V",
                    "Canvas, RectTransform (Anchor, Pivot, SizeDelta), CanvasGroup, Layout Group, TextMeshPro rich text, and 3D components when enabled")),
                (Tr("DOCS_OV_PRE_FMT_K", "C# Format & Math"), Tr("DOCS_OV_PRE_FMT_V",
                    "Numeric format specifiers (0, #, %, #,##0), inline math functions, interpolation, and easing curves")),
                (Tr("DOCS_OV_PRE_JS_K", "JavaScript & .NET / CLR"), Tr("DOCS_OV_PRE_JS_V",
                    "Reactive Fx expressions, custom tags, V8 scripts, runtime method patching (Harmony), and Unity/CLR interop"))
            ),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_OV_PRE_NOTE_TITLE", "Starting without prior experience"),
                Tr("DOCS_OV_PRE_NOTE",
                    "Importing .o5cp packages or using built-in presets requires no coding or Unity knowledge. For simple edits, start with the Tutorial tab and the HUD Editor before diving into Fx or scripts"))
        ),
        new DocSection(
            Tr("DOCS_OV_ARCH_TITLE", "Core Architecture"),
            Tr("DOCS_OV_ARCH_SUMMARY",
                "Every Overlayer HUD is composed of the same four layers, from the outermost container down to the live value inside a text"),
            new DocTable(
                Tr("DOCS_OV_ARCH_COL_LAYER", "Layer"),
                Tr("DOCS_OV_ARCH_COL_DESC", "Description"),
                ("OvCanvas", Tr("DOCS_OV_ARCH_CANVAS",
                    "An independent Unity Canvas rendered above the game. It is the unit for toggling, screen scaling, export as a .o5cp package, and import")),
                ("OvObject", Tr("DOCS_OV_ARCH_OBJECT",
                    "An element inside a canvas: a text, an image, or an empty group. Objects nest into parent/child trees; a group moves, hides, and lays out its children as a single unit")),
                ("Components", Tr("DOCS_OV_ARCH_COMPONENTS",
                    "The building blocks attached to an object: RectTransform, TextMeshProUGUI, Image, CanvasGroup, and layout groups, plus Overlayer's own Graph, MovingMan, ColorRange, and Rain")),
                ("Fx & TextEngine", Tr("DOCS_OV_ARCH_FX",
                    "Texts use {Tag} templates, which TextEngine replaces with live values every frame. Any inspector field can also enable Fx to be driven by a JavaScript expression instead of a fixed value"))
            ),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_OV_ARCH_NOTE_TITLE", "Canvases are independent"),
                Tr("DOCS_OV_ARCH_NOTE",
                    "Each canvas is saved to its own file and runs independently. Disabling, deleting, or replacing one with an imported package does not affect the others. One HUD concept per canvas is recommended"))
        ),
        new DocSection(
            Tr("DOCS_OV_START_TITLE", "Quick Start Workflow"),
            Tr("DOCS_OV_START_SUMMARY",
                "The shortest path from a fresh install to a shareable HUD"),
            new DocTable(
                Tr("DOCS_OV_START_COL_STEP", "Step"),
                Tr("DOCS_OV_START_COL_WHAT", "What to do"),
                (Tr("DOCS_OV_START_1_KEY", "1. First launch"), TrKeys("DOCS_OV_START_1",
                    "On a fresh install, the Setup Wizard opens automatically. Select a language and UI scale, then confirm the two shortcuts: {WindowKey} for this window and {HudKey} for the HUD Editor")),
                (Tr("DOCS_OV_START_2_KEY", "2. Tutorial & presets"), Tr("DOCS_OV_START_2",
                    "Follow the Tutorial tab with Olly for a guided tour, or start from a preset such as the built-in FPS counter or clock in the Overlayer tab")),
                (Tr("DOCS_OV_START_3_KEY", "3. HUD Editor"), TrKeys("DOCS_OV_START_3",
                    "Press {HudKey} to edit directly on top of the game. Add texts and images, drag them into position, and refine them in the side panel")),
                (Tr("DOCS_OV_START_4_KEY", "4. Tags & Fx"), Tr("DOCS_OV_START_4",
                    "Enter tags such as {Fps} or {Hour:00}:{Minute:00} into a text, then animate values, colors, and sizes with Fx, effect tags, and Easing curves")),
                (Tr("DOCS_OV_START_5_KEY", "5. Share"), Tr("DOCS_OV_START_5",
                    "Export a canvas as a .o5cp package from its tile in the Overlayer tab. Fonts, images, and scripts in use are bundled, so the package imports in a single step"))
            ),
            new DocCallout(
                DocCalloutKind.Tip,
                Tr("DOCS_OV_START_TIP_TITLE", "Test prior to building"),
                Tr("DOCS_OV_START_TIP",
                    "The Text Engine chapter of this wiki provides a live sandbox. Paste a template there to preview its output prior to placing it on a canvas"))
        ),
        new DocSection(
            Tr("DOCS_OV_KEYS_TITLE", "Global Shortcuts & Window Controls"),
            Tr("DOCS_OV_KEYS_SUMMARY",
                "Shortcuts available anywhere in the game, and mouse gestures shared by every Overlayer window"),
            new DocTable(
                Tr("DOCS_OV_KEYS_COL_INPUT", "Shortcut"),
                Tr("DOCS_OV_KEYS_COL_ACTION", "Action"),
                (TrKeys("DOCS_OV_KEY_WINDOW", "{WindowKey}"), Tr("DOCS_OV_KEY_WINDOW_DESC",
                    "Opens or closes the Overlayer window. If the HUD Editor is open, switches to the main window")),
                (TrKeys("DOCS_OV_KEY_WINDOW_HOLD", "Hold {WindowKey}"), Tr("DOCS_OV_KEY_WINDOW_HOLD_DESC",
                    "Hold for 0.4 seconds to restore the window to its default position and size. Use this when the window is off screen or out of reach")),
                (TrKeys("DOCS_OV_KEY_HUD", "{HudKey}"), Tr("DOCS_OV_KEY_HUD_DESC",
                    "Opens or closes the HUD Editor, including during play. Both shortcuts can be rebound in Settings")),
                (Tr("DOCS_OV_KEY_TOPBAR", "Top bar"), Tr("DOCS_OV_KEY_TOPBAR_DESC",
                    "Drag to move the window, drag its edges and corners to resize, and double-click to maximize or restore")),
                (Tr("DOCS_OV_KEY_LOGO", "Logo"), Tr("DOCS_OV_KEY_LOGO_DESC",
                    "Click to expand or collapse the left menu")),
                (Tr("DOCS_OV_KEY_CLOSE", "X button"), Tr("DOCS_OV_KEY_CLOSE_DESC",
                    "Closes the window. HUDs continue running")),
                (Tr("DOCS_OV_KEY_POWER", "Power button"), Tr("DOCS_OV_KEY_POWER_DESC",
                    "Turns Overlayer on or off as a whole. While off, every HUD is hidden and suspends updates")),
                (Tr("DOCS_OV_KEY_POWER_SHIFT", "Shift + hold Power"), Tr("DOCS_OV_KEY_POWER_SHIFT_DESC",
                    "Hold for 2 seconds to wipe the window away and shut Overlayer down completely. Releasing early cancels the shutdown and the window slides back")),
                (Tr("DOCS_OV_KEY_RDRAG", "Right-drag"), Tr("DOCS_OV_KEY_RDRAG_DESC",
                    "Scrolls as if grabbing the scrollbar: the cursor height within the window maps directly to the scroll position. The mouse wheel is also supported")),
                (Tr("DOCS_OV_KEY_SHIFT_RDRAG", "Shift + right-drag"), Tr("DOCS_OV_KEY_SHIFT_RDRAG_DESC",
                    "Touch-style scrolling: the content follows the mouse and continues gliding after a flick and release. Pressing or releasing Shift mid-drag switches modes. Adjust with Scroll Sensitivity")),
                (Tr("DOCS_OV_KEY_EDGE_DRAG", "Left-drag on right edge"), Tr("DOCS_OV_KEY_EDGE_DRAG_DESC",
                    "Left-dragging the empty padding along the right edge behaves identically to right-drag, Shift included. The thin bar on the right border indicates the current position")),
                (Tr("DOCS_OV_KEY_MIDDLE", "Middle-click"), Tr("DOCS_OV_KEY_MIDDLE_DESC",
                    "Resets a setting or field to its default value. Supported fields display a small dot in their top-left corner"))
            ),
            new DocCallout(
                DocCalloutKind.Warning,
                Tr("DOCS_OV_KEYS_WARN_TITLE", "Power off vs. shut down"),
                Tr("DOCS_OV_KEYS_WARN",
                    "A normal click on Power only pauses Overlayer; it can be re-enabled at any time. Shift + hold unloads Overlayer for the remainder of the session, and restoring it requires a game restart"))
        )
    );
}
