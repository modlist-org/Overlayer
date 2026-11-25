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
    UIIcon? icon,
    UISprite? spriteIcon,
    string title,
    string subtitle,
    DocSpecialKind special,
    params DocSection[] sections
) {
    public readonly string Id = id;
    public readonly UIIcon? Icon = icon;
    public readonly UISprite? SpriteIcon = spriteIcon;
    public readonly string Title = title;
    public readonly string Subtitle = subtitle;
    public readonly DocSpecialKind Special = special;
    public readonly DocSection[] Sections = sections;
}

/// <summary>
/// Built-in Overlayer V5 wiki content grounded directly in the codebase.
/// English strings live inline as fallbacks; localized translations are loaded
/// via MainCore.Tr from Lang/Docs/*.json (e.g., ko-KR.json, zh-CN.json).
/// </summary>
internal static partial class DocsContent {
    public static bool IsKorean =>
        string.Equals(Core.MainCore.Tr?.Language, "ko-KR", StringComparison.OrdinalIgnoreCase);

    public static string Tr(string key, string fallback) =>
        Core.MainCore.Tr != null ? Core.MainCore.Tr.Get(key, fallback) : fallback;

    public static string TrFmt(string key, string fallback, params object[] args) {
        string template = Tr(key, fallback);
        try {
            return string.Format(template, args);
        } catch {
            return string.Format(fallback, args);
        }
    }

    public static string TrKeys(string key, string fallback) {
        string text = Tr(key, fallback);
        if (text.IndexOf('{') < 0) {
            return text;
        }
        return text
            .Replace("{WindowKey}", UICore.WindowKey.ToDisplayString())
            .Replace("{HudKey}", HudEdit.HudEditor.Key.ToDisplayString());
    }

    public static DocChapter[] BuildChapters() => [
        BuildOverview(),
        BuildWorkspace(),
        BuildTagsAndText(),
        BuildTagExplorer(),
        BuildHudEditor(),
        BuildComponentsAndFx(),
        BuildEasingAndCurves(),
        BuildJavaScriptApi(),
        BuildPackagesAndModules(),
    ];
}
