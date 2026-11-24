using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageCredits {
    private static Sprite logoSprite;

    // Large art loaded by manifest path (not an Asset enum, so never listed
    // as an internal resource), with a mip chain so it stays sharp and
    // alias-free at any UI scale.
    internal static Sprite LoadMipSprite(string path) {
        byte[] data = MainCore.Res.Load(path);
        if (data == null || data.Length == 0) {
            return null;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
        if (!O5Kit.Compat.O5Texture.LoadImage(tex, data)) {
            UnityEngine.Object.Destroy(tex);
            return null;
        }
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // 512px render of ov5_logo.svg (the 128px icon upscales blocky).
    private static Sprite LogoSprite() => logoSprite != null ? logoSprite : logoSprite = LoadMipSprite("Image.OV5Logo512.png");

    public static void Create(RectTransform parent) {
        GameObject logo = new("Logo");
        logo.transform.SetParent(parent, false);

        var logoRect = logo.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.sizeDelta = new Vector2(200, 200);
        logoRect.anchoredPosition = new Vector2(0, 150);

        var logoImg = logo.AddComponent<Image>();
        logoImg.sprite = LogoSprite() ?? MainCore.Spr.Get(UISprite.OV5LogoOutline128);
        logoImg.preserveAspect = true;

        GameObject title = new("Title");
        title.transform.SetParent(parent, false);

        var titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(800, 60);
        titleRect.anchoredPosition = new Vector2(0, -10);

        var tmp = title.AddComponent<TextMeshProUGUI>();
        tmp.text = "Overlayer V5";
        tmp.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        tmp.fontSize = 38;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        GameObject subtitle = new("Subtitle");
        subtitle.transform.SetParent(parent, false);

        var subtitleRect = subtitle.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.pivot = new Vector2(0.5f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(800, 40);
        subtitleRect.anchoredPosition = new Vector2(0, -60);

        var subtitleTmp = subtitle.AddComponent<TextMeshProUGUI>();
        subtitleTmp.text = "Display everything as you wish.";
        subtitleTmp.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        subtitleTmp.fontSize = 20;
        subtitleTmp.color = new Color(1f, 1f, 1f, 0.45f);
        subtitleTmp.alignment = TextAlignmentOptions.Center;

        GameObject credits = new("Credits");
        credits.transform.SetParent(parent, false);

        var creditsRect = credits.AddComponent<RectTransform>();
        creditsRect.anchorMin = new Vector2(0.5f, 0.5f);
        creditsRect.anchorMax = new Vector2(0.5f, 0.5f);
        creditsRect.pivot = new Vector2(0.5f, 0.5f);
        creditsRect.sizeDelta = new Vector2(900, 220);
        creditsRect.anchoredPosition = new Vector2(0, -180);

        var creditsTmp = credits.AddComponent<TextMeshProUGUI>();
        creditsTmp.text =
            "<color=#FFFFFF66>from modlist.org</color>\n" +
            "<color=#FFFFFF88>Thank you for using Overlayer.</color>\n" +
            "<size=12><color=#FFFFFF33>\nLicensed under GPLv3</color></size>";
        creditsTmp.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        creditsTmp.fontSize = 26;
        creditsTmp.color = Color.white;
        creditsTmp.lineSpacing = 18;
        creditsTmp.alignment = TextAlignmentOptions.Center;

        KkitutRoom.Build(parent);
    }
}
