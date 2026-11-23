using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Input;
using Overlayer.Core;
using Overlayer.HudEdit;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Tutorial.Olly;
using Overlayer.UI;
using Overlayer.UI.Factory;
using Overlayer.UI.Factory.Page;
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
        var settings = PagePart(OriginalMenuState.Settings, null);
        if (settings == null || !settings.gameObject.activeInHierarchy) {
            return null;
        }
        var label = settings.GetComponentsInChildren<TextLocalization>(false).FirstOrDefault(t => t.Key == key);
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

    private static void EnsureEnabled() {
        if (!MainCore.IsModEnabled) {
            MainCore.SetModEnabled(true);
        }
    }

    private static void ToEditor() {
        EnsureEnabled();
        if (!HudEditor.IsOpen) {
            UICore.Close(true);
            HudEditor.Open();
        }
    }

    private static void ToWindow() {
        if (HudEditor.IsOpen) {
            HudEditor.Close(keepWindowOpen: true);
        }
        UICore.Open(true);
        if (UICore.Panel != null && !UICore.Panel.gameObject.activeSelf) {
            UICore.ShowPanelFromHudEditor(immediate: true);
        }
    }

    private static void ToMenu() {
        ToWindow();
        if (!MenuOpen) {
            UICore.OpenMenu();
        }
    }

    private static void ToPage(OriginalMenuState page, bool needEnabled = false, bool closeSubpages = false) {
        if (needEnabled) {
            EnsureEnabled();
        }
        ToWindow();
        MenuFactory.GoTo((int)page);
        if (closeSubpages) {
            PageOverlayer.CloseSubpages();
        }
    }

    // ---- context guards (resilience if the user closes/leaves the UI mid-tutorial) ----

    private static (string Key, string Fallback)? LostEnabled() => !MainCore.IsModEnabled
        ? ("TUTORIAL_LOST_POWER", "Overlayer is turned off. Click <b>Return</b> to turn it back on and continue.")
        : null;

    private static (string Key, string Fallback)? LostEditor() => LostEnabled() ?? (!HudEditor.IsOpen
        ? ("TUTORIAL_LOST_EDITOR", "The HUD editor was closed. Press «{HudKey}» or click <b>Return</b> to continue.")
        : null);

    private static (string Key, string Fallback)? LostWindow() => !WindowOpen
        ? ("TUTORIAL_LOST_WINDOW", "The Overlayer window was closed. Press «{WindowKey}» or click <b>Return</b> to continue.")
        : null;

    private static (string Key, string Fallback)? LostMenu() => LostWindow() ?? (!MenuOpen
        ? ("TUTORIAL_LOST_MENU", "The menu was closed. Open the top-left menu or click <b>Return</b> to continue.")
        : null);

    private static (string Key, string Fallback)? LostPage(OriginalMenuState page, bool needEnabled = false, bool closeSubpages = false)
        => (needEnabled ? LostEnabled() : null) ?? LostWindow() ?? (!On(page) || (closeSubpages && PageOverlayer.IsSubpageOpen)
            ? ("TUTORIAL_LOST_PAGE", "You moved to another screen. Click <b>Return</b> to go back and continue.")
            : null);

    private static TutorialStep NeedEnabled(this TutorialStep s) => s.Needs(LostEnabled, EnsureEnabled);
    private static TutorialStep NeedEditor(this TutorialStep s) => s.Needs(LostEditor, ToEditor);
    private static TutorialStep NeedWindow(this TutorialStep s) => s.Needs(LostWindow, ToWindow);
    private static TutorialStep NeedMenu(this TutorialStep s) => s.Needs(LostMenu, ToMenu);
    private static TutorialStep NeedPage(this TutorialStep s, OriginalMenuState page, bool needEnabled = false, bool closeSubpages = false)
        => s.Needs(() => LostPage(page, needEnabled, closeSubpages), () => ToPage(page, needEnabled, closeSubpages));

    // ---- Olly's sample text ----

    private static OvObject sample;

    // Still on a canvas (a removed one lingers until the end of the frame).
    private static bool Alive(OvObject o) => o?.GameObject != null && AllObjects().Contains(o);

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
        HudEditor.DemoRotationFx(s, null);
        HudEditor.DemoTextColorFx(s, null);
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
        .AutoInCourse(() => ToPage(page))
        .NeedWindow();

    private static TutorialStep OpenWindow() => new TutorialStep("OPEN_WINDOW", "Open Overlayer",
        "Press «{WindowKey}» to open the Overlayer window.")
        .Looks(Face.Talk)
        .Until(() => WindowOpen)
        .Only(() => !WindowOpen)
        .AutoInCourse(ToWindow);

    private static TutorialStep OpenEditor() => new TutorialStep("OPEN_EDITOR", "Open the HUD editor",
        "Press «{HudKey}» to open the HUD editor.")
        .Looks(Face.Talk)
        .Until(() => HudEditor.IsOpen)
        .Only(() => !HudEditor.IsOpen)
        .AutoInCourse(ToEditor)
        .NeedEnabled();

    private static int textsAtStart, objectsAtStart;
    private static OvObject moved;
    private static OvObject[] demoRow = [];
    private static Vector2 movedFrom;
    private static float rightDragScrollDistance;
    private static Action<UIScrollController, float> onRightDragScrollHandler;
    private static bool fallbackRightDragging;
    private static float fallbackLastY;

    public static TutorialChapter Hello => Chapters[0];

    /// <summary>After one chapter on its own: where the rest is.</summary>
    public static readonly TutorialChapter ChapterEnd = new("end", "Chapter done", string.Empty,
        new TutorialStep("END", "Chapter done!",
            "All other chapters are available on the <b>Tutorial</b> page in the menu.")
            .Looks(Face.Happy));

    public static readonly List<TutorialChapter> Chapters = [
        new("hello", "Meet Olly", "What Overlayer is, and how to get started",
            new TutorialStep("HELLO_HI", "Hello!",
                "Hi, I'm Olly. Overlayer places custom texts and images on screen to display live values like FPS, clocks, or scripts.",
                "Build from scratch, use ready-made presets, or import someone else's package.")
                .Looks(Face.Smile),
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
                .At(() => MenuTarget(OriginalMenuState.Tutorial))
                .Only(() => !Tutorial.InCourse)),

        new("basics", "The Overlayer window", "Opening, moving, right-click scrolling, menu and pages",
            OpenWindow(),
            new TutorialStep("BASICS_MOVE", "Move and resize",
                "You can drag the top bar to move the window, or drag any edge to resize it.",
                "Just like the windows you're used to—you can drag it around and close it.\nOf course, you can also double-click the top bar to maximize it.\nIf a bug or a slip leaves it forever out of reach,\nhold the toggle key «{WindowKey}» to restore the window to its default size and position.")
                .Looks(Face.Surprised)
                .At(() => PanelPart("TopBar"))
                .NeedWindow(),
            new TutorialStep("BASICS_MENU", "The menu",
                "Click the top-left logo to open the navigation menu.")
                .Looks(Face.Curious)
                .At(() => PanelPart("TopBar/Logo"))
                .Until(() => MenuOpen)
                .Only(() => !MenuOpen)
                .NeedWindow(),
            new TutorialStep("BASICS_PAGES", "Pages",
                "Here is what each page in the menu does:",
                "Manage canvases and packages in Overlayer, and add fonts and images in Resources. Work with scripts and curves in JS and Easing, and configure language and shortcuts in Settings.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .OnEnter(() => { if (WindowOpen && !MenuOpen) UICore.OpenMenu(); })
                .At(() => MenuOpen ? UICore.MenuContent : PanelPart("TopBar/Logo"))
                .NeedMenu(),
            new TutorialStep("BASICS_POWER", "Power toggle",
                "Turns Overlayer completely on or off. When off, all HUD elements hide and stop updating.")
                .Looks(Face.Talk)
                .OnEnter(() => { if (WindowOpen && !MenuOpen) UICore.OpenMenu(); })
                .At(() => UICore.Menu != null ? UICore.Menu.Find("Power") as RectTransform : null)
                .NeedMenu(),
            new TutorialStep("BASICS_SCROLL", "Right-click scrolling",
                "Almost every scrollable page in Overlayer replaces scrollbars with right-click dragging. Hold <b>right-click</b> on this Tutorial page and drag up or down to try it.",
                "Moving the cursor toward the top or bottom jumps straight to that spot, just like grabbing a scrollbar. Mouse wheel scrolling still works too.")
                .Looks(Face.Curious)
                .OnEnter(() => {
                    ToPage(OriginalMenuState.Tutorial);
                    if (MenuOpen) UICore.CloseMenu();
                    TutorialPage.Scroller?.ScrollTo(0f, false);
                    rightDragScrollDistance = 0f;
                    fallbackRightDragging = false;
                    if (onRightDragScrollHandler != null) {
                        UIScrollController.OnRightDragScrolled -= onRightDragScrollHandler;
                    }
                    onRightDragScrollHandler = (sc, delta) => {
                        if (sc == TutorialPage.Scroller) {
                            rightDragScrollDistance += delta;
                        }
                    };
                    UIScrollController.OnRightDragScrolled += onRightDragScrollHandler;
                })
                .OnExit(() => {
                    if (onRightDragScrollHandler != null) {
                        UIScrollController.OnRightDragScrolled -= onRightDragScrollHandler;
                        onRightDragScrollHandler = null;
                    }
                    fallbackRightDragging = false;
                })
                .At(() => PagePart(OriginalMenuState.Tutorial, null))
                .Until(() => {
                    if (rightDragScrollDistance >= 60f) {
                        return true;
                    }
                    var sc = TutorialPage.Scroller;
                    if (sc != null && sc.MaxOffset <= 1f) {
                        bool down = O5Input.GetMouseButton(1);
                        if (down) {
                            Vector2 pos = O5Input.MousePosition;
                            bool over = sc.viewport == null || RectTransformUtility.RectangleContainsScreenPoint(sc.viewport, pos, null);
                            if (!fallbackRightDragging) {
                                if (over) {
                                    fallbackRightDragging = true;
                                    fallbackLastY = pos.y;
                                }
                            } else {
                                rightDragScrollDistance += Mathf.Abs(pos.y - fallbackLastY);
                                fallbackLastY = pos.y;
                            }
                        } else {
                            fallbackRightDragging = false;
                        }
                    }
                    return rightDragScrollDistance >= 60f;
                })
                .NeedPage(OriginalMenuState.Tutorial)),

        new("first", "Your first HUD text", "Create, tag, move, and style a HUD text",
            new TutorialStep("FIRST_OPEN", "Open the HUD editor",
                "Press «{HudKey}» to open the HUD editor directly on the game screen.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenMicro))
                .Until(() => HudEditor.IsOpen)
                .Only(() => !HudEditor.IsOpen)
                .NeedEnabled(),
            new TutorialStep("FIRST_SCREEN", "HUD editor layout",
                "You edit directly on the game screen, so what you see is what you get.",
                "The list is on the left, and the properties panel is on the right. Tool buttons sit along the top and bottom bars.")
                .Looks(Face.Smile)
                .OnEnter(HudEditor.DemoShowLayers)
                .NeedEditor(),
            new TutorialStep("FIRST_DEMO", "Watch an example",
                "You can click <b>A</b> to add a text, double-click it, and enter a tag.",
                "Names inside «{ }» are tags replaced with live values. For example, «{Fps:0}» displays FPS with no decimals.")
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
                .OnExit(PutSampleAway)
                .NeedEditor(),
            new TutorialStep("FIRST_ADD", "Add a text",
                "Your turn. Click the <b>A</b> button on the top bar (or right-click the screen).")
                .Looks(Face.Smile)
                .At(() => HudEditor.Control("add_text"))
                .OnEnter(() => textsAtStart = CountTexts())
                .Until(() => CountTexts() > textsAtStart)
                .NeedEditor(),
            new TutorialStep("FIRST_TYPE", "Enter a live tag",
                "<b>Double-click</b> the text, type «FPS {Fps:0}», then click outside to finish.",
                "You can also enter the same text in the inspector panel's Text box, which is handy for longer expressions. Typing «{» opens the tag list, and tag names are case-sensitive.")
                .Looks(Face.Talk)
                .Until(() => !O5InputBlocker.IsEditing
                    && UserObjects().Any(o => o.Config?.TextEngineConfig?.Text.StaticValue?.Contains("{Fps") == true))
                .Keeping(() => { if (CountTexts() == 0) Tutorial.Jump("FIRST_ADD"); })
                .NeedEditor(),
            new TutorialStep("FIRST_MOVE", "Move it",
                "Nice! Now drag the text somewhere else. Pink lines show alignment with the screen or other objects.")
                .Looks(Face.Happy)
                .OnEnter(() => {
                    moved = Alive(HudEditor.SelectedObject) ? HudEditor.SelectedObject : UserObjects().LastOrDefault();
                    movedFrom = moved?.RectTransform != null ? (Vector2)moved.RectTransform.position : Vector2.zero;
                })
                .Until(() => {
                    if (!Alive(moved) || (HudEditor.SelectedObject != null && !ReferenceEquals(moved, HudEditor.SelectedObject) && Alive(HudEditor.SelectedObject))) {
                        moved = Alive(HudEditor.SelectedObject) ? HudEditor.SelectedObject : UserObjects().LastOrDefault();
                        if (moved?.RectTransform != null) {
                            movedFrom = moved.RectTransform.position;
                        }
                        return false;
                    }
                    return !Input.GetMouseButton(0)
                        && moved?.RectTransform != null
                        && ((Vector2)moved.RectTransform.position - movedFrom).sqrMagnitude > 400f;
                })
                .Keeping(() => { if (CountObjects() == 0) Tutorial.Jump("FIRST_ADD"); })
                .NeedEditor(),
            new TutorialStep("FIRST_HANDLES", "Selection handles",
                "You can resize and rotate with the handles around the selected object:",
                "Drag corners to scale, and edge bars to change width or height. Hold Alt to scale from the center, and use the top circle to rotate. Use arrow keys to nudge, or hold Alt while dragging to temporarily disable snapping and measure distances.")
                .Looks(Face.Normal)
                .NeedEditor(),
            new TutorialStep("FIRST_PANEL", "Inspector panel",
                "The inspector panel holds position, size, rotation, opacity, text, font size, alignment, and color.",
                "It moves to the opposite side if it covers your selection, and you can also flip sides with «← / →» at the top. Click All properties at the bottom to open every setting.")
                .Looks(Face.Talk)
                .OnEnter(() => {
                    if (HudEditor.SelectedObject == null) {
                        var existing = UserObjects().LastOrDefault(o => o.GameObject != null && o.GameObject.activeInHierarchy);
                        if (existing != null) HudEditor.DemoSelect(existing);
                        if (HudEditor.SelectedObject == null) PlainSample("FPS {Fps:0}");
                    }
                })
                .At(() => HudEditor.Part("Panel"))
                .OnExit(() => { if (Alive(sample)) PutSampleAway(); })
                .NeedEditor(),
            new TutorialStep("FIRST_PIN", "Pin to",
                "The 3×3 grid sets which screen corner or edge an object anchors to, keeping its spot across resolutions.",
                "Changing the anchor keeps the object's current position on screen.")
                .Looks(Face.Surprised)
                .At(() => HudEditor.PartNamed("PinTo") ?? HudEditor.Part("Panel"))
                .OnEnter(() => PlainSample("{Hour:00}:{Minute:00}:{Second:00}"))
                .OnExit(PutSampleAway)
                .NeedEditor(),
            new TutorialStep("FIRST_DONE", "Finish",
                "Click the check mark on the top bar to close the editor. Changes save automatically.")
                .Looks(Face.Happy)
                .At(() => HudEditor.Control("close"))
                .Until(() => !HudEditor.IsOpen)
                .Only(() => !Tutorial.InCourse),
            new TutorialStep("FIRST_DONE_COURSE", "Basics complete",
                "The check mark closes the editor and saves automatically. We'll stay here for the next chapters.")
                .Looks(Face.Smile)
                .At(() => HudEditor.Control("close"))
                .Only(() => Tutorial.InCourse)),

        new("format", "Numbers: decimals and formats", "0, #, commas, percent, padding, rounding",
            OpenEditor(),
            new TutorialStep("FMT_RAW", "Unformatted numbers",
                "Without a format, «{Fps}» prints every trailing decimal digit.",
                "A colon and format can be added after the tag name to control how it displays.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.Normal))
                .At(() => SampleRect)
                .Shows(Typing("{Fps}"))
                .NeedEditor(),
            new TutorialStep("FMT_DECIMALS", "Whole numbers & decimals",
                "«0» stands for one digit here: with no dot it rounds to a whole number, and each «0» after the dot fixes one decimal place.",
                "«{Fps:0}» displays 60, «{Fps:0.0}» displays 59.9, and «{Fps:0.00}» displays 59.94. Trailing places are always padded with 0.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("0  =  {Fps:0}\n0.0  =  {Fps:0.0}\n0.00  =  {Fps:0.00}"))
                .NeedEditor(),
            new TutorialStep("FMT_HASH", "0 vs #",
                "«#» shows a digit only when needed, dropping trailing zeros.",
                "0 always fills empty places, while # drops unnecessary trailing zeros. «{1.5:0.00}» gives 1.50, and «{1.5:0.##}» gives 1.5.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("1.5 / 0.00  =  {1.5:0.00}\n1.5 / 0.##  =  {1.5:0.##}\n2 / 0.##  =  {2:0.##}"))
                .NeedEditor(),
            new TutorialStep("FMT_PAD", "Leading zeros",
                "Extra «0»s before the dot pad with leading zeros—useful for clocks.",
                "«{7:000}» displays as 007, and «{Hour:00}:{Minute:00}» displays like 09:05.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.Smile))
                .At(() => SampleRect)
                .Shows(Showing("7 / 000  =  {7:000}\n{Hour:00}:{Minute:00}:{Second:00}"))
                .NeedEditor(),
            new TutorialStep("FMT_GROUP", "Thousands & percent",
                "A comma adds thousands separators, while «%» multiplies a 0–1 value by 100 and appends %.",
                "«{1234567:#,##0}» displays 1,234,567, and «{0.856:0.0%}» displays 85.6%. For values already in 0–100, put % outside the braces.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("#,##0  =  {1234567:#,##0}\n0.0%  =  {0.856:0.0%}"))
                .NeedEditor(),
            new TutorialStep("FMT_ROUND", "Rounding vs truncating",
                "Formats round half-up. «Math.floor» can be used inside braces to truncate instead.",
                "Writing «{Math.floor(Fps * 10) / 10:0.0}» floors 59.99 down to 59.9.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("0.0  =  {Fps:0.0}\nfloor  =  {Math.floor(Fps * 10) / 10:0.0}"))
                .NeedEditor(),
            new TutorialStep("FMT_UNITS", "Units go outside",
                "Units and labels should go <b>outside</b> the braces so they aren't treated as format codes.",
                "Put units outside the braces, like «{Fps:0} FPS». Text tags such as DayOfWeek do not take numeric formats.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("{Fps:0} FPS"))
                .NeedEditor(),
            new TutorialStep("FMT_TRY", "Your turn",
                "Double-click the sample and make it show FPS with <b>one</b> decimal place.",
                "Entering «{Fps:0.0}» displays one decimal place.")
                .Looks(new(Eye.Normal, Brow.Curious, Mouth.Smile))
                .At(() => SampleRect)
                .OnEnter(() => Sample("{Fps}"))
                .Until(() => !O5InputBlocker.IsEditing
                    && SampleText()?.Replace(" ", "") is { } s && s.Contains("{Fps:0.0}") && !s.Contains("{Fps:0.00"))
                .Keeping(() => { if (!O5InputBlocker.IsEditing && !Alive(sample)) Sample("{Fps}"); })
                .OnExit(PutSampleAway)
                .NeedEditor()),

        new("editor", "HUD editor tools", "Selection, canvas list, toolbars, guides, auto layout",
            OpenEditor(),
            new TutorialStep("EDITOR_SELECT", "Selecting",
                "Selection works from groups down to inner items:",
                "Click to select a group and double-click to step inside. Ctrl+click picks an inner element directly, Shift+click toggles multi-selection, and dragging empty space box-selects.")
                .Looks(Face.Grin)
                .NeedEditor(),
            new TutorialStep("EDITOR_CANVASES", "The canvas list",
                "A <b>canvas</b> is a layer of HUD objects, shown here as a tree.",
                "Click to select and double-click to rename. Drag to reorder or nest, and use the right-side icons to toggle visibility and lock. Click + to create a new canvas and X to delete.")
                .Looks(Face.Talk)
                .OnEnter(HudEditor.DemoShowLayers)
                .At(() => HudEditor.Part("Layers") ?? HudEditor.Control("layers"))
                .NeedEditor(),
            new TutorialStep("EDITOR_TOP", "The top bar",
                "The top bar creates objects, toggles helpers, and manages undo and deletion.",
                "Choose where new objects are added with Placement. Hold the delete button, or Shift+click it to delete right away.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.SadSmall))
                .At(() => HudEditor.Part("ToolbarArea/Toolbar"))
                .NeedEditor(),
            new TutorialStep("EDITOR_BOTTOM", "The bottom bar",
                "The bottom bar aligns, distributes, groups, orders, locks, and hides.",
                "Align elements to the selection or screen, and distribute spacing evenly with 3 or more selected. Grouping, order, lock, and hide are here too.")
                .Looks(Face.Talk)
                .At(() => HudEditor.Part("BottomBarArea/BottomBar"))
                .NeedEditor(),
            new TutorialStep("EDITOR_GRID", "Grid, rulers and guides",
                "Turning on the grid snaps objects. With rulers on, guide lines can be dragged out from a ruler.",
                "Guides can be deleted by dragging them back onto the ruler. They are saved as a fraction of the screen so they keep their spot across resolutions.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))
                .At(() => HudEditor.Control("rulers"))
                .NeedEditor(),
            new TutorialStep("EDITOR_LAYOUT", "Auto layout",
                "Multiple objects can be lined up automatically by selecting them and clicking <b>Group as row</b> in the inspector panel.",
                "You can also select a group and set Auto layout to Row or Column, and adjust gap and padding directly in the inspector panel.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.Clenched))
                .At(() => HudEditor.PartNamed("Dock_layout") ?? HudEditor.Control("group_row") ?? SampleRect)
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
                .OnExit(PutSampleAway)
                .NeedEditor(),
            new TutorialStep("EDITOR_TEAR", "Detachable panels",
                "Any section header in the inspector panel (like <b>Look</b>) can be dragged out into its own window.",
                "Dragging it back over the panel docks it again.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.OpenSmall))
                .At(() => HudEditor.PartNamed("Dock_look") ?? HudEditor.Part("Panel"))
                .OnEnter(() => PlainSample("{Fps:0} FPS"))
                .OnExit(PutSampleAway)
                .NeedEditor()),

        new("presets", "Presets", "Using built-in presets",
            OpenEditor(),
            new TutorialStep("PRESETS_MENU", "Presets",
                "Click the star button on the top bar to open ready-made presets.")
                .Looks(Face.Surprised)
                .At(() => HudEditor.Control("presets"))
                .Until(() => HudEditor.PresetMenuOpen)
                .Only(() => !HudEditor.PresetMenuOpen)
                .NeedEditor(),
            new TutorialStep("PRESETS_PICK", "Insert a preset",
                "Pick any preset from the list.",
                "Presets are added as normal objects, so you can customize anything about them.")
                .Looks(Face.Talk)
                .OnEnter(() => objectsAtStart = CountObjects())
                .Until(() => CountObjects() > objectsAtStart)
                .Keeping(() => { if (!HudEditor.PresetMenuOpen) Tutorial.Jump("PRESETS_MENU"); })
                .NeedEditor()),

        new("tags", "Tags: the useful tricks", "Arguments, math, styling, colors, animated numbers",
            OpenEditor(),
            new TutorialStep("TAGS_ARGS", "Tag arguments",
                "Some tags accept options after a colon or in parentheses—for example, averaging FPS over a time window.",
                "Both «{Fps:500,0}» and «{Fps(500,0)}» show the 500 ms average as a whole number. Writing just «{Fps:500}» treats 500 as a format, so include the decimal format too.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("{Fps:500,0}"))
                .NeedEditor(),
            new TutorialStep("TAGS_MATH", "Math & conditions",
                "You can write math and ternary operators directly inside braces.",
                "Write expressions like «{Fps * 2:0}» or «{Fps < 30 ? 'slow' : 'smooth'}». Tags that return a boolean or string take parentheses, like «IsKeyHeld('Space')» or «DayOfWeek()».")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(Showing("{Fps * 2:0}\n{Fps < 30 ? 'slow' : 'smooth'}"))
                .NeedEditor(),
            new TutorialStep("TAGS_RICH", "TextMeshPro",
                "Text objects render with Unity's <b>TextMeshPro</b>. When <b>Rich Text</b> is enabled in the text properties, inline markup tags can style color, size, weight, and more.",
                "Mix formatting right into your text, such as «<color=#FF5050>red</color>», «<i>italic</i>», or «{Fps:0}<size=50%> FPS</size>».")
                .WithLink("Unity TextMeshPro Rich Text Reference", "https://docs.unity3d.com/Packages/com.unity.ugui@7.0/manual/TextMeshPro/RichText.html")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .Shows(Showing("<color=#FF5050>red</color> <b>bold</b> <i>italic</i>\n{Fps:0}<size=50%> FPS</size>"))
                .NeedEditor(),
            new TutorialStep("TAGS_COLORRANGE", "Color by value",
                "«ColorRange» interpolates a hex color from a value—here red at 0 s to green at 59 s.",
                "Use «<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second}</color>», with arguments in order: tag, min, max, start color, end color.")
                .Looks(Face.Talk)
                .At(() => SampleRect)
                .Shows(Showing("<color=#{ColorRange(Second,0,59,FF4040,40FF40)}>{Second:00}</color>"))
                .NeedEditor(),
            new TutorialStep("TAGS_ANIM", "Animated numbers",
                "Numbers can smoothly ease or pop when their value changes:",
                "Transition smoothly with «{EasedValue(Second,2,500,OutBack)}» or pop the size with «<size={MovingMan(Second,60,36,36,300)}>{Second}</size>». You can also use a saved curve name from the Easing tab.")
                .Looks(new(Eye.Small, Brow.NormalHigh, Mouth.OpenMicro))
                .At(() => SampleRect)
                .Shows(Showing("{EasedValue(Second,2,500,OutBack)}\n<size={MovingMan(Second,60,36,36,300)}>{Second}</size>"))
                .NeedEditor(),
            new TutorialStep("TAGS_TRY", "Your turn",
                "Set the sample to show FPS averaged over <b>1 second</b> (1000 ms) with 0 decimals.",
                "Enter «{Fps:1000,0}» or «{Fps(1000,0)}».")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .OnEnter(() => Sample("{Fps}"))
                .Until(() => !O5InputBlocker.IsEditing
                    && SampleText()?.Replace(" ", "") is { } s && (s.Contains("{Fps:1000,0}") || s.Contains("{Fps(1000,0)}")))
                .Keeping(() => { if (!O5InputBlocker.IsEditing && !Alive(sample)) Sample("{Fps}"); })
                .OnExit(PutSampleAway)
                .NeedEditor()),

        new("fx", "ƒ: dynamic properties", "Binding JS formulas to properties",
            OpenEditor(),
            new TutorialStep("FX_WHAT", "The ƒ button",
                "Any property with an <b>ƒ</b> button in <b>All properties</b> can run a JavaScript formula instead of a fixed value—like this pulsing opacity.",
                "Inside a formula, every tag is called as a function: «Fps()», «Second()».")
                .Looks(Face.Talk)
                .OnEnter(() => PlainSample("{Fps:0} FPS"))
                .At(() => HudEditor.FxArea() ?? HudEditor.Control("allprops"))
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoSetAlphaFx(sample, "0.55 + 0.45 * Math.sin(Time() * 4)");
                    })
                    .Point(() => HudEditor.FxArea() ?? HudEditor.Control("allprops"))
                    .Wait(0.9f)
                    .HidePointer())
                .NeedEditor(),
            new TutorialStep("FX_POSITION", "Dynamic position",
                "Position takes two numbers «[x, y]»—here adding a sine wave to x.",
                "Write «[x + Math.sin(Time() * 3) * 40, y]» with the base position's numbers in place of x and y. «Time()» is elapsed seconds.")
                .Looks(Face.Grin)
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoWobble(sample, true);
                    })
                    .Wait(0.8f))
                .NeedEditor(),
            new TutorialStep("FX_MORE", "Number or array",
                "What a formula returns depends on the property. Single values like rotation take one number, and values with several parts take an «[ ]» array—like this color.",
                "Rotation is in degrees, like «Math.sin(Time() * 2) * 15». Colors are «[r, g, b, a]» from 0 to 1, position and size «[x, y]», scale «[x, y, z]». If the result doesn't fit, the plain value is used.")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Shift))
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoRotationFx(sample, "Math.sin(Time() * 2) * 15");
                        HudEditor.DemoTextColorFx(sample, "[1, 0.6 + 0.4 * Math.sin(Time() * 3), 0.3, 1]");
                    })
                    .Wait(0.8f))
                .NeedEditor(),
            new TutorialStep("FX_EXAMPLES", "Switches and text",
                "Formulas can react to what you do, too. Hold <b>Space</b>—this opacity is «IsKeyHeld('Space') ? 1 : 0.3».",
                "On/off switches take «true» or «false», and Text takes a string, like «Fps() < 60 ? 'slow' : 'smooth'». Multi-line scripts return the result of the last expression.")
                .Looks(Face.Smile)
                .At(() => SampleRect)
                .Shows(new TutorialDemo()
                    .Wait(0.3f)
                    .Do(() => {
                        PlainSample("{Fps:0} FPS");
                        HudEditor.DemoSetAlphaFx(sample, "IsKeyHeld('Space') ? 1 : 0.3");
                    })
                    .Wait(0.8f))
                .OnExit(PutSampleAway)
                .NeedEditor()),

        new("canvases", "Canvases, sharing and packages", "Managing canvases, packages, export and import",
            OpenWindow(),
            GoTo("CANVAS_GOTO", OriginalMenuState.Overlayer, "Overlayer"),
            new TutorialStep("CANVAS_TILES", "Your canvases",
                "Each tile is a canvas. Clicking one opens the canvas editor, showing its object tree and full inspector.",
                "Use the toggle to enable or disable, and the small buttons to copy, export, or delete. Manage package info and scripts under «...»; this edits the same canvases as the HUD editor.")
                .Looks(Face.Talk)
                .OnEnter(PageOverlayer.CloseSubpages)
                .At(() => PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid"))
                .NeedPage(OriginalMenuState.Overlayer, needEnabled: true, closeSubpages: true),
            new TutorialStep("CANVAS_NEW", "New, preset, import",
                "The last tile creates an empty canvas (left), adds a preset (top right), or imports a file (bottom right).")
                .Looks(new(Eye.Normal, Brow.NormalHigh, Mouth.Normal))
                .At(() => {
                    var grid = PagePart(OriginalMenuState.Overlayer, "ScrollView/Viewport/Content/Grid");
                    return grid != null && grid.childCount > 0 ? grid.GetChild(grid.childCount - 1) as RectTransform : grid;
                })
                .NeedPage(OriginalMenuState.Overlayer, needEnabled: true, closeSubpages: true),
            new TutorialStep("CANVAS_SHARE", "Sharing",
                "<b>Export</b> bundles a canvas with its fonts, images, and scripts into an <b>.o5cp</b> package.",
                "Imported packages stay read-only so updates stay intact. Use Copy to make an editable canvas.")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.Smile))
                .NeedPage(OriginalMenuState.Overlayer, needEnabled: true, closeSubpages: true)),

        new("resources", "Fonts and images", "Adding and using custom fonts and images",
            OpenWindow(),
            GoTo("RES_GOTO", OriginalMenuState.Resources, "Resources"),
            new TutorialStep("RES_ADD", "Adding files",
                "Use <b>Browse</b> (or paste a path), set a name, and click <b>Add Image / Font</b> to register your own fonts and images.",
                "The top toggle switches between images and fonts.")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.Caret))
                .At(() => PagePart(OriginalMenuState.Resources, "ResourcesRoot"))
                .NeedPage(OriginalMenuState.Resources),
            new TutorialStep("RES_USE", "Using resources",
                "Custom fonts can be picked under a text's <b>Font</b> setting, and images under an image object's <b>Sprite</b> setting.",
                "Set 9-slice borders in Sprite Editor on an image card so corners stretch without distorting.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))
                .NeedPage(OriginalMenuState.Resources)),

        new("js", "Custom tags with JavaScript", "Script files, RegisterTag, inline tags",
            OpenWindow(),
            GoTo("JS_GOTO", OriginalMenuState.JS, "JS"),
            new TutorialStep("JS_FOLDER", "Script folder",
                "Every .js file in the scripts folder runs at startup. The list shows registered tags and any script errors.",
                "Use Open Folder to open the scripts folder and Reload All to rerun them. Turn on Auto Reload to apply changes automatically when files are edited.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.Shift))
                .At(() => PagePart(OriginalMenuState.JS, "JSRoot"))
                .NeedPage(OriginalMenuState.JS),
            new TutorialStep("JS_REGISTER", "Creating a tag",
                "Registering a global tag takes one line:",
                "Register with «RegisterTag('DoubleFps', () => Fps() * 2);» and use it as «{DoubleFps:0}». You can also check the sample files in the Example folder.")
                .Looks(Face.Smile)
                .NeedPage(OriginalMenuState.JS),
            new TutorialStep("JS_LOCAL", "Per-text tags",
                "You can also define a tag inline inside a single text without a separate file. It renders nothing itself.",
                "Write it right inside the text, like «{RegisterTag('Half', () => Fps() / 2)}FPS/2: {Half:0}».")
                .Looks(Face.Grin)
                .NeedPage(OriginalMenuState.JS)),

        new("easing", "Custom easing", "Draw motion curves for animated numbers",
            OpenWindow(),
            GoTo("EASE_GOTO", OriginalMenuState.Easing, "Easing"),
            new TutorialStep("EASE_GRAPH", "Drawing a curve",
                "Horizontal is time, vertical is progress. Going above the top line creates an overshoot bounce.",
                "Click empty space to add a point and drag to move it. Right-click or press Del to remove, and hold Shift to snap to the grid.")
                .Looks(new(Eye.Small, Brow.Normal, Mouth.OpenSmall))
                .At(() => PagePart(OriginalMenuState.Easing, null))
                .NeedPage(OriginalMenuState.Easing),
            new TutorialStep("EASE_USE", "Using a curve",
                "<b>Copy example tag</b> copies a tag with your curve so you can paste it into any HUD text.",
                "Use it directly like «{EasedValue(Second,0,300,curve(0,0;0.4,1.1;1,1))}». Click Save to store the curve and reference it by name in tags.")
                .Looks(Face.Talk)
                .At(() => UI.Easing.CurveEditor.CopyTagRect)
                .NeedPage(OriginalMenuState.Easing)),

        new("settings", "Settings worth knowing", "Language, scale, tooltips, fonts, HUD editor options",
            OpenWindow(),
            GoTo("SET_GOTO", OriginalMenuState.Settings, "Settings"),
            new TutorialStep("SET_OVERLAYER", "Language & Overlayer settings",
                "<b>Language</b>, <b>UI Scale</b>, tooltips, startup behavior, and system/code fonts can be adjusted here.",
                "Use the Find box at the top to search settings by name.")
                .Looks(Face.Talk)
                .OnEnter(PageSettings.ClearSearch)
                .At(() => PagePart(OriginalMenuState.Settings, null))
                .NeedPage(OriginalMenuState.Settings),
            new TutorialStep("SET_HUD", "HUD Editor settings",
                "The HUD editor shortcut, snapping, snap distance, and grid size can be configured here.",
                "Hold Alt while dragging in the HUD editor to temporarily disable snapping.")
                .Looks(new(Eye.Normal, Brow.Normal, Mouth.OpenSmallHarf))
                .OnEnter(PageSettings.ClearSearch)
                .At(() => PagePart(OriginalMenuState.Settings, null))
                .NeedPage(OriginalMenuState.Settings)),

        new("outro", "Wrapping up", "Replay the tutorial",
            new TutorialStep("OUTRO_PAGE", "All set!",
                "That covers the core features. You can replay any chapter anytime from the <b>Tutorial</b> page—have fun customizing your HUD!")
                .Looks(Face.Happy)
                .OnEnter(() => ToPage(OriginalMenuState.Tutorial))
                .At(() => PagePart(OriginalMenuState.Tutorial, null))
                .NeedPage(OriginalMenuState.Tutorial)),
    ];
}
