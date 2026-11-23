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
        public ITweenHandle labelSeq;
        public TMP_Text label;
    }

    private const float ItemHeight = 48f;
    private const float ItemSpacing = 6f;
    private const float MenuSidePadding = 10f;
    private const float MenuTopPadding = 10f;
    private static readonly List<MenuItem> items = [];
    private static bool paletteHooked;
    private static bool labelsVisible;
    private static RectTransform selectionRect;
    private static Image selectionImg;
    private static ITweenHandle selectionTween;

    public static void CreateMenu(Transform parent) {
        items.Clear();
        selectionTween?.Kill();
        selectionTween = null;
        labelsVisible = false;
        if (!paletteHooked) {
            paletteHooked = true;
            UIColors.OnPaletteChanged += () => {
                if (selectionImg) {
                    selectionImg.color = UIColors.MenuSelected;
                }
                ApplyState(UICore.CurrentMenuState, true);
            };
        }

        GameObject selObj = new("SelectionIndicator");
        selObj.transform.SetParent(parent, false);
        selectionRect = selObj.GetComponent<RectTransform>() ?? selObj.AddComponent<RectTransform>();
        var selLayout = selObj.AddComponent<LayoutElement>();
        selLayout.ignoreLayout = true;
        selectionRect.anchorMin = new Vector2(0f, 1f);
        selectionRect.anchorMax = new Vector2(1f, 1f);
        selectionRect.pivot = new Vector2(0.5f, 1f);
        selectionRect.sizeDelta = new Vector2(-MenuSidePadding * 2f, ItemHeight);
        selectionRect.anchoredPosition = new Vector2(0f, -MenuTopPadding);
        selectionImg = selObj.AddComponent<Image>();
        selectionImg.sprite = O5KitAdapters.Ctx.Sprites.RoundedControl;
        selectionImg.type = Image.Type.Sliced;
        selectionImg.color = UIColors.MenuSelected;
        selectionImg.raycastTarget = false;
        selectionRect.SetAsFirstSibling();

        var overlayer = CreateItem(parent, "Overlayer", MainCore.Spr.Get(UISprite.Monitor128), 0);
        var settings = CreateItem(parent, "Settings", MainCore.Spr.Get(UISprite.Gear128), 1);
        var resources = CreateItem(parent, "Resources", MainCore.Spr.Get(UISprite.Box128), 4);
        var easing = CreateItem(parent, "Easing", MainCore.Spr.Get(UISprite.Ease128), (int)OriginalMenuState.Easing);
        var js = CreateItem(parent, "JS", MainCore.Spr.Get(UISprite.CodeBlock128), 5);
        var docs = CreateItem(parent, "Docs", MainCore.Spr.Get(UISprite.Book128), 2);
        var tutorial = CreateItem(parent, "Tutorial", MainCore.Spr.Get(UISprite.Triangle128), (int)OriginalMenuState.Tutorial);
        // The triangle points down; a quarter turn makes it a play button.
        var playIcon = (RectTransform)tutorial.obj.transform.Find("Icon");
        playIcon.pivot = new Vector2(0.5f, 0.5f);
        playIcon.anchoredPosition = new Vector2(9f + 14f, 0f);
        playIcon.localEulerAngles = new Vector3(0f, 0f, 90f);
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

        SetItemVisible((int)OriginalMenuState.Tutorial, !MainCore.Conf.HideTutorial);
        SetLabelsVisible(MainCore.Conf.TabLabelMode.Value == IO.TabLabelMode.Show, true);
        ApplyState(UICore.CurrentMenuState, true);
    }

    public static MenuItem CreateItem(Transform parent, string name, Sprite icon, int state) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);

        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 1);
        rect.sizeDelta = new(0, ItemHeight);

        Image bg = item.AddComponent<Image>();
        bg.sprite = O5KitAdapters.Ctx.Sprites.RoundedControl;
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.MenuNormal;

        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(item.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0, 0.5f);
        iconRect.anchoredPosition = new(9, 0);
        iconRect.sizeDelta = new(28, 28);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.raycastTarget = false;

        GameObject textObj = new("Text");
        textObj.transform.SetParent(item.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new(0, 0);
        textRect.anchorMax = new(1, 1);
        textRect.offsetMin = new(46, 0);
        textRect.offsetMax = Vector2.zero;

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Regular);
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.raycastTarget = false;
        label.alpha = labelsVisible ? 1f : 0f;

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

    /// <summary>Fades all menu item labels in or out.</summary>
    public static void SetLabelsVisible(bool visible, bool instant = false) {
        if (labelsVisible == visible && !instant) {
            return;
        }
        labelsVisible = visible;
        float targetAlpha = visible ? 1f : 0f;
        float duration = visible ? 0.22f : 0.16f;
        for (int i = 0; i < items.Count; i++) {
            var it = items[i];
            if (it.label == null || !it.label) {
                continue;
            }
            it.labelSeq?.Kill();
            if (instant) {
                it.label.alpha = targetAlpha;
            } else {
                var lbl = it.label;
                float startAlpha = lbl.alpha;
                it.labelSeq = O5KitAdapters.Ctx.Tween.TweenFloat(
                    () => 0f,
                    t => {
                        if (lbl) {
                            lbl.alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, t);
                        }
                    },
                    1f,
                    duration,
                    ease: O5Ease.OutSine
                );
            }
        }
    }

    /// <summary>Shows or hides the menu item for <paramref name="state"/>; falls back to Overlayer if hidden while active.</summary>
    public static void SetItemVisible(int state, bool visible) {
        MenuItem item = items.FirstOrDefault(it => it.state == state);
        if (item == null) {
            return;
        }

        if (!visible && UICore.CurrentMenuState == state) {
            SetState((int)OriginalMenuState.Overlayer);
        }

        if (item.obj) {
            item.obj.SetActive(visible);
        }

        ApplyState(UICore.CurrentMenuState, true);
    }

    /// <summary>Destroys the menu item for <paramref name="state"/>; falls back to the Overlayer page if it was selected.</summary>
    public static void RemoveItem(int state) {
        int index = items.FindIndex(it => it.state == state);
        if (index < 0) {
            return;
        }

        MenuItem item = items[index];
        item.hoverSeq?.Kill();
        item.labelSeq?.Kill();
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
        int visibleIndex = 0;
        int targetVisibleIndex = -1;

        for (int i = 0; i < items.Count; i++) {
            var it = items[i];
            it.hoverSeq?.Kill();
            it.bg.color = UIColors.MenuNormal;

            if (it.obj != null && it.obj.activeSelf) {
                if (it.state == id) {
                    targetVisibleIndex = visibleIndex;
                }
                visibleIndex++;
            }
        }

        if (!selectionRect) {
            return;
        }

        selectionTween?.Kill();
        if (targetVisibleIndex < 0) {
            selectionRect.gameObject.SetActive(false);
            return;
        }

        selectionRect.gameObject.SetActive(true);
        Vector2 targetPos = new(0f, -(MenuTopPadding + targetVisibleIndex * (ItemHeight + ItemSpacing)));
        if (noAnimate) {
            selectionRect.anchoredPosition = targetPos;
        } else {
            selectionTween = selectionRect.TAnchorPos(targetPos, 0.2f, O5Ease.OutCirc);
        }
    }
}
