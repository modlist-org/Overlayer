using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    private static DocChapter BuildEasingAndCurves() => new(
        "easing",
        UIIcon.Ease,
        null,
        Tr("DOCS_EASE_TITLE", "Easing and Custom Curves"),
        Tr("DOCS_EASE_SUBTITLE", "Presets, resolution order, inline curve syntax, and the curve editor"),
        DocSpecialKind.None,
        new DocSection(
            Tr("DOCS_EASE_S1_TITLE", "Built-in Easing Presets"),
            Tr("DOCS_EASE_S1_SUMMARY", "31 ready-made O5Ease curves for tags and components"),
            new DocBadges(
                Tr("DOCS_EASE_S1_BADGES", "O5Ease Presets"),
                true,
                "Linear",
                "InSine", "OutSine", "InOutSine",
                "InQuad", "OutQuad", "InOutQuad",
                "InCubic", "OutCubic", "InOutCubic",
                "InQuart", "OutQuart", "InOutQuart",
                "InQuint", "OutQuint", "InOutQuint",
                "InExpo", "OutExpo", "InOutExpo",
                "InCirc", "OutCirc", "InOutCirc",
                "InBack", "OutBack", "InOutBack",
                "InElastic", "OutElastic", "InOutElastic",
                "InBounce", "OutBounce", "InOutBounce"),
            new DocParagraph(Tr("DOCS_EASE_S1_BODY", "Preset names are case-insensitive and have no aliases, so OutExpo and outexpo both work but abbreviations do not. Hovering over a badge previews its curve. The same names are accepted wherever an ease is expected, including EasedValue, ColorRange, MovingMan, and component Ease fields"))
        ),
        new DocSection(
            Tr("DOCS_EASE_S2_TITLE", "Resolution Order and Silent Fallback"),
            Tr("DOCS_EASE_S2_SUMMARY", "How an ease string is resolved, step by step"),
            new DocParagraph(Tr("DOCS_EASE_S2_BODY", "An ease argument is resolved in the following order. Empty or null text, including the literal word null, means Linear. Next come the 31 built-in O5Ease presets, matched case-insensitively, followed by inline CustomEase curves written as curve, mono, lines, or anim. After that, names saved in Easings.json are matched case-insensitively; a saved entry may itself hold a preset name, and editing the library updates every tag that uses it. Anything else falls back to Linear")),
            new DocCallout(
                DocCalloutKind.Warning,
                Tr("DOCS_EASE_S2_WARN_TITLE", "Unknown names fail silently"),
                Tr("DOCS_EASE_S2_WARN_TEXT", "Unrecognized ease names and malformed curves raise no error and fall back to Linear, so a typo shows up only as motion without easing. If a curve appears to have no effect, check the spelling first"))
        ),
        new DocSection(
            Tr("DOCS_EASE_S3_TITLE", "Custom Curve Formats and Syntax Rules"),
            Tr("DOCS_EASE_S3_SUMMARY", "Four inline curve formats, plus names saved in the curve library"),
            new DocParagraph(Tr("DOCS_EASE_S3_FORMATS", "curve(x,y;...) draws a smooth Catmull-Rom curve through every point. mono(x,y;...) draws a monotone cubic curve that never overshoots its neighboring points. lines(x,y;...) connects the points with straight segments. anim(...) follows the Unity AnimationCurve model, where each key takes one of three forms: x,y for an automatic smooth slope, x,y,slope for one slope shared by both sides, or x,y,in,out for separate incoming and outgoing tangents. A name saved in Easings.json can be used anywhere an inline curve is accepted, and tags pick up library edits immediately")),
            new DocParagraph(Tr("DOCS_EASE_S3_RULES", "Curves require at least 2 points, with x strictly increasing between 0 and 1, and numbers written in invariant culture, meaning dot decimals, with NaN and Infinity rejected. Progress t is clamped to the range 0 to 1, and the curve holds its endpoint values beyond that range. The editor adds further limits: y is clamped between -0.5 and 1.5, adjacent x values stay at least 0.01 apart, slopes are limited to plus or minus 50, and the endpoints are fixed in x and cannot be deleted")),
            new DocCode(
                Tr("DOCS_EASE_S3_CODE_CAPTION", "EasedValue with inline curves"),
                "{EasedValue(Combo,0,300,curve(0,0;0.3,0.8;1,1))}\n{EasedValue(Combo,0,300,mono(0,0;0.5,0.4;1,1))}\n{EasedValue(Combo,0,300,anim(0,0;0.5,0.5,2;1,1))}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true)
        ),
        new DocSection(
            Tr("DOCS_EASE_S4_TITLE", "Curve Editor Interface and Controls"),
            Tr("DOCS_EASE_S4_SUMMARY", "The Easing tab, where curves are drawn, previewed, and copied into tags"),
            new DocParagraph(Tr("DOCS_EASE_S4_MODES", "Basic mode offers a single Smooth toggle: when enabled, a smooth curve passes through every point; when disabled, the points are joined by straight segments. Advanced mode adds No overshoot, which applies mono smoothing so the curve stays within the range of neighboring points; Movable start and end height, which unlocks the y position of the endpoints; and Tangent handles for Anim mode with Auto, Flat, and Join buttons")),
            new DocParagraph(Tr("DOCS_EASE_S4_TOOLS", "The toolbar and library provide the following tools: Tidy points removes redundant points while preserving the shape; Reset restores a straight line; Copy curve copies the curve text; Copy example tag copies a ready-made EasedValue tag that uses the current curve; Save and Load manage Easings.json entries, with Delete requiring a hold to confirm; Start from preset samples a preset into 40 points and then simplifies it; Compare with overlays another preset or saved curve in orange. A looping graph and track preview and a 200-step undo history are also included")),
            new DocTable(
                Tr("DOCS_EASE_S4_TABLE_KEY", "Control"),
                Tr("DOCS_EASE_S4_TABLE_VALUE", "Action"),
                (Tr("DOCS_EASE_S4_R1K", "Click / Drag"), Tr("DOCS_EASE_S4_R1V", "Click an empty area of the graph to add a point, or drag a point to move it")),
                (Tr("DOCS_EASE_S4_R2K", "Right-click / Del"), Tr("DOCS_EASE_S4_R2V", "Removes the point under the cursor or the selected point")),
                (Tr("DOCS_EASE_S4_R3K", "Shift"), Tr("DOCS_EASE_S4_R3V", "Snaps to a 0.05 grid while adding or dragging points")),
                (Tr("DOCS_EASE_S4_R4K", "Alt"), Tr("DOCS_EASE_S4_R4V", "In Anim mode, splits the tangents so only one handle moves while dragging")),
                (Tr("DOCS_EASE_S4_R5K", "Ctrl+Z"), Tr("DOCS_EASE_S4_R5V", "Undoes the last change, up to 200 steps")),
                (Tr("DOCS_EASE_S4_R6K", "Ctrl+Y / Ctrl+Shift+Z"), Tr("DOCS_EASE_S4_R6V", "Redoes the last undone change")))
        )
    );
}
