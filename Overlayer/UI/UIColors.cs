using O5Kit.Core;
using Overlayer.Compat;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.UI;

public readonly struct UIPalettePreset(string id, string langKey, string fallbackName, string hex) {
    public string Id { get; } = id;
    public string LangKey { get; } = langKey;
    public string FallbackName { get; } = fallbackName;
    public string Hex { get; } = hex;
}

public static class UIColors {
    public const string DefaultBaseHex = O5Palette.DefaultBaseHex;
    public const string StaticPresetId = "OverlayerStatic";
    public const string StaticConfigToken = "STATIC";
    public const string CustomPresetId = "Custom";
    public static Color DefaultBaseColor => O5Palette.DefaultBaseColor;
    public static Color StaticDisplayColor { get; } = new(0.569f, 0.604f, 1f, 1f);

    public static readonly UIPalettePreset[] Presets = [
        new("Overlayer", "UI_PALETTE_OVERLAYER", "Overlayer", DefaultBaseHex),
        new(StaticPresetId, "UI_PALETTE_OVERLAYER_STATIC", "Overlayer (Static)", StaticConfigToken),
        new("Slate", "UI_PALETTE_SLATE", "Slate", "A0A4B0"),
        new("Ocean", "UI_PALETTE_OCEAN", "Ocean", "4E9EF0"),
        new("Teal", "UI_PALETTE_TEAL", "Teal", "38BFA8"),
        new("Emerald", "UI_PALETTE_EMERALD", "Emerald", "47C478"),
        new("Amber", "UI_PALETTE_AMBER", "Amber", "E6A845"),
        new("Rose", "UI_PALETTE_ROSE", "Rose", "E8647A"),
        new("Violet", "UI_PALETTE_VIOLET", "Violet", "9B6BF0"),
    ];

    public static Color BaseColor { get; private set; } = DefaultBaseColor;
    public static string BaseHex { get; private set; } = DefaultBaseHex;
    public static bool IsStatic { get; private set; }

    public static event Action OnPaletteChanged;

    private static PaletteSnapshot current = PaletteSnapshot.From(DefaultBaseColor);

    public static Color ComponentHeader => current.ComponentHeader;
    public static Color ComponentPanel => current.ComponentPanel;
    public static Color ComponentFxOff => current.ComponentFxOff;
    public static Color ComponentFxEditor => current.ComponentFxEditor;
    public static Color FxField => current.FxField;
    public static Color FxOff => current.FxOff;
    public static Color FxOn => current.FxOn;
    public static Color PanelBG => current.PanelBG;

    public static Color TopBar => current.TopBar;
    public static Color WindowOutline => current.WindowOutline;

    public static Color MenuBG => current.MenuBG;
    public static Color MenuNormal => current.MenuNormal;
    public static Color MenuHover => current.MenuHover;
    public static Color MenuSelected => current.MenuSelected;
    public static Color MenuHighlight => current.MenuHighlight;

    public static Color ObjectBG => current.ObjectBG;
    public static Color ObjectActive => current.ObjectActive;
    public static Color ObjectInactive => current.ObjectInactive;
    public static Color ObjectActiveBright => current.ObjectActiveBright;
    public static Color ObjectActiveLightBright => current.ObjectActiveLightBright;
    public static Color ObjectButton => current.ObjectButton;
    public static Color ButtonHover => current.ButtonHover;
    public static Color ButtonPressed => current.ButtonPressed;

    public static Color Surface => current.Surface;
    public static Color SurfaceOpaque => current.SurfaceOpaque;
    public static Color DocsSidebarBg => current.DocsSidebarBg;
    public static Color DocsStageBg => current.DocsStageBg;
    public static Color DocsCardBg => current.DocsCardBg;
    public static Color DocsSurfaceBg => current.DocsSurfaceBg;
    public static Color DocsCodeBg => current.DocsCodeBg;
    public static Color DocsCodeHeaderBg => current.DocsCodeHeaderBg;
    public static Color DocsTextPrimary => current.DocsTextPrimary;
    public static Color DocsTextBody => current.DocsTextBody;
    public static Color DocsTextMuted => current.DocsTextMuted;
    public static Color DocsTextDim => current.DocsTextDim;
    public static Color AnchorCellHeader => current.AnchorCellHeader;
    public static Color AnchorCell => current.AnchorCell;

    public static readonly Color ObjectActiveMathOk = new(0.588f, 1f, 0.569f, 1f);
    public static readonly Color ObjectActiveMathWarn = new(1f, 0.898f, 0.569f, 1f);
    public static readonly Color ObjectActiveMathErr = new(1f, 0.569f, 0.569f, 1f);
    public static readonly Color SoftRed = new(0.886f, 0.404f, 0.427f, 1f);

    public static bool IsStaticToken(string value)
        => string.Equals(value?.Trim(), StaticConfigToken, StringComparison.OrdinalIgnoreCase);

    public static Color ParseBaseColor(string hex)
        => O5Palette.TryParseHexRgb(hex, out Color parsed) ? parsed : DefaultBaseColor;

    public static string ToHexRgb(Color color) => O5Palette.ToHexRgb(color);

    public static string MatchPresetId(Color color) {
        string hex = ToHexRgb(color);
        for (int i = 0; i < Presets.Length; i++) {
            if (!IsStaticToken(Presets[i].Hex) && string.Equals(Presets[i].Hex, hex, StringComparison.OrdinalIgnoreCase)) {
                return Presets[i].Id;
            }
        }
        return CustomPresetId;
    }

    public static string MatchConfiguredPresetId(string value) {
        if (IsStaticToken(value)) {
            return StaticPresetId;
        }
        if (O5Palette.TryParseHexRgb(value, out Color parsed)) {
            return MatchPresetId(parsed);
        }
        return Presets[0].Id;
    }

    public static bool TryGetPresetColor(string presetId, out Color color) {
        for (int i = 0; i < Presets.Length; i++) {
            if (!IsStaticToken(Presets[i].Hex) && string.Equals(Presets[i].Id, presetId, StringComparison.OrdinalIgnoreCase)) {
                color = ParseBaseColor(Presets[i].Hex);
                return true;
            }
        }
        color = DefaultBaseColor;
        return false;
    }

    public static O5Theme BuildTheme(Color baseColor)
        => O5Palette.FromMonochrome(baseColor) with {
            ControlOutlineIdle = new Color32(255, 255, 255, 0),
        };

    public static O5Theme BuildStaticTheme()
        => O5Theme.OverlayerStatic with {
            ControlOutlineIdle = new Color32(255, 255, 255, 0),
        };

    public static O5Theme BuildCurrentTheme() => IsStatic ? BuildStaticTheme() : BuildTheme(BaseColor);

    private static readonly Dictionary<int, PaletteSnapshot> pendingPageSnapshots = [];
    private static int trackedCanvasId;
    private static bool pageSwitchHooked;

    private static void EnsurePageSwitchHook() {
        if (pageSwitchHooked) {
            return;
        }
        pageSwitchHooked = true;
        Factory.MenuFactory.OnStateChanged += OnPageSwitched;
    }

    private static void OnPageSwitched(int state) {
        if (!pendingPageSnapshots.TryGetValue(state, out var oldSnap)) {
            return;
        }
        pendingPageSnapshots.Remove(state);
        if (MaxDiffRgba(oldSnap.ObjectActive, current.ObjectActive) < 0.0005f
            && MaxDiffRgba(oldSnap.PanelBG, current.PanelBG) < 0.0005f) {
            return;
        }
        if (UICore.Pages != null && UICore.Pages.TryGetValue(state, out var page) && page != null) {
            RemapSubtree(page.gameObject, oldSnap, current);
        }
    }

    public static void ApplyBaseColor(Color baseColor, bool refreshLiveUI = true) {
        Color opaque = new(Mathf.Clamp01(baseColor.r), Mathf.Clamp01(baseColor.g), Mathf.Clamp01(baseColor.b), 1f);
        if (!IsStatic && MaxDiffRgb(BaseColor, opaque) < 0.0005f) {
            return;
        }
        PaletteSnapshot prev = current;
        O5Theme theme = BuildTheme(opaque);
        IsStatic = false;
        BaseColor = opaque;
        BaseHex = ToHexRgb(opaque);
        current = PaletteSnapshot.From(opaque, theme);

        if (refreshLiveUI) {
            // Remap active UI immediately and defer hidden pages until opened.
            RemapLiveTree(UICore.CanvasObj, prev, current);
        }

        if (O5KitAdapters.Ctx != null) {
            O5KitAdapters.Ctx.SetTheme(theme);
        }

        if (refreshLiveUI) {
            OnPaletteChanged?.Invoke();
        }
    }

    public static void ApplyStatic(bool refreshLiveUI = true) {
        if (IsStatic) {
            return;
        }
        PaletteSnapshot prev = current;
        O5Theme theme = BuildStaticTheme();
        IsStatic = true;
        BaseColor = StaticDisplayColor;
        BaseHex = StaticConfigToken;
        current = PaletteSnapshot.Static();

        if (refreshLiveUI) {
            RemapLiveTree(UICore.CanvasObj, prev, current);
        }

        if (O5KitAdapters.Ctx != null) {
            O5KitAdapters.Ctx.SetTheme(theme);
        }

        if (refreshLiveUI) {
            OnPaletteChanged?.Invoke();
        }
    }

    public static void ApplyConfigured(string value, bool refreshLiveUI = true) {
        if (IsStaticToken(value)) {
            ApplyStatic(refreshLiveUI);
        } else if (O5Palette.TryParseHexRgb(value, out Color parsed)) {
            ApplyBaseColor(parsed, refreshLiveUI);
        } else {
            ApplyBaseColor(DefaultBaseColor, refreshLiveUI);
        }
    }

    public static void ApplyBaseHex(string hex, bool refreshLiveUI = true)
        => ApplyConfigured(hex, refreshLiveUI);

    internal static void RemapLiveTree(GameObject root, PaletteSnapshot prev, PaletteSnapshot next) {
        if (root == null) {
            return;
        }

        // If this is the main UI canvas, only remap active pages now and defer inactive pages until opened.
        if (UICore.CanvasObj != null && root == UICore.CanvasObj && UICore.Pages != null && UICore.Pages.Count > 0) {
            EnsurePageSwitchHook();
            int cid = root.GetInstanceID();
            if (cid != trackedCanvasId) {
                trackedCanvasId = cid;
                pendingPageSnapshots.Clear();
            }

            foreach (var kv in UICore.Pages) {
                var page = kv.Value;
                if (page == null) {
                    continue;
                }
                if (page.gameObject.activeSelf) {
                    pendingPageSnapshots.Remove(kv.Key);
                } else if (!pendingPageSnapshots.ContainsKey(kv.Key)) {
                    pendingPageSnapshots[kv.Key] = prev;
                }
            }

            (Color Old, Color New)[] pairs = prev.MapTo(next);
            var skipCache = new Dictionary<int, bool>(64);
            RemapCanvasBranch(root.transform, Factory.PageFactory.PagesContaner, pairs, skipCache);
            return;
        }

        RemapSubtree(root, prev, next);
    }

    private static void RemapCanvasBranch(Transform node, Transform pagesContainer, (Color Old, Color New)[] pairs, Dictionary<int, bool> skipCache) {
        if (node == null || !node.gameObject.activeSelf) {
            return;
        }
        if (pagesContainer != null && node == pagesContainer) {
            for (int i = 0; i < node.childCount; i++) {
                var pageTr = node.GetChild(i);
                if (pageTr != null && pageTr.gameObject.activeSelf) {
                    RemapSubtree(pageTr.gameObject, pairs, skipCache);
                }
            }
            return;
        }
        if (pagesContainer != null && pagesContainer.IsChildOf(node)) {
            RemapSingleGraphic(node.GetComponent<Graphic>(), pairs, skipCache);
            for (int i = 0; i < node.childCount; i++) {
                RemapCanvasBranch(node.GetChild(i), pagesContainer, pairs, skipCache);
            }
            return;
        }
        RemapSubtree(node.gameObject, pairs, skipCache);
    }

    private static void RemapSubtree(GameObject root, PaletteSnapshot prev, PaletteSnapshot next) {
        if (root == null) {
            return;
        }
        RemapSubtree(root, prev.MapTo(next), new Dictionary<int, bool>(64));
    }

    private static void RemapSubtree(GameObject root, (Color Old, Color New)[] pairs, Dictionary<int, bool> skipCache) {
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int idx = 0; idx < graphics.Length; idx++) {
            RemapSingleGraphic(graphics[idx], pairs, skipCache);
        }
    }

    private static void RemapSingleGraphic(Graphic g, (Color Old, Color New)[] pairs, Dictionary<int, bool> skipCache) {
        if (g == null || g is RawImage) {
            return;
        }

        const float epsilon = 0.0045f;
        Color c = g.color;
        int bestExact = -1;
        float bestExactDist = epsilon;
        for (int i = 0; i < pairs.Length; i++) {
            float d = MaxDiffRgba(c, pairs[i].Old);
            if (d < bestExactDist) {
                bestExactDist = d;
                bestExact = i;
            }
        }

        Color targetColor;
        if (bestExact >= 0) {
            targetColor = pairs[bestExact].New;
        } else if (c.a > 0.001f) {
            // RGB-only match (preserves alpha for translucent overlays derived from a palette role)
            int bestRgb = -1;
            float bestRgbDist = epsilon;
            for (int i = 0; i < pairs.Length; i++) {
                if (pairs[i].Old.a < 0.99f) {
                    continue;
                }
                float d = MaxDiffRgb(c, pairs[i].Old);
                if (d < bestRgbDist) {
                    bestRgbDist = d;
                    bestRgb = i;
                }
            }
            if (bestRgb < 0) {
                return;
            }
            targetColor = O5Palette.WithAlpha(pairs[bestRgb].New, c.a);
        } else {
            return;
        }

        if (MaxDiffRgba(c, targetColor) < 0.0005f || ShouldSkipGraphic(g, skipCache)) {
            return;
        }
        g.color = targetColor;
    }

    private static float MaxDiffRgba(Color a, Color b) {
        float dr = Mathf.Abs(a.r - b.r);
        float dg = Mathf.Abs(a.g - b.g);
        float db = Mathf.Abs(a.b - b.b);
        float da = Mathf.Abs(a.a - b.a);
        return Mathf.Max(Mathf.Max(dr, dg), Mathf.Max(db, da));
    }

    private static float MaxDiffRgb(Color a, Color b) {
        float dr = Mathf.Abs(a.r - b.r);
        float dg = Mathf.Abs(a.g - b.g);
        float db = Mathf.Abs(a.b - b.b);
        return Mathf.Max(dr, Mathf.Max(dg, db));
    }

    private static bool ShouldSkipGraphic(Graphic g, Dictionary<int, bool> skipCache) {
        string name = g.gameObject.name;
        if (name == "Preview" || name == "Wheel" || name == "Thumbnail" || name == "PreviewImage"
            || name == "Logo" || name == "LogoIcon") {
            return true;
        }
        return HasSkippedAncestor(g.transform.parent, skipCache);
    }

    private static bool HasSkippedAncestor(Transform p, Dictionary<int, bool> skipCache) {
        if (p == null) {
            return false;
        }
        int id = p.GetInstanceID();
        if (skipCache.TryGetValue(id, out bool cached)) {
            return cached;
        }
        string pn = p.name;
        bool skip = pn == "PortraitMask" || pn == "PreviewImage" || pn == "Wheel"
            || HasSkippedAncestor(p.parent, skipCache);
        skipCache[id] = skip;
        return skip;
    }

    internal readonly struct PaletteSnapshot {
        public readonly Color ComponentHeader;
        public readonly Color ComponentPanel;
        public readonly Color ComponentFxOff;
        public readonly Color ComponentFxEditor;
        public readonly Color FxField;
        public readonly Color FxOff;
        public readonly Color FxOn;
        public readonly Color PanelBG;
        public readonly Color TopBar;
        public readonly Color WindowOutline;
        public readonly Color MenuBG;
        public readonly Color MenuNormal;
        public readonly Color MenuHover;
        public readonly Color MenuSelected;
        public readonly Color MenuHighlight;
        public readonly Color ObjectBG;
        public readonly Color ObjectActive;
        public readonly Color ObjectInactive;
        public readonly Color ObjectActiveBright;
        public readonly Color ObjectActiveLightBright;
        public readonly Color ObjectButton;
        public readonly Color ButtonHover;
        public readonly Color ButtonPressed;
        public readonly Color Surface;
        public readonly Color SurfaceOpaque;
        public readonly Color DocsSidebarBg;
        public readonly Color DocsStageBg;
        public readonly Color DocsCardBg;
        public readonly Color DocsSurfaceBg;
        public readonly Color DocsCodeBg;
        public readonly Color DocsCodeHeaderBg;
        public readonly Color DocsTextPrimary;
        public readonly Color DocsTextBody;
        public readonly Color DocsTextMuted;
        public readonly Color DocsTextDim;
        public readonly Color AnchorCellHeader;
        public readonly Color AnchorCell;

        private PaletteSnapshot(Color baseColor, O5Theme theme) {
            PanelBG = theme.PanelBG;
            TopBar = theme.TopBar;
            WindowOutline = theme.Outline;
            MenuBG = theme.MenuBG;
            ObjectBG = theme.ObjectBG;
            ObjectButton = theme.ObjectButton;
            ButtonHover = theme.ButtonHover;
            ButtonPressed = theme.ButtonPressed;
            ObjectActive = theme.ObjectActive;
            ObjectInactive = theme.ObjectInactive;
            ObjectActiveBright = theme.ObjectActiveBright;
            ObjectActiveLightBright = theme.ButtonHover;
            ComponentHeader = theme.CardHeader;
            ComponentPanel = theme.CardPanel;

            ComponentFxOff = O5Palette.ShadeOklch(baseColor, 0.022f, 0.220f);
            ComponentFxEditor = O5Palette.ShadeOklch(baseColor, 0.014f, 0.165f);
            FxField = O5Palette.ShadeOklch(baseColor, 0.032f, 0.270f);
            FxOff = O5Palette.ShadeOklch(baseColor, 0.068f, 0.370f, 1f, adaptBaseLightness: true);
            FxOn = ObjectActive;

            MenuNormal = O5Palette.Transparent(theme.MenuHover);
            MenuHover = theme.MenuHover;
            MenuSelected = O5Palette.WithAlpha(ObjectActive, 0.92f);
            MenuHighlight = ObjectActiveBright;

            SurfaceOpaque = O5Palette.ShadeOklch(baseColor, 0.015f, 0.175f);
            Surface = O5Palette.WithAlpha(SurfaceOpaque, 0.95f);

            DocsSidebarBg = O5Palette.ShadeOklch(baseColor, 0.015f, 0.168f);
            DocsStageBg = PanelBG;
            DocsCardBg = ComponentPanel;
            DocsSurfaceBg = SurfaceOpaque;
            DocsCodeBg = ComponentFxEditor;
            DocsCodeHeaderBg = O5Palette.ShadeOklch(baseColor, 0.020f, 0.195f);
            DocsTextPrimary = theme.Text;
            DocsTextBody = O5Palette.ShadeOklch(baseColor, 0.018f, 0.895f);
            DocsTextMuted = O5Palette.ShadeOklch(baseColor, 0.026f, 0.690f);
            DocsTextDim = O5Palette.ShadeOklch(baseColor, 0.028f, 0.540f);

            AnchorCellHeader = O5Palette.ShadeOklch(baseColor, 0.022f, 0.225f);
            AnchorCell = O5Palette.ShadeOklch(baseColor, 0.015f, 0.180f);
        }

        private PaletteSnapshot(
            Color componentHeader,
            Color componentPanel,
            Color componentFxOff,
            Color componentFxEditor,
            Color fxField,
            Color fxOff,
            Color fxOn,
            Color panelBG,
            Color topBar,
            Color windowOutline,
            Color menuBG,
            Color menuNormal,
            Color menuHover,
            Color menuSelected,
            Color menuHighlight,
            Color objectBG,
            Color objectActive,
            Color objectInactive,
            Color objectActiveBright,
            Color objectActiveLightBright,
            Color objectButton,
            Color buttonHover,
            Color buttonPressed,
            Color surface,
            Color surfaceOpaque,
            Color docsSidebarBg,
            Color docsStageBg,
            Color docsCardBg,
            Color docsSurfaceBg,
            Color docsCodeBg,
            Color docsCodeHeaderBg,
            Color docsTextPrimary,
            Color docsTextBody,
            Color docsTextMuted,
            Color docsTextDim,
            Color anchorCellHeader,
            Color anchorCell
        ) {
            ComponentHeader = componentHeader;
            ComponentPanel = componentPanel;
            ComponentFxOff = componentFxOff;
            ComponentFxEditor = componentFxEditor;
            FxField = fxField;
            FxOff = fxOff;
            FxOn = fxOn;
            PanelBG = panelBG;
            TopBar = topBar;
            WindowOutline = windowOutline;
            MenuBG = menuBG;
            MenuNormal = menuNormal;
            MenuHover = menuHover;
            MenuSelected = menuSelected;
            MenuHighlight = menuHighlight;
            ObjectBG = objectBG;
            ObjectActive = objectActive;
            ObjectInactive = objectInactive;
            ObjectActiveBright = objectActiveBright;
            ObjectActiveLightBright = objectActiveLightBright;
            ObjectButton = objectButton;
            ButtonHover = buttonHover;
            ButtonPressed = buttonPressed;
            Surface = surface;
            SurfaceOpaque = surfaceOpaque;
            DocsSidebarBg = docsSidebarBg;
            DocsStageBg = docsStageBg;
            DocsCardBg = docsCardBg;
            DocsSurfaceBg = docsSurfaceBg;
            DocsCodeBg = docsCodeBg;
            DocsCodeHeaderBg = docsCodeHeaderBg;
            DocsTextPrimary = docsTextPrimary;
            DocsTextBody = docsTextBody;
            DocsTextMuted = docsTextMuted;
            DocsTextDim = docsTextDim;
            AnchorCellHeader = anchorCellHeader;
            AnchorCell = anchorCell;
        }

        public static PaletteSnapshot From(Color baseColor) => new(baseColor, O5Palette.FromMonochrome(baseColor));
        public static PaletteSnapshot From(Color baseColor, O5Theme theme) => new(baseColor, theme);

        public static PaletteSnapshot Static() {
            PaletteSnapshot fb = From(DefaultBaseColor);
            return new(
                componentHeader: new Color32(76, 77, 102, 255),
                componentPanel: new Color32(47, 46, 58, 255),
                componentFxOff: new Color32(43, 44, 61, 255),
                componentFxEditor: new Color32(39, 40, 54, 255),
                fxField: new Color32(61, 60, 74, 255),
                fxOff: new Color32(101, 101, 144, 255),
                fxOn: new Color32(148, 148, 255, 255),
                panelBG: new Color(0.165f, 0.161f, 0.196f, 1f),
                topBar: new Color(0.255f, 0.259f, 0.333f, 1f),
                windowOutline: fb.WindowOutline,
                menuBG: new Color(0.42f, 0.431f, 0.545f, 1f),
                menuNormal: new Color(0.635f, 0.655f, 0.878f, 0f),
                menuHover: new Color(0.635f, 0.655f, 0.878f, 0.4f),
                menuSelected: new Color(0.635f, 0.655f, 0.878f, 1f),
                menuHighlight: new Color(0.824f, 0.835f, 0.965f, 1f),
                objectBG: new Color(0.235f, 0.227f, 0.294f, 1f),
                objectActive: new Color(0.569f, 0.604f, 1f, 1f),
                objectInactive: new Color(0.569f, 0.604f, 1f, 0.4f),
                objectActiveBright: new Color(0.812f, 0.827f, 1f, 1f),
                objectActiveLightBright: new Color(0.557f, 0.596f, 1f, 1f),
                objectButton: new Color(0.478f, 0.514f, 0.875f, 1f),
                buttonHover: new Color(0.612f, 0.639f, 0.925f, 1f),
                buttonPressed: new Color(0.812f, 0.827f, 1f, 1f),
                surface: fb.Surface,
                surfaceOpaque: fb.SurfaceOpaque,
                docsSidebarBg: fb.DocsSidebarBg,
                docsStageBg: fb.DocsStageBg,
                docsCardBg: fb.DocsCardBg,
                docsSurfaceBg: fb.DocsSurfaceBg,
                docsCodeBg: fb.DocsCodeBg,
                docsCodeHeaderBg: fb.DocsCodeHeaderBg,
                docsTextPrimary: fb.DocsTextPrimary,
                docsTextBody: fb.DocsTextBody,
                docsTextMuted: fb.DocsTextMuted,
                docsTextDim: fb.DocsTextDim,
                anchorCellHeader: fb.AnchorCellHeader,
                anchorCell: fb.AnchorCell
            );
        }

        public (Color Old, Color New)[] MapTo(PaletteSnapshot next) => [
            (PanelBG, next.PanelBG),
            (TopBar, next.TopBar),
            (WindowOutline, next.WindowOutline),
            (MenuBG, next.MenuBG),
            (MenuHover, next.MenuHover),
            (MenuSelected, next.MenuSelected),
            (MenuHighlight, next.MenuHighlight),
            (ObjectBG, next.ObjectBG),
            (ObjectButton, next.ObjectButton),
            (ButtonHover, next.ButtonHover),
            (ButtonPressed, next.ButtonPressed),
            (ObjectActive, next.ObjectActive),
            (ObjectInactive, next.ObjectInactive),
            (ObjectActiveBright, next.ObjectActiveBright),
            (ComponentHeader, next.ComponentHeader),
            (ComponentPanel, next.ComponentPanel),
            (ComponentFxOff, next.ComponentFxOff),
            (ComponentFxEditor, next.ComponentFxEditor),
            (FxField, next.FxField),
            (FxOff, next.FxOff),
            (Surface, next.Surface),
            (SurfaceOpaque, next.SurfaceOpaque),
            (DocsSidebarBg, next.DocsSidebarBg),
            (DocsCodeHeaderBg, next.DocsCodeHeaderBg),
            (DocsTextPrimary, next.DocsTextPrimary),
            (DocsTextBody, next.DocsTextBody),
            (DocsTextMuted, next.DocsTextMuted),
            (DocsTextDim, next.DocsTextDim),
            (AnchorCellHeader, next.AnchorCellHeader),
            (AnchorCell, next.AnchorCell),
        ];
    }
}
