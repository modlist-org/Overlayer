using DG.Tweening;
using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.UI.Transition;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Factory;

public static class MenuFactory {
    public static Action<int> OnStateChanged;

    public sealed class MenuItem {
        public int state;
        public GameObject obj;
        public Image bg;
        public Sequence hoverSeq;
        public TMP_Text label;
    }

    private static readonly List<MenuItem> items = [];

    public static void CreateMenu(Transform parent) {
        items.Clear();

        var overlayer = CreateItem(parent, "Overlayer", MainCore.Spr.Get(UISprite.Monitor128), 0);
        var settings = CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128), 1);
        var docs = CreateItem(parent, "Docs", MainCore.Spr.Get(UISprite.Book128), 2);
        var credits = CreateItem(parent, "Credits", MainCore.Spr.Get(UISprite.Star128), 3);

        overlayer.label.gameObject.AddComponent<TextLocalization>()
            .Init("OVERLAYER", "Overlayer");

        settings.label.gameObject.AddComponent<TextLocalization>()
            .Init("SETTINGS", "Settings");

        docs.label.gameObject.AddComponent<TextLocalization>()
            .Init("DOCS", "Docs");

        credits.label.gameObject.AddComponent<TextLocalization>()
            .Init("CREDITS", "Credits");

        ApplyState(UICore.CurrentMenuState);
    }

    public static MenuItem CreateItem(Transform parent, string name, Sprite icon, int state) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);

        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, 54);

        Image bg = item.AddComponent<Image>();
        bg.color = UIColors.MenuNormal;

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

        GameObject textObj = new("Text");
        textObj.transform.SetParent(item.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(70, 0);
        textRect.offsetMax = Vector2.zero;

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;

        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = state,
            label = label
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
            if(UICore.CurrentMenuState == state) {
                return;
            }

            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = DOTween.Sequence()
                .Append(bg.DOColor(UIColors.MenuHover, 0.2f))
                .SetUpdate(true);
        });

        Add(EventTriggerType.PointerExit, () => {
            if(UICore.CurrentMenuState == state) {
                return;
            }

            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = DOTween.Sequence()
                .Append(bg.DOColor(UIColors.MenuNormal, 0.25f))
                .SetUpdate(true);
        });

        Add(EventTriggerType.PointerClick, () => SetState(state));

        return menuItem;
    }

    private static void SetState(int to) {
        int from = UICore.CurrentMenuState;

        if(from == to) {
            return;
        }

        UICore.CurrentMenuState = to;

        PageSwicher.SwitchPage(from, to);
        ApplyState(to);

        OnStateChanged?.Invoke(to);
    }

    private static void ApplyState(int id) {
        for(int i = 0; i < items.Count; i++) {
            var it = items[i];

            it.hoverSeq?.Kill();

            bool selected = it.state == id;

            it.bg.DOKill();
            it.bg.color = selected
                ? UIColors.MenuSelected
                : UIColors.MenuNormal;
        }
    }
}