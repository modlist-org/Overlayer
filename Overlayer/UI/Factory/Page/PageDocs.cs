using Overlayer.Core;
using Overlayer.Resource;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageDocs {
    public static void Create(RectTransform parent) {
        GameObject title = new("NotYet");
        title.transform.SetParent(parent, false);

        var titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(400, 400);
        titleRect.anchoredPosition = new Vector2(0, 0);

        var tmp = title.AddComponent<TextMeshProUGUI>();
        tmp.text = ":(";
        tmp.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        tmp.fontSize = 52;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }
}
