using O5Kit.Control;
using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Tutorial.Olly;
using Overlayer.UI;
using Overlayer.UI.Factory;
using UnityEngine;

namespace Overlayer.Tutorial;

// Every chapter of the tutorial, told by Olly. Steps point at the real UI and, where it makes
// sense, she does the thing first and then waits for the user to do it.
// «code» in a text is shown as code; each step sets one expression at line start via .Looks(...).
internal static class TutorialContent {
    // ---- where things are ----

    private static bool WindowOpen => UICore.CanvasObj != null && UICore.CanvasObj.activeSelf
        && UICore.Panel != null && UICore.Panel.gameObject.activeSelf && !HudEditor.IsOpen;

    private static bool MenuOpen => UICore.Menu != null && UICore.IsMenuOpen;

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

    private static IEnumerable<OvObject> AllObjects() {
        static IEnumerable<OvObject> Walk(OvObject o) => new[] { o }.Concat(o.Children.SelectMany(Walk));
        return OverlayCore.Canvases.Where(c => c != null).SelectMany(c => c.OvObjects).SelectMany(Walk);
    }

    private static IEnumerable<OvObject> UserObjects() => AllObjects().Where(o => o.Config?.Name.Value != HudEditor.DemoName);

    private static int CountTexts() => UserObjects().Count(o => o.Config?.TextConfig != null);

    private static int CountObjects() => UserObjects().Count();

    private static string SampleText() => Alive(sample) ? sample.Config?.TextEngineConfig?.Text.StaticValue : null;

    // ---- getting somewhere (done for you in the course) ----

    private static void ToEditor() {
        if (!HudEditor.IsOpen) {
            UICore.Close(true);
            HudEditor.Open();
        }
    }

    private static void ToWindow() {
        if (HudEditor.IsOpen) {
            HudEditor.Close();
        }
        UICore.Open(true);
    }

    private static void ToPage(OriginalMenuState page) {
        ToWindow();
        MenuFactory.GoTo((int)page);
    }

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

    // A plain sample again: no formulas, full opacity.
    private static OvObject PlainSample(string text) {
        var s = Sample(text);
        HudEditor.DemoTextFx(s, null);
        HudEditor.DemoWobble(s, false);
        HudEditor.DemoSetAlphaFx(s, null);
        HudEditor.DemoSetAlpha(s, 1f);
        return s;
    }

    private static void PutSampleAway() {
        HudEditor.DemoRemoveAll();
        sample = null;
    }

    // Shows a sample text: one line per example.
    private static TutorialDemo Showing(string text) => new TutorialDemo().Wait(0.4f).Do(() => Sample(text)).Wait(1.2f);

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
        $"Open the menu (top-left logo) and click <b>{name}</b>.")
        .Looks(Face.Talk)
        .At(() => WindowOpen ? MenuTarget(page) : null)
        .Until(() => On(page))
        .Only(() => !On(page))
        .AutoInCourse(() => ToPage(page));

    private static TutorialStep OpenWindow(string key) => new TutorialStep(key, "Open Overlayer",
        "Press «Alt + `» (the key left of 1) to open the Overlayer window.")
        .Looks(Face.Talk)
        .Until(() => WindowOpen)
        .Only(() => !WindowOpen)
        .AutoInCourse(ToWindow);

    private static TutorialStep OpenEditor(string key) => new TutorialStep(key, "Open the HUD editor",
        "Press «Alt + H» to open the HUD editor.")
        .Looks(Face.Talk)
        .Until(() => HudEditor.IsOpen)
        .Only(() => !HudEditor.IsOpen)
        .AutoInCourse(ToEditor);

    private static int textsAtStart, objectsAtStart;
    private static OvObject moved;
    private static OvObject[] demoRow = [];
    private static Vector2 movedFrom;

    public static TutorialChapter Hello => Chapters[0];

    /// <summary>After one chapter on its own: where the rest is.</summary>
    public static readonly TutorialChapter ChapterEnd = new("end", "Chapter done", string.Empty,
        new TutorialStep("END", "Chapter done!",
            "All other chapters are available on the <b>Tutorial</b> page in the menu.")
            .Looks(Face.Happy));

    public static readonly List<TutorialChapter> Chapters = [
        new("hello", "Meet Olly", "What Overlayer is, and how to get started",
            new TutorialStep("HELLO_HI", "Hello!",
                "Hi, I'm Olly. Let me walk you through how Overlayer works.")
                .Looks(Face.Smile),
            new TutorialStep("HELLO_WHAT", "What is Overlayer?",
                "Overlayer lets you place custom texts and images anywhere on the screen to display live values.",
                "FPS, frame time, clock, keys per second, and custom JS tags.\n"
                + "Build from scratch, pick a preset, or import someone else's package.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.Caret)),
            new TutorialStep("HELLO_ASK", "Start the guide?",
                "Want a quick walkthrough? I'll demonstrate the main features and let you try them step by step.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.SadSmall))
                .Ask(
                    new TutorialChoice("SHOW_ME", "Start", Tutorial.IntoCourse, primary: true),
                    new TutorialChoice("LATER", "Later", null))
                .Only(() => !Tutorial.InCourse),
            new TutorialStep("HELLO_LATER", "Anytime",
                "Got it. You can start any chapter anytime from the <b>Tutorial</b> page in the menu.")
                .Looks(Face.Smile)
                .At(() => PanelPart("TopBar/Logo"))
                .Only(() => !Tutorial.InCourse)),

        new("basics", "The Overlayer window", "Opening, moving, menu and pages",
            OpenWindow("BASICS_OPEN"),
            new TutorialStep("BASICS_MOVE", "Move and resize",
                "Drag the top bar to move the window, or drag any edge to resize it.",
                "Double-click the top bar: maximize / restore.\nOff-screen? <b>Hold</b> «Alt + `» to reset position and size.")
                .Looks(Face.Surprised)
                .At(() => PanelPart("TopBar")),
            new TutorialStep("BASICS_MENU", "The menu",
                "Click the top-left logo to open the navigation menu.")
                .Looks(Face.Curious)
                .At(() => PanelPart("TopBar/Logo"))
                .Until(() => MenuOpen)
                .Only(() => !MenuOpen),
            new TutorialStep("BASICS_PAGES", "Pages",
                "Here is what each page in the menu does:",
                "<b>Overlayer</b>: canvases and imported packages\n"
                + "<b>Tutorial</b>: interactive guide\n"
                + "<b>Resources</b>: custom fonts and images\n"
                + "<b>JS</b>: custom tag scripts\n"
                + "<b>Easing</b>: custom animation curves\n"
                + "<b>Settings</b>: language, shortcuts, HUD editor and more")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => MenuOpen ? UICore.MenuContent : PanelPart("TopBar/Logo")),
            new TutorialStep("BASICS_POWER", "Power toggle",
                "Turns Overlayer completely on or off. When off, all HUD elements hide and stop updating.")
                .Looks(Face.Talk)
                .At(() => UICore.Menu != null ? UICore.Menu.Find("Power") as RectTransform : null),
            GoTo("BASICS_SETTINGS", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("BASICS_LANGUAGE", "Language",
                "Change the UI language here. Changes apply immediately.")
                .Looks(Face.Smile)
                .At(() => RowOf("LANGUAGE")),
            new TutorialStep("BASICS_CLOSE", "Closing",
                "Press «Alt + `» or click X to close. While the window is open, the game ignores keyboard input, so close it before playing.")
                .Looks(Face.Talk)
                .At(() => PanelPart("TopBar/Close"))),

        new("first", "Your first HUD text", "Create, tag, move, and style a HUD text",
            new TutorialStep("FIRST_OPEN", "Open the HUD editor",
                "Press «Alt + H» to open the HUD editor directly on the game screen.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenMicro))
                .Until(() => HudEditor.IsOpen)
                .Only(() => !HudEditor.IsOpen),
            new TutorialStep("FIRST_SCREEN", "HUD editor layout",
                "You edit directly on the game screen, so what you see is what you get.",
                "<b>Top bar</b>: add objects, presets, grid, rulers, undo, done\n"
                + "<b>Left</b>: canvas and object list\n"
                + "<b>Right</b>: properties of the selection\n"
                + "<b>Bottom bar</b>: align, group, order, lock, hide")
                .Looks(Face.Smile)
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("FIRST_DEMO", "Watch an example",
                "Click <b>A</b> to add a text, double-click it, and enter a tag.",
                "Anything in «{ }» is a <b>tag</b> replaced by a live value.\n«{Fps:0}» shows frame rate with no decimals.")
                .Looks(Face.Talk)
                .At(() => SampleRect ?? HudEditor.Control("add_text"))
                .Shows(new TutorialDemo()
                    .Wait(0.5f)
                    .Point(() => HudEditor.Control("add_text"))
                    .Click(() => sample = HudEditor.DemoAddText(SamplePoint, string.Empty))
                    .Wait(0.4f)
                    .Point(() => SampleRect)
                    .DoubleClick()
                    .Type(() => sample, "FPS {Fps:0}")
                    .Wait(0.6f)
                    .HidePointer())
                .OnExit(PutSampleAway),
            new TutorialStep("FIRST_ADD", "Add a text",
                "Your turn. Click the <b>A</b> button on the top bar (or right-click the screen).")
                .Looks(Face.Smile)
                .At(() => HudEditor.Control("add_text"))
                .OnEnter(() => textsAtStart = CountTexts())
                .Until(() => CountTexts() > textsAtStart),
            new TutorialStep("FIRST_TYPE", "Enter a live tag",
                "<b>Double-click</b> the text, type «FPS {Fps:0}», then click outside to finish.",
                "You can also type in the <b>Text</b> box on the right.\n"
                + "Typing «{» lists all tags. Names are case-sensitive.")
                .Looks(Face.Talk)
                .Until(() => !O5InputBlocker.IsEditing
                    && UserObjects().Any(o => o.Config?.TextEngineConfig?.Text.StaticValue?.Contains("{Fps") == true)),
            new TutorialStep("FIRST_MOVE", "Move it",
                "Nice! Now drag the text somewhere else. Pink lines show alignment with the screen or other objects.")
                .Looks(Face.Happy)
                .OnEnter(() => {
                    moved = HudEditor.SelectedObject;
                    movedFrom = moved?.RectTransform != null ? (Vector2)moved.RectTransform.position : Vector2.zero;
                })
                .Until(() => {
                    moved ??= HudEditor.SelectedObject;
                    return !Input.GetMouseButton(0)
                        && moved?.RectTransform != null
                        && ((Vector2)moved.RectTransform.position - movedFrom).sqrMagnitude > 400f;
                }),
            new TutorialStep("FIRST_HANDLES", "Selection handles",
                "Selected objects show handles around them:",
                "<b>Corners</b>: scale (Alt: from center)\n"
                + "<b>Edge bars</b>: width or height\n"
                + "<b>Top circle</b>: rotate (Shift: 15° steps)\n"
                + "<b>Arrow keys</b>: nudge 1 px (Shift: 10 px)\n"
                + "Hold <b>Alt</b> while dragging: disable snap & show distances.")
                .Looks(Face.Normal),
            new TutorialStep("FIRST_PANEL", "Inspector panel",
                "The right panel holds position, size, rotation, opacity, text, font size, alignment, and color.",
                "<b>All properties…</b> at the bottom opens every setting.")
                .Looks(Face.Talk)
                .At(() => HudEditor.Part("Panel")),
            new TutorialStep("FIRST_TEXTBOX", "The Text box",
                "You can also edit text in the right panel's <b>Text</b> box—useful for longer expressions.",
                "Edits update live on screen as you type.")
                .Looks(Face.Talk)
                .At(() => HudEditor.Control("text"))
                .Shows(new TutorialDemo()
                    .Wait(0.4f)
                    .Do(() => PlainSample(string.Empty))
                    .Point(() => HudEditor.Control("text"))
                    .Click()
                    .Type(() => sample, "{Hour:00}:{Minute:00}:{Second:00}")
                    .Wait(0.5f)
                    .HidePointer()),
            new TutorialStep("FIRST_OPACITY", "Opacity",
                "Drag the <b>Opacity</b> slider right above it to adjust transparency.",
                "100: opaque · 0: invisible")
                .Looks(Face.Talk)
                .At(() => HudEditor.Control("opacity"))
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => PlainSample("{Hour:00}:{Minute:00}:{Second:00}"))
                    .Drag(() => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.97f, 0.5f), 1.2f,
                        t => HudEditor.DemoSetAlpha(sample, 1f - 0.75f * t))
                    .Wait(0.6f)
                    .Drag(() => TutorialDemo.Inside(HudEditor.Control("opacity"), 0.97f, 0.5f), 1f,
                        t => HudEditor.DemoSetAlpha(sample, 0.25f + 0.75f * t))
                    .HidePointer()),
            new TutorialStep("FIRST_FX", "Formulas (ƒ)",
                "Properties can use a JavaScript formula instead of a fixed value—like this pulsing opacity.",
                "In <b>All properties…</b>, click <b>ƒ</b> next to a setting and write JS:\n"
                + "«0.55 + 0.45 * Math.sin(Time() * 4)»  pulsing\n"
                + "«IsKeyHeld('Space') ? 1 : 0.2»  bright while holding Space\n"
                + "«Math.min(1, Fps() / 60)»  dims below 60 FPS")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Normal))
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Hour:00}:{Minute:00}:{Second:00}");
                        HudEditor.DemoSetAlphaFx(sample, "0.55 + 0.45 * Math.sin(Time() * 4)");
                    })
                    .Point(() => HudEditor.Control("allprops"))
                    .Wait(0.9f)
                    .HidePointer()),
            new TutorialStep("FIRST_PIN", "Pin to",
                "The 3×3 grid sets which screen corner or edge an object anchors to, keeping its spot across resolutions.",
                "Clicking a dot updates the anchor without moving the object.")
                .Looks(Face.Surprised)
                .At(() => HudEditor.Control("pin") ?? HudEditor.Part("Panel"))
                .OnEnter(() => PlainSample("{Hour:00}:{Minute:00}:{Second:00}"))
                .OnExit(PutSampleAway),
            new TutorialStep("FIRST_DONE", "Finish",
                "Click the check mark on the top bar to close the editor. Changes save automatically.")
                .Looks(Face.Happy)
                .At(() => HudEditor.Control("close"))
                .Until(() => !HudEditor.IsOpen)
                .Only(() => !Tutorial.InCourse),
            new TutorialStep("FIRST_DONE_COURSE", "Basics complete",
                "The check mark closes the editor and saves automatically. Stay here for the next chapters.")
                .Looks(Face.Smile)
                .At(() => HudEditor.Control("close"))
                .Only(() => Tutorial.InCourse)),

        new("format", "Numbers: decimals and formats", "0, #, commas, percent, padding, rounding",
            OpenEditor("FMT_OPEN"),
            new TutorialStep("FMT_RAW", "Unformatted numbers",
                "Without a format, «{Fps}» prints every trailing decimal digit.",
                "Add a colon and format after the tag name to control how it displays.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.Normal))
                .At(() => SampleRect)
                .Shows(Typing("{Fps}")),
            new TutorialStep("FMT_COLON", "Integer format",
                "«0» means one digit here. With no decimal point, it rounds to a whole number.",
                "«{Fps:0}»  =  60  (59.6 rounds to 60)")
                .Looks(Face.Normal)
                .At(() => SampleRect)
                .Shows(Typing("{Fps:0}")),
            new TutorialStep("FMT_DECIMALS", "Decimal places",
                "Each «0» after the dot adds one fixed decimal place, even if it's zero.",
                "«{Fps:0.0}»  =  59.9\n«{Fps:0.00}»  =  59.94\n«{Fps:0.000}»  =  59.940")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("0.0  =  {Fps:0.0}\n0.00  =  {Fps:0.00}\n0.000  =  {Fps:0.000}")),
            new TutorialStep("FMT_HASH", "0 vs #",
                "«#» shows a digit only when needed, dropping trailing zeros.",
                "<b>0</b>: always shown · <b>#</b>: drops trailing zeros\n"
                + "«{1.5:0.00}» = 1.50  |  «{1.5:0.##}» = 1.5  |  «{2:0.##}» = 2")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("1.5 / 0.00  =  {1.5:0.00}\n1.5 / 0.##  =  {1.5:0.##}\n2 / 0.##  =  {2:0.##}")),
            new TutorialStep("FMT_PAD", "Leading zeros",
                "Extra «0»s before the dot pad with leading zeros—useful for clocks.",
                "«{7:000}»  =  007\n«{Hour:00}:{Minute:00}»  =  09:05")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.Smile))
                .At(() => SampleRect)
                .Shows(Showing("7 / 000  =  {7:000}\n{Hour:00}:{Minute:00}:{Second:00}")),
            new TutorialStep("FMT_GROUP", "Thousands",
                "Include a comma in the format to add thousands separators.",
                "«{1234567:#,##0}»  =  1,234,567\n«{1234567.891:#,##0.00}»  =  1,234,567.89")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("#,##0  =  {1234567:#,##0}\n#,##0.00  =  {1234567.891:#,##0.00}")),
            new TutorialStep("FMT_PERCENT", "Percent",
                "«%» multiplies a 0–1 value by 100 and adds the % sign. For 0–100 values, type % outside.",
                "«{0.856:0.0%}»  =  85.6%")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenMicro))
                .At(() => SampleRect)
                .Shows(Showing("0.856 / 0.0%  =  {0.856:0.0%}")),
            new TutorialStep("FMT_LETTERS", "Standard codes",
                "Short letter codes work too:",
                "«F2» = «0.00» · «N0» = «#,##0» · «N2» = «#,##0.00» · «P1» = «0.0%»\n"
                + "«D3» = «000» (integers only; use «000» for decimal tags like FPS)")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.OpenSmallHarf))
                .At(() => SampleRect)
                .Shows(Showing("F2  =  {Fps:F2}\nN2  =  {1234567.891:N2}\nP1  =  {0.856:P1}")),
            new TutorialStep("FMT_ROUND", "Rounding vs truncating",
                "Formats round half-up. Use «Math.floor» inside braces to truncate instead.",
                "«{Math.floor(Fps * 10) / 10:0.0}»  (59.99 → 59.9)")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("0.0  =  {Fps:0.0}\nfloor  =  {Math.floor(Fps * 10) / 10:0.0}")),
            new TutorialStep("FMT_UNITS", "Units go outside",
                "Write units and labels <b>outside</b> the braces so they aren't treated as format codes.",
                "«{Fps:0} FPS»  (text tags like DayOfWeek ignore formats)")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("{Fps:0} FPS")),
            new TutorialStep("FMT_TRY", "Your turn",
                "Double-click the sample and make it show FPS with <b>one</b> decimal place.",
                "Hint: «{Fps:0.0}»")
                .Looks(new(Eye.Normal, Brow.Curious, Mouth.Smile))
                .At(() => SampleRect)
                .OnEnter(() => Sample("{Fps}"))
                .Until(() => !O5InputBlocker.IsEditing
                    && SampleText() is { } s && s.Contains("{Fps:0.0}") && !s.Contains("{Fps:0.00")),
            new TutorialStep("FMT_DONE", "Formats done",
                "Perfect! That covers number formatting.")
                .Looks(Face.Happy)
                .OnExit(PutSampleAway)),

        new("editor", "HUD editor tools", "Selection, canvas list, toolbars, guides, auto layout, shortcuts",
            OpenEditor("EDITOR_OPEN"),
            new TutorialStep("EDITOR_SELECT", "Selecting",
                "Selection works from groups down to inner items:",
                "<b>Click</b>: whole group\n"
                + "<b>Double-click</b>: enter one level into a group\n"
                + "<b>Ctrl + click</b>: innermost object directly\n"
                + "<b>Shift + click</b>: add/remove from selection\n"
                + "<b>Drag empty space</b>: box select\n"
                + "<b>Right-click</b>: context menu & objects under cursor")
                .Looks(Face.Grin),
            new TutorialStep("EDITOR_CANVASES", "The canvas list",
                "A <b>canvas</b> is a layer of HUD objects, shown here as a tree.",
                "Click: select · Double-click: rename · Drag: reorder or nest\n"
                + "Dot: show / hide · Square: lock\n"
                + "<b>+</b>: new canvas · X: delete (Ctrl+Z undoes)")
                .Looks(Face.Talk)
                .At(() => HudEditor.Part("Layers")),
            new TutorialStep("EDITOR_TOP", "The top bar",
                "Hover any button on the top bar to see its name.",
                "Canvas list · text · image · presets · <b>placement mode</b> · grid · rulers · duplicate · delete · undo · redo · done")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.SadSmall))
                .At(() => HudEditor.Part("ToolbarArea/Toolbar")),
            new TutorialStep("EDITOR_BOTTOM", "The bottom bar",
                "The bottom bar aligns, distributes, groups, orders, locks, and hides.",
                "<b>Align</b>: to selection, parent, or screen\n"
                + "<b>Distribute</b>: even gaps (3+ objects)\n"
                + "<b>Group / Ungroup</b>, <b>Front / Back</b>, <b>Lock</b>, <b>Hide</b>")
                .Looks(Face.Talk)
                .At(() => HudEditor.Part("BottomBarArea/BottomBar")),
            new TutorialStep("EDITOR_GRID", "Grid, rulers and guides",
                "Turn on the grid to snap objects. With rulers on, drag out from a ruler to place a guide line.",
                "Drag a guide back onto the ruler to delete it. Guides are stored per aspect ratio.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))
                .At(() => HudEditor.Control("rulers")),
            new TutorialStep("EDITOR_LAYOUT", "Auto layout",
                "Select multiple objects and click <b>Group as row</b> in the right panel to line them up automatically.",
                "Or select a group and set <b>Auto layout</b> to Row or Column. Gap and padding are adjustable in the panel.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.Clenched))
                .At(() => HudEditor.PartNamed("Dock_layout") ?? SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PutSampleAway();
                        var a = HudEditor.DemoAddText(SamplePoint + new Vector2(-120f, 40f), "{Hour:00}");
                        var b = HudEditor.DemoAddText(SamplePoint + new Vector2(10f, -50f), ":");
                        var c = HudEditor.DemoAddText(SamplePoint + new Vector2(130f, 20f), "{Minute:00}");
                        demoRow = [a, b, c];
                    })
                    .Wait(1f)
                    .Do(() => HudEditor.DemoSelectAll(demoRow.Where(o => o != null)))
                    .Wait(0.5f)
                    .Point(() => HudEditor.Control("group_row"))
                    .Click(() => sample = HudEditor.DemoGroupAsRow(demoRow.Where(o => o != null)))
                    .Wait(0.8f)
                    .HidePointer())
                .OnExit(PutSampleAway),
            new TutorialStep("EDITOR_TEAR", "Detachable panels",
                "Drag any section header in the right panel (like <b>Look</b>) out of the panel to pop it into its own window.",
                "Drag it back over the panel to dock it again.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.OpenSmall))
                .At(() => HudEditor.PartNamed("Dock_look") ?? HudEditor.Part("Panel"))
                .OnEnter(() => PlainSample("{Fps:0} FPS"))
                .OnExit(PutSampleAway),
            new TutorialStep("EDITOR_KEYS", "Shortcuts",
                "Useful keyboard shortcuts in the HUD editor:",
                "Ctrl+C / Ctrl+V copy, paste (Ctrl+Shift+V: in place)\n"
                + "Ctrl+Alt+C / Ctrl+Alt+V copy, paste <b>look</b> only\n"
                + "Ctrl+D duplicate · Del delete · Ctrl+G / Ctrl+Shift+G group, ungroup\n"
                + "Ctrl+] / Ctrl+[ order · Ctrl+Shift+L lock · Ctrl+Shift+H hide · Ctrl+Z / Ctrl+Y undo, redo")
                .Looks(Face.Smile)),

        new("presets", "Presets and the preview", "Ready-made presets and live preview",
            OpenEditor("PRESETS_OPEN"),
            new TutorialStep("PRESETS_MENU", "Presets",
                "Click the star button to insert ready-made presets.")
                .Looks(Face.Surprised)
                .At(() => HudEditor.Control("presets"))
                .Until(() => HudEditor.PresetMenuOpen)
                .Only(() => !HudEditor.PresetMenuOpen),
            new TutorialStep("PRESETS_PICK", "Insert a preset",
                "Pick any preset from the list.",
                "Presets are added as normal objects, so you can customize anything about them.")
                .Looks(Face.Talk)
                .OnEnter(() => objectsAtStart = CountObjects())
                .Until(() => CountObjects() > objectsAtStart),
            new TutorialStep("PRESETS_PREVIEW", "Preview window",
                "<b>Right-click</b> any text and choose <b>Preview in game</b> to test it in a dedicated preview window.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .Until(() => HudPreview.IsOpen),
            new TutorialStep("PRESETS_PREVIEW_WINDOW", "The preview",
                "Edits update live in this window.",
                "Drag the title bar to move it, or resize from the bottom-right corner.")
                .Looks(Face.Smile)
                .Only(() => HudEditor.IsOpen)),

        new("tags", "Tags: the useful tricks", "Arguments, math, styling, colors, animated numbers",
            OpenEditor("TAGS_OPEN"),
            new TutorialStep("TAGS_ARGS", "Tag arguments",
                "Some tags accept options after a colon or in parentheses—for example, averaging FPS over a time window.",
                "«{Fps:500,0}» = 500 ms average, 0 decimals\n«{Fps(500,0)}» = same\n"
                + "Note: «{Fps:500}» reads 500 as a <b>format</b>. Include decimals too.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("{Fps:500,0}")),
            new TutorialStep("TAGS_MATH", "Math & conditions",
                "You can write math and «? :» conditions directly inside braces.",
                "«{Fps * 2:0}»\n«{Fps < 30 ? 'slow' : 'smooth'}»\n"
                + "Tags returning true/false or text need «()» inside math: «IsKeyHeld('Space')», «DayOfWeek()».")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("{Fps * 2:0}\n{Fps < 30 ? 'slow' : 'smooth'}")),
            new TutorialStep("TAGS_RICH", "Color, size, bold",
                "Texts support rich text tags for color, size, and style.",
                "«<color=#FF5050>red</color>»  «<b>bold</b>»  «<i>italic</i>»\n«{Fps:0}<size=50%> FPS</size>»")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .Shows(Showing("<color=#FF5050>red</color> <b>bold</b> <i>italic</i>\n{Fps:0}<size=50%> FPS</size>")),
            new TutorialStep("TAGS_COLORRANGE", "Color by value",
                "«ColorRange» interpolates a hex color from a value—here red at 0 s to green at 59 s.",
                "«<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second}</color>»\n"
                + "Arguments: tag, min, max, start color, end color.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second:00}</color>")),
            new TutorialStep("TAGS_ANIM", "Animated numbers",
                "Numbers can smoothly ease or pop when their value changes:",
                "«{EasedValue(Second,2,500,OutBack)}»: eases over 500 ms\n"
                + "«<size={MovingMan(Second,60,36,36,300)}>{Second}</size>»: pulses size on change\n"
                + "Use a preset name or a custom curve from the <b>Easing</b> page.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.OpenMicro))
                .At(() => SampleRect)
                .Shows(Showing("{EasedValue(Second,2,500,OutBack)}\n<size={MovingMan(Second,60,36,36,300)}>{Second}</size>")),
            new TutorialStep("TAGS_TRY", "Your turn",
                "Set the sample to show FPS averaged over <b>1 second</b> (1000 ms) with 0 decimals.",
                "Hint: «{Fps:1000,0}» or «{Fps(1000,0)}»")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .OnEnter(() => Sample("{Fps}"))
                .Until(() => !O5InputBlocker.IsEditing
                    && SampleText() is { } s && (s.Contains("{Fps:1000,0}") || s.Contains("{Fps(1000,0)}"))),
            new TutorialStep("TAGS_CORE", "Useful built-in tags",
                "Common built-in tags available everywhere:",
                "«{Hour:00}:{Minute:00}» clock · «{AmPm}» · «{DayOfWeek}»\n"
                + "«{Fps:500,0}» FPS · «{FrameTime:500,0.00}» ms/frame\n"
                + "«{Kps:500,0}» keys/sec · «{IsKeyHeld:Space}» Space held state")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.SadSmall))
                .OnExit(PutSampleAway)),

        new("fx", "ƒ: dynamic properties", "Drive opacity, position, or text with formulas",
            OpenEditor("FX_OPEN"),
            new TutorialStep("FX_WHAT", "The ƒ button",
                "Any property with an <b>ƒ</b> button in <b>All properties</b> can run a JavaScript formula instead of a fixed value.",
                "Every tag works as a function: «Fps()», «Second()». Vectors and colors use «[ ]».")
                .Looks(Face.Talk)
                .At(() => HudEditor.Control("allprops"))
                .OnEnter(() => PlainSample("{Fps:0} FPS")),
            new TutorialStep("FX_POSITION", "Dynamic position",
                "Position takes two numbers «[x, y]»—here adding a sine wave to x.",
                "«[x + Math.sin(Time() * 3) * 40, y]»\nx and y are the base position; «Time()» is elapsed seconds.")
                .Looks(Face.Grin)
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoWobble(sample, true);
                    })
                    .Wait(0.8f)),
            new TutorialStep("FX_TEXT", "Dynamic text",
                "On the Text property itself, the returned string is evaluated with tags included.",
                "«Fps() < 60 ? 'slow... {Fps:0}' : 'smooth! {Fps:0}'»\n"
                + "If a formula fails, the static value is used and the error appears below.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoTextFx(sample, "Fps() < 60 ? 'slow... {Fps:0}' : 'smooth! {Fps:0}'");
                    })
                    .Wait(0.8f)),
            new TutorialStep("FX_EXAMPLES", "Formula examples",
                "Common ƒ patterns:",
                "Show while holding Space: «IsKeyHeld('Space') ? 1 : 0»\n"
                + "Fade below 60 FPS: «Math.min(1, Fps() / 60)»\n"
                + "Color RGBA (0–1): «[1, Fps() / 144, 0, 1]»\n"
                + "Multi-line scripts are supported; the last expression is returned.")
                .Looks(Face.Smile)
                .OnExit(PutSampleAway)),

        new("canvases", "Canvases, sharing and packages", "Managing canvases, packages, export and import",
            OpenWindow("CANVAS_OPEN"),
            GoTo("CANVAS_GOTO", OriginalMenuState.Overlayer, "Overlayer"),
            new TutorialStep("CANVAS_TILES", "Your canvases",
                "Each tile is a canvas. Click one to open the canvas editor, showing its object tree and full inspector.",
                "Toggle: enable/disable · Small buttons: copy, export, delete (click twice, or Shift+click)\n"
                + "<b>…</b>: package metadata, thumbnail, and bundled scripts.")
                .Looks(Face.Talk)
                .At(() => PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid")),
            new TutorialStep("CANVAS_NEW", "New, preset, import",
                "The last tile creates an empty canvas (left), adds a preset (top right), or imports a file (bottom right).")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Normal))
                .At(() => {
                    var grid = PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid");
                    return grid != null && grid.childCount > 0 ? grid.GetChild(grid.childCount - 1) as RectTransform : grid;
                }),
            new TutorialStep("CANVAS_SHARE", "Sharing",
                "<b>Export</b> bundles a canvas with its fonts, images, and scripts into an <b>.o5cp</b> package.",
                "Imported packages stay read-only so updates remain clean. Press <b>Copy</b> to make an editable copy.")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.Smile)),
            new TutorialStep("CANVAS_VS_EDITOR", "Which editor?",
                "Use the HUD editor for visual layout on screen, and the canvas editor for direct property editing.",
                "Both edit the exact same canvases and objects.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.OpenSmall))),

        new("resources", "Fonts and images", "Importing and using custom files",
            OpenWindow("RES_OPEN"),
            GoTo("RES_GOTO", OriginalMenuState.Resources, "Resources"),
            new TutorialStep("RES_ADD", "Adding files",
                "Import custom fonts and images here. Use <b>Browse</b> (or paste a path), enter a name, and click <b>Add</b>.",
                "The top toggle switches between images and fonts.")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.Caret))
                .At(() => PagePart(OriginalMenuState.Resources, "ResourcesRoot")),
            new TutorialStep("RES_USE", "Using resources",
                "Select custom fonts under a text's <b>Font</b> setting, or add an image object and pick it under <b>Sprite</b>.",
                "Use <b>Sprite Editor</b> on an image card to configure 9-slice borders for clean stretching.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))),

        new("js", "Custom tags with JavaScript", "Scripts, RegisterTag, per-text tags",
            OpenWindow("JS_OPEN"),
            GoTo("JS_GOTO", OriginalMenuState.JS, "JS"),
            new TutorialStep("JS_FOLDER", "Script folder",
                "Every .js file in the scripts folder runs at startup. The list shows registered tags and any script errors.",
                "<b>Open Folder</b> opens it · <b>Reload All</b> reruns scripts · <b>Auto Reload</b> watches file changes.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.Shift))
                .At(() => PagePart(OriginalMenuState.JS, "JSRoot")),
            new TutorialStep("JS_REGISTER", "Creating a tag",
                "Registering a global tag takes one line:",
                "«RegisterTag('DoubleFps', () => Fps() * 2);»  →  use «{DoubleFps:0}» anywhere\n"
                + "With arguments: «RegisterTag('Greet', (name) => 'hi ' + name);»  →  «{Greet('you')}»\n"
                + "Check the <b>Example</b> folder for sample scripts.")
                .Looks(Face.Smile),
            new TutorialStep("JS_LOCAL", "Per-text tags",
                "You can also define a tag inline inside a single text without a separate file. It renders nothing itself.",
                "«{RegisterTag('Half', () => Fps() / 2)}FPS/2: {Half:0}»")
                .Looks(Face.Grin)),

        new("easing", "Custom easing", "Draw motion curves for animated numbers",
            OpenWindow("EASE_OPEN"),
            GoTo("EASE_GOTO", OriginalMenuState.Easing, "Easing"),
            new TutorialStep("EASE_GRAPH", "Drawing a curve",
                "Horizontal is time, vertical is progress. Going above the top line creates an overshoot bounce.",
                "<b>Click</b>: add point · <b>Drag</b>: move · <b>Right-click</b> / Del: remove · Shift: snap")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.OpenSmall))
                .At(() => PagePart(OriginalMenuState.Easing, null)),
            new TutorialStep("EASE_USE", "Using a curve",
                "<b>Copy example tag</b> copies a tag with your curve; <b>Preview in game</b> tests it on a sample counter.",
                "«{EasedValue(Second,0,300,curve(0,0;0.4,1.1;1,1))}»\n<b>Save</b> stores curves for reuse.")
                .Looks(Face.Talk)),

        new("settings", "Settings worth knowing", "Scale, tooltips, fonts, HUD editor options",
            OpenWindow("SET_OPEN"),
            GoTo("SET_GOTO", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("SET_OVERLAYER", "Overlayer settings",
                "Adjust <b>UI Scale</b>, tooltips, startup behavior, and system/code fonts here.",
                "Use <b>Find</b> at the top to filter settings by name.")
                .Looks(Face.Talk)
                .At(() => RowOf("OVERLAYER")),
            new TutorialStep("SET_HUD", "HUD Editor settings",
                "Configure the HUD editor shortcut, snapping, hints, and <b>Main Canvas</b> for new objects.",
                "Under Advanced: snap distance, grid size, and opening while playing.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))
                .At(() => RowOf("HUD_EDITOR"))),

        new("outro", "Wrapping up", "Where to find chapters anytime",
            new TutorialStep("OUTRO_PAGE", "Revisit anytime",
                "That covers the core features. Come back to the <b>Tutorial</b> page anytime to replay the full guide or any chapter.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.Smile))
                .OnEnter(() => ToPage(OriginalMenuState.Tutorial))
                .At(() => PagePart(OriginalMenuState.Tutorial, null)),
            new TutorialStep("OUTRO_BYE", "All set!",
                "Have fun customizing your HUD with Overlayer!")
                .Looks(Face.Happy)),
    ];
}
