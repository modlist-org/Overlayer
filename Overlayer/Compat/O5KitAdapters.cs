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
    public Sprite RoundedPanel => MainCore.Spr.Get(UISliceSprite.Circle256P1024);
    public Sprite RoundedControl => MainCore.Spr.Get(UISliceSprite.Circle256P2048);
    public Sprite TopBar => MainCore.Spr.Get(UISliceSprite.CircleHalf256P1024);
    public Sprite RoundedOutline => MainCore.Spr.Get(UISliceSprite.CircleOutline256O64P2048);
    public Sprite Circle => MainCore.Spr.Get(UISprite.Circle256);

    public Sprite Icon(string name) {
        if(name == "toggle-on") {
            return MainCore.Spr.Get(UISprite.Circle256);
        }

        if(name == "toggle-off") {
            return MainCore.Spr.Get(UISprite.ToggleCircle128);
        }

        if(name == "x") {
            return MainCore.Spr.Get(UISprite.X128);
        }

        if(name == "triangle") {
            return MainCore.Spr.Get(UISprite.Triangle128);
        }

        return Enum.TryParse<UISprite>(name, out UISprite sprite) ? MainCore.Spr.Get(sprite) : null;
    }
}

public sealed class OverlayerFontProvider : IFontProvider {
    private const string BuiltinFallback = "@builtin-default";
    private static readonly Dictionary<string, TMP_FontAsset> compositeCache = [];
    private TMP_FontAsset _regular;
    private TMP_FontAsset _medium;
    private TMP_FontAsset _monospace;
    private bool _useUserResources;

    public TMP_FontAsset Regular => _regular = ResolveSlot(
        "regular", Asset.SUIT_Regular, MainCore.Conf.SystemFontKey.Value, MainCore.Conf.SystemFontFallbacks.Value);
    public TMP_FontAsset Medium => _medium = ResolveSlot(
        "medium", Asset.SUIT_Medium, MainCore.Conf.SystemFontKey.Value, MainCore.Conf.SystemFontFallbacks.Value);
    public TMP_FontAsset Monospace => _monospace = ResolveSlot(
        "monospace", Asset.JetBrainsMonoNL_Medium, MainCore.Conf.CodeFontKey.Value, MainCore.Conf.CodeFontFallbacks.Value);

    public void SetUseUserResources(bool enabled) {
        if(_useUserResources == enabled) {
            return;
        }
        var oldFonts = new[] { _regular, _medium, _monospace };
        _useUserResources = enabled;
        var newFonts = new[] { Regular, Medium, Monospace };
        if(Overlayer.UI.UICore.CanvasObj != null) {
            var directUserFonts = new HashSet<TMP_FontAsset>();
            if(!enabled) {
                foreach(string key in UserResourceManager.Fnt.Keys) {
                    if(O5cpFormat.IsPackageKey(key)) {
                        continue;
                    }
                    if(UserResourceManager.Fnt.TryGet(key, out var userFont) && userFont != null) {
                        directUserFonts.Add(userFont);
                    }
                }
            }
            foreach(var text in Overlayer.UI.UICore.CanvasObj.GetComponentsInChildren<TMP_Text>(true)) {
                if(text == null || text.font == null) {
                    continue;
                }
                for(int i = 0; i < oldFonts.Length; i++) {
                    if(oldFonts[i] != null && text.font == oldFonts[i]) {
                        text.font = newFonts[i];
                        break;
                    }
                }
                if(!enabled && directUserFonts.Contains(text.font)) {
                    text.font = newFonts[0];
                }
            }
        }
        if(!enabled) {
            foreach(var font in compositeCache.Values) {
                if(font != null) {
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
        if(Overlayer.UI.UICore.CanvasObj != null) {
            foreach(var text in Overlayer.UI.UICore.CanvasObj.GetComponentsInChildren<TMP_Text>(true)) {
                if(text == null || text.font == null) {
                    continue;
                }
                if(oldMonospace != null && text.font == oldMonospace) {
                    text.font = newMonospace;
                } else if(oldMedium != null && text.font == oldMedium) {
                    text.font = newMedium;
                } else if(oldRegular != null && text.font == oldRegular) {
                    text.font = newRegular;
                }
            }
        }
    }

    private TMP_FontAsset ResolveSlot(string slot, Asset builtinAsset, string primaryKey, List<string> fallbackKeys) {
        TMP_FontAsset builtin = MainCore.Res.Get<TMP_FontAsset>(builtinAsset);
        return _useUserResources
            ? WithFallbacks(slot, primaryKey, fallbackKeys, () => builtin)
            : builtin;
    }

    private static TMP_FontAsset WithFallbacks(string slot, string primaryKey, List<string> fallbackKeys, Func<TMP_FontAsset> builtin) {
        TMP_FontAsset fallback = builtin();
        var chain = new List<TMP_FontAsset>();
        TMP_FontAsset primary = Resolve(primaryKey);
        chain.Add(primary ?? fallback);
        if(fallbackKeys != null) {
            foreach(string key in fallbackKeys) {
                if(string.IsNullOrEmpty(key) || key == primaryKey || chain.Count >= 9) {
                    continue;
                }
                TMP_FontAsset asset = key == BuiltinFallback ? fallback : Resolve(key);
                if(asset != null && !chain.Contains(asset)) {
                    chain.Add(asset);
                }
            }
        }
        if(chain.Count == 0) {
            return fallback;
        }
        if(!chain.Contains(fallback) && fallback != null) {
            chain.Add(fallback);
        }
        if(chain.Count == 1) {
            return chain[0];
        }
        string sig = slot + ":" + string.Join(",", chain.Select(a => a.GetInstanceID()));
        if(compositeCache.TryGetValue(sig, out var composite) && composite != null) {
            return composite;
        }
        composite = UnityEngine.Object.Instantiate(chain[0]);
        composite.name = chain[0].name + "_WithFallbacks";
        composite.fallbackFontAssetTable = chain.Skip(1).ToList();
        compositeCache[sig] = composite;
        return composite;
    }

    private static TMP_FontAsset Resolve(string key) {
        if(string.IsNullOrEmpty(key)) {
            return null;
        }
        try {
            return UserResourceManager.Fnt.TryGet(key, out var font) ? font : null;
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
        _fonts = new OverlayerFontProvider();
        Ctx = new O5Context(
            config: new O5Config(),
            theme: O5Theme.Dark with {
                ControlOutlineIdle = new Color32(255, 255, 255, 0),
            },
            sprites: new OverlayerSpriteProvider(),
            fonts: _fonts);
        SyncConfig();

        O5ShortcutManager.IsSuspended = () => O5Kit.Control.O5InputBlocker.IsEditing;
        O5Kit.Behaviour.UIScrollController.ShouldConsumeParentScroll = () => UICodeInputField.ShouldConsumeParentScroll;
        if(!_wired) {
            _wired = true;
            _enabledHandler = (enabled, _) => Ctx.NotifyEnabledChanged(enabled);
            MainCore.OnModEnabledChanged += _enabledHandler;
        }
    }

    public static void RefreshFonts() => _fonts?.RefreshExistingText();

    /// <summary>Copies live settings into the O5Kit runtime config. Call after any related setting changes (O5Kit reads <see cref="O5Context.Config"/> live).</summary>
    public static void SyncConfig() {
        if (Ctx == null) {
            return;
        }

        Ctx.Config.UIScale = MainCore.Conf.UIScale.Value;
        Ctx.Config.TooltipEnabled = MainCore.Conf.Tooltip.Value;
        Ctx.Config.MiddleClickToDefault = MainCore.Conf.MiddleClickToDefault.Value;
        Ctx.Config.SliderSensitivity = MainCore.Conf.SliderSensitivity.Value;
        Ctx.Config.AnimationSpeed = MainCore.Conf.AnimationSpeed.Value;
    }

    public static void Teardown() {
        if(_enabledHandler != null) {
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
