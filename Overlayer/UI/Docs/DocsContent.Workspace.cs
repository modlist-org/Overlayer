using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    // ------------------------------------------------------------------
    // 2. Workspace, Settings & Resources
    // ------------------------------------------------------------------
    private static DocChapter BuildWorkspace() => new(
        "workspace",
        UIIcon.Monitor,
        null,
        Tr("DOCS_WS_TITLE", "Workspace"),
        Tr("DOCS_WS_SUBTITLE",
            "A tour of the main window: every sidebar page, resource management, key settings, and the location of Overlayer's files"),
        DocSpecialKind.None,
        new DocSection(
            Tr("DOCS_WS_PAGES_TITLE", "Sidebar Pages"),
            Tr("DOCS_WS_PAGES_SUMMARY",
                "The left menu switches between eight pages. Clicking the logo expands or collapses it"),
            new DocTable(
                Tr("DOCS_WS_PAGES_COL_PAGE", "Page"),
                Tr("DOCS_WS_PAGES_COL_DESC", "What it does"),
                (Tr("DOCS_WS_PAGE_OVERLAYER", "Overlayer"), Tr("DOCS_WS_PAGE_OVERLAYER_DESC",
                    "Canvases and installed packages, displayed as tiles. Each can be toggled, exported as .o5cp, cloned, deleted, or opened in package properties. Installed packages can be copied into an editable canvas; the last tile creates a new canvas, adds a preset, or imports a file")),
                (Tr("DOCS_WS_PAGE_SETTINGS", "Settings"), Tr("DOCS_WS_PAGE_SETTINGS_DESC",
                    "All options, grouped into Interface, Input, Palette, Fonts, and HUD Editor, with a Find box at the top. Reset All and Run Setup Again are located at the bottom")),
                (Tr("DOCS_WS_PAGE_RESOURCES", "Resources"), Tr("DOCS_WS_PAGE_RESOURCES_DESC",
                    "Images and fonts that HUDs reference by name. Details follow in the next section")),
                (Tr("DOCS_WS_PAGE_EASING", "Easing"), Tr("DOCS_WS_PAGE_EASING_DESC",
                    "A visual curve editor. Saved curves are written to Easings.json and are available by name in any ease argument")),
                (Tr("DOCS_WS_PAGE_JS", "JS"), Tr("DOCS_WS_PAGE_JS_DESC",
                    "Lists the scripts in JS/Script, grouped by folder. Supports per-file and per-folder toggles, Reload All, Open Folder, and Auto Reload. Scripts that failed to load are shown in red with the error on hover, alongside tag and patch counts")),
                (Tr("DOCS_WS_PAGE_DOCS", "Docs"), Tr("DOCS_WS_PAGE_DOCS_DESC",
                    "This wiki: global search, live counters on the overview, a tag sandbox with diagnostics, and a tag explorer that displays live values and copies a tag on click")),
                (Tr("DOCS_WS_PAGE_TUTORIAL", "Tutorial"), Tr("DOCS_WS_PAGE_TUTORIAL_DESC",
                    "Olly guides the user through the real UI, chapter by chapter, from window basics to tags, Fx, easing, and JS. A single chapter or the full course can be run, with ghost-pointer demos and a Practice room for the unusual controls, where nothing is saved. The speech bubble is draggable, and back, skip, replay, and return are available at every step. The Hide Tutorial setting hides this page")),
                (Tr("DOCS_WS_PAGE_CREDITS", "Credits"), Tr("DOCS_WS_PAGE_CREDITS_DESC",
                    "The people behind Overlayer. Some say Shift-clicking this tab leads somewhere else.."))
            ),
            new DocCallout(
                DocCalloutKind.Tip,
                Tr("DOCS_WS_PAGES_TIP_TITLE", "Tab labels and transitions"),
                Tr("DOCS_WS_PAGES_TIP",
                    "Tab Labels in Settings determines whether menu labels are always shown, shown on hover, or hidden. Transition Effect selects how the window appears and disappears: Fly In, Fade, Diagonal Swipe, Taskbar, or Clock"))
        ),
        new DocSection(
            Tr("DOCS_WS_RES_TITLE", "Resources & Asset Management"),
            Tr("DOCS_WS_RES_SUMMARY",
                "Images and fonts are added once and referenced anywhere by name: in Image and Text components, in sprites, and in exported packages"),
            new DocTable(
                Tr("DOCS_WS_RES_COL_ITEM", "Item"),
                Tr("DOCS_WS_RES_COL_DESC", "Details"),
                (Tr("DOCS_WS_RES_IMAGES", "Images"), Tr("DOCS_WS_RES_IMAGES_DESC",
                    "Supports .png, .jpg, .jpeg, .bmp, and .tga. Mip Chain smooths images rendered below their native size; Linear loads the data as linear color instead of sRGB")),
                (Tr("DOCS_WS_RES_SPRITE", "Sprite Editor"), Tr("DOCS_WS_RES_SPRITE_DESC",
                    "Drag the green guides to set 9-slice borders, allowing an image to stretch as a panel or button without distorting its corners")),
                (Tr("DOCS_WS_RES_FONTS", "Fonts"), Tr("DOCS_WS_RES_FONTS_DESC",
                    "Supports .ttf and .otf. Each file is converted into a TextMeshPro font asset on import")),
                (Tr("DOCS_WS_RES_FOLDERS", "Folders"), Tr("DOCS_WS_RES_FOLDERS_DESC",
                    "Naming a resource as folder/name places it in a folder. Images and fonts maintain separate folder lists and are searchable by name")),
                (Tr("DOCS_WS_RES_STORAGE", "Copy vs. Reference"), Tr("DOCS_WS_RES_STORAGE_DESC",
                    "Copy stores the file inside the Overlayer folder. Reference retains only the original path, indicated by a dot in the card's top-left corner. Paths inside the Overlayer folder are saved with a portable {ModPath} token and remain valid if the install is moved")),
                (Tr("DOCS_WS_RES_MISSING", "Missing files"), Tr("DOCS_WS_RES_MISSING_DESC",
                    "When a file disappears, its card remains with a warning badge rather than being removed. The file can be restored and reloaded, renamed, or removed without restarting the game")),
                (Tr("DOCS_WS_RES_INTERNAL", "Internal resources"), Tr("DOCS_WS_RES_INTERNAL_DESC",
                    "Show Internal Images and Show Internal Fonts in Settings list Overlayer's built-in assets here and in pickers. These assets are read-only"))
            ),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_WS_RES_NOTE_TITLE", "Resources travel with packages"),
                Tr("DOCS_WS_RES_NOTE",
                    "On export, the images and fonts used by a canvas are bundled into the .o5cp file. Referenced resources are included as well, so the recipient does not require the original paths"))
        ),
        new DocSection(
            Tr("DOCS_WS_SET_TITLE", "Key Settings Reference"),
            Tr("DOCS_WS_SET_SUMMARY",
                "The most relevant options in the Settings page, grouped identically. The left column is the name stored in Settings.json"),
            new DocTable(
                Tr("DOCS_WS_SET_COL_KEY", "Setting"),
                Tr("DOCS_WS_SET_COL_DESC", "Effect"),
                ("Language", Tr("DOCS_WS_SET_LANGUAGE",
                    "Interface language. Detected automatically on first run")),
                ("UIScale", Tr("DOCS_WS_SET_UISCALE",
                    "Size of the whole interface, from 0.8 to 1.6")),
                ("WindowOpacity / HudWindowOpacity / TooltipOpacity", Tr("DOCS_WS_SET_OPACITY",
                    "Opacity of the main window, the HUD Editor bars and panels, and tooltips, from 0.2 to 1.0")),
                ("UIBaseColor", Tr("DOCS_WS_SET_PALETTE",
                    "UI Palette. Builds every UI color from one base color. Pick a preset or a custom color")),
                ("AnimationSpeed", Tr("DOCS_WS_SET_ANIM",
                    "Speed of UI animations, from 0 (instant) to 3")),
                ("ScrollEasing / ScrollSensitivity", Tr("DOCS_WS_SET_SCROLL",
                    "Smooths wheel and right-drag scrolling, and sets how far touch-style scrolling follows the mouse")),
                ("UISoundVolume", Tr("DOCS_WS_SET_SOUND",
                    "Volume of UI sound effects")),
                ("PanelFadeMode / TabLabelMode", Tr("DOCS_WS_SET_PANEL",
                    "Window transition effect and sidebar label visibility")),
                ("ShowOnStartup / Tooltip", Tr("DOCS_WS_SET_STARTUP",
                    "Whether this window opens on game start, and whether tooltips appear on hover")),
                ("WindowKey / HudEditorKey", Tr("DOCS_WS_SET_KEYS",
                    "Shortcuts for this window and the HUD Editor. Click the field, then press a key, optionally combined with Ctrl, Shift, or Alt. Esc cancels")),
                ("UseRawInput", Tr("DOCS_WS_SET_RAWINPUT",
                    "Reads the keyboard at the OS level for lower latency and better accuracy (Windows and Linux). When off, Unity's standard input polling is used")),
                ("SliderSensitivity / WheelAdjustSlider", Tr("DOCS_WS_SET_SLIDER",
                    "Drag sensitivity of sliders, and whether the mouse wheel adjusts sliders and dropdowns. Hold Shift for finer steps")),
                ("MiddleClickToDefault", Tr("DOCS_WS_SET_MIDDLE",
                    "Middle-click a field to reset it to its default value")),
                ("SystemFontKey / SystemMediumFontKey / CodeFontKey", Tr("DOCS_WS_SET_FONTS",
                    "UI fonts for regular text, headings, and code, selected from font resources. Each has a fallback chain for characters missing from the main font. Empty uses the built-in default")),
                ("HudSnap / HudSnapDistance / HudGrid / HudGridSize", Tr("DOCS_WS_SET_HUDSNAP",
                    "HUD Editor snapping and grid defaults. Holding Alt while dragging disables snapping temporarily")),
                ("HudRulers / HudAutoAnchor / HudPlacement", Tr("DOCS_WS_SET_HUDMISC",
                    "Rulers and guides, automatic anchoring, and the placement target for new objects. These are identical to the toggles in the HUD Editor toolbar")),
                ("Enable3DFeatures", Tr("DOCS_WS_SET_3D",
                    "Unlocks 3D objects and components such as Mesh, Collider, Rigidbody, and Light")),
                ("EnableJSScriptWatcher", Tr("DOCS_WS_SET_WATCHER",
                    "Reloads scripts automatically when a file in JS/Script changes. Equivalent to Auto Reload on the JS page")),
                ("HideTutorial", Tr("DOCS_WS_SET_HIDETUT",
                    "Hides the Tutorial page from the left menu"))
            ),
            new DocCallout(
                DocCalloutKind.Warning,
                Tr("DOCS_WS_SET_RESET_TITLE", "Reset All & Run Setup Again"),
                Tr("DOCS_WS_SET_RESET",
                    "Reset All Settings restores every option to its default; hold the button to confirm. Canvases, resources, and scripts are not affected. Run Setup Again reopens the Setup Wizard at any time"))
        ),
        new DocSection(
            Tr("DOCS_WS_DATA_TITLE", "Setup Wizard & Data Directory Layout"),
            Tr("DOCS_WS_DATA_SUMMARY",
                "How first-run setup proceeds, and where every file written by Overlayer is stored"),
            new DocParagraph(Tr("DOCS_WS_DATA_WIZARD",
                "On a fresh install, the Setup Wizard opens automatically after approximately 4 seconds. It consists of four steps: Welcome (language), Scale, Keys (essential shortcuts), and Finish, which offers to start the tutorial. It can be skipped at any point and reopened later from Settings")),
            new DocParagraph(Tr("DOCS_WS_DATA_ROOT",
                "All other data resides in a single Overlayer data folder created by the mod loader, typically UserData/Overlayer/ inside the game folder. Backing up this folder preserves the entire setup")),
            new DocCode(
                Tr("DOCS_WS_DATA_TREE_CAPTION", "Overlayer data folder"),
                "Overlayer/\n"
                + "├─ Settings.json          # CoreSettings\n"
                + "├─ Easings.json           # saved easing curves\n"
                + "├─ UserResources.json     # image & font registry, folders\n"
                + "├─ Canvases/              # one .json per canvas\n"
                + "│  └─ Objects/            # bare / 3D objects\n"
                + "├─ Resources/\n"
                + "│  ├─ Images/             # copied images\n"
                + "│  └─ Fonts/              # copied fonts\n"
                + "├─ JS/\n"
                + "│  ├─ Script/             # your .js files, Prefs.json, DisabledScripts.json\n"
                + "│  ├─ impl.js             # built-in tag bindings\n"
                + "│  └─ impl.d.ts           # TypeScript definitions for editors\n"
                + "├─ Packages/              # installed .o5cp packages\n"
                + "├─ Presets/               # your .o5cp presets\n"
                + "├─ Module/                # game module files\n"
                + "├─ Lang/                  # translation files\n"
                + "└─ Temp/                  # scratch space",
                DocsHighlight.Mode.Plain
            ),
            new DocCallout(
                DocCalloutKind.Tip,
                Tr("DOCS_WS_DATA_TIP_TITLE", "Script with IntelliSense"),
                Tr("DOCS_WS_DATA_TIP",
                    "Open the JS folder in an editor such as VS Code. impl.d.ts provides autocomplete for every tag and API, and with Auto Reload enabled, saved files apply in game immediately"))
        )
    );
}
