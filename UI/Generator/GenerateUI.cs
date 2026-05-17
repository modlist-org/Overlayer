using DG.Tweening;
using MelonLoader.TinyJSON;
using Overlayer.Resource;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.SpriteManage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;

namespace Overlayer.UI.Generator;

public static class GenerateUI {
    public static RectTransform Row(Transform parent, float height = 50f) {
        GameObject obj = new("Row");
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        return rect;
    }

    public static UIToggle Toggle(
        Transform parent,
        bool defaultValue,
        bool value,
        Action<bool> onChanged,
        string text,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);

        TextMeshProUGUI tmp = AddText(rect);
        tmp.text = text;

        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();

        GameObject toggleCircle = new("ToggleCircle");
        toggleCircle.transform.SetParent(rect, false);

        RectTransform circleRect = toggleCircle.AddComponent<RectTransform>();
        circleRect.anchorMin = new(1f, 0.5f);
        circleRect.anchorMax = new(1f, 0.5f);
        circleRect.pivot = new(0.5f, 0.5f);
        circleRect.anchoredPosition = new(-23f, 0f);
        circleRect.sizeDelta = new(26f, 26f);

        Image circleImage = toggleCircle.AddComponent<Image>();

        UIToggle toggle = new(
            id,
            rect,
            tmp,
            circleImage,
            circleRect,
            changeImg,
            defaultValue,
            value,
            onChanged
        );

        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    toggle.Toggle();
                    break;

                case InputButton.Middle:
                    if(
                        Core.Config.MiddleClickToDefault &&
                        toggle.Value != toggle.DefaultValue
                    ) {
                        toggle.Reset();
                    }

                    break;
            }
        });

        return toggle;
    }

    public static UIButton Button(
        Transform parent,
        Action onClick,
        string text,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);

        TextMeshProUGUI tmp = AddText(rect, true);
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;

        Image bg = rect.GetComponent<Image>();
        bg.color = UIColors.ObjectButton;

        UIButton button = new(
            id,
            rect,
            tmp,
            bg,
            onClick
        );

        AddButton(rect.gameObject, btn => {
            if(btn != InputButton.Middle) {
                button.Click();
            }
        }, false);

        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();

        AddEvent(EventTriggerType.PointerEnter, e => button.OnHoverEnter(), trigger);
        AddEvent(EventTriggerType.PointerExit, e => button.OnHoverExit(), trigger);

        return button;
    }

    public static UIDropDown<T> DropDown<T>(
        Transform parent,
        T defaultValue,
        T value,
        IReadOnlyList<T> values,
        Func<T, string> display,
        Action<T> onChanged,
        string id
    ) {
        GameObject root = new("Dropdown");
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new(0f, 0f);
        rootRect.anchorMax = new(1f, 1f);
        rootRect.pivot = new(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        RectTransform rect = BackGround();
        rect.SetParent(root.transform, false);
        rect.pivot = new(rect.pivot.x, 1f);
        rect.anchorMin = new(rect.anchorMin.x, 1f);
        rect.anchorMax = new(rect.anchorMax.x, 1f);
        rect.sizeDelta = new(rect.sizeDelta.x, 50f);

        TextMeshProUGUI tmp = AddText(rect);
        tmp.text = display(value);

        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();

        GameObject triangle = new("Triangle");
        triangle.transform.SetParent(rect, false);

        RectTransform triangleRect = triangle.AddComponent<RectTransform>();
        triangleRect.anchorMin = new(1f, 0.5f);
        triangleRect.anchorMax = new(1f, 0.5f);
        triangleRect.pivot = new(0.5f, 0.5f);
        triangleRect.anchoredPosition = new(-23f, 0f);
        triangleRect.sizeDelta = new(26f, 26f);

        Image triangleImage = triangle.AddComponent<Image>();
        triangleImage.sprite = SpriteDatabase.Get(UISprite.Triangle128);
        triangleImage.color = UIColors.ObjectInactive;

        GameObject list = new("List");
        list.transform.SetParent(root.transform, false);

        RectTransform listRect = list.AddComponent<RectTransform>();
        listRect.anchorMin = new(0f, 1f);
        listRect.anchorMax = new(1f, 1f);
        listRect.pivot = new(0.5f, 1f);
        listRect.offsetMin = new(0f, -62f);
        listRect.offsetMax = new(-300f, -62f);

        Image listBg = list.AddComponent<Image>();
        listBg.sprite = SpriteDatabase.Get(UISliceSprite.Circle256P2048);
        listBg.type = Image.Type.Sliced;
        listBg.color = UIColors.ObjectBG;

        VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CanvasGroup listCg = list.AddComponent<CanvasGroup>();
        listCg.alpha = 0f;

        list.SetActive(false);

        UIDropDown<T> dropdown = new(
            id,
            rootRect,
            tmp,
            triangleImage,
            triangleRect,
            changeImg,
            list,
            listRect,
            listCg,
            values,
            display,
            defaultValue,
            value,
            onChanged
        );

        Sequence layoutSeq = null;
        LayoutElement parentLayout = parent.GetComponent<LayoutElement>();
        void UpdateHeight() {
            float rowHeight = 50f;
            float spacing = layout.spacing;

            float listHeight =
                (values.Count * rowHeight) +
                (Mathf.Max(0, values.Count - 1) * spacing);

            float targetHeight = dropdown.Expanded ? 162f + listHeight : 50f;
            float targetAlpha = dropdown.Expanded ? 1f : 0f;

            layoutSeq?.Kill();

            layoutSeq = DOTween.Sequence()
                .Join(DOTween.To(
                    () => parentLayout.preferredHeight,
                    x => {
                        parentLayout.preferredHeight = x;
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                    },
                    targetHeight,
                    0.14f
                ).SetEase(Ease.OutBack))
                .Join(DOTween.To(
                    () => listCg.alpha,
                    x => listCg.alpha = x,
                    targetAlpha,
                    0.16f
                ).SetEase(Ease.OutSine))
                .SetUpdate(true);
        }

        dropdown.OnLayoutChanged = () => {
            UpdateHeight();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        };

        foreach(T item in values) {
            GameObject row = new("Row");
            row.transform.SetParent(list.transform, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new(0f, 50f);

            Image rowImage = row.AddComponent<Image>();
            rowImage.color = Color.clear;
            rowImage.type = Image.Type.Sliced;
            rowImage.sprite = SpriteDatabase.Get(UISliceSprite.Circle256P2048);

            TextMeshProUGUI rowText = AddText(rowRect);
            rowText.text = display(item);

            EventTrigger trigger = row.AddComponent<EventTrigger>();

            Sequence hoverSeq = null;

            AddEvent(EventTriggerType.PointerEnter, e => {
                hoverSeq?.Kill();

                hoverSeq = DOTween.Sequence().Append(
                    rowImage.DOColor(
                        UIColors.ObjectActive,
                        0.12f
                    ).SetEase(Ease.OutSine)
                ).SetUpdate(true);
            }, trigger);

            AddEvent(EventTriggerType.PointerExit, e => {
                hoverSeq?.Kill();

                hoverSeq = DOTween.Sequence().Append(
                    rowImage.DOColor(
                        Color.clear,
                        0.12f
                    ).SetEase(Ease.OutSine)
                ).SetUpdate(true);
            }, trigger);
        }

        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    dropdown.ToggleExpanded();
                    UpdateHeight();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                    break;

                case InputButton.Middle:
                    if(
                        Core.Config.MiddleClickToDefault && dropdown.DefaultValue != null &&
                        !EqualityComparer<T>.Default.Equals(
                            dropdown.Value,
                            dropdown.DefaultValue
                        )
                    ) {
                        dropdown.Reset();
                    }
                    break;
            }
        });

        UpdateHeight();
        return dropdown;
    }

    public static void AddEvent(EventTriggerType type, Action<PointerEventData> cb, EventTrigger trigger) {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(e => cb((PointerEventData)e));
        trigger.triggers.Add(entry);
    }

    public enum BackGroundType {
        Main,
        Sub,
        Full
    }

    public static RectTransform BackGround() {
        GameObject obj = new("Bg");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new(-300f, 0f);

        Image img = obj.AddComponent<Image>();
        img.color = UIColors.ObjectBG;
        img.sprite = SpriteDatabase.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;

        return rect;
    }

    public static void AddButton(GameObject obj, Action<InputButton> onClick, bool outline = true) {
        EventTrigger trigger = obj.AddComponent<EventTrigger>();

        Sequence hoverSeq = null;

        GameObject hover = new("Hover");
        hover.transform.SetParent(obj.transform, false);
        hover.transform.SetAsFirstSibling();

        RectTransform hoverRect = hover.AddComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.pivot = new(0.5f, 0.5f);
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;

        Image hoverImage = null;
        if(outline) {
            hoverImage = hover.AddComponent<Image>();
            hoverImage.sprite = SpriteDatabase.Get(UISliceSprite.CircleOutline256P2048);
            hoverImage.type = Image.Type.Sliced;
            hoverImage.color = UIColors.ObjectActive;
            hoverImage.color = new Color(hoverImage.color.r, hoverImage.color.g, hoverImage.color.b, 0f);
        }

        AddEvent(EventTriggerType.PointerClick, (e) => onClick?.Invoke(e.button), trigger);

        if(outline) {
            AddEvent(EventTriggerType.PointerEnter, (e) => {
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
                ).SetUpdate(true);
            }, trigger);

            AddEvent(EventTriggerType.PointerExit, (e) => {
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
                ).SetUpdate(true);
            }, trigger);
        }
    }

    public static TextMeshProUGUI AddText(RectTransform parent, bool noPad = false) => CreateText(parent, 24f, false, noPad);

    public static TextMeshProUGUI AddTextH1(RectTransform parent) => CreateText(parent, 32f, true, true);

    private static TextMeshProUGUI CreateText(RectTransform parent, float size, bool bold, bool noPad) {
        GameObject obj = new("Text");
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.offsetMin = new(noPad ? 0f : 16f, 0f);
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = ResourceManager.Get<TMP_FontAsset>(bold ? Asset.SUITMedium : Asset.SUITRegular);
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.characterSpacing = -3f;

        return tmp;
    }

    public static GameObject AddSmallChangedCircle(RectTransform parent) {
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
        img.color = UIColors.ObjectInactive;

        return obj;
    }

    public static Transform AddToolTip(this Transform parent, string key, string def) {
        EventTrigger trigger = parent.gameObject.GetComponent<EventTrigger>()
            ?? parent.gameObject.AddComponent<EventTrigger>();

        AddEvent(EventTriggerType.PointerEnter, (e) => Tooltip.Show(Core.Tr.Get(key, def)), trigger);

        AddEvent(EventTriggerType.PointerExit, (e) => Tooltip.Hide(), trigger);

        return parent;
    }
}
