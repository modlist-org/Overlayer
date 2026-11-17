using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Tag.Core;
using Overlayer.Tutorial.Olly;
using Overlayer.UI;
using Overlayer.UI.Factory;
using UnityEngine;

namespace Overlayer.Tutorial;

// Every chapter of the tutorial, told by Olly. Steps point at the real UI and, where it makes
// sense, she does the thing first and then waits for the user to do it.
// «code» in a text is shown as code (see Tutorial.Styled).
internal static class TutorialContent {
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

    private static bool Game => HasTag("XAccuracyPercent");

    private static IEnumerable<OvObject> AllObjects() {
        static IEnumerable<OvObject> Walk(OvObject o) => new[] { o }.Concat(o.Children.SelectMany(Walk));
        return OverlayCore.Canvases.Where(c => c != null).SelectMany(c => c.OvObjects).SelectMany(Walk);
    }

    private static IEnumerable<OvObject> UserObjects() => AllObjects().Where(o => o.Config?.Name.Value != HudEditor.DemoName);

    private static int CountTexts() => UserObjects().Count(o => o.Config?.TextConfig != null);

    private static int CountObjects() => UserObjects().Count();

    private static bool AnyText(string part) => AllObjects().Any(o => o.Config?.TextEngineConfig?.Text.StaticValue?.Contains(part) == true);

    // ---- Olly's sample text ----

    private static OvObject sample;

    // Still on a canvas (a removed one lingers until the end of the frame).
    private static bool Alive(OvObject o) => o?.GameObject != null && OverlayCore.Canvases.Any(c => c != null && c.OvObjects.Contains(o));

    private static Vector2 SamplePoint => new(Screen.width * 0.36f, Screen.height * 0.6f);

    private static RectTransform SampleRect => Alive(sample) ? sample.RectTransform : null;

    // The sample with this text: the one already there, or a new one.
    private static OvObject Sample(string text) {
        if (Alive(sample)) {
            HudEditor.DemoSetText(sample, text);
            HudEditor.DemoSelect(sample);
        } else {
            sample = HudEditor.DemoAddText(SamplePoint, text);
        }
        return sample;
    }

    private static void PutSampleAway() {
        HudEditor.DemoRemoveAll();
        sample = null;
    }

    // Shows a sample text: one line per example.
    private static TutorialDemo Showing(string text) => new TutorialDemo().Wait(0.3f).Do(() => Sample(text)).Wait(0.6f);

    // Olly writes into the sample herself: double-click, type.
    private static TutorialDemo Typing(string text) => new TutorialDemo()
        .Wait(0.4f)
        .Do(() => Sample(string.Empty))
        .Point(() => SampleRect)
        .DoubleClick()
        .Type(() => sample, text)
        .Wait(0.6f)
        .HidePointer();

    // ---- common steps ----

    // "Go to this page" (open the menu first if needed).
    private static TutorialStep GoTo(string key, OriginalMenuState page, string name) => new TutorialStep(key, $"Open {name}",
        $"Open the menu (the logo at the top left) and click <b>{name}</b>.")
        .At(() => WindowOpen ? MenuTarget(page) : null)
        .Until(() => On(page))
        .Only(() => !On(page));

    private static TutorialStep OpenWindow(string key) => new TutorialStep(key, "Open Overlayer",
        "Open the Overlayer window first: «Alt + `» (the key left of 1).")
        .Until(() => WindowOpen)
        .Only(() => !WindowOpen);

    private static TutorialStep OpenEditor(string key) => new TutorialStep(key, "Open the HUD editor",
        "We need the HUD editor for this. Press «Alt + H». It works anywhere: the menu, level select, a paused game.")
        .Until(() => HudEditor.IsOpen)
        .Only(() => !HudEditor.IsOpen);

    private static int textsAtStart, objectsAtStart;
    private static OvObject moved;
    private static Vector2 movedFrom;

    public static TutorialChapter Hello => Chapters[0];

    public static readonly List<TutorialChapter> Chapters = [
        new("hello", "Meet Olly", "Who I am, and what Overlayer is",
            new TutorialStep("HELLO_HI", "Hello!",
                "Oh! You opened it.\n...Ahem. Hi. I'm Olly. I live in Overlayer, and apparently I'm the one showing you around.")
                .Looks(Face.Surprised),
            new TutorialStep("HELLO_WHAT", "What is Overlayer?",
                "Overlayer puts your own HUD on the game screen: texts and images that show live values. "
                + "You place them yourself, right on the screen, and they show what you want, how you want.",
                "FPS, the time, keys per second, and with a game module: combo, accuracy, judgments, progress, BPM...\n"
                + "Make one from scratch, start from a ready-made preset, or import someone else's.")
                .Looks(Face.Proud),
            new TutorialStep("HELLO_ASK", "Shall we?",
                "So. Want me to show you how it all works? I'll do things first, then you try. It's faster than poking around, promise.")
                .Looks(Face.Curious)
                .Ask(
                    new TutorialChoice("SHOW_ME", "Show me", Tutorial.IntoCourse, primary: true),
                    new TutorialChoice("LATER", "Later", null)),
            new TutorialStep("HELLO_LATER", "Later, then",
                "Tch. Fine.\nWhen you change your mind, it's all on the <b>Tutorial</b> page in the menu. I'll be there.")
                .Looks(Face.Annoyed)
                .At(() => PanelPart("TopBar/Logo"))),

        new("basics", "The Overlayer window", "Opening it, moving it, the menu and its pages",
            OpenWindow("BASICS_OPEN"),
            new TutorialStep("BASICS_MOVE", "Move and resize",
                "First, this window. Drag the top bar to move it, drag an edge to resize it.",
                "Double-click the top bar: big / small.\nLost it off screen? <b>Hold</b> «Alt + `» for a moment: size and spot reset.")
                .At(() => PanelPart("TopBar")),
            new TutorialStep("BASICS_MENU", "The menu", "Now click the logo. Yes, that one. It opens the menu.")
                .Looks(Face.Smile)
                .At(() => PanelPart("TopBar/Logo"))
                .Until(() => MenuOpen),
            new TutorialStep("BASICS_PAGES", "Pages", "These are all the pages. Quick tour:",
                "<b>Overlayer</b>: your canvases (groups of HUD pieces) and packages\n"
                + "<b>Tutorial</b>: me\n"
                + "<b>Resources</b>: your own fonts and images\n"
                + "<b>JS</b>: scripts that add your own tags\n"
                + "<b>Easing</b>: draw motion curves for animated numbers\n"
                + "<b>Settings</b>: language, keys, the HUD editor and more\n"
                + "Game modules can add a page of their own too (like <b>ADOFAI</b>).")
                .At(() => MenuOpen ? UICore.MenuContent : PanelPart("TopBar/Logo")),
            new TutorialStep("BASICS_POWER", "The power button",
                "This switches all of Overlayer off: every HUD piece hides and nothing runs. If a level acts weird, try this first.")
                .At(() => UICore.Menu != null ? UICore.Menu.Find("Power") as RectTransform : null),
            GoTo("BASICS_SETTINGS", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("BASICS_LANGUAGE", "Language", "Language is here. Pick one and I'll speak it right away. Well, if someone translated me.")
                .Looks(Face.Smile)
                .At(() => RowOf("LANGUAGE")),
            new TutorialStep("BASICS_CLOSE", "Closing",
                "«Alt + `» again, or the X, closes the window. While it's open the game ignores your keys, so close it before you play.\n"
                + "Don't blame me for missed tiles.")
                .Looks(Face.Smug)
                .At(() => PanelPart("TopBar/Close"))),

        new("first", "Your first HUD text", "Watch Olly make one, then make your own",
            OpenEditor("FIRST_OPEN"),
            new TutorialStep("FIRST_SCREEN", "This is the HUD editor",
                "This is the HUD editor. You edit right on the game screen, so what you see is what you get.",
                "<b>Top bar</b>: add things, presets, grid, rulers, undo, done\n"
                + "<b>Left</b>: your canvases and everything in them\n"
                + "<b>Right</b>: settings of what you selected\n"
                + "<b>Bottom bar</b>: align, group, order, lock, hide")
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("FIRST_DEMO", "Watch me",
                "Let me do one first. I click <b>A</b>, double-click the new text, and type a tag.",
                "Anything in «{ }» is a <b>tag</b>: it's replaced by a live value.\n«{Fps:0}» is your frame rate with no decimals.")
                .Looks(Face.Proud)
                .At(() => SampleRect ?? HudEditor.Control("add_text"))
                .Shows(new TutorialDemo()
                    .Wait(0.5f)
                    .Point(() => HudEditor.Control("add_text"))
                    .Click(() => sample = HudEditor.DemoAddText(SamplePoint, string.Empty))
                    .Wait(0.4f)
                    .Point(() => SampleRect)
                    .DoubleClick()
                    .Type(() => sample, "FPS {Fps:0}")
                    .Wait(0.5f)
                    .HidePointer())
                .OnExit(PutSampleAway),
            new TutorialStep("FIRST_ADD", "Your turn: add a text", "Your turn. Click the <b>A</b> button. (Right-clicking the screen works too.)")
                .At(() => HudEditor.Control("add_text"))
                .OnEnter(() => textsAtStart = CountTexts())
                .Until(() => CountTexts() > textsAtStart),
            new TutorialStep("FIRST_TYPE", "Your turn: a live value",
                "Now <b>double-click</b> it and type «FPS {Fps:0}», then click somewhere else.",
                "Or type it in the <b>Text</b> box in the right panel: same thing.\n"
                + "Typing «{» opens a list of every tag. Names are case-sensitive: «{fps}» shows nothing.")
                .Until(() => UserObjects().Any(o => o.Config?.TextEngineConfig?.Text.StaticValue?.Contains("{Fps") == true)),
            new TutorialStep("FIRST_MOVE", "Move it", "Nice. Now drag it somewhere else. Pink lines show up when it lines up with the screen or other things.")
                .Looks(Face.Smile)
                .OnEnter(() => {
                    moved = HudEditor.SelectedObject;
                    movedFrom = moved?.RectTransform != null ? (Vector2)moved.RectTransform.position : Vector2.zero;
                })
                .Until(() => {
                    moved ??= HudEditor.SelectedObject;
                    return moved?.RectTransform != null && ((Vector2)moved.RectTransform.position - movedFrom).sqrMagnitude > 400f;
                }),
            new TutorialStep("FIRST_HANDLES", "Handles", "A selected thing has handles. Here's what they do:",
                "<b>Corners</b>: scale (the opposite corner stays; Alt: from the middle)\n"
                + "<b>Edge bars</b>: width or height\n"
                + "<b>Circle on top</b>: rotate (Shift: 15° steps)\n"
                + "<b>Arrow keys</b>: nudge by a pixel (Shift: 10)\n"
                + "Hold <b>Alt</b> while dragging: no snapping, and distances to the things around are shown."),
            new TutorialStep("FIRST_PANEL", "The right panel",
                "Everything about the selected thing is on the right: position, size, rotation, then its look: opacity, text, font size, alignment, color.",
                "<b>All properties…</b> at the bottom opens every setting there is.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("FIRST_TEXTBOX", "The Text box",
                "Double-clicking is one way to write. The <b>Text</b> box on the right is the other: same text, more room. Watch.",
                "Whatever you type there shows on the screen right away, tags and all.")
                .At(() => HudEditor.Control("text"))
                .Shows(new TutorialDemo()
                    .Wait(0.4f)
                    .Do(() => Sample(string.Empty))
                    .Point(() => HudEditor.Control("text"))
                    .Click()
                    .Type(() => sample, "{Hour:00}:{Minute:00}:{Second:00}")
                    .Wait(0.4f)
                    .HidePointer()),
            new TutorialStep("FIRST_OPACITY", "Opacity",
                "<b>Opacity</b> is right above it. Drag it left and the text fades.",
                "100: solid. 0: invisible (still there, just not shown).")
                .At(() => HudEditor.Control("opacity"))
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        Sample("{Hour:00}:{Minute:00}:{Second:00}");
                        HudEditor.DemoSetAlphaFx(sample, null);
                        HudEditor.DemoSetAlpha(sample, 1f);
                    })
                    .Drag(() => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.97f, 0.5f),
                        () => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.25f, 0.5f), 1.2f,
                        t => HudEditor.DemoSetAlpha(sample, 1f - 0.75f * t))
                    .Wait(0.6f)
                    .Drag(() => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.25f, 0.5f),
                        () => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.97f, 0.5f), 1f,
                        t => HudEditor.DemoSetAlpha(sample, 0.25f + 0.75f * t))
                    .HidePointer()),
            new TutorialStep("FIRST_FX", "A formula instead of a number",
                "Now the fun part. A setting like opacity can be a <b>formula</b> instead of a number. Look, I made it breathe.",
                "In <b>All properties…</b>, find the setting and click its <b>ƒ</b>, then write JavaScript. Every tag is a function in it:\n"
                + "«0.55 + 0.45 * Math.sin(Time() * 4)»  what you see now\n«IsPlaying() ? 1 : 0»  only while playing\n"
                + "«Math.min(1, Fps() / 60)»  fades under 60 FPS\nThe ƒ chapter has more.")
                .Looks(Face.Smug)
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        Sample("{Hour:00}:{Minute:00}:{Second:00}");
                        HudEditor.DemoSetAlphaFx(sample, "0.55 + 0.45 * Math.sin(Time() * 4)");
                    })
                    .Point(() => HudEditor.Control("allprops"))
                    .Wait(0.8f)
                    .HidePointer())
                .OnExit(PutSampleAway),
            new TutorialStep("FIRST_PIN", "Pin to",
                "The 3×3 dots pick which corner or edge of the screen it sticks to. Pin a top-right counter to the top right "
                + "and it stays there on any screen size. Clicking a dot doesn't move it.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("FIRST_DONE", "Done", "That's a HUD! Click the check mark to finish. Everything saves as you go, there's no save button.")
                .Looks(Face.Happy)
                .At(() => HudEditor.Control("close"))
                .Until(() => !HudEditor.IsOpen)),

        new("format", "Numbers: decimals and formats", "0, #, commas, percent, padding, rounding",
            OpenEditor("FMT_OPEN"),
            new TutorialStep("FMT_RAW", "A bare number",
                "Here's a plain «{Fps}». Ugh, look at all those digits.",
                "With no format, a number shows every digit it has.")
                .Looks(Face.Annoyed)
                .At(() => SampleRect)
                .Shows(Typing("{Fps}")),
            new TutorialStep("FMT_COLON", "A format",
                "Put a colon and a format after the name. «0» means: one digit here, always. No dot, so no decimals.",
                "«{Fps:0}»  =  60\nIt rounds: 59.6 becomes 60.")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .Shows(Typing("{Fps:0}")),
            new TutorialStep("FMT_DECIMALS", "Decimal places",
                "Every «0» after the dot is one decimal place, and it's always shown, even when it's a zero.",
                "«{Fps:0.0}»  =  59.9\n«{Fps:0.00}»  =  59.94\n«{Fps:0.000}»  =  59.940")
                .At(() => SampleRect)
                .Shows(Showing("0.0  =  {Fps:0.0}\n0.00  =  {Fps:0.00}\n0.000  =  {Fps:0.000}")),
            new TutorialStep("FMT_HASH", "0 or #",
                "«#» is a digit that only shows up when it's needed. So «0.##» keeps up to two decimals, but drops zeros at the end.",
                "<b>0</b>: always shown (a zero if there's nothing)\n<b>#</b>: only if it isn't a zero at the end\n"
                + "«{1.5:0.00}»  =  1.50\n«{1.5:0.##}»  =  1.5\n«{2:0.##}»  =  2")
                .Looks(Face.Curious)
                .At(() => SampleRect)
                .Shows(Showing("1.5 / 0.00  =  {1.5:0.00}\n1.5 / 0.##  =  {1.5:0.##}\n2 / 0.##  =  {2:0.##}")),
            new TutorialStep("FMT_PAD", "Leading zeros",
                "Zeros before the dot pad the number to that many digits. Clocks love this.",
                "«{7:000}»  =  007\n«{Hour:00}:{Minute:00}»  =  09:05")
                .At(() => SampleRect)
                .Shows(Showing("7 / 000  =  {7:000}\n{Hour:00}:{Minute:00}:{Second:00}")),
            new TutorialStep("FMT_GROUP", "Thousands",
                "A comma in the format turns on thousands separators.",
                "«{1234567:#,##0}»  =  1,234,567\n«{1234567.891:#,##0.00}»  =  1,234,567.89")
                .At(() => SampleRect)
                .Shows(Showing("#,##0  =  {1234567:#,##0}\n#,##0.00  =  {1234567.891:#,##0.00}")),
            new TutorialStep("FMT_PERCENT", "Percent",
                "«%» multiplies by 100 and adds the sign. Only for values from 0 to 1.",
                "«{0.856:0.0%}»  =  85.6%")
                .At(() => SampleRect)
                .Shows(Showing("0.856 / 0.0%  =  {0.856:0.0%}"))
                .Only(() => !Game),
            new TutorialStep("FMT_PERCENT_GAME", "Percent",
                "«%» multiplies by 100 and adds the sign. Only for values from 0 to 1, like «XAccuracy». "
                + "«XAccuracyPercent» is already 0 to 100, so type the % yourself.",
                "«{XAccuracy:0.00%}»  =  99.12%\n«{XAccuracyPercent:0.00}%»  =  99.12%  (same)")
                .At(() => SampleRect)
                .Shows(Showing("0.856 / 0.0%  =  {0.856:0.0%}"))
                .Only(() => Game),
            new TutorialStep("FMT_LETTERS", "Shortcuts",
                "There are letter shortcuts too. Same thing, shorter.",
                "«F2» = «0.00»   (F + how many decimals)\n«N0» = «#,##0»   «N2» = «#,##0.00»\n«P1» = «0.0%»\n"
                + "«D3» = «000», but only for whole-number tags. On a decimal one (FPS, accuracy) it fails: use «000».")
                .Looks(Face.Talk),
            new TutorialStep("FMT_ROUND", "Rounding",
                "Formats round: half goes up. If you want to cut off instead, do it with math.",
                "«{Math.floor(Fps * 10) / 10:0.0}»: 59.99 shows as 59.9, not 60.0")
                .Looks(Face.Curious)
                .Only(() => !Game),
            new TutorialStep("FMT_ROUND_GAME", "Rounding",
                "Formats round: half goes up. So 99.996% shows as 100.00%... and it wasn't. If that annoys you too, cut it off with math.",
                "«{Math.floor(XAccuracyPercent * 100) / 100:0.00}»: 99.996 shows as 99.99")
                .Looks(Face.Annoyed)
                .Only(() => Game),
            new TutorialStep("FMT_UNITS", "Units go outside",
                "Units and words go <b>outside</b> the braces. Inside, they're quietly ignored.",
                "«{Fps:0} FPS»  ✓\n«{Fps:0 FPS}»  ✗\nTags that give text (like a song title) take no format at all.")
                .Looks(Face.Sigh),
            new TutorialStep("FMT_TRY", "Your turn",
                "Your turn. Double-click my sample and make it show FPS with <b>one</b> decimal.",
                "Hint: one «0» after the dot.")
                .Looks(Face.Smug)
                .At(() => SampleRect)
                .OnEnter(() => Sample("{Fps}"))
                .Until(() => AnyText("{Fps:0.0}") && !AnyText("{Fps:0.00")),
            new TutorialStep("FMT_DONE", "Formats: done",
                "Perfect. That's all there is to numbers. I'll put my sample away.")
                .Looks(Face.Happy)
                .OnExit(PutSampleAway)),

        new("editor", "HUD editor tools", "Selecting, the canvas list, both bars, rulers, auto layout, shortcuts",
            OpenEditor("EDITOR_OPEN"),
            new TutorialStep("EDITOR_SELECT", "Selecting", "Selecting has a few tricks. Groups first, then what's inside:",
                "<b>Click</b>: a whole group (a preset card, say)\n"
                + "<b>Double-click</b>: one level into the group\n"
                + "<b>Ctrl + click</b>: the innermost thing right away\n"
                + "<b>Shift + click</b>: add to the selection\n"
                + "<b>Drag on empty space</b>: box select\n"
                + "<b>Right-click</b>: a menu that also lists everything under the pointer"),
            new TutorialStep("EDITOR_CANVASES", "The canvas list",
                "A <b>canvas</b> is a layer of HUD pieces. Here they all are, as a tree.",
                "Click a row: select. Double-click: rename. Drag rows: reorder, or put one inside another.\n"
                + "The dot shows / hides, the square locks (locked things can't be clicked on screen).\n"
                + "<b>+</b> makes a canvas; the X deletes one (Ctrl+Z brings it back).")
                .At(() => HudEditor.Part("Layers")),
            new TutorialStep("EDITOR_TOP", "The top bar", "The top bar. Hover any button and it tells you its name.",
                "Canvas list · text · image · presets · <b>where new things go</b> (main canvas, a new canvas, next to / inside / around "
                + "the selection) · grid · rulers · duplicate · delete · undo · redo · done")
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("EDITOR_BOTTOM", "The bottom bar", "The bottom bar lines things up. Greyed out means it can't do anything with what's selected.",
                "<b>Align</b> left / center / right / top / middle / bottom. One thing selected: to what it's in, or the screen.\n"
                + "<b>Even gaps</b> across or down (3 or more)\n<b>Group / Ungroup</b>, <b>Front / Back</b>, <b>Lock</b>, <b>Hide</b>")
                .At(() => HudEditor.Part("BottomBarArea/BottomBar")),
            new TutorialStep("EDITOR_GRID", "Grid, rulers and guides",
                "Turn on the grid and things snap to it. With rulers on, drag out of a ruler to make a guide line.",
                "Drag a guide back onto the ruler to remove it. Things snap to guides too. Guides are kept per screen ratio, "
                + "so they stay put on another resolution.")
                .At(() => HudEditor.Control("rulers")),
            new TutorialStep("EDITOR_LAYOUT", "Auto layout",
                "Lining up a row of numbers by hand? Don't. Select their group and set <b>Auto layout</b> to Row or Column.",
                "Children line up with an even gap and the group hugs them. Gap and padding are right there too.\n"
                + "Several things selected? <b>Group as row / column</b> does both at once.")
                .Looks(Face.Smug)
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("EDITOR_TEAR", "Take the panel apart",
                "Drag a section title in the right panel (like <b>Look</b>), or a card in All properties, <b>out</b> of the panel: "
                + "it becomes its own window. Drag it back to put it back.")
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("EDITOR_KEYS", "Shortcuts", "And the shortcuts. You don't have to learn them, but they're nice.",
                "Ctrl+C / Ctrl+V copy, paste (Ctrl+Shift+V: same spot)\n"
                + "Ctrl+Alt+C / Ctrl+Alt+V copy, paste the <b>look</b> only\n"
                + "Ctrl+D duplicate · Del delete · Ctrl+G group · Ctrl+Shift+G ungroup\n"
                + "Ctrl+] / Ctrl+[ forward / backward (Shift: to the front / back)\n"
                + "Ctrl+Shift+L lock · Ctrl+Shift+H hide · Ctrl+A all · Ctrl+Z / Ctrl+Y")),

        new("presets", "Presets and the preview", "Ready-made HUDs, and seeing them as in a real play",
            OpenEditor("PRESETS_OPEN"),
            new TutorialStep("PRESETS_MENU", "Presets", "Don't feel like building it all? Click the star.")
                .Looks(Face.Smile)
                .At(() => HudEditor.Control("presets"))
                .Until(() => HudEditor.PresetMenuOpen),
            new TutorialStep("PRESETS_PICK", "Pick one",
                "<b>Elements</b> are single pieces, <b>Layouts</b> are whole HUDs. Pick any one.",
                "They go in as normal objects: change anything you like about them.")
                .OnEnter(() => objectsAtStart = CountObjects())
                .Until(() => CountObjects() > objectsAtStart),
            new TutorialStep("PRESETS_PLAY", "They show while playing",
                "Game presets only show while you play, and while this editor is open, so you can edit them. In a menu, most values are 0.")
                .Only(() => Game),
            new TutorialStep("PRESETS_PREVIEW", "See it in a play",
                "Want to see it move? <b>Right-click</b> any text (inside a preset is fine) and pick <b>Preview in game</b>.")
                .Until(() => HudPreview.IsOpen),
            new TutorialStep("PRESETS_PREVIEW_WINDOW", "The preview",
                "A pretend game screen with made-up judgments, so you see how your text behaves. Edits show up live.",
                "<b>Both</b> switches between a menu and a play, or pick one. Move it by its title, resize from the corner.")
                .Only(() => HudEditor.IsOpen)),

        new("tags", "Tags: the useful tricks", "Arguments, math, colors, size, animated numbers",
            OpenEditor("TAGS_OPEN"),
            new TutorialStep("TAGS_ARGS", "Tags that take values",
                "Some tags take settings, after the colon or in parentheses. Fps can average over a time, for example.",
                "«{Fps:500,0}» = FPS averaged over 500 ms, 0 decimals\n«{Fps(500,0)}» = the same\n"
                + "Careful: «{Fps:500}» reads 500 as a <b>format</b>. Give the decimals too.")
                .At(() => SampleRect)
                .Shows(Showing("{Fps:500,0}")),
            new TutorialStep("TAGS_MATH", "Math",
                "Braces do math, too. And «? :» picks one of two things.",
                "«{Fps * 2:0}»\n«{Fps < 30 ? 'slow' : 'smooth'}»\n"
                + "Inside math, tags that give yes/no or text need «()»: «IsPlaying()», «Title()». Without them a condition is always true.")
                .At(() => SampleRect)
                .Shows(Showing("{Fps * 2:0}\n{Fps < 30 ? 'slow' : 'smooth'}")),
            new TutorialStep("TAGS_RICH", "Color, size, bold",
                "Texts understand formatting tags. A big number with a small unit looks nice, right?",
                "«<color=#FF5050>red</color>»  «<b>bold</b>»  «<i>italic</i>»\n«{Fps:0}<size=50%> FPS</size>»")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .Shows(Showing("<color=#FF5050>red</color> <b>bold</b> <i>italic</i>\n{Fps:0}<size=50%> FPS</size>")),
            new TutorialStep("TAGS_COLORRANGE", "Color by value",
                "«ColorRange» picks a color by value: here red at 0 seconds, green at 59.",
                "«<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second}</color>»\n"
                + "Tag, from, to, color at «from», color at «to». Swap Second for any number tag.")
                .At(() => SampleRect)
                .Shows(Showing("<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second:00}</color>")),
            new TutorialStep("TAGS_ANIM", "Smooth and bouncy numbers",
                "And my favorite: numbers that move. Watch the seconds.",
                "«{EasedValue(Second,2,500,OutBack)}»: slides to each new value in 500 ms\n"
                + "«<size={MovingMan(Second,60,36,36,300)}>{Second}</size>»: pops bigger on each change\n"
                + "The last value of EasedValue is the motion: a preset name, or a curve from the <b>Easing</b> page.")
                .Looks(Face.Happy)
                .At(() => SampleRect)
                .Shows(Showing("{EasedValue(Second,2,500,OutBack)}\n<size={MovingMan(Second,60,36,36,300)}>{Second}</size>")),
            new TutorialStep("TAGS_PLAYING", "Values outside a play",
                "Game values only change while you play. In a menu they keep the last play's value; before the first play the text shows the tag itself.",
                "Different text in and out of a play:\n«{IsPlaying() ? 'Combo ' + Combo() : 'Ready'}»")
                .Only(() => HasTag("IsPlaying")),
            new TutorialStep("TAGS_GAME", "Handy game tags", "The game tags people use the most:",
                "«{Combo}» «{MaxCombo}» · «{XAccuracyPercent:0.00}» «{AccuracyPercent:0.00}»\n"
                + "«{P}» all perfects · «{EP}» «{LP}» «{VE}» «{VL}» «{TE}» «{TL}» · «{Miss}» «{Overload}»\n"
                + "«{ActualProgressPercent:0.0}» progress · «{CurBpm:0}» BPM · «{TimingMs:0}» last hit timing\n"
                + "«{Attempts}» · «{Title}» «{Artist}» · «{MarginCombo:XP}» current XPerfect streak")
                .Only(() => Game),
            new TutorialStep("TAGS_CORE", "Handy everywhere", "And ones that work in any game:",
                "«{Hour:00}:{Minute:00}» clock · «{AmPm}» · «{DayOfWeek}»\n«{Fps:500,0}» FPS · «{FrameTime:500,0.00}» ms per frame\n"
                + "«{Kps:500,0}» key presses per second · «{IsKeyHeld:Space}» True while Space is held")
                .OnExit(PutSampleAway)),

        new("fx", "ƒ: settings that follow the game", "Make opacity, position or color depend on what's happening",
            new TutorialStep("FX_WHAT", "The ƒ button",
                "In <b>All properties</b> and the canvas editor, many settings have an <b>ƒ</b> button. Click it and the setting "
                + "becomes a small formula instead of a fixed value.",
                "It's JavaScript, and every tag is a function in it: «Fps()», «Combo()».")
                .Looks(Face.Curious),
            new TutorialStep("FX_EXAMPLES", "Examples", "A few to steal:",
                "Opacity, only while playing: «IsPlaying() ? 1 : 0»\n"
                + "Fades out under 60 FPS: «Math.min(1, Fps() / 60)»\n"
                + "Position wobbling sideways: «[Math.sin(Time() * 2) * 30, 0]»\n"
                + "Color (red, green, blue, alpha, 0 to 1): «[1, Fps() / 144, 0, 1]»\n"
                + "Two or more numbers go in «[ ]». Several lines are fine: the last one is the value.")
                .Looks(Face.Smug),
            new TutorialStep("FX_TEXT", "A text's ƒ",
                "On the Text itself, the formula's result <b>is</b> the text, tags and all. "
                + "If a formula breaks, the normal value is used, and the box under it says what's wrong.",
                "«IsPlaying() ? '{Combo}x' : 'Ready'»")),

        new("canvases", "Canvases, sharing and packages", "The canvas list page, the canvas editor, export and import",
            OpenWindow("CANVAS_OPEN"),
            GoTo("CANVAS_GOTO", OriginalMenuState.Overlayer, "Overlayer"),
            new TutorialStep("CANVAS_TILES", "Your canvases",
                "Each tile is a canvas. Click one to open the canvas editor: the same objects as a list, every setting on the right.",
                "The toggle turns a canvas on or off. The small buttons copy, export, delete (click twice; Shift + click: at once).\n"
                + "<b>…</b> opens its info: author, picture, scripts to bundle.")
                .At(() => PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid")),
            new TutorialStep("CANVAS_NEW", "New, preset, import",
                "The last tile does three things: left makes an empty canvas, top right adds a preset, bottom right imports a file.")
                .At(() => {
                    var grid = PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid");
                    return grid != null && grid.childCount > 0 ? grid.GetChild(grid.childCount - 1) as RectTransform : grid;
                }),
            new TutorialStep("CANVAS_SHARE", "Sharing",
                "<b>Export</b> saves a canvas as an <b>.o5cp</b> package, fonts, images and scripts included. Send it to anyone.",
                "An imported package works but can't be edited, so the author's updates stay clean. "
                + "To change it, press its <b>Copy</b>: you get an editable canvas, and its fonts and images are added to yours.")
                .Looks(Face.Proud),
            new TutorialStep("CANVAS_VS_EDITOR", "Which editor?",
                "The HUD editor is for placing things on the screen; the canvas editor is for when you know exactly which setting you want. "
                + "Same objects either way. Use whichever.")),

        new("resources", "Fonts and images", "Bringing your own files in and using them",
            OpenWindow("RES_OPEN"),
            GoTo("RES_GOTO", OriginalMenuState.Resources, "Resources"),
            new TutorialStep("RES_ADD", "Adding a file",
                "Pick a file with <b>Browse</b> (or paste its path), give it a name, press <b>Add</b>. The top button switches images / fonts.")
                .At(() => PagePart(OriginalMenuState.Resources, "ResourcesRoot")),
            new TutorialStep("RES_USE", "Using them",
                "Fonts: a text's <b>Font</b> setting. Images: add an image (the mountain button in the HUD editor) and pick it under <b>Sprite</b>.",
                "An image card's <b>Sprite Editor</b> sets nine-slice borders, so a frame or a rounded box stretches without stretching its corners.")),

        new("js", "Your own tags with JavaScript", "Scripts, RegisterTag, tags that live in one text",
            OpenWindow("JS_OPEN"),
            GoTo("JS_GOTO", OriginalMenuState.JS, "JS"),
            new TutorialStep("JS_FOLDER", "The script folder",
                "Every .js file in the scripts folder runs when Overlayer starts. The list shows each file's tags and any errors.",
                "<b>Open Folder</b> opens it. <b>Reload All</b> runs them again; <b>Auto Reload</b> does it whenever a file changes.")
                .At(() => PagePart(OriginalMenuState.JS, "JSRoot")),
            new TutorialStep("JS_REGISTER", "Making a tag",
                "Making a tag is one line. Really.",
                "«RegisterTag('Double', () => Combo() * 2);»\nAfter a reload, «{Double}» works in any text.\n"
                + "With arguments: «RegisterTag('Greet', (name) => 'hi ' + name);» then «{Greet('you')}»\n"
                + "The <b>Example</b> folder next to it has more. Copy a file into the scripts folder to try it.")
                .Looks(Face.Smug),
            new TutorialStep("JS_LOCAL", "A tag for one text only",
                "No file needed, even: write RegisterTag right in a text, in braces. It shows nothing itself, and the tag only lives in that text.",
                "«{RegisterTag('Half', () => Fps() / 2)}FPS/2: {Half:0}»")),

        new("easing", "Custom easing", "Draw the motion of animated numbers",
            OpenWindow("EASE_OPEN"),
            GoTo("EASE_GOTO", OriginalMenuState.Easing, "Easing"),
            new TutorialStep("EASE_GRAPH", "Draw a curve",
                "Left to right is time, bottom to top is how far the number has moved. Going above the top line overshoots and comes back: a bounce.",
                "<b>Click</b>: add a point · <b>Drag</b>: move it · <b>Right-click</b> or Del: remove · Shift: snap")
                .At(() => PagePart(OriginalMenuState.Easing, null)),
            new TutorialStep("EASE_USE", "Use it",
                "<b>Copy example tag</b> gives you a tag with your curve in it. <b>Preview in game</b> shows it on a pretend counter.",
                "«{EasedValue(Combo,0,300,curve(0,0;0.4,1.1;1,1))}»\n<b>Save</b> keeps curves for later; the dropdown starts from a preset.")),

        new("settings", "Settings worth knowing", "Startup, tooltips, scale, fonts, the HUD editor",
            OpenWindow("SET_OPEN"),
            GoTo("SET_GOTO", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("SET_OVERLAYER", "Overlayer",
                "If this window is too small or too big for you, <b>UI Scale</b> is here. So are tooltips, startup and fonts.",
                "<b>Find</b> at the top filters the settings by name.")
                .At(() => RowOf("OVERLAYER")),
            new TutorialStep("SET_HUD", "HUD Editor",
                "The HUD editor's key, snapping, hints, and <b>Main Canvas</b>: which canvas new things go into.",
                "Under Advanced: snap distance, grid size, and whether it may open while playing. During <b>autoplay</b> it always may.")
                .At(() => RowOf("HUD_EDITOR")),
            new TutorialStep("SET_END", "That's everything!",
                "...And that's everything. You know all of Overlayer now.\nIf you forget something, the Tutorial page keeps every chapter. I'll be there. Have fun!")
                .Looks(Face.Happy)),
    ];
}
