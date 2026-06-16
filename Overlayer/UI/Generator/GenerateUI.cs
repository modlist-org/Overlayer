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
using GTweens.Builders;
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

        AddOutlineHover(rect.gameObject, rect.gameObject.AddComponent<EventTrigger>());
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

        var trigger = rect.gameObject.AddComponent<EventTrigger>();

        AddOutlineHover(rect.gameObject, trigger);

        AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) {
                button.Click();
            }
        });

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, button.OnHoverEnter),
            (EventTriggerType.PointerExit, button.OnHoverExit)
        );

        return button;
    }

    public static UISlider Slider(
        Transform parent,
        float defaultValue,
        float min,
        float max,
        float value,
        string format,
        bool useInputClamp,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string text,
        string id
    ) {
        RectTransform rect = BackGround();
        rect.SetParent(parent, false);

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

        GameObject inputObj = new("ValueInput");
        inputObj.transform.SetParent(rect, false);

        CanvasGroup inputCanvasGroup = inputObj.AddComponent<CanvasGroup>();
        inputCanvasGroup.blocksRaycasts = false;

        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new(16f, 4f);
        inputRect.offsetMax = new(-8f, -4f);
        inputObj.AddComponent<RectMask2D>();

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        var textComp = AddText(inputObj.transform);
        textComp.alignment = TextAlignmentOptions.Right;
        textComp.verticalAlignment = VerticalAlignmentOptions.Middle;

        inputField.textComponent = textComp;
        inputField.textViewport = inputRect;

        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImg.type = Image.Type.Sliced;
        fill.AddComponent<Mask>().showMaskGraphic = true;

        GameObject changeUp = AddSmallChangedCircle(fillRect);
        Image changeUpImg = changeUp.GetComponent<Image>();

        var trigger = rect.gameObject.AddComponent<EventTrigger>();

        UISlider slider = new(
            id, rect, fillRect, fillImg, label, inputField,
            changeImg, changeUpImg, AddOutlineHover(rect.gameObject, trigger), defaultValue, min, max,
            value, format, useInputClamp, filter, onChanged, onComplete
        );

        AddButton(rect.gameObject, e => {
            switch(e) {
                case InputButton.Middle:
                    if(!MainCore.Conf.MiddleClickToDefault) {
                        break;
                    }
                    slider.Set(Apply(defaultValue));
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
            }
        });

        float Apply(float v) {
            v = filter != null ? filter(v) : v;
            return Math.Clamp(v, min, max);
        }

        bool isDragging = false;
        float cachedValue = 0f;
        Vector2Int resetPos = Vector2Int.zero;
        Vector2 previousMousePos = Vector2.zero;
        bool justWarped = false;

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.BeginDrag, (e) => {
                if(!OVC_Input.GetMouseButton(0)) {
                    return;
                }
                isDragging = true;
                cachedValue = slider.Value;

                resetPos = Vector2Int.RoundToInt(OVC_Input.OSMousePosition);
                previousMousePos = OVC_Input.MousePosition;
                justWarped = false;
            }
        ),
            (EventTriggerType.Drag, (e) => {
                if(isDragging && OVC_Input.GetMouseButton(0)) {
                    Vector2 currentMousePos = OVC_Input.MousePosition;
                    Vector2 mousePixelDelta = currentMousePos - previousMousePos;
                    previousMousePos = currentMousePos;

                    if(justWarped) {
                        mousePixelDelta = Vector2.zero;
                        justWarped = false;
                    }

                    float finalPixelWidth = inputRect.rect.width * UICore.Canvas.scaleFactor;
                    cachedValue += mousePixelDelta.x * (slider.Max - slider.Min) * MainCore.Conf.SliderSensitivity / finalPixelWidth;
                    cachedValue = Math.Clamp(cachedValue, min, max);
                    slider.Set(Apply(cachedValue));

                    Cursor.visible = false;

                    Vector2Int currentOSPos = OVC_Input.OSMousePosition;
                    int screenWidth = Screen.currentResolution.width;
                    int padding = 5;

                    if(currentOSPos.x <= padding) {
                        currentOSPos.x = screenWidth - padding - 1;
                        OVC_Input.OSMousePosition = new(currentOSPos.x, currentOSPos.y);
                        previousMousePos = new(Screen.width - padding - 1, currentMousePos.y);
                        justWarped = true;
                    } else if(currentOSPos.x >= screenWidth - padding) {
                        currentOSPos.x = padding + 1;
                        OVC_Input.OSMousePosition = new(currentOSPos.x, currentOSPos.y);
                        previousMousePos = new(padding + 1, currentMousePos.y);
                        justWarped = true;
                    }
                } else {
                    isDragging = false;
                }
            }
        ),
            (EventTriggerType.EndDrag, (e) => {
                if(isDragging) {
                    isDragging = false;
                    slider.OnComplete?.Invoke(slider.Value);

                    OVC_Input.OSMousePosition = resetPos;
                    Cursor.visible = true;
                }
            }
        ),
            (EventTriggerType.PointerUp, (e) => {
                if(isDragging) {
                    return;
                }

#pragma warning disable IDE0019
                var ped =
#pragma warning restore IDE0019
#if ML && IL2CPP                
                e.TryCast<PointerEventData>();
#else
                e as PointerEventData;
#endif
                if(ped != null && ped.button != InputButton.Left) {
                    return;
                }

                if(EventSystem.current) {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                inputField.Select();
                inputField.ActivateInputField();
            }
        )
        );

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

        LayoutElement parentLayout = parent.GetComponent<LayoutElement>();
        void UpdateHeight() {
            float rowHeight = 50f;
            float spacing = layout.spacing;

            float listHeight = (dropdown.Values.Count * rowHeight) + spacing;

            float targetHeight = dropdown.Expanded ? (62f + listHeight) : 50f;
            float targetAlpha = dropdown.Expanded ? 1f : 0f;

            dropdown.LayoutSeq?.Kill();

            dropdown.LayoutSeq = GTweenSequenceBuilder.New()
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
            MainCore.TC.Play(dropdown.LayoutSeq);
        }

        dropdown.OnLayoutChanged = UpdateHeight;

        AddOutlineHover(rect.gameObject, rect.gameObject.AddComponent<EventTrigger>());

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

        dropdown.RebuildList();
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
        if(icon == null) {
            iconImage.enabled = false;
        } else {
            iconImage.color = new Color(1f, 1f, 1f, 0.2f);
        }

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

        var trigger = rect.gameObject.AddComponent<EventTrigger>();
        AddOutlineHover(rect.gameObject, trigger);

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerUp, (e) => {
#pragma warning disable IDE0019
                var ped =
#pragma warning restore IDE0019
#if ML && IL2CPP                
                e.TryCast<PointerEventData>();
#else
                e as PointerEventData;
#endif
                if(ped != null && ped.button != InputButton.Left) {
                    return;
                }

                if(EventSystem.current) {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                inputField.Select();
                inputField.ActivateInputField();
            })
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

    public static OventHandler AddButton(GameObject obj, Action<InputButton> onClick) {
        var com = obj.AddComponent<OventHandler>();
        com.OnClick += onClick;

        return com;
    }

    public static Image AddOutlineHover(GameObject obj, EventTrigger trigger) {
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

        var destroyer = hover.AddComponent<TweenDestroyer>();

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Append(GTweenExtensions.Tween(
                        () => hoverImage ? hoverImage.color.a : 0f,
                        x => { if(hoverImage) { Color c = hoverImage.color; c.a = x; hoverImage.color = c; } },
                        1f, 0.1f
                    ).SetEasing(Easing.OutSine)).Build();
                destroyer.Set(hoverSeq);
                MainCore.TC.Play(hoverSeq);
            }
        ),
            (EventTriggerType.PointerExit, () => {
                hoverSeq?.Kill();
                hoverSeq = GTweenSequenceBuilder.New()
                    .Append(GTweenExtensions.Tween(
                        () => hoverImage ? hoverImage.color.a : 0f,
                        x => { if(hoverImage) { Color c = hoverImage.color; c.a = x; hoverImage.color = c; } },
                        0f, 0.1f
                    ).SetEasing(Easing.OutSine)).Build();
                destroyer.Set(hoverSeq);
                MainCore.TC.Play(hoverSeq);
            }
        )
        );

        return hoverImage;
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

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => Tooltip.Show(getText())),
            (EventTriggerType.PointerExit, Tooltip.Hide)
        );

        return parent;
    }

    public static (RectTransform cardRect, RectTransform contentRect) ComponentCard(
        Transform parent,
        string title,
        bool activeValue,
        Action<bool> onActiveChanged,
        Action onDeleteClick,
        bool showDeleteButton = true
    ) {
        GameObject cardGo = new("ComponentCard_" + title);
        cardGo.transform.SetParent(parent, false);

        RectTransform cardRect = cardGo.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);
        cardRect.sizeDelta = new Vector2(0f, 0f);

        var cardLayout = cardGo.AddComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 0f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        GameObject headerGo = new("Header");
        headerGo.transform.SetParent(cardGo.transform, false);
        RectTransform headerRect = headerGo.AddComponent<RectTransform>();

        var headerLayout = headerGo.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 40f;
        headerLayout.minHeight = 40f;

        var headerImg = headerGo.AddComponent<Image>();
        headerImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        headerImg.type = Image.Type.Sliced;
        headerImg.color = UIColors.MenuBG;

        var headerHLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
        headerHLayout.padding = new RectOffset(10, 10, 0, 0);
        headerHLayout.spacing = 8f;
        headerHLayout.childControlWidth = true;
        headerHLayout.childControlHeight = true;
        headerHLayout.childForceExpandWidth = false;
        headerHLayout.childForceExpandHeight = false;
        headerHLayout.childAlignment = TextAnchor.MiddleLeft;

        GameObject checkboxGo = new("Checkbox");
        checkboxGo.transform.SetParent(headerGo.transform, false);
        RectTransform checkboxRect = checkboxGo.AddComponent<RectTransform>();
        checkboxRect.sizeDelta = new Vector2(20f, 20f);
        var checkboxLE = checkboxGo.AddComponent<LayoutElement>();
        checkboxLE.preferredWidth = 20f;
        checkboxLE.preferredHeight = 20f;

        var checkboxImg = checkboxGo.AddComponent<Image>();
        checkboxImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
        checkboxImg.color = activeValue ? UIColors.ObjectActive : new Color(0.2f, 0.2f, 0.2f, 0.8f);

        bool isCurrentActive = activeValue;
        AddButton(checkboxGo, btn => {
            if(btn == InputButton.Left) {
                isCurrentActive = !isCurrentActive;
                checkboxImg.color = isCurrentActive ? UIColors.ObjectActive : new Color(0.2f, 0.2f, 0.2f, 0.8f);
                onActiveChanged?.Invoke(isCurrentActive);
            }
        });

        GameObject titleTriggerGo = new("TitleTrigger");
        titleTriggerGo.transform.SetParent(headerGo.transform, false);
        RectTransform titleTriggerRect = titleTriggerGo.AddComponent<RectTransform>();
        var titleTriggerLE = titleTriggerGo.AddComponent<LayoutElement>();
        titleTriggerLE.flexibleWidth = 1f;

        var triggerHLayout = titleTriggerGo.AddComponent<HorizontalLayoutGroup>();
        triggerHLayout.padding = new RectOffset();
        triggerHLayout.childControlWidth = true;
        triggerHLayout.childControlHeight = true;
        triggerHLayout.childForceExpandWidth = true;
        triggerHLayout.childForceExpandHeight = true;

        GameObject titleGo = new("TitleText");
        titleGo.transform.SetParent(titleTriggerGo.transform, false);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        titleText.fontSize = 20f;
        titleText.text = title;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
        titleText.characterSpacing = -3f;

        if(showDeleteButton) {
            GameObject deleteGo = new("DeleteBtn");
            deleteGo.transform.SetParent(headerGo.transform, false);
            RectTransform deleteRect = deleteGo.AddComponent<RectTransform>();
            deleteRect.sizeDelta = new Vector2(22f, 22f);
            var deleteLE = deleteGo.AddComponent<LayoutElement>();
            deleteLE.preferredWidth = 22f;
            deleteLE.preferredHeight = 22f;

            var deleteImg = deleteGo.AddComponent<Image>();
            deleteImg.sprite = MainCore.Spr.Get(UISprite.X128);
            deleteImg.color = UIColors.SoftRed;

            AddOutlineHover(deleteGo, deleteGo.AddComponent<EventTrigger>());
            AddButton(deleteGo, btn => {
                if(btn == InputButton.Left) {
                    onDeleteClick?.Invoke();
                }
            });
        }

        GameObject contentGo = new("Content");
        contentGo.transform.SetParent(cardGo.transform, false);
        RectTransform contentRect = contentGo.AddComponent<RectTransform>();

        var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(12, 12, 8, 8);
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var contentImg = contentGo.AddComponent<Image>();
        contentImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        contentImg.type = Image.Type.Sliced;
        contentImg.color = UIColors.ObjectBG;

        bool isExpanded = true;
        AddButton(titleTriggerGo, btn => {
            if(btn == InputButton.Left) {
                isExpanded = !isExpanded;
                contentGo.SetActive(isExpanded);
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);

                Transform p = cardGo.transform.parent;
                while(p != null) {
                    var pRect = p.GetComponent<RectTransform>();
                    if(pRect != null) {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(pRect);
                    }
                    p = p.parent;
                }
            }
        });

        return (cardRect, contentRect);
    }

    public class TweenDestroyer : MonoBehaviour {
        private GTween tween;
        public void Set(GTween newTween) {
            tween?.Kill();
            tween = newTween;
        }
        void OnDestroy() {
            tween?.Kill();
        }
    }
}
