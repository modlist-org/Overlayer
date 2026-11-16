using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Tag.Core;
using Overlayer.UI;
using Overlayer.UI.Factory;
using UnityEngine;

namespace Overlayer.Tutorial;

// Every chapter of the tutorial. Steps point at the real UI and, where it makes sense, wait for
// the user to do the thing. Code in the texts is shown as typed (the bubble doesn't run tags).
internal static class TutorialContent {
    private const string Code = "<color=#AEB3FF>";
    private const string End = "</color>";

    private static string C(string code) => $"{Code}<noparse>{code}</noparse>{End}";

    // ---- where things are ----

    private static bool WindowOpen => UICore.CanvasObj != null && UICore.CanvasObj.activeSelf
        && UICore.Panel != null && UICore.Panel.gameObject.activeSelf && !HudEditor.IsOpen;

    private static bool MenuOpen => UICore.Menu != null && UICore.Menu.GetComponent<CanvasGroup>()?.interactable == true;

    private static bool On(OriginalMenuState page) => WindowOpen && UICore.CurrentMenuState == (int)page;

    private static RectTransform PanelPart(string path) => UICore.Panel != null ? UICore.Panel.Find(path) as RectTransform : null;

    private static RectTransform PagePart(OriginalMenuState page, string path)
        => UICore.Pages.TryGetValue((int)page, out var p) && p != null ? (path == null ? p : p.Find(path) as RectTransform) : null;

    // A page's menu item; the menu button while the menu is closed.
    private static RectTransform MenuTarget(OriginalMenuState page) => MenuOpen ? MenuFactory.ItemOf((int)page) : PanelPart("TopBar/Logo");

    // The settings row that shows this text.
    private static RectTransform RowOf(string key) {
        if (UICore.CanvasObj == null) {
            return null;
        }
        var label = UICore.CanvasObj.GetComponentsInChildren<TextLocalization>(false).FirstOrDefault(t => t.Key == key);
        for (var t = label != null ? label.transform : null; t != null; t = t.parent) {
            if (t.name == "Row") {
                return (RectTransform)t;
            }
        }
        return label != null ? (RectTransform)label.transform : null;
    }

    private static bool HasTag(string name) => TagManager.TryGet(name, out _);

    private static IEnumerable<OvObject> AllObjects() {
        static IEnumerable<OvObject> Walk(OvObject o) => new[] { o }.Concat(o.Children.SelectMany(Walk));
        return OverlayCore.Canvases.Where(c => c != null).SelectMany(c => c.OvObjects).SelectMany(Walk);
    }

    private static int CountTexts() => AllObjects().Count(o => o.Config?.TextConfig != null);

    private static int CountObjects() => AllObjects().Count();

    // "Go to this page" (open the menu first if needed).
    private static TutorialStep GoTo(string key, OriginalMenuState page, string name) => new TutorialStep(key, $"Open {name}",
        $"Open the menu (the logo at the top left) and click <b>{name}</b>.")
        .At(() => WindowOpen ? MenuTarget(page) : null)
        .Until(() => On(page));

    private static TutorialStep OpenWindow(string key) => new TutorialStep(key, "Open Overlayer",
        $"Press {C("Alt + `")} (the key left of 1) to open the Overlayer window.")
        .Until(() => WindowOpen);

    private static TutorialStep OpenEditor(string key) => new TutorialStep(key, "Open the HUD editor",
        $"Press {C("Alt + H")}. It works from anywhere: the menu, a level select, or a paused game. "
        + "(Settings → HUD Editor has a button for it too, and lets you change the key.)")
        .Until(() => HudEditor.IsOpen);

    private static int textsAtStart, objectsAtStart;
    private static OvObject moved;
    private static Vector2 movedFrom;

    public static readonly List<TutorialChapter> Chapters = [
        new("basics", "The Overlayer window", "Opening it, moving it, the menu and its pages",
            new TutorialStep("BASICS_HI", "Welcome!",
                "Overlayer puts your own <b>HUD</b> on the game screen: texts and images that show live values like FPS, combo, "
                + "accuracy or the time. This tutorial shows you everything it can do, one small step at a time.\n\n"
                + "Steps with a <b>Next</b> button just explain. Steps that say <i>Your turn</i> wait until you do it."),
            OpenWindow("BASICS_OPEN"),
            new TutorialStep("BASICS_MOVE", "Move and resize",
                "Drag the top bar to move the window, drag its edges to resize it. Double-click the top bar to make it big or small again.\n\n"
                + $"Lost it off screen? <b>Hold</b> {C("Alt + `")} for a moment: size and spot reset.")
                .At(() => PanelPart("TopBar")),
            new TutorialStep("BASICS_MENU", "The menu", "Click the logo to open the menu.")
                .At(() => PanelPart("TopBar/Logo"))
                .Until(() => MenuOpen),
            new TutorialStep("BASICS_PAGES", "Pages",
                "<b>Overlayer</b>: your canvases (groups of HUD pieces) and packages\n"
                + "<b>Tutorial</b>: this\n"
                + "<b>Resources</b>: your own fonts and images\n"
                + "<b>JS</b>: scripts that add your own tags\n"
                + "<b>Easing</b>: draw motion curves for animated numbers\n"
                + "<b>Settings</b>: language, keys, the HUD editor and more\n"
                + "Game modules can add a page of their own too (like <b>ADOFAI</b>).")
                .At(() => MenuOpen ? UICore.MenuContent : PanelPart("TopBar/Logo")),
            new TutorialStep("BASICS_POWER", "The power button",
                "Turns Overlayer off completely: every HUD piece hides and nothing runs. Click again to turn it back on. "
                + "Handy if something misbehaves in a level.")
                .At(() => UICore.Menu != null ? UICore.Menu.Find("Power") as RectTransform : null),
            GoTo("BASICS_SETTINGS", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("BASICS_LANGUAGE", "Language", "Overlayer speaks several languages. Pick yours here; the change is instant.")
                .At(() => RowOf("LANGUAGE")),
            new TutorialStep("BASICS_CLOSE", "Closing",
                $"The X closes the window; so does {C("Alt + `")} again. While the window is open, the game ignores your key presses (you can change that), "
                + "so close it before you play.")
                .At(() => PanelPart("TopBar/Close"))),

        new("first", "Your first HUD text", "Add a text, put a live value in it, move it",
            OpenEditor("FIRST_OPEN"),
            new TutorialStep("FIRST_SCREEN", "This is the HUD editor",
                "You edit right on the game screen.\n\n"
                + "<b>Top bar</b>: add things, presets, grid, rulers, undo, done\n"
                + "<b>Left</b>: your canvases and everything in them\n"
                + "<b>Right</b>: settings of what you selected\n"
                + "<b>Bottom bar</b>: align, group, order, lock, hide")
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("FIRST_ADD", "Add a text", "Click the <b>A</b> button. (Right-clicking the screen works too.)")
                .At(() => HudEditor.Control("add_text"))
                .OnEnter(() => textsAtStart = CountTexts())
                .Until(() => CountTexts() > textsAtStart),
            new TutorialStep("FIRST_TYPE", "Put a live value in it",
                $"<b>Double-click</b> the new text and type:\n{C("FPS {Fps:0}")}\nthen click anywhere else.\n\n"
                + $"Anything in {C("{ }")} is a <b>tag</b>: it gets replaced by a live value. This one is your frame rate with no decimals.")
                .Until(() => AllObjects().Any(o => o.Config?.TextEngineConfig?.Text.StaticValue?.Contains("{Fps") == true)),
            new TutorialStep("FIRST_MOVE", "Move it", "Drag the text somewhere else. Pink lines show when it lines up with the screen or other things.")
                .OnEnter(() => {
                    moved = HudEditor.SelectedObject;
                    movedFrom = moved?.RectTransform != null ? (Vector2)moved.RectTransform.position : Vector2.zero;
                })
                .Until(() => {
                    moved ??= HudEditor.SelectedObject;
                    return moved?.RectTransform != null && ((Vector2)moved.RectTransform.position - movedFrom).sqrMagnitude > 400f;
                }),
            new TutorialStep("FIRST_HANDLES", "Handles",
                "On a selected thing:\n"
                + "<b>Corners</b>: scale (the opposite corner stays; Alt: from the middle)\n"
                + "<b>Edge bars</b>: width or height\n"
                + "<b>Circle on top</b>: rotate (Shift: 15° steps)\n"
                + "<b>Arrow keys</b>: nudge by a pixel (Shift: 10)\n"
                + "Hold <b>Alt</b> while dragging: no snapping, and distances to the things around are shown."),
            new TutorialStep("FIRST_PANEL", "The right panel",
                "Everything about what's selected: name, position, size, rotation, scale, then its look: opacity, the text itself, font size, "
                + "alignment and color. <b>All properties…</b> at the bottom opens every setting there is.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("FIRST_PIN", "Pin to",
                "The 3×3 dots pick which corner or edge of the screen the thing sticks to. Pin a top-right counter to the top right "
                + "and it stays there on any screen size. Clicking a dot doesn't move it.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("FIRST_DONE", "Done",
                "Click the check mark (or press Esc with nothing selected). Everything is saved as you go; there's no save button.")
                .At(() => HudEditor.Control("close"))
                .Until(() => !HudEditor.IsOpen)),

        new("editor", "HUD editor tools", "Selecting, the canvas list, both bars, rulers, auto layout, shortcuts",
            OpenEditor("EDITOR_OPEN"),
            new TutorialStep("EDITOR_SELECT", "Selecting",
                "<b>Click</b>: picks a whole group (a preset card, say)\n"
                + "<b>Double-click</b>: goes into the group, one level each time\n"
                + "<b>Ctrl + click</b>: picks the innermost thing right away\n"
                + "<b>Shift + click</b>: adds to the selection\n"
                + "<b>Drag on empty space</b>: box select\n"
                + "<b>Right-click</b>: a menu that also lists everything under the pointer, to reach things hidden behind others"),
            new TutorialStep("EDITOR_CANVASES", "The canvas list",
                "A <b>canvas</b> is a layer of HUD pieces. Here you see them all as a tree.\n\n"
                + "Click a row to select, double-click to rename, drag rows to change order or put one inside another. "
                + "The dot shows / hides, the square locks (locked things can't be clicked on screen). "
                + "<b>+</b> makes a new canvas; the X deletes one (Ctrl+Z brings it back).")
                .At(() => HudEditor.Part("Layers")),
            new TutorialStep("EDITOR_TOP", "The top bar",
                "Canvas list on/off · add text · add image · presets · <b>where new things go</b> (the main canvas, a new canvas, "
                + "next to / inside / around the selection) · grid · rulers · duplicate · delete · undo · redo · done.\n\nHover any button for its name.")
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("EDITOR_BOTTOM", "The bottom bar",
                "<b>Align</b> left / center / right / top / middle / bottom. With one thing selected it lines up with what it's in, "
                + "or the screen.\n<b>Even gaps</b> across or down (3 or more).\n<b>Group / Ungroup</b>, <b>Front / Back</b>, <b>Lock</b>, <b>Hide</b>.\n\n"
                + "Greyed out means it can't do anything with what's selected.")
                .At(() => HudEditor.Part("BottomBarArea/BottomBar")),
            new TutorialStep("EDITOR_GRID", "Grid, rulers and guides",
                "<b>Grid</b>: things snap to it. <b>Rulers</b>: drag out of a ruler to make a guide line, drag it back onto the ruler to remove it. "
                + "Things snap to guides too. Guides are kept per screen size ratio, so they stay put on another resolution.")
                .At(() => HudEditor.Control("rulers")),
            new TutorialStep("EDITOR_LAYOUT", "Auto layout",
                "Select a group and set <b>Auto layout</b> to Row or Column in the right panel: its children line up with an even gap, "
                + "and the group hugs them. Change the gap and padding there too. Several things selected? "
                + "<b>Group as row / column</b> does both at once.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("EDITOR_TEAR", "Take the panel apart",
                "Drag a section title in the right panel (like <b>Look</b>), or a card in All properties, <b>out</b> of the panel: "
                + "it becomes its own window. Drag it back over the panel to put it back.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("EDITOR_KEYS", "Shortcuts",
                "Ctrl+C / Ctrl+V copy and paste (Ctrl+Shift+V: in the same spot)\n"
                + "Ctrl+Alt+C / Ctrl+Alt+V copy and paste the <b>look</b> only\n"
                + "Ctrl+D duplicate · Del delete · Ctrl+G group · Ctrl+Shift+G ungroup\n"
                + "Ctrl+] / Ctrl+[ forward / backward (with Shift: to the front / back)\n"
                + "Ctrl+Shift+L lock · Ctrl+Shift+H hide · Ctrl+A select all · Ctrl+Z / Ctrl+Y undo / redo")),

        new("presets", "Presets and the preview", "Ready-made HUDs, and seeing them as in a real play",
            OpenEditor("PRESETS_OPEN"),
            new TutorialStep("PRESETS_MENU", "Presets", "Click the star.")
                .At(() => HudEditor.Control("presets"))
                .Until(() => HudEditor.PresetMenuOpen),
            new TutorialStep("PRESETS_PICK", "Pick one",
                "<b>Elements</b> are single pieces, <b>Layouts</b> are whole HUDs. Pick any, <b>Default HUD</b> for example. "
                + "They go in as normal objects: change anything about them.")
                .OnEnter(() => objectsAtStart = CountObjects())
                .Until(() => CountObjects() > objectsAtStart),
            new TutorialStep("PRESETS_PLAY", "They show while playing",
                "Game presets only show while you play (and while this editor is open, so you can edit them). "
                + "Values like combo show what they'd be right now; in a menu that's mostly 0."),
            new TutorialStep("PRESETS_PREVIEW", "See it in a play",
                "<b>Right-click</b> any text (inside a preset is fine) and choose <b>Preview in game</b>.")
                .Until(() => HudPreview.IsOpen),
            new TutorialStep("PRESETS_PREVIEW_WINDOW", "The preview",
                "A pretend game screen with made-up judgments coming in, so you see how the text behaves: <b>Both</b> switches between "
                + "a menu and a play, or pick one. Move it by its title, resize it from the corner. Edits show up live.")
                .Only(() => HudEditor.IsOpen)),

        new("tags", "Tags: the useful tricks", "Decimals, zero padding, arguments, math, colors and animation",
            new TutorialStep("TAGS_WHAT", "Tags",
                $"A tag in {C("{ }")} is replaced by a live value: {C("{Fps}")}, {C("{Hour}")}"
                + ".\n\n"
                + "Names are case-sensitive. A misspelled tag shows <b>nothing</b>. In a text box, typing " + C("{") + " lists every tag."),
            new TutorialStep("TAGS_DECIMALS", "Decimal places",
                $"Add a format after a colon:\n{C("{Fps:0}")} → 60\n{C("{Fps:0.0}")} → 59.9\n{C("{Fps:0.00}")} or {C("{Fps:F2}")} → 59.94\n"
                + $"{C("{Fps:0.##}")} → up to 2, no trailing zeros\n\nRounds like school (0.5 goes up). "
                + $"Put units <b>outside</b>: {C("{Fps:0} FPS")}."),
            new TutorialStep("TAGS_PADDING", "Leading zeros, thousands, percent",
                $"{C("{Hour:00}:{Minute:00}")} → 09:05\n{C("{CurTile:000}")} → 007\n{C("{FrameCount:#,##0}")} → 1,234,567\n"
                + $"{C("{FrameTime:N2}")} → 16.67\n{C("{XAccuracy:P2}")} → 99.12% (from 0–1)\n\n"
                + $"A text value (a name, say) takes no format: {C("{Title:0}")} shows as typed, not the title."),
            new TutorialStep("TAGS_ARGS", "Tags that take values",
                $"Some tags take settings. Two ways to write them:\n{C("{Fps:500,0}")} = FPS averaged over 500 ms, 0 decimals\n"
                + $"{C("{Fps(500,0)}")} = the same\n\nCareful: with just one number, {C("{Fps:500}")} reads 500 as a <b>format</b>. "
                + $"Give the decimals too: {C("{Fps:500,0}")}. Same for {C("{Kps:500,0.0}")} (key presses per second)."),
            new TutorialStep("TAGS_MATH", "Math and conditions",
                $"Braces also take math:\n{C("{Combo * 2}")}\n{C("{(XAccuracyPercent - 95) * 20:0}")}\n"
                + $"Conditions with {C("? :")}:\n{C("{XAccuracyPercent >= 100 ? 'PURE' : 'keep going'}")}\n\n"
                + $"Inside math, write tags that answer yes/no or text with {C("()")}: {C("IsPlaying()")}, {C("Title()")}. "
                + $"Without them a condition is always true.")
                .Only(() => HasTag("XAccuracyPercent")),
            new TutorialStep("TAGS_MATH_CORE", "Math and conditions",
                $"Braces also take math: {C("{Fps * 2}")}, {C("{Math.round(Fps / 10) * 10}")}\n"
                + $"Conditions: {C("{Fps < 30 ? 'slow' : 'ok'}")}")
                .Only(() => !HasTag("XAccuracyPercent")),
            new TutorialStep("TAGS_RICH", "Color, size, bold",
                $"Texts understand formatting tags:\n{C("<color=#FF5050>red</color>")}\n{C("<size=60%>small</size>")}  {C("<b>bold</b>")}  {C("<i>italic</i>")}\n"
                + $"{C("{Fps:0}<size=50%> FPS</size>")}: a big number with a small unit"),
            new TutorialStep("TAGS_COLORRANGE", "Color by value",
                $"{C("<color=#{ColorRange(Fps,30,144,FF4040,40FF40)}>{Fps:0}</color>")}\n\nRed at 30 FPS or less, green at 144 or more, "
                + "and blended in between. Swap Fps for any number tag."),
            new TutorialStep("TAGS_ANIM", "Smooth and bouncy numbers",
                $"{C("{EasedValue(Combo,0,300,OutExpo)}")}: slides to each new value in 300 ms instead of jumping\n"
                + $"{C("<size={MovingMan(Combo,60,40,40,300)}>{Combo}</size>")}: pops bigger on each change\n\n"
                + "The last value of EasedValue is the motion: a preset name, or a curve from the <b>Easing</b> page.")
                .Only(() => HasTag("Combo")),
            new TutorialStep("TAGS_PLAYING", "Values outside a play",
                "Game values (combo, accuracy …) only change while you play. In a menu they keep the last play's value, and before "
                + "the first play the text shows the tag itself, like {Combo}. In the HUD editor you always see the current value.\n\n"
                + $"Different text in and out of a play: {C("{IsPlaying() ? 'Combo ' + Combo() : 'Ready'}")}")
                .Only(() => HasTag("IsPlaying")),
            new TutorialStep("TAGS_GAME", "Handy game tags",
                $"{C("{Combo}")} {C("{MaxCombo}")} · {C("{XAccuracyPercent:0.00}")} {C("{AccuracyPercent:0.00}")}\n"
                + $"{C("{P}")} all perfects · {C("{EP}")} {C("{LP}")} {C("{VE}")} {C("{VL}")} {C("{TE}")} {C("{TL}")} · {C("{Miss}")} {C("{Overload}")}\n"
                + $"{C("{ActualProgressPercent:0.0}")} progress · {C("{CurBpm:0}")} BPM · {C("{TimingMs:0}")} last hit timing\n"
                + $"{C("{Attempts}")} · {C("{Title}")} {C("{Artist}")} · {C("{MarginCombo:XP}")} current XPerfect streak")
                .Only(() => HasTag("XAccuracyPercent")),
            new TutorialStep("TAGS_CORE", "Handy everywhere",
                $"{C("{Hour:00}:{Minute:00}")} clock · {C("{AmPm}")} · {C("{DayOfWeek}")}\n{C("{Fps:500,0}")} FPS · {C("{FrameTime:500,0.00}")} ms per frame\n"
                + $"{C("{Kps:500,0}")} key presses per second · {C("{IsKeyHeld:Space}")} True while Space is held")),

        new("fx", "ƒ: settings that follow the game", "Make opacity, position or color depend on what's happening",
            new TutorialStep("FX_WHAT", "The ƒ button",
                "In <b>All properties</b> (HUD editor) and in the canvas editor, many settings have an <b>ƒ</b> button. "
                + "Click it and the setting becomes a small formula instead of a number. It's JavaScript, and every tag is a function in it."),
            new TutorialStep("FX_EXAMPLES", "Examples",
                $"Opacity, only while playing:\n{C("IsPlaying() ? 1 : 0")}\n"
                + $"Opacity that fades out under 30 FPS:\n{C("Math.min(1, Fps() / 60)")}\n"
                + $"Position wobbling sideways:\n{C("[Math.sin(Time() * 2) * 30, 0]")}\n"
                + $"Color (red, green, blue, alpha from 0 to 1):\n{C("[1, Fps() / 144, 0, 1]")}\n\n"
                + $"Two numbers go in {C("[ ]")}. Several lines are fine: the last one is the value."),
            new TutorialStep("FX_TEXT", "A text's ƒ",
                $"On the Text itself, the formula's result <b>is</b> the text, tags and all: {C("IsPlaying() ? '{Combo}x' : 'Ready'")}\n\n"
                + "If a formula breaks, the normal value is used, and the box underneath says what's wrong.")),

        new("canvases", "Canvases, sharing and packages", "The canvas list page, the canvas editor, export and import",
            OpenWindow("CANVAS_OPEN"),
            GoTo("CANVAS_GOTO", OriginalMenuState.Overlayer, "Overlayer"),
            new TutorialStep("CANVAS_TILES", "Your canvases",
                "Each tile is a canvas. <b>Click</b> one to open the canvas editor: a tree of its objects on the left, every setting "
                + "on the right (the same as All properties). The toggle turns a canvas on or off; the small buttons copy it, export it "
                + "or delete it (click twice; Shift + click deletes at once). <b>…</b> opens its info: author, picture, scripts to bundle.")
                .At(() => PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid")),
            new TutorialStep("CANVAS_NEW", "New, preset, import",
                "The last tile does three things: the left part makes an empty canvas, the top right adds a preset, the bottom right "
                + "imports a file (.o5cp or .json).")
                .At(() => {
                    var grid = PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid");
                    return grid != null && grid.childCount > 0 ? grid.GetChild(grid.childCount - 1) as RectTransform : grid;
                }),
            new TutorialStep("CANVAS_SHARE", "Sharing",
                "<b>Export</b> saves a canvas as an <b>.o5cp</b> package with its fonts, images and scripts inside. Send it to anyone.\n\n"
                + "An imported package shows as a tile marked as a package: it works, but can't be edited, so updates from the author "
                + "stay clean. To change it, use its <b>Copy</b> button: that makes an editable canvas of it, with its fonts and images "
                + "added to yours."),
            new TutorialStep("CANVAS_VS_EDITOR", "Which editor?",
                "The <b>HUD editor</b> (Alt+H) is for laying things out on the screen. The <b>canvas editor</b> (click a tile) shows the "
                + "same objects as a list, for when you know exactly which setting you want. Use whichever is easier; they edit the same thing.")),

        new("resources", "Fonts and images", "Bringing your own files in and using them",
            OpenWindow("RES_OPEN"),
            GoTo("RES_GOTO", OriginalMenuState.Resources, "Resources"),
            new TutorialStep("RES_ADD", "Adding a file",
                "Pick a file with <b>Browse</b> (or paste its path), give it a name, then <b>Add</b>. The button at the top switches between "
                + "images and fonts.")
                .At(() => PagePart(OriginalMenuState.Resources, "ResourcesRoot")),
            new TutorialStep("RES_USE", "Using them",
                "Fonts: in a text's settings, <b>Font</b>. Images: add an image (the mountain button in the HUD editor) and pick it under "
                + "<b>Sprite</b>.\n\nAn image card's <b>Sprite Editor</b> sets nine-slice borders, so a frame or a rounded box can stretch "
                + "without its corners stretching.")),

        new("js", "Your own tags with JavaScript", "Scripts, RegisterTag, tags that live in one text",
            OpenWindow("JS_OPEN"),
            GoTo("JS_GOTO", OriginalMenuState.JS, "JS"),
            new TutorialStep("JS_FOLDER", "The script folder",
                "Every .js file in the scripts folder runs when Overlayer starts. <b>Open Folder</b> opens it, <b>Reload All</b> runs them again "
                + "(<b>Auto Reload</b> does it whenever a file changes). The list shows each file's tags and any errors.")
                .At(() => PagePart(OriginalMenuState.JS, "JSRoot")),
            new TutorialStep("JS_REGISTER", "Making a tag",
                $"In a file in that folder:\n{C("RegisterTag('Double', () => Combo() * 2);")}\nAfter a reload, {C("{Double}")} works in any text.\n\n"
                + $"Arguments work too: {C("RegisterTag('Greet', (name) => 'hi ' + name);")} → {C("{Greet('you')}")}\n"
                + "The <b>Example</b> folder next to it has more; copy a file into the scripts folder to try it."),
            new TutorialStep("JS_LOCAL", "A tag for one text only",
                $"Write the RegisterTag right in a text, in braces:\n{C("{RegisterTag('Half', () => Fps() / 2)}FPS/2: {Half:0}")}\n"
                + "It shows nothing itself, and the tag only exists in that text. No file needed.")),

        new("easing", "Custom easing", "Draw the motion of animated numbers",
            OpenWindow("EASE_OPEN"),
            GoTo("EASE_GOTO", OriginalMenuState.Easing, "Easing"),
            new TutorialStep("EASE_GRAPH", "Draw a curve",
                "Left to right is time, bottom to top is how far the number has moved. <b>Click</b> to add a point, <b>drag</b> to move it, "
                + "<b>right-click</b> or Del removes it, Shift snaps. Going above the top line overshoots and comes back: a bounce.")
                .At(() => PagePart(OriginalMenuState.Easing, null)),
            new TutorialStep("EASE_USE", "Use it",
                "<b>Copy example tag</b> gives a ready tag with your curve, like "
                + C("{EasedValue(Combo,0,300,curve(0,0;0.4,1.1;1,1))}") + ". <b>Preview in game</b> shows it on a pretend counter. "
                + "<b>Save</b> keeps curves for later; start from a preset with the dropdown.")),

        new("settings", "Settings worth knowing", "Startup, tooltips, scale, fonts, the HUD editor",
            OpenWindow("SET_OPEN"),
            GoTo("SET_GOTO", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("SET_OVERLAYER", "Overlayer",
                "<b>Show panel at startup</b>, tooltips (and advanced tooltips with more detail), <b>UI Scale</b> if the window is too "
                + "small or big, animation speed, and the fonts the window itself uses. <b>Find</b> at the top filters the settings by name.")
                .At(() => RowOf("OVERLAYER")),
            new TutorialStep("SET_HUD", "HUD Editor",
                "Its key (Alt+H), snapping, hints, which canvas new things go into (<b>Main Canvas</b>), and under Advanced: snap distance, "
                + "grid size, and whether it may open while playing. During <b>autoplay</b> it always may.")
                .At(() => RowOf("HUD_EDITOR")),
            new TutorialStep("SET_END", "That's everything!",
                "You've seen every part of Overlayer. The Tutorial page keeps every chapter, so come back whenever you want a reminder. Have fun!")),
    ];
}
