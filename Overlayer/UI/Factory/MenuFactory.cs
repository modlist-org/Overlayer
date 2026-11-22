using Overlayer.Tween;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Resource;
using O5Kit.Behaviour;
using O5Kit.Transition;
using O5Kit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory;

public static class MenuFactory {
    public static Action<int> OnStateChanged;

    public sealed class MenuItem {
        public int state;
        public GameObject obj;
        public Image bg;
        public ITweenHandle hoverSeq;
        public TMP_Text label;
    }

    private static readonly List<MenuItem> items = [];
    private static bool paletteHooked;

    public static void CreateMenu(Transform parent) {
        items.Clear();
        if (!paletteHooked) {
            paletteHooked = true;
            UIColors.OnPaletteChanged += () => ApplyState(UICore.CurrentMenuState, true);
        }

        var overlayer = CreateItem(parent, "Overlayer", MainCore.Spr.Get(UISprite.Monitor128), 0);
        var tutorial = CreateItem(parent, "Tutorial", MainCore.Spr.Get(UISprite.Triangle128), (int)OriginalMenuState.Tutorial);
        // The triangle points down; a quarter turn makes it a play button.
        var playIcon = (RectTransform)tutorial.obj.transform.Find("Icon");
        playIcon.pivot = new Vector2(0.5f, 0.5f);
        playIcon.anchoredPosition = new Vector2(24f + 14f, 0f);
        playIcon.localEulerAngles = new Vector3(0f, 0f, 90f);
        var resources = CreateItem(parent, "Resources", MainCore.Spr.Get(UISprite.Box128), 4);
        var js = CreateItem(parent, "JS", MainCore.Spr.Get(UISprite.CodeBlock128), 5);
        var easing = CreateItem(parent, "Easing", MainCore.Spr.Get(UISprite.Ease128), (int)OriginalMenuState.Easing);
        var settings = CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128), 1);
        var docs = CreateItem(parent, "Docs", MainCore.Spr.Get(UISprite.Book128), 2);
        var credits = CreateItem(parent, "Credits", MainCore.Spr.Get(UISprite.Star128), 3);

        overlayer.label.gameObject.AddComponent<TextLocalization>()
            .Init("OVERLAYER", "Overlayer");

        resources.label.gameObject.AddComponent<TextLocalization>()
            .Init("RESOURCES", "Resources");

        settings.label.gameObject.AddComponent<TextLocalization>()
            .Init("SETTINGS", "Settings");

        docs.label.gameObject.AddComponent<TextLocalization>()
            .Init("DOCS", "Docs");

        credits.label.gameObject.AddComponent<TextLocalization>()
            .Init("CREDITS", "Credits");

        js.label.gameObject.AddComponent<TextLocalization>()
            .Init("JS", "JS");

        easing.label.gameObject.AddComponent<TextLocalization>()
            .Init("EASING", "Easing");

        tutorial.label.gameObject.AddComponent<TextLocalization>()
            .Init("TUTORIAL", "Tutorial");

        ApplyState(UICore.CurrentMenuState, true);
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

        void Add(EventTriggerType type, Action<BaseEventData> cb) {
            var e = new EventTrigger.Entry {
                eventID = type
            };

            e.callback.AddListener(
#if ML && IL2CPP
                new Action<BaseEventData>(d => cb(d))
#else
                d => cb(d)
#endif
            );

            trigger.triggers.Add(e);
        }

        Add(EventTriggerType.PointerEnter, _ => {
            if (UICore.CurrentMenuState == state) {
                return;
            }

            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = bg.TColor(UIColors.MenuHover, 0.2f, O5Ease.OutSine);
        });

        Add(EventTriggerType.PointerExit, _ => {
            if (UICore.CurrentMenuState == state) {
                return;
            }

            menuItem.hoverSeq?.Kill();
            menuItem.hoverSeq = bg.TColor(UIColors.MenuNormal, 0.25f, O5Ease.OutSine);
        });

        Add(EventTriggerType.PointerClick, e => {
            if (UnityUtils.AsPointerEventData(e) is { } ped && ped.button != PointerEventData.InputButton.Left) {
                return;
            }
            if (UICore.CurrentMenuState != state) {
                UISound.PlayConfirm();
                SetState(state);
            }
        });

        return menuItem;
    }

    /// <summary>Destroys the menu item for <paramref name="state"/>; falls back to the Overlayer page if it was selected.</summary>
    public static void RemoveItem(int state) {
        int index = items.FindIndex(it => it.state == state);
        if (index < 0) {
            return;
        }

        MenuItem item = items[index];
        item.hoverSeq?.Kill();
        items.RemoveAt(index);

        if (UICore.CurrentMenuState == state) {
            SetState((int)OriginalMenuState.Overlayer);
        }

        if (item.obj) {
            UnityEngine.Object.Destroy(item.obj);
        }
    }

    /// <summary>Switches to a page as if its menu item was clicked.</summary>
    public static void GoTo(int state) => SetState(state);

    /// <summary>The menu item of a page (for pointing at it).</summary>
    public static RectTransform ItemOf(int state) => items.FirstOrDefault(i => i.state == state)?.obj?.GetComponent<RectTransform>();

    private static void SetState(int to) {
        int from = UICore.CurrentMenuState;

        if (from == to) {
            return;
        }

        UICore.CurrentMenuState = to;

        O5PageSwitcher.SwitchPage(O5KitAdapters.Ctx, UICore.Pages, from, to);
        ApplyState(to);

        OnStateChanged?.Invoke(to);
    }

    private static void ApplyState(int id, bool noAnimate = false) {
        for (int i = 0; i < items.Count; i++) {
            var it = items[i];

            it.hoverSeq?.Kill();

            bool selected = it.state == id;

            if (selected) {
                if (noAnimate) {
                    it.bg.color = UIColors.MenuSelected;
                } else {
                    it.bg.color = UIColors.MenuHighlight;

                    it.hoverSeq = it.bg.TColor(UIColors.MenuSelected, 0.3f, O5Ease.OutSine);
                }
            } else {
                it.bg.color = UIColors.MenuNormal;
            }
        }
    }
}
