using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Core;
using Overlayer.IO.User;
using Overlayer.Package;
using Overlayer.Resource;
using O5Kit.Control;
using Overlayer.UI.Objects.Impl;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Compat;

public sealed class OverlayerSpriteProvider : ISpriteProvider {
    public Sprite RoundedPanel => MainCore.Spr.Get(UISliceSprite.Circle128P512);
    public Sprite RoundedControl => MainCore.Spr.Get(UISliceSprite.Circle128P1024);
    public Sprite TopBar => MainCore.Spr.Get(UISliceSprite.CircleHalf128P512);
    public Sprite RoundedOutline => MainCore.Spr.Get(UISliceSprite.CircleOutline128O32P1024);
    public Sprite Circle => MainCore.Spr.Get(UISprite.Circle128);
}

public sealed class OverlayerFontProvider : IFontProvider {
    private const string BuiltinFallback = "@builtin-default";
    private static readonly Dictionary<string, TMP_FontAsset> compositeCache = [];
    private TMP_FontAsset _regular;
    private TMP_FontAsset _medium;
    private TMP_FontAsset _monospace;
    private bool _useUserResources;

    public TMP_FontAsset Regular {
        get {
            _regular = ResolveSlot("regular", Asset.SUIT_Regular, EffectiveRegularKey(), MainCore.Conf.SystemFontFallbacks.Value);
            AttachBoldWeight(_regular, Medium);
            return _regular;
        }
    }
    public TMP_FontAsset Medium => _medium = ResolveSlot(
        "medium", Asset.SUIT_Medium, EffectiveMediumKey(), EffectiveMediumFallbacks());
    public TMP_FontAsset Monospace => _monospace = ResolveSlot(
        "monospace", Asset.JetBrainsMonoNL_Medium, MainCore.Conf.CodeFontKey.Value, MainCore.Conf.CodeFontFallbacks.Value);
    public TMP_FontAsset Icons => MainCore.Icons?.O5KitFont;

    private static List<string> EffectiveMediumFallbacks() {
        var med = MainCore.Conf.SystemMediumFontFallbacks.Value;
        var reg = MainCore.Conf.SystemFontFallbacks.Value;
        var combined = new List<string>();
        if (med != null) {
            foreach (string k in med) {
                if (!string.IsNullOrEmpty(k) && !combined.Contains(k)) {
                    combined.Add(k);
                }
            }
        }
        // Fall back to Regular and Regular's fallbacks if missing from Medium.
        string regPrimary = EffectiveRegularKey();
        if (!string.IsNullOrEmpty(regPrimary) && !combined.Contains(regPrimary)) {
            combined.Add(regPrimary);
        }
        if (reg != null) {
            foreach (string k in reg) {
                if (!string.IsNullOrEmpty(k) && !combined.Contains(k)) {
                    combined.Add(k);
                }
            }
        }
        return combined;
    }

    private static void AttachBoldWeight(TMP_FontAsset regular, TMP_FontAsset medium) {
        if (regular == null || medium == null) {
            return;
        }
        try {
            var table = regular.fontWeightTable;
            if (table != null && table.Length > 7) {
                var pair = table[7];
                pair.regularTypeface = medium;
                table[7] = pair;
            }
        } catch {
        }
    }

    private static string EffectiveRegularKey() {
        string reg = MainCore.Conf.SystemFontKey.Value;
        // Medium alone is ignored unless Regular is also set.
        return string.IsNullOrEmpty(reg) ? null : reg;
    }

    private static string EffectiveMediumKey() {
        string reg = MainCore.Conf.SystemFontKey.Value;
        if (string.IsNullOrEmpty(reg)) {
            return null;
        }
        string med = MainCore.Conf.SystemMediumFontKey.Value;
        return string.IsNullOrEmpty(med) ? reg : med;
    }

    public void SetUseUserResources(bool enabled) {
        if (_useUserResources == enabled) {
            return;
        }
        var oldFonts = new[] { _regular, _medium, _monospace };
        _useUserResources = enabled;
        var newFonts = new[] { Regular, Medium, Monospace };
        if (Overlayer.UI.UICore.CanvasObj != null) {
            var directUserFonts = new HashSet<TMP_FontAsset>();
            if (!enabled) {
                foreach (string key in UserResourceManager.Fnt.Keys) {
                    if (O5cpFormat.IsPackageKey(key)) {
                        continue;
                    }
                    if (UserResourceManager.Fnt.TryGet(key, out var userFont) && userFont != null) {
                        directUserFonts.Add(userFont);
                    }
                }
            }
            foreach (var text in Overlayer.UI.UICore.CanvasObj.GetComponentsInChildren<TMP_Text>(true)) {
                if (text == null || text.font == null) {
                    continue;
                }
                for (int i = 0; i < oldFonts.Length; i++) {
                    if (oldFonts[i] != null && text.font == oldFonts[i]) {
                        text.font = newFonts[i];
                        break;
                    }
                }
                if (!enabled && directUserFonts.Contains(text.font)) {
                    text.font = newFonts[0];
                }
            }
        }
        if (!enabled) {
            foreach (var font in compositeCache.Values) {
                if (font != null) {
                    try { UnityEngine.Object.Destroy(font); } catch { }
                }
            }
            compositeCache.Clear();
        }
    }

    public void RefreshExistingText() {
        TMP_FontAsset oldRegular = _regular;
        TMP_FontAsset oldMedium = _medium;
        TMP_FontAsset oldMonospace = _monospace;
        TMP_FontAsset newRegular = Regular;
        TMP_FontAsset newMedium = Medium;
        TMP_FontAsset newMonospace = Monospace;
        if (Overlayer.UI.UICore.CanvasObj != null) {
            foreach (var text in Overlayer.UI.UICore.CanvasObj.GetComponentsInChildren<TMP_Text>(true)) {
                if (text == null || text.font == null) {
                    continue;
                }
                if (oldMonospace != null && text.font == oldMonospace) {
                    text.font = newMonospace;
                } else if (oldMedium != null && text.font == oldMedium) {
                    text.font = newMedium;
                } else if (oldRegular != null && text.font == oldRegular) {
                    text.font = newRegular;
                }
            }
        }
    }

    private TMP_FontAsset ResolveSlot(string slot, Asset builtinAsset, string primaryKey, List<string> fallbackKeys) {
        TMP_FontAsset builtin = MainCore.Res.Get<TMP_FontAsset>(builtinAsset);
        Asset? cjkAsset = slot switch {
            "regular" => Asset.OverlayerSC_Regular,
            "medium" => Asset.OverlayerSC_Medium,
            _ => null
        };
        TMP_FontAsset cjk = cjkAsset.HasValue ? MainCore.Res.Get<TMP_FontAsset>(cjkAsset.Value) : null;
        if (_useUserResources) {
            return WithFallbacks(slot, primaryKey, fallbackKeys, () => builtin, cjk);
        }
        return builtin;
    }

    private static TMP_FontAsset WithFallbacks(string slot, string primaryKey, List<string> fallbackKeys, Func<TMP_FontAsset> builtin, TMP_FontAsset cjk) {
        TMP_FontAsset fallback = builtin();
        var chain = new List<TMP_FontAsset>();
        TMP_FontAsset primary = Resolve(primaryKey);
        chain.Add(primary ?? fallback);
        if (fallbackKeys != null) {
            foreach (string key in fallbackKeys) {
                if (string.IsNullOrEmpty(key) || key == primaryKey || chain.Count >= 9) {
                    continue;
                }
                TMP_FontAsset asset = key == BuiltinFallback ? fallback : Resolve(key);
                if (asset != null && !chain.Contains(asset)) {
                    chain.Add(asset);
                }
            }
        }
        if (chain.Count == 0) {
            return fallback;
        }
        if (!chain.Contains(fallback) && fallback != null) {
            chain.Add(fallback);
        }
        if (cjk != null && !chain.Contains(cjk)) {
            chain.Add(cjk);
        }
        // The builtin already carries the bundled CJK fallback (ResourceManager attaches
        // it per weight). Instantiating it here would clone the dynamic atlas, and the
        // clone's glyph packing would overlap the original's.
        if (chain.Count == 2 && chain[0] == fallback && chain[1] == cjk
            && fallback.fallbackFontAssetTable != null && fallback.fallbackFontAssetTable.Contains(cjk)) {
            return fallback;
        }
        if (chain.Count == 1) {
            return chain[0];
        }
        string sig = slot + ":" + string.Join(",", chain.Select(a => a.GetInstanceID()));
        if (compositeCache.TryGetValue(sig, out var composite) && composite != null) {
            return composite;
        }
        // Never clone a dynamic font asset: Instantiate shares its atlas texture, and
        // both copies then pack glyphs into the same texture with separate free lists.
        // Rebuild from the source font instead so the composite owns its atlas.
        Font source = null;
        try {
            source = chain[0].sourceFontFile;
        } catch {
        }
        composite = source != null ? ResourceManager.CreateRuntimeFontAsset(source) : null;
        if (composite == null) {
            composite = UnityEngine.Object.Instantiate(chain[0]);
        }
        composite.name = chain[0].name + "_WithFallbacks";
#if ML && IL2CPP
        var fallbackList = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
        foreach (var fb in chain.Skip(1)) {
            fallbackList.Add(fb);
        }
        composite.fallbackFontAssetTable = fallbackList;
#else
        composite.fallbackFontAssetTable = [.. chain.Skip(1)];
#endif
        compositeCache[sig] = composite;
        return composite;
    }

    private static TMP_FontAsset Resolve(string key) {
        if (string.IsNullOrEmpty(key)) {
            return null;
        }
        try {
            return UserResourceManager.TryGetFont(key, out var font) ? font : null;
        } catch {
            return null;
        }
    }
}

public static class O5KitAdapters {
    private static bool _wired;
    private static Action<bool, bool> _enabledHandler;
    private static OverlayerFontProvider _fonts;

    /// <summary>Overlayer's owned O5Kit instance. Pass this to every O5Factory call.</summary>
    public static O5Context Ctx { get; private set; } = null!;

    public static void Setup() {
#if ML && IL2CPP
        RegisterO5KitBehaviours();
#endif
        _fonts = new OverlayerFontProvider();
        UI.UIColors.ApplyConfigured(MainCore.Conf.UIBaseColor.Value, refreshLiveUI: false);
        Ctx = new O5Context(
            config: new O5Config(),
            theme: UI.UIColors.BuildCurrentTheme(),
            sprites: new OverlayerSpriteProvider(),
            fonts: _fonts,
            shapes: MainCore.Spr.Shapes);
        SyncConfig();

        O5ShortcutManager.IsSuspended = () =>
            O5Kit.Control.O5InputBlocker.IsEditing
            || UI.Utility.KeyCapture.SuppressShortcuts
            || Overlayer.Setup.SetupIntro.IsBlocking;
        O5Kit.Control.O5Slider.EnableWheelAdjust = () => WheelAdjust;
        O5Kit.Control.O5Slider.OnAnyWheelChanged = s => UI.UISound.PlaySliderTick((float)s.Normalize());
        O5Kit.Control.O5Dropdown.EnableWheelAdjust = () => WheelAdjust;
        O5Kit.Control.O5Dropdown.OnAnyWheelChanged = UI.UISound.PlayDropdownPick;
        O5Kit.Control.O5UiEvents.OnDropdownToggled = UI.UISound.PlayDropdownToggle;
        O5Kit.Control.O5UiEvents.OnDropdownPicked = UI.UISound.PlayDropdownPick;
        O5Kit.Control.O5UiEvents.OnToggleChanged = UI.UISound.PlayToggle;
        O5Kit.Behaviour.UIScrollController.ShouldConsumeParentScroll = () =>
            UICodeInputField.ShouldConsumeParentScroll || (WheelAdjust && O5Kit.Control.O5Slider.AnyHovered)
            || O5Kit.Control.O5Dropdown.AnyWheelTarget;
        if (!_wired) {
            _wired = true;
            _enabledHandler = (enabled, _) => Ctx.NotifyEnabledChanged(enabled);
            MainCore.OnModEnabledChanged += _enabledHandler;
        }
    }

    public static void RefreshFonts() => _fonts?.RefreshExistingText();

#if ML && IL2CPP
    private static bool _behavioursRegistered;

    /// <summary>
    /// UserLibs assemblies are not scanned for <c>[RegisterTypeInIl2Cpp]</c>,
    /// so O5Kit behaviours are registered explicitly before any AddComponent
    /// call. Runs once per process.
    /// </summary>
    private static void RegisterO5KitBehaviours() {
        if (_behavioursRegistered) {
            return;
        }
        _behavioursRegistered = true;
        try {
            O5Kit.Core.O5Context.RegisterIl2CppTypes();
        } catch {
        }
    }
#endif

    /// <summary>Copies live settings into the O5Kit runtime config. Call after any related setting changes (O5Kit reads <see cref="O5Context.Config"/> live).</summary>
    // The tutorial's practice room teaches wheel adjusting and middle-click reset, so both stay on
    // there even when turned off in Settings (otherwise those steps could never be completed).
    private static bool OnPracticePage => UI.UICore.CurrentMenuState == (int)UI.OriginalMenuState.Practice;

    private static bool WheelAdjust => MainCore.Conf.WheelAdjustSlider.Value || OnPracticePage;

    public static void SyncConfig() {
        if (Ctx == null) {
            return;
        }

        Ctx.Config.UIScale = MainCore.Conf.UIScale.Value;
        Ctx.Config.TooltipEnabled = MainCore.Conf.Tooltip.Value;
        Ctx.Config.TooltipOpacity = Mathf.Clamp(MainCore.Conf.TooltipOpacity.Value, 0.2f, 1f);
        Ctx.Config.MiddleClickToDefault = MainCore.Conf.MiddleClickToDefault.Value || OnPracticePage;
        Ctx.Config.SliderSensitivity = MainCore.Conf.SliderSensitivity.Value;
        Ctx.Config.ScrollSensitivity = MainCore.Conf.ScrollSensitivity.Value;
        Ctx.Config.AnimationSpeed = MainCore.Conf.AnimationSpeed.Value;
        Ctx.Config.ScrollEasing = MainCore.Conf.ScrollEasing.Value;
    }

    public static void Teardown() {
        if (_enabledHandler != null) {
            try {
                MainCore.OnModEnabledChanged -= _enabledHandler;
            } catch {
            }
            _enabledHandler = null;
        }
        _wired = false;
    }

    public static void SetUserFontsEnabled(bool enabled) => _fonts?.SetUseUserResources(enabled);
}
