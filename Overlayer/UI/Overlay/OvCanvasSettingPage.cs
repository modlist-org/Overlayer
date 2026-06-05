using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Utility;
using TMPro;
using UnityEngine;
using DG.Tweening;
using static UnityEngine.EventSystems.PointerEventData;

namespace Overlayer.UI.Overlay;

public class OvCanvasSettingPage : IDisposable {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    private readonly TextMeshProUGUI titleText;
    private readonly Transform contentTransform;
    private readonly Action onBackAction;

    private OvCanvas currentCanvas;

    public OvCanvasSettingPage(Transform parent, Action onBack) {
        onBackAction = onBack;

        GameObject = new(nameof(OvCanvasSettingPage), typeof(RectTransform), typeof(CanvasGroup));
        GameObject.transform.SetParent(parent, false);

        RectTransform = GameObject.GetComponent<RectTransform>();
        RectTransform.anchorMin = Vector2.zero;
        RectTransform.anchorMax = Vector2.one;
        RectTransform.offsetMin = Vector2.zero;
        RectTransform.offsetMax = Vector2.zero;

        CanvasGroup = GameObject.GetComponent<CanvasGroup>();
        CanvasGroup.alpha = 0f;
        CanvasGroup.blocksRaycasts = false;
        GameObject.SetActive(false);

        GameObject headerGo = new("Header", typeof(RectTransform));
        headerGo.transform.SetParent(GameObject.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = new Vector2(0, -60);
        headerRect.offsetMax = Vector2.zero;

        GameObject backBtnGo = new("BackButton", typeof(RectTransform), typeof(EmptyGraphic));
        backBtnGo.transform.SetParent(headerGo.transform, false);
        var backBtnRect = backBtnGo.GetComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0, 0.5f);
        backBtnRect.anchorMax = new Vector2(0, 0.5f);
        backBtnRect.sizeDelta = new Vector2(90, 50);
        backBtnRect.anchoredPosition = new Vector2(70, 0);

        GameObject backTxtGo = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        backTxtGo.transform.SetParent(backBtnGo.transform, false);
        var backTxtRect = backTxtGo.GetComponent<RectTransform>();
        backTxtRect.anchorMin = Vector2.zero;
        backTxtRect.anchorMax = Vector2.one;
        backTxtRect.offsetMin = Vector2.zero;
        backTxtRect.offsetMax = Vector2.zero;

        var bTxt = backTxtGo.GetComponent<TextMeshProUGUI>();
        bTxt.text = "←";
        bTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        bTxt.fontSize = 26;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.color = Color.white;

        GenerateUI.AddButton(backBtnGo, btn => {
            if(btn == InputButton.Left) {
                onBackAction?.Invoke();
            }
        }, false);

        GameObject titleGo = new("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(400, 50);
        titleRect.anchoredPosition = Vector2.zero;

        titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        GameObject contentGo = new("Content", typeof(RectTransform));
        contentGo.transform.SetParent(GameObject.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = new Vector2(0, -60);
        contentTransform = contentGo.transform;
    }

    public void Open(OvCanvas canvas, bool noAnimate = false) {
        currentCanvas = canvas;
        titleText.text = $"{(string.IsNullOrEmpty(canvas.Config.Name) ? "(Empty)" : canvas.Config.Name)}";

        GameObject.SetActive(true);
        CanvasGroup.blocksRaycasts = false;

        CanvasGroup.DOKill();
        if(noAnimate) {
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = true;
        } else {
            CanvasGroup.DOFade(1f, 0.25f).SetUpdate(true)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => {
                    CanvasGroup.blocksRaycasts = true;
                });
        }
    }

    public void Close(bool noAnimate = false) {
        CanvasGroup.blocksRaycasts = false;

        CanvasGroup.DOKill();
        if(noAnimate) {
            CanvasGroup.alpha = 0f;
            GameObject.SetActive(false);
        } else {
            CanvasGroup.DOFade(0f, 0.25f).SetUpdate(true)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => {
                    GameObject.SetActive(false);
                });
        }
    }

    public void Dispose() {
        CanvasGroup.DOKill();

        if(GameObject != null) {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
}