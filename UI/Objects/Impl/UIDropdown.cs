using DG.Tweening;
using Overlayer.UI.Generator;
using Overlayer.UI.SpriteManage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overlayer.UI.Objects.Impl;

public class UIDropDown<T> : UIObject {
    public T DefaultValue { get; }
    public T Value { get; private set; }

    public IReadOnlyList<T> Values { get; private set; }

    public Func<T, string> Display { get; }
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

    private Sequence triangleSeq;
    private Sequence changeSeq;

    public UIDropDown(
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
        DefaultValue = defaultValue;

        Value = value;

        OnChanged = onChanged;

        Label.text = Display(Value);

        RebuildList();
        UpdateVisual();
    }

    public void Set(T value, bool invoke = true) {
        Value = value;

        Label.text = Display(Value);

        if(invoke) {
            OnChanged?.Invoke(value);
        }

        UpdateVisual();

        OnLayoutChanged?.Invoke();
    }

    public void SetValues(IReadOnlyList<T> values) {
        Values = values;

        RebuildList();

        if(!Values.Contains(Value)) {
            if(Values.Count > 0) {
                Set(Values[0], false);
            }
        }
    }

    public void Reset() => Set(DefaultValue);

    public void SetExpanded(bool expanded) {
        Expanded = expanded;

        ListObject?.SetActive(expanded);

        UpdateVisual();

        OnLayoutChanged?.Invoke();
    }

    public void ToggleExpanded() => SetExpanded(!Expanded);

    public void UpdateVisual() {
        triangleSeq?.Kill();

        triangleSeq = DOTween.Sequence()
            .Join(
                TriangleRect.DORotate(
                    Expanded
                        ? new Vector3(0f, 0f, 180f)
                        : Vector3.zero,
                    0.4f
                ).SetEase(Ease.OutBack)
            )
            .Join(
                TriangleImage.DOColor(
                    Expanded
                        ? UIColors.ObjectActive
                        : UIColors.ObjectInactive,
                    0.2f
                ).SetEase(Ease.OutSine)
            ).SetUpdate(true);

        changeSeq?.Kill();

        bool isDefault =
            DefaultValue == null ||
            EqualityComparer<T>.Default.Equals(DefaultValue, Value);

        changeSeq = DOTween.Sequence().Append(
            DOTween.To(
                () => ChangedImage.color.a,
                x => {
                    Color c = ChangedImage.color;
                    c.a = x;
                    ChangedImage.color = c;
                },
                isDefault ? 0f : 1f,
                0.2f
            ).SetEase(Ease.OutSine).SetUpdate(true)
        );
    }

    public void RebuildList() {
        if(ListObject == null) {
            return;
        }

        foreach(Transform child in ListObject.transform) {
            Object.Destroy(child.gameObject);
        }

        foreach(T item in Values) {
            GameObject row = new("Row");
            row.transform.SetParent(ListObject.transform, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new(0f, 50f);

            Image rowImage = row.AddComponent<Image>();
            rowImage.sprite = SpriteDatabase.Get(UISliceSprite.Circle256P2048);
            rowImage.type = Image.Type.Sliced;
            rowImage.color = Color.clear;

            TextMeshProUGUI rowText = GenerateUI.AddText(rowRect);
            rowText.text = Display(item);

            EventTrigger trigger = row.AddComponent<EventTrigger>();

            Sequence hoverSeq = null;

            GenerateUI.AddEvent(EventTriggerType.PointerEnter, e => {
                hoverSeq?.Kill();

                hoverSeq = DOTween.Sequence().Append(
                    rowImage.DOColor(
                        UIColors.ObjectActive,
                        0.12f
                    ).SetEase(Ease.OutSine)
                ).SetUpdate(true);
            }, trigger);

            GenerateUI.AddEvent(EventTriggerType.PointerExit, e => {
                hoverSeq?.Kill();

                hoverSeq = DOTween.Sequence().Append(
                    rowImage.DOColor(
                        Color.clear,
                        0.12f
                    ).SetEase(Ease.OutSine)
                ).SetUpdate(true);
            }, trigger);

            GenerateUI.AddEvent(EventTriggerType.PointerClick, e => {
                if(e.button != PointerEventData.InputButton.Left) {
                    return;
                }

                Set(item);

                rowImage.color = Color.clear;

                SetExpanded(false);
            }, trigger);
        }
    }

    public override void SetBlocked(bool blocked, bool noAnimate = false) {
        base.SetBlocked(blocked, noAnimate);
        SetExpanded(false);
    }
}