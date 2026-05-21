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

    private class MenuItem {
        public int state;
        public GameObject obj;
        public Image bg;

        public Sequence hoverSeq;
        public Sequence selectSeq;
    }

    private static readonly List<MenuItem> items = [];

    public static void CreateMenu(Transform parent) {
        items.Clear();

        CreateItem(parent, "Overlayer", MainCore.Spr.Get(UISprite.Monitor128), (int)OriginalMenuState.Overlayer);
        CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128), (int)OriginalMenuState.Settings);
        CreateItem(parent, "Docs", MainCore.Spr.Get(UISprite.Book128), (int)OriginalMenuState.Docs);
        CreateItem(parent, "Credits", MainCore.Spr.Get(UISprite.Star128), (int)OriginalMenuState.Credits);

        ApplyState(UICore.CurrentMenuState, true);
    }

    public static void CreateItem(Transform parent, string name, Sprite icon, int id) {
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
        label.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.gameObject.AddComponent<TextLocalization>().Init(name.ToUpper(), name);

        MenuItem menuItem = new() {
            obj = item,
            bg = bg,
            state = id
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
            if(UICore.CurrentMenuState != id) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence().SetUpdate(true)
                    .Append(bg.DOColor(UIColors.MenuHover, 0.2f).SetEase(Ease.OutSine));
            }
        });

        Add(EventTriggerType.PointerExit, () => {
            if(UICore.CurrentMenuState != id) {
                menuItem.hoverSeq?.Kill();
                menuItem.hoverSeq = DOTween.Sequence().SetUpdate(true)
                    .Append(bg.DOColor(UIColors.MenuNormal, 0.3f).SetEase(Ease.OutSine));
            }
        });

        Add(EventTriggerType.PointerClick, () => {
            SetState(id);

            bg.DOKill();
            bg.color = UIColors.MenuHighlight;

            menuItem.hoverSeq = DOTween.Sequence().SetUpdate(true)
                .Append(bg.DOColor(UIColors.MenuSelected, 0.3f).SetEase(Ease.OutSine));
        });
    }

    private static void SetState(int state) {
        bool applied = ApplyState(state);
        bool switched = PageSwicher.SwitchPage(UICore.CurrentMenuState, state);

        if(!applied || !switched) {
            return;
        }

        UICore.CurrentMenuState = state;

        OnStateChanged?.Invoke(state);
    }

    private static bool ApplyState(int state, bool noAnimate = false) {
        bool found = false;

        for(int i = 0; i < items.Count; i++) {
            var it = items[i];

            bool selected = it.state == state;

            if(selected) {
                found = true;
            }

            it.hoverSeq?.Kill();
            it.selectSeq?.Kill();
            it.bg.DOKill();

            Color target = selected
                ? UIColors.MenuSelected
                : UIColors.MenuNormal;

            if(noAnimate) {
                it.bg.color = target;
                continue;
            }

            it.selectSeq = DOTween.Sequence().SetUpdate(true)
                .Join(it.bg.DOColor(target, 0.25f).SetEase(Ease.OutSine));
        }

        return found;
    }
}