using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal enum DocSpecialKind {
    None,
    Overview,
    TagPlayground,
    TagExplorer,
}

internal enum DocCalloutKind {
    Info,
    Tip,
    Warning,
}

internal abstract class DocBlock;

internal sealed class DocParagraph(string text) : DocBlock {
    public readonly string Text = text;
}

internal sealed class DocCallout(DocCalloutKind kind, string title, string text) : DocBlock {
    public readonly DocCalloutKind Kind = kind;
    public readonly string Title = title;
    public readonly string Text = text;
}

internal sealed class DocCode(string caption, string code, DocsHighlight.Mode mode, bool tryInSandbox = false) : DocBlock {
    public readonly string Caption = caption;
    public readonly string Code = code;
    public readonly DocsHighlight.Mode Mode = mode;
    public readonly bool TryInSandbox = tryInSandbox;
}

internal sealed class DocTable(string keyHeader, string valueHeader, params (string Key, string Value)[] rows) : DocBlock {
    public readonly string KeyHeader = keyHeader;
    public readonly string ValueHeader = valueHeader;
    public readonly (string Key, string Value)[] Rows = rows;
}

internal sealed class DocBadges(string title, bool isEase, params string[] items) : DocBlock {
    public readonly string Title = title;
    public readonly bool IsEase = isEase;
    public readonly string[] Items = items;
}

internal sealed class DocLink(string prefix, string label, string url) : DocBlock {
    public readonly string Prefix = prefix;
    public readonly string Label = label;
    public readonly string Url = url;
}

internal sealed class DocSection(string title, string summary, params DocBlock[] blocks) {
    public readonly string Title = title;
    public readonly string Summary = summary;
    public readonly DocBlock[] Blocks = blocks;
}

internal sealed class DocChapter(
    string id,
    UISprite icon,
    string title,
    string subtitle,
    DocSpecialKind special,
    params DocSection[] sections
) {
    public readonly string Id = id;
    public readonly UISprite Icon = icon;
    public readonly string Title = title;
    public readonly string Subtitle = subtitle;
    public readonly DocSpecialKind Special = special;
    public readonly DocSection[] Sections = sections;
}

/// <summary>
/// Built-in Overlayer V5 wiki content grounded directly in the codebase.
/// Automatically switches between Korean (ko-KR) and English based on MainCore.Tr.Language.
/// </summary>
internal static class DocsContent {
    public static bool IsKorean =>
        string.Equals(Core.MainCore.Tr?.Language, "ko-KR", StringComparison.OrdinalIgnoreCase);

    public static string T(string ko, string en) => IsKorean ? ko : en;

    public static DocChapter[] BuildChapters() => [
        BuildOverview(),
        BuildTagsAndText(),
        BuildTagExplorer(),
        BuildHudEditor(),
        BuildComponentsAndFx(),
        BuildEasingAndCurves(),
        BuildJavaScriptApi(),
        BuildPackagesAndModules(),
    ];

    // ------------------------------------------------------------------
    // 1. Overview
    // ------------------------------------------------------------------
    private static DocChapter BuildOverview() => new(
        "overview",
        UISprite.OV5LogoOutline256,
        T("개요", "Overview"),
        T("게임 위에 텍스트, 이미지, 그래프, 스크립트 HUD를 구성할 수 있는 오버레이 모드입니다.",
          "An overlay mod for building text, image, graph, and script HUDs over Unity games."),
        DocSpecialKind.Overview,
        new DocSection(
            "Core Architecture",
            T("Overlayer의 HUD는 4단계 구조로 이루어져 있습니다.",
              "Overlayer HUDs follow a four-layer structure."),
            new DocTable(
                T("계층", "Layer"),
                T("설명", "Description"),
                ("OvCanvas", T("독립된 Unity Canvas 단위입니다. 활성 토글, 해상도 스케일러, .o5cp 내보내기와 가져오기의 기준이 됩니다.",
                               "An independent Unity Canvas unit. Controls enable state, canvas scaling, and .o5cp export/import.")),
                ("OvObject", T("캔버스 안의 텍스트, 이미지, 빈 그룹 같은 요소입니다. 부모와 자식 트리로 묶어 관리할 수 있습니다.",
                               "An element inside a canvas such as text, image, or group. Supports nested parent/child trees.")),
                ("Components", T("RectTransform, TextMeshProUGUI, Image, CanvasGroup, Layout, Graph, MovingMan, ColorRange, Rain 같은 컴포넌트를 조합해 사용합니다.",
                                 "Combine Unity and Overlayer components such as RectTransform, TextMeshProUGUI, Image, CanvasGroup, Layouts, Graph, MovingMan, ColorRange, and Rain.")),
                ("Fx & TextEngine", T("텍스트에는 {Tag} 템플릿을 쓰고, 인스펙터의 각 속성에서는 Fx 버튼을 켜 JavaScript 수식을 연결할 수 있습니다.",
                                      "Use {Tag} templates in text objects, or toggle Fx on inspector fields to bind live JavaScript expressions."))
            )
        ),
        new DocSection(
            "Global Shortcuts",
            T("게임 어디서나 쓸 수 있는 단축키입니다.",
              "Shortcuts available anywhere in the game."),
            new DocTable(
                T("단축키", "Shortcut"),
                T("동작", "Action"),
                (UICore.WindowKey.ToDisplayString(), T("Overlayer 창을 열거나 닫습니다. HUD 편집기 상태라면 메인 창으로 전환합니다.",
                                     "Open or close the Overlayer window. Switches back to the main window if HUD Editor is open.")),
                (T($"{UICore.WindowKey.ToDisplayString()} 홀드", $"Hold {UICore.WindowKey.ToDisplayString()}"), T("0.4초간 홀드하여 메인 창 위치와 크기를 기본값으로 초기화할 수 있습니다.",
                                 "Hold for 0.4s to reset the main window position and size to default.")),
                (HudEdit.HudEditor.Key.ToDisplayString(), T("HUD 편집기를 열거나 닫습니다. 플레이 중에도 사용할 수 있으며 설정에서 키를 바꿀 수 있습니다.",
                            "Open or close the HUD Editor, including during play. Rebindable in Settings.")),
                ("TopBar Double Click", T("메인 창을 최대화하거나 이전 크기로 되돌립니다.",
                                          "Maximize or restore the main window.")),
                ("Logo Click", T("좌측 네비게이션 메뉴를 열거나 접습니다.",
                                 "Open or collapse the left navigation menu."))
            )
        )
    );

    // ------------------------------------------------------------------
    // 2. Tags & Text Engine
    // ------------------------------------------------------------------
    private static DocChapter BuildTagsAndText() => new(
        "tags",
        UISprite.Text128,
        T("태그 & 텍스트 엔진", "Text Engine"),
        T("{Tag} 문법, 인자 전달, 숫자 포맷, 인라인 수식, TMP 리치 텍스트, 내장 이펙트 태그를 다룹니다.",
          "Covers {Tag} syntax, arguments, numeric formatting, inline expressions, TMP rich text, and built-in effect tags."),
        DocSpecialKind.TagPlayground,
        new DocSection(
            "Tag Syntax & Formatting",
            T("TextEngine은 텍스트 안의 태그를 파싱해 실시간 값으로 치환합니다.",
              "TextEngine parses tags in text and replaces them with live values."),
            new DocTable(
                T("문법", "Syntax"),
                T("설명", "Description"),
                ("{TagName}", T("인자가 없거나 기본값을 쓰는 태그를 호출합니다. {TagName()} 형태도 쓸 수 있습니다.",
                                "Calls a tag with no arguments or default values. {TagName()} also works.")),
                ("{Tag:arg1,arg2}", T("콜론 뒤에 쉼표로 인자를 넘깁니다. Fps, Kps 같은 ProcessFormat 태그는 마지막 인자를 숫자 포맷으로 인식합니다.",
                                      "Passes comma-separated arguments after a colon. ProcessFormat tags like Fps and Kps parse a trailing format string.")),
                ("{Tag(arg1, arg2)}", T("함수 호출 형태입니다. 인자가 여러 개이거나 curve(...)를 넣을 때 읽기 편합니다.",
                                        "Function-call syntax. Cleaner for multiple arguments or inline curve(...) easings.")),
                ("{Tag:0.00} / {Tag:0.##}", T("C# 숫자 포맷을 지정합니다. 0은 빈 자릿수를 채우고, #은 끝자리 0을 생략하며, N0는 천 단위 쉼표를 넣습니다.",
                                              "C# numeric format specifiers: 0 pads zeros, # trims trailing zeros, and N0 adds thousands separators.")),
                ("{Fps:500,0}", T("500ms 구간 평균 FPS를 정수로 반올림해 표시하는 예시입니다.",
                                  "Shows a 500ms average FPS rounded to an integer."))
            ),
            new DocCode(
                T("포맷 & 인자 예시", "Formatting & Argument Examples"),
                "<color=#A8B1FF>{Fps:500,0}</color> FPS  ·  {FrameTime:500,0.00} ms\n"
                + "{Year}/{Month:00}/{Day:00}  {Hour:00}:{Minute:00}:{Second:00}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            )
        ),
        new DocSection(
            "Inline Math & JS Expressions",
            T("중괄호 안에 계산식이나 JS 삼항 연산자를 바로 쓸 수 있습니다. 단순 숫자 수식은 네이티브로 처리되고, 복잡한 식은 V8에서 실행됩니다.",
              "Write math or JS expressions directly inside braces. Simple numeric expressions run natively; full JS falls back to V8."),
            new DocCode(
                T("수학 & JS 표현식 예시", "Math & JS Expression Examples"),
                "Half FPS: {round(Fps / 2)}  ·  Clamped: {clamp(Fps, 0, 144):0}\n"
                + "Status: {Fps() >= 60 ? '<color=#96FF91>SMOOTH</color>' : '<color=#FF9191>LOW</color>'}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            ),
            new DocBadges(
                "ExprCore Math Functions & Constants",
                false,
                "sin", "cos", "tan", "asin", "acos", "atan", "atan2",
                "sinh", "cosh", "tanh", "sqrt", "cbrt", "exp", "exp2",
                "ln", "log", "log2", "log10", "abs", "sign", "floor",
                "ceil", "round", "trunc", "min", "max", "clamp", "pow",
                "mod", "fmod", "hypot", "lerp", "step", "smoothstep",
                "deg2rad", "rad2deg", "pi", "e", "t"
            ),
            new DocCallout(
                DocCalloutKind.Warning,
                "[Obsolete]",
                T("구버전의 {JSExpr:...} 문법은 이후 지원 중단될 예정입니다. {Fps() * 2}처럼 중괄호 안에 바로 수식을 작성하는 것을 권장합니다.",
                  "The legacy {JSExpr:...} syntax is deprecated. Write expressions directly inside braces such as {Fps() * 2}.")
            )
        ),
        new DocSection(
            "Built-in Effect Tags",
            T("숫자 태그의 변화를 추적해 보간하거나 색상, 크기로 바꿀 수 있습니다. ease 인자에는 31종 프리셋, 저장한 커브 이름, 인라인 curve(...)를 넣을 수 있습니다.",
              "Track a numeric tag to animate its value, color, or size. The ease argument accepts presets, saved curve names, or inline curve(...)."),
            new DocTable(
                T("이펙트 태그", "Effect Tag"),
                T("설명", "Description"),
                ("EasedValue", T("(tagName, digits = -1, speed = 500, ease = \"Linear\")\n대상 태그 값이 바뀔 때 speed 밀리초 동안 ease 곡선으로 보간합니다.",
                                 "(tagName, digits = -1, speed = 500, ease = \"Linear\")\nInterpolates toward the target tag over speed ms using the ease curve.")),
                ("ColorRange", T("(tagName, min, max, minHex, maxHex, ease = \"Linear\", maxLen = -1)\nmin~max 구간에서 두 Hex 색상을 보간해 반환합니다. <color=#{ColorRange(...)}> 형태로 사용합니다.",
                                 "(tagName, min, max, minHex, maxHex, ease = \"Linear\", maxLen = -1)\nReturns an interpolated hex color across [min, max] for use with <color=#...>.")),
                ("MovingMan", T("(tagName, startSize, endSize, defaultSize, speed, invert = false, ease = \"OutExpo\")\n대상 태그 값이 바뀔 때마다 startSize에서 endSize로 speed 밀리초 동안 변한 뒤 defaultSize로 돌아갑니다.",
                                "(tagName, startSize, endSize, defaultSize, speed, invert = false, ease = \"OutExpo\")\nAnimates from startSize to endSize over speed ms whenever the target tag changes, then returns to defaultSize."))
            ),
            new DocCode(
                T("이펙트 태그 조합 예시", "Combining Effect Tags"),
                "<color=#{ColorRange(Fps,30,144,FF5555,96FF91,OutCubic)}>{EasedValue(Fps,0,300,OutExpo)}</color> FPS\n"
                + "<size={MovingMan(Second,34,24,24,400,false,OutExpo)}>{Second:00}</size>",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            )
        ),
        new DocSection(
            "TMP Rich Text Tags",
            T("TextMeshPro 서식 태그를 {Tag}와 함께 사용할 수 있습니다.",
              "Combine TextMeshPro rich text tags with Overlayer {Tag} placeholders."),
            new DocTable(
                T("TMP 태그", "TMP Tag"),
                T("효과", "Effect"),
                ("<color=#RRGGBB>", T("글자 색상을 바꿉니다. 알파를 포함한 #RRGGBBAA도 쓸 수 있으며 </color>로 닫습니다.",
                                      "Sets text color using #RRGGBB or #RRGGBBAA. Close with </color>.")),
                ("<size=24> / <size=120%>", T("픽셀 혹은 비율 단위로 글자 크기를 바꿉니다.",
                                              "Sets font size in pixels or percentage.")),
                ("<b> / <i> / <u> / <s>", T("굵게, 기울임, 밑줄, 취소선을 적용합니다.",
                                            "Applies bold, italic, underline, or strikethrough.")),
                ("<alpha=#88>", T("투명도만 바꿉니다. 00이 투명, FF가 불투명입니다.",
                                  "Changes opacity only, from 00 transparent to FF opaque.")),
                ("<mspace=0.6em>", T("고정폭 간격을 지정해 숫자가 바뀔 때 흔들리지 않게 합니다.",
                                     "Sets fixed character width so changing numbers stay aligned.")),
                ("<pos=160> / <align=right>", T("수평 위치 이동 혹은 줄별 정렬을 지정합니다.",
                                                "Sets horizontal tab position or per-line alignment."))
            ),
            new DocLink(
                "More infos To ",
                T("Unity TextMeshPro Rich Text 문서", "Unity TextMeshPro Rich Text documentation"),
                "https://docs.unity3d.com/Packages/com.unity.ugui@7.0/manual/TextMeshPro/RichText.html"
            )
        )
    );

    // ------------------------------------------------------------------
    // 3. Tag List
    // ------------------------------------------------------------------
    private static DocChapter BuildTagExplorer() => new(
        "explorer",
        UISprite.MagnifyingGlass128,
        T("태그 목록", "Tag List"),
        T("현재 로드된 코어, 게임 모듈, JS 스크립트 태그를 검색하고 복사할 수 있습니다.",
          "Search and copy all loaded Core, Module, and JS tags."),
        DocSpecialKind.TagExplorer
    );

    // ------------------------------------------------------------------
    // 4. HUD Editor
    // ------------------------------------------------------------------
    private static DocChapter BuildHudEditor() {
        string hudKey = HudEdit.HudEditor.Key.ToDisplayString();
        return new(
            "hud",
            UISprite.Expand128,
            T("HUD 편집기", "HUD Editor"),
            T($"{hudKey} 키로 화면 위의 HUD 요소를 드래그, 크기 조절, 회전, 정렬, 그룹화할 수 있습니다.",
              $"Press {hudKey} to drag, resize, rotate, align, and group HUD elements directly on screen."),
            DocSpecialKind.None,
            new DocSection(
                "Workspace & Direct Manipulation",
                T($"플레이 중에도 {hudKey} 키로 열 수 있으며, 편집기가 열린 동안에는 HudEditorOpen 태그가 true가 되어 플레이 전용 HUD도 숨겨지지 않습니다.",
                  $"Open with {hudKey} even during play. While open, HudEditorOpen() returns true so play-only HUDs remain visible."),
            new DocTable(
                T("기능", "Feature"),
                T("설명", "Description"),
                (T("핸들 & 변형", "Handles & Transform"), T("모서리와 변 핸들로 크기를 조절하고, 모서리 바깥을 드래그해 회전할 수 있습니다. Shift를 홀드하면 비율이 고정됩니다.",
                                                            "Drag corner or edge handles to resize, or outside a corner to rotate. Hold Shift to lock aspect ratio.")),
                (T("스냅 & 거리 측정", "Snap & Measure"), T("드래그할 때 다른 요소의 가장자리 혹은 화면 중앙선에 맞게 조정됩니다. 미세한 조정이 필요하다면 Alt를 홀드하여 잠시 스냅을 끄고 주변 요소와의 거리를 볼 수 있습니다.",
                                                           "Snaps to other elements, screen edges, and guides while dragging. For fine adjustments, hold Alt to temporarily disable snapping and inspect pixel distances.")),
                (T("눈금자 & 가이드", "Rulers & Guides"), T("상단 툴바의 눈금자 버튼으로 켤 수 있습니다. 눈금자에서 드래그해 가이드선을 꺼내고, 눈금자로 되돌려 지웁니다.",
                                                           "Toggle rulers from the top bar. Drag out from a ruler to add a guide, and drag it back to remove.")),
                (T("자동 중심점", "Auto Anchor"), T("상단 툴바의 자동 중심점을 켜면 이동할 때 화면 9분할 중 가까운 쪽으로 Anchor와 Pivot이 맞춰집니다.",
                                                  "Enable Auto Anchor in the top bar to pin Anchor and Pivot to the closest of the 9 screen zones while moving.")),
                (T("레이어 패널", "Layers Panel"), T("캔버스와 오브젝트 트리를 관리합니다. 클릭 선택, 더블클릭 이름 변경, 드래그 순서 변경과 중첩, 잠금, 숨김, 새 캔버스, 삭제를 지원합니다.",
                                                   "Manage canvases and objects with click select, double-click rename, drag reorder/nesting, lock, hide, add, and undoable delete.")),
                ("Pin to", T("우측 인스펙터의 3x3 모서리 피커를 이용해 화면 위치를 유지한 채 Anchor와 Pivot을 바꿀 수 있습니다.",
                              "Use the 3x3 anchor picker in the inspector to change Anchor and Pivot without moving the object on screen.")),
                (T("섹션 분리", "Tear-off Docks"), T("인스펙터 섹션 헤더를 패널 밖으로 드래그해 띄우고, 다시 패널로 드래그해 합칠 수 있습니다.",
                                                    "Drag an inspector section header out of the panel to float it, or drag it back to redock."))
            )
        ),
        new DocSection(
            "Complete HUD Editor Shortcuts",
            T("편집 내역은 UndoStack에 기록되어 실행 취소와 다시 실행을 할 수 있습니다.",
              "Edits are recorded in UndoStack for undo and redo."),
            new DocTable(
                T("단축키", "Shortcut"),
                T("동작", "Action"),
                ("Double Click", T("그룹 안으로 들어가거나 텍스트를 바로 수정합니다.", "Enter a group or edit text inline.")),
                ("Ctrl + Click", T("그룹 안에 중첩된 하위 오브젝트를 바로 선택합니다.", "Deep-select a nested child inside a group.")),
                ("Shift + Click", T("다중 선택에 추가하거나 해제합니다.", "Add or remove an object from selection.")),
                ("Drag Empty", T("빈 영역을 드래그해 여러 오브젝트를 한 번에 선택합니다.", "Drag across empty space to box-select objects.")),
                ("Right Click", T("생성, 프리셋, 정렬, 잠금 같은 컨텍스트 메뉴를 엽니다.", "Open context menu for create, presets, align, and lock.")),
                ("Arrows / Shift+Arrows", T("1px 혹은 10px 단위로 이동합니다.", "Nudge selection by 1px or 10px.")),
                ("Ctrl+C / Ctrl+V", T("선택을 복사하거나 붙여넣습니다. Ctrl+Shift+V는 같은 자리에 붙여넣습니다.", "Copy or paste selection. Ctrl+Shift+V pastes in place.")),
                ("Ctrl+Alt+C / V", T("폰트, 색상, 외곽선 같은 모양만 복사해 붙여넣습니다.", "Copy and paste visual style only.")),
                ("Ctrl+D / Del", T("선택을 복제하거나 삭제합니다. 상단 X 버튼은 홀드하거나 Shift+클릭으로 바로 삭제할 수 있습니다.", "Duplicate or delete selection. Shift+click toolbar X to delete immediately.")),
                ("Ctrl+G / Ctrl+Shift+G", T("선택한 요소들을 그룹으로 묶거나 해제합니다.", "Group or ungroup selection.")),
                ("Ctrl+Shift+] / [", T("맨 앞 혹은 맨 뒤로 보냅니다. Ctrl+]와 Ctrl+[는 한 단계씩 이동합니다.", "Bring to front or send to back. Ctrl+] and Ctrl+[ step by one.")),
                ("Ctrl+Shift+L / H", T("선택을 잠그거나 숨깁니다.", "Toggle lock or visibility on selection.")),
                ("Ctrl+Z", T("실행 취소", "Undo")),
                ("Ctrl+Y / Ctrl+Shift+Z", T("다시 실행", "Redo")),
                ("Esc", T("팝업 닫기, 드래그 취소, 선택 해제, HUD 편집기 닫기", "Close popup, cancel drag, deselect, or exit editor"))
            )
        )
    );
    }

    // ------------------------------------------------------------------
    // 5. Components & Fx Expressions
    // ------------------------------------------------------------------
    private static DocChapter BuildComponentsAndFx() => new(
        "components",
        UISprite.Cube128,
        T("컴포넌트 & Fx 수식", "Component & Fx"),
        T("Overlayer 전용 컴포넌트와 인스펙터 속성에 JavaScript 수식을 연결하는 Fx 기능을 설명합니다.",
          "Overlayer custom components and reactive Fx JavaScript expressions for inspector properties."),
        DocSpecialKind.None,
        new DocSection(
            "Overlayer Custom Components",
            T("기본 Unity UI 컴포넌트 외에 Overlayer 전용 컴포넌트를 추가해 사용할 수 있습니다.",
              "Add Overlayer components alongside standard Unity UI components."),
            new DocTable(
                T("컴포넌트", "Component"),
                T("설명", "Description"),
                ("Graph", T("실시간 그래프를 그립니다.\n• JsCode: 시간 변수 t를 쓰는 JS 수식\n• Window, Samples, Min/Max, AutoScale, LineColor, Thickness, ShowAxes, ShowGrid, ShowFill, FillColor",
                            "Plots a live graph.\n• JsCode: JS expression using time variable t\n• Window, Samples, Min/Max, AutoScale, LineColor, Thickness, ShowAxes, ShowGrid, ShowFill, FillColor")),
                ("MovingMan", T("태그 값이 바뀔 때 크기를 애니메이션합니다.\n• TagName, Target, StartSize, EndSize, DefaultSize, Speed, Invert, Ease",
                                "Animates size when a tag changes.\n• TagName, Target, StartSize, EndSize, DefaultSize, Speed, Invert, Ease")),
                ("ColorRange", T("태그 값에 따라 색상을 보간합니다.\n• TagName, Minimum, Maximum, MinimumColor, MaximumColor, Ease",
                                 "Interpolates color or gradient from a tag value.\n• TagName, Minimum, Maximum, MinimumColor, MaximumColor, Ease")),
                ("Rain", T("키 입력 트레일을 생성합니다.\n• Active에 Fx로 IsKeyHeld('A') 연결, Speed, Length, Width, Offset, FadeIn, FadeOut, Color, SpriteKey, MaxTrails",
                           "Generates key input trails.\n• Bind Active via Fx e.g. IsKeyHeld('A'), Speed, Length, Width, Offset, FadeIn, FadeOut, Color, SpriteKey, MaxTrails")),
                (T("3D 컴포넌트", "3D Components"), T("설정에서 Enable 3D Features를 켜면 Transform, MeshFilter, MeshRenderer, Light, Rigidbody, Collider를 쓸 수 있습니다.",
                                                     "Enable 3D Features in Settings to use Transform, MeshFilter, MeshRenderer, Light, Rigidbody, and Colliders."))
            )
        ),
        new DocSection(
            "Reactive Fx Expressions",
            T("인스펙터 항목 옆의 Fx 버튼을 켜면 고정값 대신 매 프레임 평가되는 JavaScript 수식을 입력할 수 있습니다.",
              "Toggle Fx next to an inspector field to evaluate a JavaScript expression every frame."),
            new DocTable(
                T("속성 타입", "Property Type"),
                T("반환 형식 & 예시", "Return Format & Example"),
                ("float / double / int", T("숫자 반환\n예: 24 + Math.sin(TimeSinceStart() * 4) * 6",
                                           "Number\nEx: 24 + Math.sin(TimeSinceStart() * 4) * 6")),
                ("bool", T("불리언 반환\n예: IsKeyHeld('Space') || HudEditorOpen()",
                           "Boolean\nEx: IsKeyHeld('Space') || HudEditorOpen()")),
                ("Vector2", T("[x, y] 배열\n예: [Math.min(Math.max(Fps() / 144, 0), 1), 1]",
                              "[x, y] array\nEx: [Math.min(Math.max(Fps() / 144, 0), 1), 1]")),
                ("Vector3", T("[x, y, z] 배열\n예: [1 + Kps() * 0.02, 1 + Kps() * 0.02, 1]",
                              "[x, y, z] array\nEx: [1 + Kps() * 0.02, 1 + Kps() * 0.02, 1]")),
                ("Vector4 / Rect", T("[x, y, z, w] 혹은 [x, y, width, height]",
                                     "[x, y, z, w] or [x, y, width, height]")),
                ("Quaternion", T("[x, y, z] 오일러 각도 혹은 [x, y, z, w] 쿼터니언",
                                 "[x, y, z] Euler degrees or [x, y, z, w] quaternion")),
                ("Color", T("[r, g, b] 혹은 [r, g, b, a] 실수 0.0~1.0",
                            "[r, g, b] or [r, g, b, a] in 0.0 to 1.0")),
                ("GradientColor", T("단색 [r, g, b, a] 4개 혹은 4개 코너 16개 배열",
                                    "4 floats for solid or 16 floats for 4 corners"))
            ),
            new DocCode(
                T("Fx 패턴 예시: 플레이 중에만 표시 + HUD 편집기에서는 유지", "Fx Example: Play-only HUD visible in HUD Editor"),
                "// CanvasGroup -> Alpha (Fx)\n"
                + "(IsPlaying() || HudEditorOpen()) ? 1 : 0\n\n"
                + "// RectTransform -> SizeDelta (Fx, progress bar)\n"
                + "[Math.min(Math.max(Fps() / 144, 0), 1) * 300, 12]",
                DocsHighlight.Mode.Js
            ),
            new DocCallout(
                DocCalloutKind.Info,
                T("성능 & 안전장치", "Performance & Safety"),
                T("단순 숫자 배열 수식은 네이티브로 컴파일되어 V8을 거치지 않고 실행됩니다. V8 수식은 500ms 타임아웃과 오류 시 5초 대기 로직이 있어 게임이 멈추지 않도록 막아줍니다.",
                  "Simple numeric expressions compile natively without V8 overhead. V8 expressions enforce a 500ms timeout and a 5s error backoff to prevent freezes.")
            )
        )
    );

    // ------------------------------------------------------------------
    // 6. Easing & Curves
    // ------------------------------------------------------------------
    private static DocChapter BuildEasingAndCurves() => new(
        "easing",
        UISprite.Ease128,
        T("가감속 & 커브 에디터", "Easing"),
        T("31종 내장 이징 프리셋과 4가지 커스텀 커브 포맷, 저장된 커브 사용법을 다룹니다.",
          "Covers all 31 built-in O5Ease presets, 4 custom curve formats, and saved curves."),
        DocSpecialKind.None,
        new DocSection(
            "Built-in Easing Presets",
            T("EasedValue, ColorRange, MovingMan의 ease 인자에 대소문자 구분 없이 입력할 수 있습니다.\n각 항목에 마우스를 올리면 실제 곡선과 움직임을 볼 수 있습니다.",
              "Pass any preset name, case insensitive, to EasedValue, ColorRange, MovingMan, or the Easing tab.\nHover any preset to preview its curve and motion."),
            new DocBadges(
                "O5Ease Presets",
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
                "InBounce", "OutBounce", "InOutBounce"
            )
        ),
        new DocSection(
            "Custom Curve Formats",
            T("Easing 탭에서 그린 커브를 복사해 태그나 컴포넌트에 붙여넣거나, 이름을 붙여 Easings.json에 저장해 쓸 수 있습니다.",
              "Copy curves drawn in the Easing tab directly into tags or components, or save them by name in Easings.json."),
            new DocTable(
                T("포맷", "Format"),
                T("설명", "Description"),
                ("curve(x,y;...)", T("Catmull-Rom 스플라인 보간입니다. x는 0..1 범위이며 세미콜론으로 점을 나눕니다.",
                                     "Catmull-Rom spline through control points with x in 0..1, separated by semicolons.")),
                ("mono(x,y;...)", T("단조 큐빅 보간입니다. 부드럽게 이어지면서 목표값을 넘어 튀지 않습니다.",
                                    "Monotone cubic interpolation without overshoot.")),
                ("lines(x,y;...)", T("점 사이를 직선으로 연결합니다.",
                                     "Straight line segments between points.")),
                ("anim(x,y,in,out;...)", T("Unity AnimationCurve 방식입니다. 각 점에 진입과 진출 기울기를 지정하며 Alt를 홀드한 채 드래그해 핸들을 분리할 수 있습니다.",
                                           "Unity AnimationCurve style with explicit in/out tangents per key. Hold Alt while dragging to split tangents.")),
                (T("저장된 이름", "Saved Name"), T("Easing 탭에 저장한 이름을 ease 인자에 넣으면 바로 불러오며, 커브를 수정하면 태그에도 즉시 반영됩니다.",
                                                   "Pass a saved curve name as the ease argument. Edits to the saved curve update tags immediately."))
            ),
            new DocCode(
                T("커스텀 커브 예시", "Custom Curve Examples"),
                "{EasedValue(Fps,0,400,curve(0,0;0.3,0.85;0.7,1.1;1,1))}\n"
                + "{EasedValue(Fps,0,400,anim(0,0,0,2.5;1,1,0,0))}",
                DocsHighlight.Mode.Tag,
                tryInSandbox: true
            ),
            new DocTable(
                T("그래프 조작", "Curve Graph Controls"),
                T("동작", "Action"),
                ("Click / Drag", T("빈 곳을 클릭해 점을 추가하고, 점을 드래그해 옮깁니다.", "Click empty space to add a point; drag a point to move it.")),
                ("Right Click / Del", T("선택한 점을 지웁니다.", "Delete the selected point.")),
                ("Shift", T("0.05 단위로 격자에 맞춥니다.", "Snap coordinates to a 0.05 grid.")),
                ("Alt", T("Anim 모드에서 접선 핸들의 좌우 기울기를 분리합니다.", "Split in/out tangent handles in Anim mode.")),
                ("Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z", T("커브 편집 실행 취소 및 다시 실행", "Undo and redo curve edits"))
            )
        )
    );

    // ------------------------------------------------------------------
    // 7. JavaScript & V8 API
    // ------------------------------------------------------------------
    private static DocChapter BuildJavaScriptApi() => new(
        "js",
        UISprite.CodeBlock128,
        T("Javascript", "Javascript"),
        T("JS/Script 폴더에 .js 파일을 넣어 커스텀 태그, 설정 탭, 런타임 패치, Unity/CLR 제어를 작성할 수 있습니다.",
          "Author .js scripts in JS/Script for custom tags, settings tabs, runtime patches, and Unity/CLR interop."),
        DocSpecialKind.None,
        new DocSection(
            "Script Folder & IntelliSense",
            T("JS 탭의 Open Folder로 열리는 JS/Script/ 폴더의 .js 파일들이 V8 엔진에 로드됩니다. Auto Reload를 켜면 파일을 저장할 때 바로 다시 불러옵니다.",
              "Files in JS/Script/ load into the V8 engine. Enable Auto Reload to reload scripts on save."),
            new DocCallout(
                DocCalloutKind.Tip,
                T("VS Code 자동완성", "VS Code IntelliSense"),
                T("등록된 태그와 API 정의가 JS/impl.js와 JS/impl.d.ts에 생성됩니다. JS 폴더를 VS Code로 열면 자동완성과 타입 검사를 쓸 수 있습니다.",
                  "Overlayer generates JS/impl.js and JS/impl.d.ts for all tags and APIs. Open the JS folder in VS Code for IntelliSense.")
            )
        ),
        new DocSection(
            "Registering Tags: RegisterTag",
            T("RegisterTag(name, fn, options)로 새 태그를 등록할 수 있습니다. 텍스트 Fx 안에서 호출하면 해당 텍스트 전용 로컬 태그가 됩니다.",
              "Call RegisterTag(name, fn, options) to register a global tag, or a local tag when called inside a text's Fx code."),
            new DocCode(
                T("커스텀 태그 & 숫자 포맷 예시", "Custom Tag with Numeric Format"),
                "RegisterTag(\"FpsRatio\", (target = 144) => {\n"
                + "    return (Tag.Fps(500) / Number(target)) * 100;\n"
                + "}, {\n"
                + "    Type: TagType.ProcessFormat,\n"
                + "    ReturnType: \"number\", // enables {FpsRatio:144,0.0}\n"
                + "    Desc: \"Current FPS as a percentage of target\"\n"
                + "});",
                DocsHighlight.Mode.Js
            ),
            new DocTable(
                T("옵션 / 플래그", "Option / Flag"),
                T("설명", "Description"),
                ("ReturnType", T("\"number\", \"double\", \"float\", \"decimal\", \"int\", \"long\", \"string\" 중 하나를 지정합니다. 숫자 타입을 넣어야 {MyTag:0.00} 포맷을 쓸 수 있습니다.",
                                 "One of \"number\", \"double\", \"float\", \"decimal\", \"int\", \"long\", \"string\". Numeric types enable {MyTag:0.00} format validation.")),
                ("TagType.None (0)", T("기본값으로 항상 평가합니다.", "Default; always evaluated.")),
                ("ProcessFormat (256)", T("마지막 인자가 포맷 문자열이면 숫자 포맷으로 처리합니다.", "Treat a trailing format string as a numeric format.")),
                ("JsOnly (512) / Hide (65536)", T("JS 전용 태그 혹은 목록 숨김", "JS-only tag or hidden from listings")),
                ("Tag.<Name>(...args)", T("JS 안에서 등록된 코어, 모듈, JS 태그를 호출할 때 사용합니다.", "Proxy object for calling any registered Core, Module, or JS tag from JS."))
            )
        ),
        new DocSection(
            "State & Persistence: Store & Prefs",
            T("스크립트 간 메모리 공유에는 Store를, 재시작 뒤에도 유지할 설정에는 Prefs를 사용합니다.",
              "Use Store for in-memory sharing across scripts, and Prefs for persistent settings in Prefs.json."),
            new DocTable(
                T("API", "API"),
                T("메서드", "Methods"),
                ("Store", T("Store.Get(key, fallback), Store.Set(key, value), Store.Has(key), Store.Remove(key), Store.Clear(), Store.Keys(), Store.Count, 최대 1024개",
                            "Store.Get(key, fallback), Store.Set(key, value), Store.Has(key), Store.Remove(key), Store.Clear(), Store.Keys(), Store.Count, max 1024 entries")),
                ("Prefs", T("Prefs.Get(key, fallback), Prefs.Set(key, value), Prefs.Remove(key). JS/Script/Prefs.json에 바로 저장합니다.",
                            "Prefs.Get(key, fallback), Prefs.Set(key, value), Prefs.Remove(key). Saves immediately to JS/Script/Prefs.json."))
            )
        ),
        new DocSection(
            "Custom Menu Tabs: AddTab",
            T("Overlayer 사이드 메뉴에 커스텀 설정 탭을 추가할 수 있습니다. 스크립트를 다시 불러오거나 끄면 탭도 함께 정리됩니다.",
              "Add custom settings tabs to the Overlayer sidebar. Tabs clean up when the script reloads or disables."),
            new DocCode(
                T("AddTab + Prefs 예시", "AddTab + Prefs Example"),
                "const tabId = AddTab(\"My Tool\", tab => {\n"
                + "    tab.Header(\"My Settings\");\n"
                + "    const card = tab.Card(\"Options\");\n"
                + "    card.Toggle(\"Enabled\", Prefs.Get(\"my.on\", true), v => Prefs.Set(\"my.on\", v));\n"
                + "    card.Slider(\"Gain\", 0, 10, Prefs.Get(\"my.gain\", 2), v => Prefs.Set(\"my.gain\", v));\n"
                + "    const row = card.Row(8);\n"
                + "    row.Button(\"Log\", () => Log.Msg(\"Gain = \" + Prefs.Get(\"my.gain\", 2)));\n"
                + "    row.Button(\"Rebuild\", () => RebuildTab(tabId));\n"
                + "}, { icon: \"Gear128\" });",
                DocsHighlight.Mode.Js
            ),
            new DocBadges(
                "JSUIBuilder Methods",
                false,
                "Header(text)", "Text(text, size)", "Button(text, onClick)",
                "Toggle(text, val, onChanged)", "Slider(text, min, max, val, onChanged, fmt)",
                "Input(placeholder, val, onChanged, multiline)",
                "Dropdown(values[], val, onChanged)", "Color(label, color, onChanged)",
                "Row(spacing)", "Card(title)", "Clear()", "Root"
            )
        ),
        new DocSection(
            "Runtime Patching & Unity / CLR Interop",
            T("AddPatch로 메서드를 후킹하거나 Unity, Clr 헬퍼로 게임 오브젝트와 타입에 접근할 수 있습니다.",
              "Hook methods with AddPatch or access Unity objects and CLR types via Unity and Clr helpers."),
            new DocCode(
                T("AddPatch & Unity / Clr 예시", "AddPatch & Unity / Clr Example"),
                "// Harmony patch (__instance binds target instance)\n"
                + "const handle = AddPatch(\"SomeNamespace.Player::Jump(float)\", {\n"
                + "    prefix: (power, __instance) => { Log.Msg(\"Jump \" + power); },\n"
                + "    postfix: (power, __instance) => { Store.Set(\"jumps\", Store.Get(\"jumps\", 0) + 1); }\n"
                + "});\n\n"
                + "// Main thread scheduling & Unity lookup\n"
                + "const tickId = Unity.Repeat(1.0, () => {\n"
                + "    const cam = Unity.FindObjectOfType(\"Camera\");\n"
                + "    if (Unity.IsValid(cam)) Log.Msg(cam.name);\n"
                + "});",
                DocsHighlight.Mode.Js
            ),
            new DocTable(
                T("전역 객체", "Global Object"),
                T("설명", "Description"),
                ("AddPatch / RemovePatch", T("AddPatch(\"Type::Method(Args)\", { prefix, postfix }) / RemovePatch(handle). __instance 매개변수로 대상 인스턴스를 받을 수 있습니다.",
                                             "AddPatch(\"Type::Method(Args)\", { prefix, postfix }) / RemovePatch(handle). Name a parameter __instance to receive the target instance.")),
                ("Unity", T("FindObjectOfType, FindObjectsOfType, GetComponent, GetComponents, AddComponent, HasComponent, IsValid, NextTick(fn), Repeat(sec, fn), CancelTick(handle)",
                            "FindObjectOfType, FindObjectsOfType, GetComponent, GetComponents, AddComponent, HasComponent, IsValid, NextTick(fn), Repeat(sec, fn), CancelTick(handle)")),
                ("Clr", T("Get, Set, Call, TryGet, TrySet, TryCall, Prepare, TryPrepare, Create. 정적 타입에 접근할 때는 타입 이름을 문자열로 넘깁니다.",
                          "Get, Set, Call, TryGet, TrySet, TryCall, Prepare, TryPrepare, Create. Pass the type name as a string for static access.")),
                ("Log / console", T("Log.Msg / Wrn / Err 및 console.log / warn / error",
                                    "Log.Msg / Wrn / Err and console.log / warn / error"))
            ),
            new DocBadges(
                "Exposed UnityEngine Host Types",
                false,
                "GameObject", "Transform", "RectTransform", "Component", "Behaviour",
                "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32",
                "Mathf", "Time", "Random", "UnityObject", "SceneManager", "Scene",
                "Application", "Screen", "Input", "O5Input", "KeyCode",
                "Cursor", "CursorLockMode", "Camera", "Canvas", "AudioSource"
            )
        )
    );

    // ------------------------------------------------------------------
    // 8. Packages (.o5cp) & Modules
    // ------------------------------------------------------------------
    private static DocChapter BuildPackagesAndModules() => new(
        "packages",
        UISprite.Box128,
        T("패키지 (.o5cp) & 모듈", "Packages & Modules"),
        T("캔버스와 리소스를 하나로 묶는 .o5cp 패키지와 게임별 모듈 확장 구조를 다룹니다.",
          "Covers .o5cp canvas packages with bundled assets and game-specific OverlayerModule extensions."),
        DocSpecialKind.None,
        new DocSection(
            ".o5cp Canvas Packages & Presets",
            T("Overlayer 탭에서 캔버스를 .o5cp 파일로 내보내거나 불러올 수 있습니다. 내부는 일반 ZIP 아카이브입니다.",
              "Export or import canvases as .o5cp files from the Overlayer tab. Each .o5cp is a standard ZIP archive."),
            new DocTable(
                T("기능", "Feature"),
                T("설명", "Description"),
                (T("번들 리소스", "Bundled Assets"), T("manifest.json, 캔버스 JSON, 폰트, 텍스처, 스프라이트, JS 스크립트, 필요 모듈 정보를 한 파일에 담습니다.",
                                                       "Packs manifest.json, canvas JSON, fonts, textures, sprites, JS scripts, and module info into one file.")),
                (T("프리셋", "Presets"), T("Presets/ 폴더의 .o5cp 혹은 모듈이 등록한 프리셋을 Overlayer 탭이나 HUD 편집기 별 버튼에서 바로 불러올 수 있습니다.",
                                           "Load .o5cp files from Presets/ or module presets from the Overlayer tab or HUD Editor star menu.")),
                (T("편집 가능 캔버스로 복사", "Promote to Editable"), T("읽기 전용 패키지를 사용자 캔버스로 복사할 때 리소스 충돌이 나면 재사용, 가져오기, 이름 변경 중 하나를 고를 수 있습니다.",
                                                                     "When promoting a read-only package into an editable canvas, resolve asset conflicts via Reuse, Import, or Rename."))
            )
        ),
        new DocSection(
            "Game Module API",
            T("Overlayer 코어는 게임에 종속되지 않으며, 특정 게임 전용 태그, 패치, UI, 프리셋은 Overlayer.Module.ADOFAI 같은 별도 모듈에서 등록합니다.",
              "Overlayer Core stays game-agnostic; game-specific tags, patches, UI tabs, and presets live in separate OverlayerModule assemblies."),
            new DocCode(
                T("OverlayerModule 기본 구조 (C#)", "OverlayerModule Skeleton (C#)"),
                "public sealed class MyGameModule : OverlayerModule {\n"
                + "    public override string Name => \"MyGame\";\n"
                + "    public override string Author => \"Author\";\n"
                + "    public override string Version => \"1.0.0\";\n\n"
                + "    public override void OnInitialize() {\n"
                + "        _ = TagManager.RegisterAsync(typeof(MyGameModule).Assembly);\n"
                + "        PresetStore.Register(new PackagePreset { Id = \"module:MyGame:Default\", Name = \"Default\", Module = Name, ReadBytes = ... });\n"
                + "    }\n\n"
                + "    public override void OnDispose() {\n"
                + "        PresetStore.UnregisterByModule(Name);\n"
                + "    }\n"
                + "}",
                DocsHighlight.Mode.Plain
            ),
            new DocTable(
                T("확장 포인트", "Extension Point"),
                T("설명", "Description"),
                ("TagManager.RegisterAsync", T("어셈블리의 [Tag] 어트리뷰트가 붙은 메서드, 프로퍼티, 필드를 스캔해 등록합니다.",
                                               "Scans an assembly for [Tag]-attributed methods, properties, and fields and registers them.")),
                ("PresetStore.Register", T("ADOFAI 모듈의 Moon 프리셋처럼 모듈 전용 .o5cp 프리셋을 등록하거나 해제합니다.",
                                           "Registers or unregisters module-owned .o5cp presets such as ADOFAI's Moon preset.")),
                ("PlaybackState", T("플레이 중, 일시정지, 오토플레이 상태를 노출해 IsPlaying, IsPaused 태그와 HUD 조건에 사용합니다.",
                                    "Exposes playing, paused, and autoplay state for IsPlaying/IsPaused tags and HUD conditions.")),
                ("TextFontProvider", T("게임 기본 폰트를 Overlayer 텍스트의 기본 폰트로 등록합니다.",
                                       "Registers the game's default font for Overlayer text objects."))
            )
        )
    );
}
