using DG.Tweening;
using Overlayer.Resource;
using Overlayer.UI.UISprites;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI;

public enum MenuState {
    Overlayer,
    Settings,
    Docs,
    Credits,
}

public static class MenuFactory {
    public static MenuState CurrentState = MenuState.Overlayer;
    public static Action<MenuState> OnStateChanged;

    private static readonly Dictionary<MenuState, RectTransform> Pages = [];
    private static Sequence pageSeq;

    private class MenuItem {
        public MenuState state;
        public GameObject obj;
        public Image bg;

        public Sequence hoverSeq;
        public Sequence selectSeq;
    }

    private static readonly List<MenuItem> items = [];

    private static readonly Color normalColor = new(0.635f, 0.655f, 0.878f, 0f);
    private static readonly Color hoverColor = new(0.635f, 0.655f, 0.878f, 0.4f);
    private static readonly Color selectedColor = new(0.635f, 0.655f, 0.878f, 1f);
    private static readonly Color highlightColor = new(0.824f, 0.835f, 0.965f, 1f);

    public static void CreateMenu(Transform parent) {
        items.Clear();

        CreateItem(parent, "Overlayer", SpriteDatabase.Get(UISprite.Monitor128), MenuState.Overlayer);
        CreateItem(parent, "Settings", SpriteDatabase.Get(UISprite.Gear128), MenuState.Settings);
        CreateItem(parent, "Docs", SpriteDatabase.Get(UISprite.Book128), MenuState.Docs);
        CreateItem(parent, "Credits", SpriteDatabase.Get(UISprite.Star128), MenuState.Credits);

        ApplyState(CurrentState);
    }

    private static void CreateItem(Transform parent, string name, Sprite icon, MenuState state) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);

        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, 54);

        Image bg = item.AddComponent<Image>();
        bg.color = normalColor;

        // ICON
        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(item.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0, 0.5f);
        iconRect.anchoredPosition = new(24, 0);
        iconRect.sizeDelta = new(28, 28);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.raycastTarget = false;

        // TEXT
        GameObject textObj = new("Text");
        textObj.transform.SetParent(item.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(70, 0);
        textRect.offsetMax = Vector2.zero;

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;

        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = state
        };

        items.Add(menuItem);

        var trigger = item.AddComponent<EventTrigger>();

        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry {
                eventID = type
            };
            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }

        Add(EventTriggerType.PointerEnter, () => {
            if(CurrentState != state) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence()
                    .Append(bg.DOColor(hoverColor, 0.2f).SetEase(Ease.OutSine));
            }
        });

        Add(EventTriggerType.PointerExit, () => {
            if(CurrentState != state) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence()
                    .Append(bg.DOColor(normalColor, 0.3f).SetEase(Ease.OutSine));
            }
        });

        Add(EventTriggerType.PointerClick, () => {
            SetState(state);

            bg.DOKill();
            bg.color = highlightColor;

            menuItem.hoverSeq = DOTween.Sequence()
                .Append(bg.DOColor(selectedColor, 0.3f).SetEase(Ease.OutSine));
        });
    }

    private static void SetState(MenuState state) {
        OnStateChanged?.Invoke(state);

        ApplyState(state);
        SwitchPage(CurrentState, state);

        CurrentState = state;
    }

    private static void ApplyState(MenuState state) {
        for(int i = 0; i < items.Count; i++) {
            var it = items[i];

            it.hoverSeq?.Kill();
            it.selectSeq?.Kill();

            bool selected = it.state == state;

            Color target = selected
                ? selectedColor
                : normalColor;

            it.bg.DOKill();
            it.bg.color = target;
        }
    }

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

            Pages[type] = rt;
        }

        Pages[CurrentState].GetComponent<CanvasGroup>().alpha = 1f;
        Pages[CurrentState].GetComponent<CanvasGroup>().interactable = true;
        Pages[CurrentState].GetComponent<CanvasGroup>().blocksRaycasts = true;

        {
            var parent = Pages[MenuState.Credits];

            {
                GameObject obj = new("Logo");
                obj.transform.SetParent(parent, false);

                var rect = obj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(200, 200);
                rect.anchoredPosition = new Vector2(0, 150);

                var img = obj.AddComponent<Image>();
                img.sprite = SpriteDatabase.Get(UISprite.OV5LogoOutline256);
                img.preserveAspect = true;
            }

            {
                GameObject obj = new("Title");
                obj.transform.SetParent(parent, false);

                var rect = obj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(800, 60);
                rect.anchoredPosition = new Vector2(0, -10);

                var tmp = obj.AddComponent<TextMeshProUGUI>();
                tmp.text = "Overlayer V5";
                tmp.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
                tmp.fontSize = 38;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
            }

            {
                GameObject obj = new("Subtitle");
                obj.transform.SetParent(parent, false);

                var rect = obj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(800, 40);
                rect.anchoredPosition = new Vector2(0, -60);

                var tmp = obj.AddComponent<TextMeshProUGUI>();
                tmp.text = "Display everything as you wish.";
                tmp.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
                tmp.fontSize = 20;
                tmp.color = new Color(1f, 1f, 1f, 0.45f);
                tmp.alignment = TextAlignmentOptions.Center;
            }

            {
                GameObject obj = new("Credits");
                obj.transform.SetParent(parent, false);

                var rect = obj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(900, 220);
                rect.anchoredPosition = new Vector2(0, -180);

                var tmp = obj.AddComponent<TextMeshProUGUI>();
                tmp.text =
                    "<color=#FFFFFF66>from modlist.org</color>\n" +
                    "<color=#FFFFFF88>Thank you for using Overlayer.</color>\n" +
                    "<size=12><color=#FFFFFF33>\nLicensed under GPLv3</color></size>";
                tmp.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
                tmp.fontSize = 26;
                tmp.color = Color.white;
                tmp.lineSpacing = 18;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }

        return container;
    }

    private static void SwitchPage(MenuState from, MenuState to) {
        pageSeq?.Kill();

        RectTransform fromPage = Pages[from];
        RectTransform toPage = Pages[to];

        CanvasGroup fromCg = fromPage.GetComponent<CanvasGroup>();
        CanvasGroup toCg = toPage.GetComponent<CanvasGroup>();

        toPage.anchoredPosition = new Vector2(600, 0);
        toCg.alpha = 0f;
        toCg.interactable = false;
        toCg.blocksRaycasts = false;

        pageSeq = DOTween.Sequence();

        pageSeq.Join(fromPage.DOAnchorPosX(-420f, 0.35f).SetEase(Ease.OutCubic));
        pageSeq.Join(fromCg.DOFade(0f, 0.3f));

        pageSeq.Join(toPage.DOAnchorPosX(0f, 0.45f).SetEase(Ease.OutExpo).SetDelay(0.05f));
        pageSeq.Join(toCg.DOFade(1f, 0.3f));

        pageSeq.OnComplete(() => {
            fromCg.interactable = false;
            fromCg.blocksRaycasts = false;

            toCg.interactable = true;
            toCg.blocksRaycasts = true;
        });
    }
}