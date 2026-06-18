using Overlayer.UI.Factory.Page;
using UnityEngine;

namespace Overlayer.UI.Factory;

public static class PageFactory {
    public static RectTransform PagesContaner;
    public static RectTransform CreatePages(GameObject panel) {
        GameObject pagesContainer = new("PagesContainer");
        pagesContainer.transform.SetParent(panel.transform, false);

        PagesContaner = pagesContainer.AddComponent<RectTransform>();

        PagesContaner.anchorMin = new Vector2(0, 0);
        PagesContaner.anchorMax = new Vector2(1, 1);
        PagesContaner.pivot = new Vector2(0.5f, 0.5f);

        PagesContaner.offsetMin = Vector2.zero;
        PagesContaner.offsetMax = new Vector2(0, -60);

        for(int i = 0; i < Enum.GetValues(typeof(OriginalMenuState)).Length; i++) {
            CreatePageBase(i);
        }

        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().alpha = 1f;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().interactable = true;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().blocksRaycasts = true;

        PageCredits.Create(UICore.Pages[(int)OriginalMenuState.Credits]);
        PageSettings.Create(UICore.Pages[(int)OriginalMenuState.Settings]);
        PageOverlayer.Create(UICore.Pages[(int)OriginalMenuState.Overlayer]);
        PageDocs.Create(UICore.Pages[(int)OriginalMenuState.Docs]);

        return PagesContaner;
    }

    public static RectTransform CreatePageBase(int num) {
        GameObject obj = new($"Page{num}");
        obj.transform.SetParent(PagesContaner, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        UICore.Pages[num] = rt;

        return rt;
    }
}
