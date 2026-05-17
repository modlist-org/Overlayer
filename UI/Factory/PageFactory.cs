using Overlayer.UI.Factory.Page;
using UnityEngine;

namespace Overlayer.UI.Factory;

internal static class PageFactory {
    public static RectTransform CreatePages(GameObject panel) {
        GameObject pagesContainer = new("PagesContainer");
        pagesContainer.transform.SetParent(panel.transform, false);

        RectTransform container = pagesContainer.AddComponent<RectTransform>();
        container.anchorMin = new Vector2(0, 0);
        container.anchorMax = new Vector2(1, 1);
        container.pivot = new Vector2(0.5f, 0.5f);

        container.offsetMin = Vector2.zero;
        container.offsetMax = new Vector2(0, -60);

        for(int i = 0; i < Enum.GetValues(typeof(MenuState)).Length; i++) {
            MenuState type = (MenuState)i;

            GameObject obj = new($"Page{type}");
            obj.transform.SetParent(pagesContainer.transform, false);

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

            UICore.Pages[type] = rt;
        }

        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().alpha = 1f;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().interactable = true;
        UICore.Pages[UICore.CurrentMenuState].GetComponent<CanvasGroup>().blocksRaycasts = true;

        PageCredits.Create(UICore.Pages[MenuState.Credits]);
        PageSettings.Create(UICore.Pages[MenuState.Settings]);

        return container;
    }
}
