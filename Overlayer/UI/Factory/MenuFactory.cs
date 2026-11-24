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
        public RectTransform rect;
        public CanvasGroup canvasGroup;
        public Image bg;
        public ITweenHandle hoverSeq;
        public ITweenHandle labelSeq;
        public ITweenHandle visibilitySeq;
        public TMP_Text label;
        public bool visible = true;
        public float visibilityProgress = 1f;
    }

    private const float ItemHeight = 48f;
    private const float ItemSpacing = 6f;
    private const float MenuSidePadding = 10f;
    private const float MenuTopPadding = 10f;
    private const float ItemShowDuration = 0.3f;
    private const float ItemHideDuration = 0.2f;
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

        var overlayer = CreateItem(parent, "Overlayer", UIIcon.Monitor, 0);
        var settings = CreateItem(parent, "Settings", UIIcon.Gear, 1);
        var resources = CreateItem(parent, "Resources", UIIcon.Box, 4);
        var easing = CreateItem(parent, "Easing", UIIcon.Ease, (int)OriginalMenuState.Easing);
        var js = CreateItem(parent, "JS", UIIcon.CodeBlock, 5);
        var docs = CreateItem(parent, "Docs", UIIcon.Book, 2);
        var tutorial = CreateItem(parent, "Tutorial", UIIcon.Triangle, (int)OriginalMenuState.Tutorial);
        // The triangle points down; a quarter turn makes it a play button.
        var playIcon = (RectTransform)tutorial.obj.transform.Find("Icon");
        playIcon.pivot = new Vector2(0.5f, 0.5f);
        playIcon.anchoredPosition = new Vector2(9f + 14f, 0f);
        playIcon.localEulerAngles = new Vector3(0f, 0f, 90f);
        var credits = CreateItem(parent, "Credits", UIIcon.Star, 3);

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

        SetItemVisible((int)OriginalMenuState.Tutorial, !MainCore.Conf.HideTutorial, instant: true);
        SetLabelsVisible(MainCore.Conf.TabLabelMode.Value == IO.TabLabelMode.Show, true);
        ApplyState(UICore.CurrentMenuState, true);
    }

    public static MenuItem CreateItem(Transform parent, string name, Sprite icon, int state)
        => CreateItemCore(parent, name, state, item => {
            GameObject iconObj = new("Icon");
            iconObj.transform.SetParent(item.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.raycastTarget = false;
            return iconImg.rectTransform;
        });

    public static MenuItem CreateItem(Transform parent, string name, UIIcon icon, int state)
        => CreateItemCore(parent, name, state, item =>
            MainCore.Icons.Create(item.transform, "Icon", icon, Color.white, 28f).rectTransform);

    private static MenuItem CreateItemCore(Transform parent, string name, int state, Func<GameObject, RectTransform> buildIcon) {
        GameObject item = new(name);
        item.transform.SetParent(parent, false);

        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = new(0, 1);
        rect.anchorMax = new(1, 1);
        rect.pivot = new(0.5f, 0.5f);
        rect.sizeDelta = new(0, ItemHeight);

        CanvasGroup cg = item.AddComponent<CanvasGroup>();

        Image bg = item.AddComponent<Image>();
        bg.sprite = O5KitAdapters.Ctx.Sprites.RoundedControl;
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.MenuNormal;

        RectTransform iconRect = buildIcon(item);
        iconRect.anchorMin = new(0, 0.5f);
        iconRect.anchorMax = new(0, 0.5f);
        iconRect.pivot = new(0, 0.5f);
        iconRect.anchoredPosition = new(9, 0);
        iconRect.sizeDelta = new(28, 28);

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
            rect = rect,
            canvasGroup = cg,
            bg = bg,
            state = state,
            label = label,
            visible = true,
            visibilityProgress = 1f,
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
            if (state == (int)OriginalMenuState.Credits) {
                bool shift = O5Kit.Input.O5Input.GetKey(KeyCode.LeftShift) || O5Kit.Input.O5Input.GetKey(KeyCode.RightShift);
                if (shift && !Page.KkitutRoom.Active) {
                    UISound.PlayConfirm();
                    if (UICore.CurrentMenuState != state) {
                        Page.KkitutRoom.Enter(instant: true);
                        SetState(state);
                    } else {
                        Page.KkitutRoom.Enter();
                    }
                    return;
                }
                if (UICore.CurrentMenuState == state && Page.KkitutRoom.Active) {
                    UISound.PlayConfirm();
                    Page.KkitutRoom.Exit(false);
                    return;
                }
            }
            if (UICore.CurrentMenuState != state) {
                UISound.PlayConfirm();
                SetState(state);
            }
        });

        return menuItem;
    }

    private static Image creditsSecretIcon;

    /// <summary>Cross-fades the Credits star with <paramref name="sprite"/> while the secret room is open.</summary>
    internal static void SetCreditsSecretIcon(Sprite sprite, bool on) {
        MenuItem credits = null;
        foreach (var it in items) {
            if (it.state == (int)OriginalMenuState.Credits && it.obj != null) {
                credits = it;
                break;
            }
        }
        if (credits == null) {
            return;
        }
        Transform starTr = credits.obj.transform.Find("Icon");
        RectTransform starRect = starTr != null ? starTr.GetComponent<RectTransform>() : null;
        if (on && creditsSecretIcon == null && sprite != null && starRect != null) {
            var go = new GameObject("SecretIcon");
            go.transform.SetParent(credits.obj.transform, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = starRect.anchorMin;
            r.anchorMax = starRect.anchorMax;
            r.pivot = starRect.pivot;
            r.anchoredPosition = starRect.anchoredPosition;
            r.sizeDelta = starRect.sizeDelta;
            creditsSecretIcon = go.AddComponent<Image>();
            creditsSecretIcon.sprite = sprite;
            creditsSecretIcon.preserveAspect = true;
            creditsSecretIcon.raycastTarget = false;
            creditsSecretIcon.canvasRenderer.SetAlpha(0f);
        }
        if (starRect != null) {
            var star = starRect.GetComponent<Graphic>();
            if (star != null) {
                star.CrossFadeAlpha(on ? 0f : 1f, 0.25f, true);
            }
        }
        if (creditsSecretIcon != null) {
            creditsSecretIcon.CrossFadeAlpha(on ? 1f : 0f, 0.25f, true);
        }
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
    public static void SetItemVisible(int state, bool visible, bool instant = false) {
        MenuItem item = items.FirstOrDefault(it => it.state == state);
        if (item == null) {
            return;
        }

        if (!visible && UICore.CurrentMenuState == state) {
            SetState((int)OriginalMenuState.Overlayer);
        }

        if (!instant && item.visible == visible && item.visibilitySeq == null) {
            return;
        }

        item.visible = visible;
        item.visibilitySeq?.Kill();
        item.visibilitySeq = null;

        void ApplyProgress(float v) {
            item.visibilityProgress = v;
            if (item.rect) {
                item.rect.localScale = new Vector3(v, v, 1f);
            }
            if (item.canvasGroup) {
                item.canvasGroup.alpha = v;
            }
            if (UICore.MenuContent != null) {
                LayoutRebuilder.MarkLayoutForRebuild(UICore.MenuContent);
            }
        }

        if (instant || !item.obj || !item.obj.transform.parent || !item.obj.transform.parent.gameObject.activeInHierarchy) {
            if (item.obj) {
                item.obj.SetActive(visible);
            }
            if (item.canvasGroup) {
                item.canvasGroup.blocksRaycasts = visible;
                item.canvasGroup.interactable = visible;
            }
            ApplyProgress(visible ? 1f : 0f);
            ApplyState(UICore.CurrentMenuState, true);
            return;
        }

        float target = visible ? 1f : 0f;
        float duration = visible ? ItemShowDuration : ItemHideDuration;

        if (visible) {
            if (!item.obj.activeSelf) {
                item.visibilityProgress = 0f;
                if (item.rect) {
                    item.rect.localScale = new Vector3(0f, 0f, 1f);
                }
                if (item.canvasGroup) {
                    item.canvasGroup.alpha = 0f;
                }
                item.obj.SetActive(true);
            }
            if (item.canvasGroup) {
                item.canvasGroup.blocksRaycasts = true;
                item.canvasGroup.interactable = true;
            }
        } else {
            item.hoverSeq?.Kill();
            item.hoverSeq = null;
            if (item.bg) {
                item.bg.color = UIColors.MenuNormal;
            }
            if (item.canvasGroup) {
                item.canvasGroup.blocksRaycasts = false;
                item.canvasGroup.interactable = false;
            }
        }

        float start = item.visibilityProgress;
        item.visibilitySeq = O5KitAdapters.Ctx.Tween.TweenFloat(
            () => 0f,
            t => {
                if (!item.obj) {
                    return;
                }
                ApplyProgress(Mathf.LerpUnclamped(start, target, t));
            },
            1f,
            duration,
            () => {
                item.visibilitySeq = null;
                if (!item.obj) {
                    return;
                }
                ApplyProgress(target);
                if (!item.visible) {
                    item.obj.SetActive(false);
                    if (UICore.MenuContent != null) {
                        LayoutRebuilder.MarkLayoutForRebuild(UICore.MenuContent);
                    }
                }
            },
            O5Ease.OutExpo
        );

        ApplyState(UICore.CurrentMenuState, false);
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
        item.visibilitySeq?.Kill();
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
        if (to == (int)OriginalMenuState.Practice) {
            Page.PagePractice.ResetAll();
        }
        // Entering/leaving the practice room flips its forced middle-click reset.
        if (to == (int)OriginalMenuState.Practice || from == (int)OriginalMenuState.Practice) {
            O5KitAdapters.SyncConfig();
        }

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

            if (it.obj != null && it.visible) {
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
