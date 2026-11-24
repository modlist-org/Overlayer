using System;

namespace Overlayer.Resource;

/// <summary>
/// Monochrome vector icons rendered via TextMeshPro using build-time compiled TTF fonts.
/// </summary>
public enum UIIcon {
    // O5Kit shared icons (O5KitIcons.ttf)
    Circle,
    ToggleCircle,
    Triangle,
    X,

    // Overlayer icons (OverlayerIcons.ttf)
    AliBaseline,
    AliCapline,
    AliCenter,
    AliFlush,
    AliGeometry,
    AliJustified,
    AliLeft,
    AliMiddle,
    AliMidline,
    AliTop,
    AnchorCorner,
    AnchorSide,
    Book,
    Box,
    Canvas,
    Check,
    Clone,
    CodeBlock,
    Cube,
    DotInCircle,
    Download,
    Ease,
    Expand,
    EyeHide,
    EyeOpen,
    F,
    Gear,
    Grid,
    Image,
    LockOff,
    LockOn,
    MagnifyingGlass,
    Monitor,
    Ping,
    Plus,
    Power,
    Ruler,
    Star,
    Text,
    TurnArrow,
    Upload,
}

public static class UIIconGlyphs {
    public static bool IsO5Kit(UIIcon icon) => icon is UIIcon.Circle or UIIcon.ToggleCircle or UIIcon.Triangle or UIIcon.X;

    public static char GetGlyph(UIIcon icon) => icon switch {
        UIIcon.Circle => '\uE001',
        UIIcon.ToggleCircle => '\uE002',
        UIIcon.Triangle => '\uE003',
        UIIcon.X => '\uE004',

        UIIcon.AliBaseline => '\uE100',
        UIIcon.AliCapline => '\uE101',
        UIIcon.AliCenter => '\uE102',
        UIIcon.AliFlush => '\uE103',
        UIIcon.AliGeometry => '\uE104',
        UIIcon.AliJustified => '\uE105',
        UIIcon.AliLeft => '\uE106',
        UIIcon.AliMiddle => '\uE107',
        UIIcon.AliMidline => '\uE108',
        UIIcon.AliTop => '\uE109',
        UIIcon.AnchorCorner => '\uE10A',
        UIIcon.AnchorSide => '\uE10B',
        UIIcon.Book => '\uE10C',
        UIIcon.Box => '\uE10D',
        UIIcon.Canvas => '\uE10E',
        UIIcon.Check => '\uE10F',
        UIIcon.Clone => '\uE110',
        UIIcon.CodeBlock => '\uE111',
        UIIcon.Cube => '\uE112',
        UIIcon.DotInCircle => '\uE113',
        UIIcon.Download => '\uE114',
        UIIcon.Ease => '\uE115',
        UIIcon.Expand => '\uE116',
        UIIcon.EyeHide => '\uE117',
        UIIcon.EyeOpen => '\uE118',
        UIIcon.F => '\uE119',
        UIIcon.Gear => '\uE11A',
        UIIcon.Grid => '\uE11B',
        UIIcon.Image => '\uE11C',
        UIIcon.LockOff => '\uE11D',
        UIIcon.LockOn => '\uE11E',
        UIIcon.MagnifyingGlass => '\uE11F',
        UIIcon.Monitor => '\uE120',
        UIIcon.Ping => '\uE121',
        UIIcon.Plus => '\uE122',
        UIIcon.Power => '\uE123',
        UIIcon.Ruler => '\uE124',
        UIIcon.Star => '\uE125',
        UIIcon.Text => '\uE126',
        UIIcon.TurnArrow => '\uE127',
        UIIcon.Upload => '\uE128',
        _ => '\0',
    };

    public static bool TryParseLegacyName(string name, out UIIcon icon) {
        icon = default;
        if (string.IsNullOrWhiteSpace(name)) {
            return false;
        }
        string trimmed = name.Trim();
        switch (trimmed.ToLowerInvariant()) {
            case "toggle-on":
                icon = UIIcon.Circle;
                return true;
            case "toggle-off":
                icon = UIIcon.ToggleCircle;
                return true;
        }
        if (trimmed.EndsWith("128", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }
        return Enum.TryParse(trimmed, ignoreCase: true, out icon);
    }
}
