using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Core;
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
    public TMP_FontAsset Regular => MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
    public TMP_FontAsset Medium => MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
    public TMP_FontAsset Monospace => MainCore.Res.Get<TMP_FontAsset>(Asset.JetBrainsMonoNL_Medium);
}

public static class O5KitAdapters {
    private static bool _wired;

    public static void Setup() {
        O5Boot.Configure(
            config: new O5Config {
                UIScale = MainCore.Conf.UIScale.Value,
                TooltipEnabled = MainCore.Conf.Tooltip.Value,
                MiddleClickToDefault = MainCore.Conf.MiddleClickToDefault.Value,
                SliderSensitivity = MainCore.Conf.SliderSensitivity.Value,
            },
            sprites: new OverlayerSpriteProvider(),
            fonts: new OverlayerFontProvider());

        O5ShortcutManager.IsSuspended = () => O5Kit.Control.O5InputBlocker.IsEditing;
        O5Kit.Behaviour.UIScrollController.ShouldConsumeParentScroll = () => UICodeInputField.ShouldConsumeParentScroll;
        if(!_wired) {
            _wired = true;
            MainCore.OnModEnabledChanged += (enabled, _) => O5Object.NotifyEnabledChanged(enabled);
        }
    }
}
