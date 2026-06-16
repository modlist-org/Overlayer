using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using Overlayer.Tween;
using GTweens.Easings;
using GTweens.Builders;
using System;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Overlay;

public class OvCanvasSettingPage : IDisposable {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    private readonly TextMeshProUGUI titleText;
    private readonly Transform contentTransform;
    private readonly Action onBackAction;

    private OvCanvas currentCanvas;
    private readonly OvCanvasEditorPage editorPage;

    private GTween canvasFadeTween;

    public OvCanvasSettingPage(Transform parent, Action onBack) {
        onBackAction = onBack;

        GameObject = new(nameof(OvCanvasSettingPage));
        GameObject.transform.SetParent(parent, false);

        RectTransform = GameObject.AddComponent<RectTransform>();
        RectTransform.anchorMin = Vector2.zero;
        RectTransform.anchorMax = Vector2.one;
        RectTransform.offsetMin = Vector2.zero;
        RectTransform.offsetMax = Vector2.zero;

        CanvasGroup = GameObject.AddComponent<CanvasGroup>();
        CanvasGroup.alpha = 0f;
        CanvasGroup.blocksRaycasts = false;
        GameObject.SetActive(false);

        GameObject headerGo = new("Header");
        headerGo.transform.SetParent(GameObject.transform, false);
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = new Vector2(0, -60);
        headerRect.offsetMax = Vector2.zero;

        GameObject backBtnGo = new("BackButton");
        backBtnGo.transform.SetParent(headerGo.transform, false);
        var backBtnRect = backBtnGo.AddComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0, 0.5f);
        backBtnRect.anchorMax = new Vector2(0, 0.5f);
        backBtnRect.sizeDelta = new Vector2(90, 50);
        backBtnRect.anchoredPosition = new Vector2(70, 0);
        backBtnGo.AddComponent<EmptyGraphic>();

        GameObject backTxtGo = new("Text");
        backTxtGo.transform.SetParent(backBtnGo.transform, false);
        var backTxtRect = backTxtGo.AddComponent<RectTransform>();
        backTxtRect.anchorMin = Vector2.zero;
        backTxtRect.anchorMax = Vector2.one;
        backTxtRect.offsetMin = Vector2.zero;
        backTxtRect.offsetMax = Vector2.zero;

        var bTxt = backTxtGo.AddComponent<TextMeshProUGUI>();
        bTxt.text = "←";
        bTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        bTxt.fontSize = 26;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.color = Color.white;

        GenerateUI.AddButton(backBtnGo, btn => {
            if(btn == InputButton.Left) {
                onBackAction?.Invoke();
            }
        });

        GameObject titleGo = new("TitleText");
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(400, 50);
        titleRect.anchoredPosition = Vector2.zero;

        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        GameObject pad = new("Pad");
        pad.transform.SetParent(GameObject.transform, false);

        RectTransform padRect = pad.AddComponent<RectTransform>();
        padRect.anchorMin = Vector2.zero;
        padRect.anchorMax = Vector2.one;
        padRect.pivot = new Vector2(0.5f, 0.5f);
        padRect.offsetMin = new Vector2(18f, 18f);
        padRect.offsetMax = new Vector2(-18f, -76f);

        GameObject viewport = new("Viewport");
        viewport.transform.SetParent(pad.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);

        viewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        GameObject content = new("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        pad.AddComponent<UIScrollController>().SetContent(contentRect, viewportRect);

        contentTransform = content.transform;

        // Instantiate Object Editor Page (sub-view)
        editorPage = new OvCanvasEditorPage(parent, () => {
            editorPage.Close(true);
            this.Open(currentCanvas); // Open this setting page back when editor is closed
        });
    }

    private void SaveConfig() {
        OverlayCore.SaveAllCanvases();
    }

    public void Open(OvCanvas canvas, bool noAnimate = false) {
        currentCanvas = canvas;
        titleText.text = string.IsNullOrEmpty(canvas.Config.Name) ? "(Empty)" : canvas.Config.Name;

        foreach(Transform child in contentTransform) {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        // 1. Canvas Name Input
        var nameRow = GenerateUI.Row(contentTransform);
        GenerateUI.Input(nameRow, null, canvas.Config.Name, val => {
            canvas.Config.Name = val;
            titleText.text = val;
            canvas.ApplyConfig();
            SaveConfig();
        }, "Canvas Name", null, "canvas_name");

        // 2. Blocks Raycasts Toggle
        var raycastRow = GenerateUI.Row(contentTransform);
        GenerateUI.Toggle(raycastRow, true, canvas.Config.CanvasGroupConfig.BlocksRaycasts, val => {
            canvas.Config.CanvasGroupConfig.BlocksRaycasts = val;
            canvas.ApplyConfig();
            SaveConfig();
        }, "Blocks Raycasts", "blocks_raycasts");

        // 3. Edit Objects Button (Macro / Unity Editor transition)
        var editRow = GenerateUI.Row(contentTransform);
        GenerateUI.Button(editRow, () => {
            this.Close(true); // Close setting page instantly
            editorPage.Open(canvas); // Open the objects hierarchy/inspector editor
        }, "Edit Objects", "btn_edit_objects");

        GameObject.SetActive(true);

        if(noAnimate) {
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = true;
        } else {
            canvasFadeTween?.Kill();
            canvasFadeTween = CanvasGroup.GTFade(1f, 0.25f).SetEasing(Easing.OutCubic);
            canvasFadeTween.OnComplete(() => CanvasGroup.blocksRaycasts = true);
            MainCore.TC.Play(canvasFadeTween);
        }
    }

    public void Close(bool noAnimate = false) {
        CanvasGroup.blocksRaycasts = false;
        canvasFadeTween?.Kill();

        if(noAnimate) {
            CanvasGroup.alpha = 0f;
            GameObject.SetActive(false);
        } else {
            canvasFadeTween = CanvasGroup.GTFade(0f, 0.25f).SetEasing(Easing.OutCubic);
            canvasFadeTween.OnComplete(() => GameObject.SetActive(false));
            MainCore.TC.Play(canvasFadeTween);
        }
    }

    public void Dispose() {
        canvasFadeTween?.Kill();
        editorPage?.Dispose();

        if(GameObject != null) {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
}