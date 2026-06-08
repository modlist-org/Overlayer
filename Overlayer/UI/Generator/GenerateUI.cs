using Overlayer.Core;
using Overlayer.Localization;
using Overlayer.Resource;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using GTweens.Easings;
using GTweens.Extensions;

using GTweens.Builders;
using Overlayer.Tween;
using Overlayer.Compat.OVC;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

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
                        MainCore.Conf.MiddleClickToDefault &&
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
            if(btn == InputButton.Left) {
                button.Click();
            }
        }, false);

        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();

        UnityUtils.AddEvent(EventTriggerType.PointerEnter, e => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, e => button.OnHoverExit(), trigger);

        return button;
    }

    public static UISlider Slider(
        Transform parent,
        float defaultValue,
        float min,
        float max,
        float value,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string text,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);

        rect.gameObject.AddComponent<EventTrigger>();

        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();

        GameObject fill = new("Fill");
        fill.transform.SetParent(rect, false);

        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI label = AddText(rect);
        label.text = text;
        label.alignment = TextAlignmentOptions.Left;

        TextMeshProUGUI valueText = AddText(rect);
        valueText.alignment = TextAlignmentOptions.Right;

        var valueTextRect = valueText.gameObject.GetComponent<RectTransform>();
        valueTextRect.offsetMin = Vector2.zero;
        valueTextRect.offsetMax = new(-16f, 0f);

        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImg.type = Image.Type.Sliced;

        fill.AddComponent<Mask>().showMaskGraphic = true;

        GameObject changeUp = AddSmallChangedCircle(fillRect);
        Image changeUpImg = changeUp.GetComponent<Image>();

        UISlider slider = new(
            id,
            rect,
            fillRect,
            fillImg,
            label,
            valueText,
            changeImg,
            changeUpImg,
            defaultValue,
            min,
            max,
            value,
            filter,
            onChanged,
            onComplete
        );

        float Apply(float v) {
            v = filter != null ? filter(v) : v;
            return Mathf.Clamp(v, min, max);
        }

        void SetFromMouse() {
            Vector2 local = rect.InverseTransformPoint(OVC_Input.MousePosition);
            float width = rect.rect.width;

            float t = Mathf.Clamp01((local.x + (width * 0.5f)) / width);
            float v = Mathf.Lerp(min, max, t);

            slider.Set(Apply(v));
        }

        AddButton(rect.gameObject, e => {
            switch(e) {
                case InputButton.Left:
                    SetFromMouse();
                    slider.OnComplete?.Invoke(slider.Value);
                    break;

                case InputButton.Middle:
                    if(!MainCore.Conf.MiddleClickToDefault) {
                        break;
                    }
                    slider.Set(Apply(defaultValue));
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
            }
        }, true);

        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        bool isDragging = false;

        UnityUtils.AddEvent(EventTriggerType.BeginDrag, _ => {
            isDragging = true;
            SetFromMouse();
        }, trigger);

        UnityUtils.AddEvent(EventTriggerType.Drag, _ => {
            if(isDragging && OVC_Input.GetMouseButton(0)) {
                SetFromMouse();
            } else {
                isDragging = false;
            }
        }, trigger);

        UnityUtils.AddEvent(EventTriggerType.EndDrag, _ => {
            if(isDragging) {
                isDragging = false;
                slider.OnComplete?.Invoke(slider.Value);
            }
        }, trigger);

        UnityUtils.AddEvent(EventTriggerType.PointerUp, _ => {
            if(isDragging) {
                isDragging = false;
                slider.OnComplete?.Invoke(slider.Value);
            }
        }, trigger);

        slider.Set(Apply(value), false);

        return slider;
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
        triangleImage.sprite = MainCore.Spr.Get(UISprite.Triangle128);

        GameObject list = new("List");
        list.transform.SetParent(root.transform, false);

        RectTransform listRect = list.AddComponent<RectTransform>();
        listRect.anchorMin = new(0f, 1f);
        listRect.anchorMax = new(1f, 1f);
        listRect.pivot = new(0.5f, 1f);
        listRect.offsetMin = new(0f, -62f);
        listRect.offsetMax = new(-250f, -62f);

        Image listBg = list.AddComponent<Image>();
        listBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
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

        GTween layoutSeq = null;
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

            layoutSeq = GTweenSequenceBuilder.New()
                .Join(
                    GTweenExtensions.Tween(
                        () => parentLayout.preferredHeight,
                        x => {
                            parentLayout.preferredHeight = x;
                            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                        },
                        targetHeight,
                        0.14f
                    ).SetEasing(Easing.OutBack)
                )
                .Join(
                    GTweenExtensions.Tween(
                        () => listCg.alpha,
                        x => listCg.alpha = x,
                        targetAlpha,
                        0.16f
                    ).SetEasing(Easing.OutSine)
                )
                .Build();
            MainCore.TC.Play(layoutSeq);
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
            rowImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);

            TextMeshProUGUI rowText = AddText(rowRect);
            rowText.text = display(item);

            EventTrigger trigger = row.AddComponent<EventTrigger>();
            GTween hoverSeq = null;

            UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Append(
                        rowImage.GTColor(UIColors.ObjectActive, 0.12f)
                            .SetEasing(Easing.OutSine)
                    )
                    .Build();

                MainCore.TC.Play(hoverSeq);
            }, trigger);

            UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Append(
                        rowImage.GTColor(Color.clear, 0.12f)
                            .SetEasing(Easing.OutSine)
                    )
                    .Build();

                MainCore.TC.Play(hoverSeq);
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
                        MainCore.Conf.MiddleClickToDefault && dropdown.DefaultValue != null &&
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

    public static UIInput Input(
        Transform parent,
        string defaultValue,
        string value,
        Action<string> onChanged,
        string placeholder,
        Sprite icon,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);

        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();

        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(rect, false);

        RectTransform circleRect = iconObj.AddComponent<RectTransform>();
        circleRect.anchorMin = new(1f, 0.5f);
        circleRect.anchorMax = new(1f, 0.5f);
        circleRect.pivot = new(0.5f, 0.5f);
        circleRect.anchoredPosition = new(-23f, 0f);
        circleRect.sizeDelta = new(26f, 26f);

        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = icon;
        iconImage.color = new Color(1f, 1f, 1f, 0.2f);

        GameObject inputObj = new("Input");
        inputObj.transform.SetParent(rect, false);

        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new(16f, 4f);
        inputRect.offsetMax = new(-12f, -4f);
        inputObj.AddComponent<RectMask2D>();

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();

        var text = AddText(inputObj.transform);
        text.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        text.text = value ?? string.Empty;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var placeholderText = AddText(inputObj.transform);
        placeholderText.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        placeholderText.text = placeholder;
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        placeholderText.color = new Color(1, 1, 1, 0.2f);

        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        inputField.textViewport = inputRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholderText;

        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.richText = false;

        var input = new UIInput(
            id,
            rect,
            inputField,
            placeholderText,
            iconImage,
            changeImg,
            defaultValue,
            value,
            onChanged
        );

        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Middle:
                    if(
                        MainCore.Conf.MiddleClickToDefault &&
                        input.Value != input.DefaultValue
                    ) {
                        input.Reset();
                    }

                    break;
            }
        });

        return input;
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
        rect.offsetMax = new(-250f, 0f);

        Image img = obj.AddComponent<Image>();
        img.color = UIColors.ObjectBG;
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;

        return rect;
    }

    public static void AddButton(GameObject obj, Action<InputButton> onClick, bool outline = true) {
        EventTrigger trigger = obj.AddComponent<EventTrigger>();

        AddClick(trigger, onClick);

        if(outline) {
            AddOutlineHover(obj, trigger);
        }
    }

    private static void AddClick(EventTrigger trigger, Action<InputButton> onClick)
        => UnityUtils.AddEvent(EventTriggerType.PointerClick, (_) => onClick?.Invoke(OVC_Input.GetClickMouseButton()), trigger);

    private static void AddOutlineHover(GameObject obj, EventTrigger trigger) {
        GTween hoverSeq = null;

        GameObject hover = new("Hover");
        hover.transform.SetParent(obj.transform, false);
        hover.transform.SetAsFirstSibling();

        RectTransform hoverRect = hover.AddComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.pivot = new Vector2(0.5f, 0.5f);
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;

        Image hoverImage = hover.AddComponent<Image>();
        hoverImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        hoverImage.type = Image.Type.Sliced;

        Color baseColor = UIColors.ObjectActive;
        hoverImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        UnityUtils.AddEvent(EventTriggerType.PointerEnter, (e) => {
            hoverSeq?.Kill();
            hoverSeq = GTweenSequenceBuilder.New()
                .Append(GTweenExtensions.Tween(
                    () => hoverImage.color.a,
                    x => {
                        Color c = hoverImage.color;
                        c.a = x;
                        hoverImage.color = c;
                    },
                    1f,
                    0.1f
                ).SetEasing(Easing.OutSine))
                .Build();

            MainCore.TC.Play(hoverSeq);
        }, trigger);

        UnityUtils.AddEvent(EventTriggerType.PointerExit, (e) => {
            hoverSeq?.Kill();
            hoverSeq = GTweenSequenceBuilder.New()
                .Append(GTweenExtensions.Tween(
                    () => hoverImage.color.a,
                    x => {
                        Color c = hoverImage.color;
                        c.a = x;
                        hoverImage.color = c;
                    },
                    0f,
                    0.1f
                ).SetEasing(Easing.OutSine))
                .Build();
            MainCore.TC.Play(hoverSeq);
        }, trigger);
    }

    public static TextMeshProUGUI AddText(Transform parent, bool noPad = false) => CreateText(parent, 24f, false, noPad);

    public static TextMeshProUGUI AddTextH1(Transform parent) => CreateText(parent, 32f, true, true);

    private static TextMeshProUGUI CreateText(Transform parent, float size, bool bold, bool noPad) {
        GameObject obj = new("Text");
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 0f);
        rect.anchorMax = new(1f, 1f);
        rect.offsetMin = new(noPad ? 0f : 16f, 0f);
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainCore.Res.Get<TMP_FontAsset>(bold ? Asset.SUIT_Medium : Asset.SUIT_Regular);
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
        img.sprite = MainCore.Spr.Get(UISprite.Circle256);
        Color c = UIColors.ObjectActive;
        c.a = 0f;
        img.color = c;

        return obj;
    }

    public static Transform AddToolTip(this Transform parent, string key, string def, Translator tr = null) {
        tr ??= MainCore.Tr;
        return parent.AddToolTipInternal(() => tr.Get(key, def));
    }

    public static Transform AddToolTip(this Transform parent, string tip)
        => parent.AddToolTipInternal(() => tip);

    private static Transform AddToolTipInternal(this Transform parent, Func<string> getText) {
        EventTrigger trigger = parent.gameObject.GetComponent<EventTrigger>()
            ?? parent.gameObject.AddComponent<EventTrigger>();

        UnityUtils.AddEvent(
            EventTriggerType.PointerEnter,
            _ => Tooltip.Show(getText()),
            trigger
        );

        UnityUtils.AddEvent(
            EventTriggerType.PointerExit,
            _ => Tooltip.Hide(),
            trigger
        );

        return parent;
    }
}
