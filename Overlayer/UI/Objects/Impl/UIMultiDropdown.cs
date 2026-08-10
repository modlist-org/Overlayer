using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using GTweens.Tweens;
using Overlayer.Tween;
using GTweens.Builders;
using GTweens.Easings;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

public class UIMultiDropDown<T> : UIObject where T : struct, Enum {
    public T DefaultValue { get; }
    public T Value { get; private set; }
    public IReadOnlyList<T> Values { get; private set; }
    public Func<T, string> Display { get; }
    public Func<T, string> Summary { get; }
    public Action<T> OnChanged { get; }
    public TextMeshProUGUI Label { get; }
    public Image TriangleImage { get; }
    public RectTransform TriangleRect { get; }
    public Image ChangedImage { get; }
    public GameObject ListObject { get; }
    public RectTransform ListRect { get; }
    public CanvasGroup ListCanvasGroup { get; }
    public bool Expanded { get; private set; }

    public Action OnLayoutChanged;

    private GTween triangleSeq, changeSeq;
    public GTween LayoutSeq { get; set; }
    private readonly List<Image> selectionImages = [];

    public UIMultiDropDown(
        string id,
        RectTransform rect,
        TextMeshProUGUI label,
        Image triangleImage,
        RectTransform triangleRect,
        Image changedImage,
        GameObject listObject,
        RectTransform listRect,
        CanvasGroup listCanvasGroup,
        IReadOnlyList<T> values,
        Func<T, string> display,
        Func<T, string> summary,
        T defaultValue,
        T value,
        Action<T> onChanged
    ) : base(id, rect) {
        Label = label;
        TriangleImage = triangleImage;
        TriangleRect = triangleRect;
        ChangedImage = changedImage;
        ListObject = listObject;
        ListRect = listRect;
        ListCanvasGroup = listCanvasGroup;
        Values = values;
        Display = display;
        Summary = summary;
        DefaultValue = defaultValue;
        Value = value;
        OnChanged = onChanged;

        Label.text = Summary(Value);
        RebuildList();
        UpdateVisual(true);
    }

    public void Set(T value, bool invoke = true) {
        if(IsDisposed) {
            return;
        }

        Value = value;
        Label.text = Summary(Value);
        UpdateSelectionVisuals();

        if(invoke) {
            OnChanged?.Invoke(value);
        }

        UpdateVisual();
    }

    public void Toggle(T value) {
        ulong current = Convert.ToUInt64(Value);
        ulong flag = Convert.ToUInt64(value);
        Set((T)Enum.ToObject(typeof(T), current ^ flag));
    }

    public void Reset() => Set(DefaultValue);

    public void SetExpanded(bool expanded) {
        if(IsDisposed) {
            return;
        }

        Expanded = expanded;
        if(ListObject != null) {
            ListObject.SetActive(expanded);
            if(expanded) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(ListRect);
            }
        }
        UpdateVisual();
        OnLayoutChanged?.Invoke();
    }

    public void ToggleExpanded() => SetExpanded(!Expanded);

    public void UpdateVisual(bool noAnimate = false) {
        if(IsDisposed) {
            return;
        }

        triangleSeq?.Kill();
        changeSeq?.Kill();

        bool isDefault = EqualityComparer<T>.Default.Equals(DefaultValue, Value);

        if(noAnimate) {
            TriangleRect.localRotation = Expanded ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            TriangleImage.color = Expanded ? UIColors.ObjectActive : UIColors.ObjectInactive;

            Color c = ChangedImage.color;
            c.a = isDefault ? 0f : 1f;
            ChangedImage.color = c;
            UpdateSelectionVisuals();
            return;
        }

        triangleSeq = GTweenSequenceBuilder.New()
            .Join(
                TriangleRect.GTRotate(Expanded ? new Vector3(0f, 0f, 180f) : Vector3.zero, 0.4f)
                    .SetEasing(Easing.OutBack)
            )
            .Join(
                TriangleImage.GTColor(Expanded ? UIColors.ObjectActive : UIColors.ObjectInactive, 0.2f)
                    .SetEasing(Easing.OutSine)
            ).Build();
        MainCore.TC.Play(triangleSeq);
        changeSeq = ChangedImage
            .GTAlpha(isDefault ? 0f : 1f, 0.2f)
            .SetEasing(Easing.OutSine);
        MainCore.TC.Play(changeSeq);
        UpdateSelectionVisuals();
    }

    public void RebuildList() {
        if(IsDisposed || ListObject == null) {
            return;
        }

        selectionImages.Clear();

        for(int i = ListObject.transform.childCount - 1; i >= 0; i--) {
            Transform child = ListObject.transform.GetChild(i);
            if(child != null && !child.Equals(null)) {
                Object.Destroy(child.gameObject);
            }
        }

        foreach(T item in Values) {
            GameObject row = new("Row");
            row.transform.SetParent(ListObject.transform, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new(0f, 50f);

            Image rowImage = row.AddComponent<Image>();
            rowImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            rowImage.type = Image.Type.Sliced;
            rowImage.color = Color.clear;

            TextMeshProUGUI rowText = GenerateUI.AddText(rowRect);
            rowText.text = Display(item);
            rowText.rectTransform.offsetMax = new(-48f, 0f);
            rowText.raycastTarget = false;

            GameObject selected = new("Selected");
            selected.transform.SetParent(row.transform, false);
            RectTransform selectedRect = selected.AddComponent<RectTransform>();
            selectedRect.anchorMin = new(1f, 0.5f);
            selectedRect.anchorMax = new(1f, 0.5f);
            selectedRect.pivot = new(0.5f, 0.5f);
            selectedRect.anchoredPosition = new(-23f, 0f);
            selectedRect.sizeDelta = new(26f, 26f);
            Image selectedImage = selected.AddComponent<Image>();
            selectedImage.raycastTarget = false;
            selectionImages.Add(selectedImage);

            EventTrigger trigger = row.AddComponent<EventTrigger>();
            GenerateUI.AddOutlineHover(row, trigger);

            UnityUtils.AddEvents(trigger,
                (EventTriggerType.PointerClick, (e) => {
#pragma warning disable IDE0019
                    PointerEventData pointerData =
#pragma warning restore IDE0019
#if ML && IL2CPP
                    e.TryCast<PointerEventData>();
#else
                    e as PointerEventData;
#endif
                    if(pointerData == null || pointerData.button != PointerEventData.InputButton.Left) {
                        return;
                    }

                    Toggle(item);
                }
            )
            );
        }

        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals() {
        for(int i = 0; i < selectionImages.Count && i < Values.Count; i++) {
            selectionImages[i].sprite = MainCore.Spr.Get(
                HasFlag(Value, Values[i]) ? UISprite.Circle256 : UISprite.ToggleCircle128
            );
            selectionImages[i].color = HasFlag(Value, Values[i])
                ? UIColors.ObjectActive
                : UIColors.ObjectInactive;
        }
    }

    private static bool HasFlag(T value, T flag) {
        ulong valueBits = Convert.ToUInt64(value);
        ulong flagBits = Convert.ToUInt64(flag);
        return (valueBits & flagBits) == flagBits;
    }

    public override void SetBlocked(bool blocked, bool noAnimate = false) {
        base.SetBlocked(blocked, noAnimate);
        SetExpanded(false);
    }

    public override void Dispose() {
        if(IsDisposed) {
            return;
        }

        triangleSeq?.Kill();
        changeSeq?.Kill();
        LayoutSeq?.Kill();
        triangleSeq = null;
        changeSeq = null;
        LayoutSeq = null;
        OnLayoutChanged = null;
        selectionImages.Clear();
        base.Dispose();
    }
}
