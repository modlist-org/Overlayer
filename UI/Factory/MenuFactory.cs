using DG.Tweening;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.UI.SpriteManage;
using Overlayer.UI.Transition;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Factory;

public static class MenuFactory {
    public static Action<MenuState> OnStateChanged;

    private class MenuItem {
        public MenuState state;
        public GameObject obj;
        public Image bg;

        public Sequence hoverSeq;
        public Sequence selectSeq;
    }

    private static readonly List<MenuItem> items = [];

    public static void CreateMenu(Transform parent) {
        items.Clear();

        CreateItem(parent, "Overlayer", SpriteDatabase.Get(UISprite.Monitor128), MenuState.Overlayer);
        CreateItem(parent, "Settings", SpriteDatabase.Get(UISprite.Gear128), MenuState.Settings);
        CreateItem(parent, "Docs", SpriteDatabase.Get(UISprite.Book128), MenuState.Docs);
        CreateItem(parent, "Credits", SpriteDatabase.Get(UISprite.Star128), MenuState.Credits);

        ApplyState(UICore.CurrentMenuState);
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
        bg.color = UIColors.MenuNormal;

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
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(name.ToUpper(), name);

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
            if(UICore.CurrentMenuState != state) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence()
                    .Append(bg.DOColor(UIColors.MenuHover, 0.2f).SetEase(Ease.OutSine))
                    .SetUpdate(true);
            }
        });

        Add(EventTriggerType.PointerExit, () => {
            if(UICore.CurrentMenuState != state) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence()
                    .Append(bg.DOColor(UIColors.MenuNormal, 0.3f).SetEase(Ease.OutSine))
                    .SetUpdate(true);
            }
        });

        Add(EventTriggerType.PointerClick, () => {
            SetState(state);

            bg.DOKill();
            bg.color = UIColors.MenuHighlight;

            menuItem.hoverSeq = DOTween.Sequence()
                .Append(bg.DOColor(UIColors.MenuSelected, 0.3f).SetEase(Ease.OutSine))
                .SetUpdate(true);
        });
    }

    private static void SetState(MenuState state) {
        OnStateChanged?.Invoke(state);

        ApplyState(state);
        PageSwicher.SwitchPage(UICore.CurrentMenuState, state);
        UICore.CurrentMenuState = state;
    }

    private static void ApplyState(MenuState state) {
        for(int i = 0; i < items.Count; i++) {
            var it = items[i];

            it.hoverSeq?.Kill();
            it.selectSeq?.Kill();

            bool selected = it.state == state;

            Color target = selected
                ? UIColors.MenuSelected
                : UIColors.MenuNormal;

            it.bg.DOKill();
            it.bg.color = target;
        }
    }
}