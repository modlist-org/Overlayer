using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    private static DocChapter BuildTagsAndText() => new(
        id: "tags",
        icon: UIIcon.Text,
        spriteIcon: null,
        title: Tr("DOCS_TAG_CHAPTER_TITLE", "Text Engine & Tags"),
        subtitle: Tr("DOCS_TAG_CHAPTER_SUBTITLE", "Tag syntax, numeric formatting, built-in tags, inline math, effects, and TMP rich text"),
        special: DocSpecialKind.TagPlayground,
        new DocSection(
            Tr("DOCS_TAG_SYNTAX_TITLE", "Tag Syntax, Escaping & Diagnostics"),
            Tr("DOCS_TAG_SYNTAX_SUMMARY", "Placeholder parsing, escaping, comma handling, format string validation, and error reporting"),
            new DocParagraph(Tr(
                "DOCS_TAG_SYNTAX_P1",
                "Placeholders are enclosed in curly braces. Parameterless tags accept {TagName} or {TagName()}; parameterized tags accept colon-separated {Tag:arg1,arg2} or function-call {Tag(arg1, arg2)} syntax. Inside inline expressions, bare TagName without braces evaluates identically with default arguments"
            )),
            new DocParagraph(Tr(
                "DOCS_TAG_SYNTAX_P2",
                "Tag names are strictly case-sensitive ({Fps} resolves; {fps} does not). Only enum arguments (such as KeyCode in input tags) and easing or curve names are matched case-insensitively. Write {{ to output a literal opening brace (no }} escape exists); any unmatched { without a closing brace also renders literally"
            )),
            new DocTable(
                Tr("DOCS_TAG_SYNTAX_TBL_KEY", "Rule"),
                Tr("DOCS_TAG_SYNTAX_TBL_VAL", "Behavior"),
                ("{Tag:a,b} vs {Tag:#,##0.00}", Tr("DOCS_TAG_SYNTAX_ROW_COMMA", "Comma-separated colon arguments split only when every segment validates as an argument or format. A thousands-grouped format such as {Accuracy:#,##0.00} retains its comma")),
                ("ProcessFormat (4 built-ins)", Tr("DOCS_TAG_SYNTAX_ROW_PROCFORMAT", "Only 4 parameterized built-in tags accept a trailing :format string: Fps, FrameTime, Kps, and EasedValue. FormatValidator rejects :format on string or bool return types (AmPm, DayOfWeek, Is*, ColorRange, SceneName, etc.)")),
                ("ArgTooMany", Tr("DOCS_TAG_SYNTAX_ROW_TOOMANY", "Reported as a warning; the tag compiles and executes with the expected arguments")),
                ("ArgTooFew / ArgConvertFail / FormatFail / TagNotFound", Tr("DOCS_TAG_SYNTAX_ROW_ERRORS", "Compile errors. Unknown tags, compile errors, and C# exceptions render the raw {...} text (with fuzzy typo suggestions in diagnostics)")),
                ("V8 Runtime Error", Tr("DOCS_TAG_SYNTAX_ROW_V8ERR", "Unlike C# exceptions or compile errors, JavaScript runtime errors inside V8 fallback expressions render an empty string (\"\")")),
                ("[ Compiling… ]", Tr("DOCS_TAG_SYNTAX_ROW_COMPILING", "Displayed temporarily as a placeholder while TextEngine compiles a template asynchronously"))
            )
        ),
        new DocSection(
            Tr("DOCS_TAG_FMT_TITLE", "Numeric Formatting Reference"),
            Tr("DOCS_TAG_FMT_SUMMARY", "Standard .NET numeric format specifiers and truncation patterns for numeric tags"),
            new DocTable(
                Tr("DOCS_TAG_FMT_TBL_KEY", "Format"),
                Tr("DOCS_TAG_FMT_TBL_VAL", "Result & Notes"),
                ("{Fps:0}", Tr("DOCS_TAG_FMT_ROW_INT", "Rounds to the nearest integer (0 is a required digit placeholder)")),
                ("{FrameTime:0.00}", Tr("DOCS_TAG_FMT_ROW_DEC", "Displays two decimal places, padding trailing zeros")),
                ("{Kps:0.#}", Tr("DOCS_TAG_FMT_ROW_OPT", "# is an optional digit placeholder; omits trailing zeros")),
                ("{Combo:000}", Tr("DOCS_TAG_FMT_ROW_PAD", "Zero-pads integers to at least 3 digits (e.g., 7 becomes 007)")),
                ("{Score:#,##0}", Tr("DOCS_TAG_FMT_ROW_THOUSANDS", "Inserts locale thousands separators (e.g., 12,345)")),
                ("{Accuracy:0.0%}", Tr("DOCS_TAG_FMT_ROW_PCT", "Multiplies the value by 100 and appends a % sign")),
                ("{floor(Fps * 10) / 10:0.0}", Tr("DOCS_TAG_FMT_ROW_TRUNC", "Standard format strings apply rounding; combine floor() with division to truncate"))
            ),
            new DocCode(
                Tr("DOCS_TAG_FMT_CODE_CAP", "Formatting & Escaping Examples"),
                "FPS: {Fps:500,0} | Frame: {FrameTime:500,0.00} ms\nKPS: {Kps:0.00} | Truncated: {floor(Fps * 10) / 10:0.0}\nLiteral braces: {{Fps}} -> {Fps:0}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            )
        ),
        new DocSection(
            Tr("DOCS_TAG_CORE_TITLE", "Built-in Core Tags Reference"),
            Tr("DOCS_TAG_CORE_SUMMARY", "Built-in tags provided by Overlayer core across Time, Performance, KPS, Input, and Unity"),
            new DocTable(
                Tr("DOCS_TAG_CORE_TBL_KEY", "Tag"),
                Tr("DOCS_TAG_CORE_TBL_VAL", "Description"),
                ("Year / Month / Day / DayOfYear / DayOfWeek", Tr("DOCS_TAG_CORE_TIME_DATE", "Time: 4-digit year, month (1-12), day of month (1-31), day of year (1-366), and weekday name (string)")),
                ("Hour / Hour12 / AmPm / IsAm / IsPm", Tr("DOCS_TAG_CORE_TIME_HOUR", "Time: 24h hour (0-23), 12h hour (1-12), 'AM'/'PM' string, and boolean AM/PM flags")),
                ("Minute / Second / Millisecond / Ticks / UtcTicks", Tr("DOCS_TAG_CORE_TIME_FINE", "Time: minute (0-59), second (0-59), millisecond (0-999), local ticks, and UTC ticks (100-ns intervals)")),
                ("Fps(windowMs = 0)", Tr("DOCS_TAG_CORE_PERF_FPS", "Performance [ProcessFormat]: unscaled FPS. 0 = instantaneous (1 / unscaledDeltaTime); >0 = windowed average up to 10,000 ms")),
                ("FrameTime(windowMs = 0)", Tr("DOCS_TAG_CORE_PERF_FT", "Performance [ProcessFormat]: unscaled frame time in milliseconds (0 = instantaneous; >0 = windowed up to 10,000 ms)")),
                ("ProcessorCount", Tr("DOCS_TAG_CORE_PERF_CPU", "Performance: logical processor core count (int)")),
                ("Kps(windowMs = 0)", Tr("DOCS_TAG_CORE_KPS", "Kps [ProcessFormat]: keys per second. 0 = instantaneous decay-sum; >0 = cached per window up to 10,000 ms. Uses RawInput when available; otherwise uses Unity key-down polling")),
                ("IsKeyHeld(key) / IsKeyDown(key) / IsKeyUp(key)", Tr("DOCS_TAG_CORE_INPUT_KEY", "Input: keyboard state for a Unity KeyCode (case-insensitive, e.g., {IsKeyHeld:Space})")),
                ("IsMouseHeld / IsMouseDown / IsMouseUp(button = 0)", Tr("DOCS_TAG_CORE_INPUT_MOUSE", "Input: mouse button state (0 = Left, 1 = Right, 2 = Middle)")),
                ("IsAnyMouseHeld / IsAnyMouseDown / IsAnyMouseUp", Tr("DOCS_TAG_CORE_INPUT_ANYMOUSE", "Input: true if any of Left/Right/Middle mouse buttons is held, pressed, or released")),
                ("IsAnyKeyHeld / IsAnyKeyDown", Tr("DOCS_TAG_CORE_INPUT_ANYKEY", "Input: true while any key/mouse button is held, or on the frame any was pressed")),
                ("MouseX / MouseY / MouseScrollX / MouseScrollY / MouseDeltaX / MouseDeltaY", Tr("DOCS_TAG_CORE_INPUT_POS", "Input: screen-space mouse coordinates, scroll wheel delta, and per-frame movement delta")),
                ("SceneName / HudEditorOpen / IsFocused", Tr("DOCS_TAG_CORE_UNITY_STATE", "Unity/Overlayer: active scene name, HUD Editor open state, and game window focus state")),
                ("ScreenWidth / ScreenHeight / RefreshRate / IsFullScreen / ScreenDpi", Tr("DOCS_TAG_CORE_UNITY_SCREEN", "Unity: screen dimensions in pixels, target refresh rate, fullscreen flag, and screen DPI")),
                ("TimeScale / TimeSinceStart / Time / FixedTime / DeltaTime / UnscaledDeltaTime / FixedDeltaTime / MaximumDeltaTime / FrameCount", Tr("DOCS_TAG_CORE_UNITY_TIME", "Unity: time scale, unscaled seconds since start (TimeSinceStart), scaled/fixed time, frame deltas, and total frame count")),
                ("IsCursorVisible / CursorLockState", Tr("DOCS_TAG_CORE_UNITY_CURSOR", "Unity: cursor visibility (bool) and lock mode ('None', 'Locked', 'Confined')")),
                ("QualityLevel / QualityName / VSyncCount / TargetFrameRate / MaxTextureSize", Tr("DOCS_TAG_CORE_UNITY_GFX", "Unity: quality index/name, VSync count (0/1/2), target FPS (-1 if uncapped), and max texture size")),
                ("MasterVolume / IsAudioMuted", Tr("DOCS_TAG_CORE_UNITY_AUDIO", "Unity: AudioListener master volume (0.0-1.0) and mute state")),
                ("UnityVersion / OperatingSystem / DeviceModel / GraphicsDeviceType", Tr("DOCS_TAG_CORE_UNITY_SYS", "Unity: engine version, OS info, device model, and graphics API (Direct3D11, Vulkan, Metal, etc.)"))
            )
        ),
        new DocSection(
            Tr("DOCS_TAG_MATH_TITLE", "Inline Math, Expressions & Local Tags"),
            Tr("DOCS_TAG_MATH_SUMMARY", "Native expression fast path, constant folding, V8 JavaScript fallback, and text-local tag registration"),
            new DocParagraph(Tr(
                "DOCS_TAG_MATH_P1",
                "Pure numeric expressions inside {...} are compiled through the native OvJsExpr / O5Expr fast path with compile-time constant folding. Expressions that reference non-numeric tags, return strings or markup (such as ternaries), or use return blocks automatically fall back to the ClearScript V8 JavaScript engine"
            )),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_TAG_MATH_T_CALLOUT_TITLE", "Variable 't' is Graph-only"),
                Tr("DOCS_TAG_MATH_T_CALLOUT_TEXT", "The elapsed-time variable 't' (Time.realtimeSinceStartup) is live only inside the Graph component (GraphExpressionEngine). In TextEngine templates, 't' evaluates to 0 natively and undefined in V8; use TimeSinceStart instead")
            ),
            new DocBadges(
                Tr("DOCS_TAG_MATH_BADGES_TITLE", "ExprCore Built-in Math Functions & Constants"),
                isEase: false,
                "sin", "cos", "tan", "asin", "acos", "atan", "atan2",
                "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
                "sqrt", "cbrt", "exp", "exp2", "ln", "log", "log2", "log10",
                "abs", "sign", "floor", "ceil", "round", "trunc",
                "min", "max", "clamp", "pow", "mod", "fmod", "hypot",
                "lerp", "step", "smoothstep", "deg2rad", "rad2deg",
                "pi", "e"
            ),
            new DocParagraph(Tr(
                "DOCS_TAG_MATH_P2",
                "round(x, digits) accepts an optional decimal digit count, log(x, base) accepts an optional base, mod(a, b) computes true mathematical modulo (non-negative for positive b), and fmod(a, b) computes C-style remainder"
            )),
            new DocParagraph(Tr(
                "DOCS_TAG_MATH_LOCAL_P",
                "Register text-local tags directly inside a template using {RegisterTag(\"Name\", () => ...)}. Local tags shadow global tags of the same name, render an empty string at the registration site, and maintain an isolated animation/effect state cache per text object"
            )),
            new DocCallout(
                DocCalloutKind.Warning,
                Tr("DOCS_TAG_MATH_JSEXPR_TITLE", "Legacy {JSExpr:...} is Obsolete"),
                Tr("DOCS_TAG_MATH_JSEXPR_TEXT", "The legacy {JSExpr:...} prefix is marked [Obsolete] and emits a compiler warning. Write expressions directly inside {...}")
            ),
            new DocCode(
                Tr("DOCS_TAG_MATH_CODE_CAP", "Inline Math, Ternary & Local Tags"),
                "{RegisterTag(\"Pulse\", () => 0.5 + 0.5 * sin(TimeSinceStart * 4))}\nFps Ratio: {clamp(Fps / 144, 0, 1):0.00}\nStatus: {Fps >= 60 ? \"<color=#4ADE80>OK</color>\" : \"<color=#F87171>LOW</color>\"}\nPulse: {round(Pulse, 2):0.00}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            )
        ),
        new DocSection(
            Tr("DOCS_TAG_FX_TITLE", "Built-in Effect Tags"),
            Tr("DOCS_TAG_FX_SUMMARY", "Numeric easing, hex color interpolation, and size pulsing inside text templates"),
            new DocTable(
                Tr("DOCS_TAG_FX_TBL_KEY", "Signature"),
                Tr("DOCS_TAG_FX_TBL_VAL", "Behavior & Defaults"),
                ("EasedValue(tagName, digits = -1, speed = 500, ease = \"Linear\")", Tr("DOCS_TAG_FX_ROW_EASED", "Interpolates a numeric tag over speed ms. Supports ProcessFormat (:format). speed <= 0 snaps immediately; unknown tags return 0")),
                ("ColorRange(tagName, min, max, minHex, maxHex, ease = \"Linear\", maxLength = -1)", Tr("DOCS_TAG_FX_ROW_COLOR", "Interpolates between two hex colors and returns hex without '#' (leading '#' in inputs is stripped; 4- or 8-digit hex preserves alpha). max <= min returns minHex; failure returns \"\". Does not support ProcessFormat")),
                ("MovingMan(tagName, startSize, endSize, defaultSize, speed, invert = false, ease = \"OutExpo\")", Tr("DOCS_TAG_FX_ROW_MOVING", "Pulses a numeric size from startSize to endSize over speed ms on tagName change, then returns defaultSize. Default ease is OutExpo; unknown tags or speed <= 0 return defaultSize. Does not support ProcessFormat"))
            ),
            new DocCode(
                Tr("DOCS_TAG_FX_CODE_CAP", "Combined Effect Tags"),
                "<size={MovingMan(Kps, 52, 38, 38, 250, false, OutExpo):0}><color=#{ColorRange(Fps, 30, 144, FF5555, 55FF88, OutCubic)}>{EasedValue(Fps, 1, 300, OutQuad):0.0}</color></size> FPS",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            )
        ),
        new DocSection(
            Tr("DOCS_TAG_TMP_TITLE", "TMP Rich Text Tags"),
            Tr("DOCS_TAG_TMP_SUMMARY", "TextMeshPro rich text markup tags supported inside Overlayer text objects"),
            new DocTable(
                Tr("DOCS_TAG_TMP_TBL_KEY", "Markup"),
                Tr("DOCS_TAG_TMP_TBL_VAL", "Effect"),
                ("<color=#RRGGBB> / <color=#RRGGBBAA>", Tr("DOCS_TAG_TMP_ROW_COLOR", "Sets text color and optional alpha. Combine with ColorRange for dynamic gradients")),
                ("<size=48> / <size=120%>", Tr("DOCS_TAG_TMP_ROW_SIZE", "Overrides font size in pixels or percentage")),
                ("<b> / <i> / <u> / <s>", Tr("DOCS_TAG_TMP_ROW_STYLE", "Bold, italic, underline, and strikethrough")),
                ("<alpha=#AA>", Tr("DOCS_TAG_TMP_ROW_ALPHA", "Sets text opacity using two hex digits (00-FF)")),
                ("<mspace=0.6em>", Tr("DOCS_TAG_TMP_ROW_MSPACE", "Forces fixed character advance width to prevent horizontal jitter on changing numbers")),
                ("<pos=200> / <align=center>", Tr("DOCS_TAG_TMP_ROW_ALIGN", "Horizontal cursor offset or per-line text alignment")),
                ("<noparse>...</noparse>", Tr("DOCS_TAG_TMP_ROW_NOPARSE", "Disables TMP rich text parsing to display literal angle brackets"))
            ),
            new DocLink(
                Tr("DOCS_TAG_TMP_LINK_PREFIX", "Full reference:"),
                Tr("DOCS_TAG_TMP_LINK_LABEL", "Unity TextMeshPro Rich Text Documentation"),
                "https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichText.html"
            )
        )
    );

    private static DocChapter BuildTagExplorer() => new(
        id: "explorer",
        icon: UIIcon.MagnifyingGlass,
        spriteIcon: null,
        title: Tr("DOCS_EXP_CHAPTER_TITLE", "Tag List"),
        subtitle: Tr("DOCS_EXP_CHAPTER_SUBTITLE", "Browse, search, and copy registered built-in, module, and JavaScript tags with live values"),
        special: DocSpecialKind.TagExplorer
    );
}
