using DG.Tweening;
using Overlayer.Resource;
using Overlayer.UI.SpriteManage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlayer.UI.Generator;

public static class GenerateUI {
    public static RectTransform Toggle(Transform parent, bool defaultValue, bool value, Action<bool> onChanged, string text) {
        RectTransform rect = BackGround();

        rect.SetParent(parent, false);

        TextMeshProUGUI tmp = AddText(rect);
        tmp.text = text;

        GameObject change = AddSmallChangedCircle(rect);
        change.SetActive(defaultValue != value);

        GameObject toggleCircle = new("ToggleCircle");
        toggleCircle.transform.SetParent(rect, false);

        RectTransform circleRect = toggleCircle.AddComponent<RectTransform>();
        circleRect.anchorMin = new(1f, 0.5f);
        circleRect.anchorMax = new(1f, 0.5f);
        circleRect.pivot = new(1f, 0.5f);

        circleRect.anchoredPosition = new(-10f, 0f);
        circleRect.sizeDelta = new(26f, 26f);
        Image circleImage = toggleCircle.AddComponent<Image>();

        void UpdateVisual() {
            circleImage.sprite = SpriteDatabase.Get(
                value ? UISprite.Circle256 : UISprite.ToggleCircle128
            );
            circleImage.color = value ? new(0.569f, 0.604f, 1f, 1f) : new(0.384f, 0.4f, 0.588f, 1f);
            change.SetActive(value != defaultValue);
        }

        UpdateVisual();

        AddButton(rect.gameObject, () => {
            value = !value;
            UpdateVisual();
            onChanged?.Invoke(value);
        });

        return rect;
    }

    private static RectTransform BackGround() {
        GameObject obj = new("Bg");

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(630f, 50f);

        Image img = obj.AddComponent<Image>();
        img.color = new(0.235f, 0.227f, 0.294f, 1f);
        img.sprite = SpriteDatabase.Get(UISliceSprite.Circle256);
        img.type = Image.Type.Sliced;

        return rect;
    }

    private static void AddButton(GameObject obj, Action onClick) {
        Button btn = obj.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        btn.onClick.AddListener(() => {
            onClick?.Invoke();
        });

        GameObject hover = new("Hover");
        hover.transform.SetParent(obj.transform, false);
        hover.transform.SetAsFirstSibling();

        RectTransform hoverRect = hover.AddComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.pivot = new(0.5f, 0.5f);
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;

        Image hoverImage = hover.AddComponent<Image>();
        hoverImage.sprite = SpriteDatabase.Get(UISliceSprite.CircleOutline256);
        hoverImage.type = Image.Type.Sliced;
        hoverImage.color = new(0.569f, 0.604f, 1f, 0f);

        EventTrigger trigger = obj.AddComponent<EventTrigger>();

        Sequence hoverSeq = null;

        void Add(EventTriggerType type, Action callback) {
            var entry = new EventTrigger.Entry {
                eventID = type
            };

            entry.callback.AddListener(_ => {
                callback();
            });

            trigger.triggers.Add(entry);
        }

        Add(EventTriggerType.PointerEnter, () => {
            hoverSeq?.Kill();

            hoverSeq = DOTween.Sequence().Append(
                DOTween.To(
                    () => hoverImage.color.a,
                    x => {
                        Color c = hoverImage.color;
                        c.a = x;
                        hoverImage.color = c;
                    },
                    1f,
                    0.1f
                ).SetEase(Ease.OutSine)
            );
        });

        Add(EventTriggerType.PointerExit, () => {
            hoverSeq?.Kill();

            hoverSeq = DOTween.Sequence().Append(
                DOTween.To(
                    () => hoverImage.color.a,
                    x => {
                        Color c = hoverImage.color;
                        c.a = x;
                        hoverImage.color = c;
                    },
                    0f,
                    0.1f
                ).SetEase(Ease.OutSine)
            );
        });
    }

    private static TextMeshProUGUI AddText(RectTransform parent) {
        GameObject obj = new("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(16f, 0f);
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = ResourceManager.Get<TMP_FontAsset>(Asset.SUITRegular);
        tmp.fontSize = 24f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        return tmp;
    }

    private static GameObject AddSmallChangedCircle(RectTransform parent) {
        GameObject obj = new("Changed");
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(6f, -6f);
        rect.sizeDelta = new Vector2(8f, 8f);

        Image img = obj.AddComponent<Image>();
        img.sprite = SpriteDatabase.Get(UISprite.Circle256);
        img.color = new Color(0.325f, 0.341f, 0.514f,1f);

        return obj;
    }
}
