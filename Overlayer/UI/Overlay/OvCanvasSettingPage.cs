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
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.IO.Overlay;

#if ML && IL2CPP
using Il2CppTMPro;
using Il2CppSystem.Collections.Generic;
#else
using TMPro;
#endif

namespace Overlayer.UI.Overlay;

public class OvCanvasSettingPage : IDisposable {
    public readonly GameObject GameObject;
    public readonly RectTransform RectTransform;
    public readonly CanvasGroup CanvasGroup;

    private readonly TextMeshProUGUI titleText;
    private readonly Action onBackAction;

    private OvCanvas currentCanvas;
    private OvObject selectedObject;

    private RectTransform hierarchyContent;
    private RectTransform inspectorContent;

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

        // Header
        GameObject headerGo = new("Header");
        headerGo.transform.SetParent(GameObject.transform, false);
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = new Vector2(0, -60);
        headerRect.offsetMax = Vector2.zero;

        // Back Button
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

        // Title Text
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

        // Pad (Layout Area)
        GameObject pad = new("Pad");
        pad.transform.SetParent(GameObject.transform, false);

        RectTransform padRect = pad.AddComponent<RectTransform>();
        padRect.anchorMin = Vector2.zero;
        padRect.anchorMax = Vector2.one;
        padRect.pivot = new Vector2(0.5f, 0.5f);
        padRect.offsetMin = new Vector2(18f, 18f);
        padRect.offsetMax = new Vector2(-18f, -76f);

        // 2-Column Horizontal Layout
        var padHLayout = pad.AddComponent<HorizontalLayoutGroup>();
        padHLayout.spacing = 18f;
        padHLayout.childControlWidth = true;
        padHLayout.childControlHeight = true;
        padHLayout.childForceExpandWidth = false;
        padHLayout.childForceExpandHeight = true;

        // ==================== 1. Hierarchy Column ====================
        GameObject hierarchyCol = new("HierarchyColumn");
        hierarchyCol.transform.SetParent(pad.transform, false);
        var hierColRect = hierarchyCol.AddComponent<RectTransform>();
        var hierColLE = hierarchyCol.AddComponent<LayoutElement>();
        hierColLE.preferredWidth = 350f;
        hierColLE.minWidth = 250f;
        hierColLE.flexibleWidth = 0f;

        var hierBG = hierarchyCol.AddComponent<Image>();
        hierBG.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        hierBG.type = Image.Type.Sliced;
        hierBG.color = UIColors.PanelBG;

        var hierVLayout = hierarchyCol.AddComponent<VerticalLayoutGroup>();
        hierVLayout.padding = new RectOffset(10, 10, 10, 10);
        hierVLayout.spacing = 10f;
        hierVLayout.childControlWidth = true;
        hierVLayout.childControlHeight = false;
        hierVLayout.childForceExpandWidth = true;
        hierVLayout.childForceExpandHeight = false;

        // Hierarchy Title
        GameObject hierTitle = new("HierarchyTitle");
        hierTitle.transform.SetParent(hierarchyCol.transform, false);
        var hierTitleTxt = hierTitle.AddComponent<TextMeshProUGUI>();
        hierTitleTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        hierTitleTxt.fontSize = 20f;
        hierTitleTxt.text = "Hierarchy";
        hierTitleTxt.color = Color.white;

        // Hierarchy Scroll View
        GameObject hierViewport = new("HierarchyViewport");
        hierViewport.transform.SetParent(hierarchyCol.transform, false);
        var hierViewportRect = hierViewport.AddComponent<RectTransform>();
        var hierViewportLE = hierViewport.AddComponent<LayoutElement>();
        hierViewportLE.flexibleHeight = 1f;

        hierViewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        hierViewport.AddComponent<RectMask2D>();

        GameObject hierContent = new("HierarchyContent");
        hierContent.transform.SetParent(hierViewport.transform, false);
        hierarchyContent = hierContent.AddComponent<RectTransform>();
        hierarchyContent.anchorMin = new Vector2(0f, 1f);
        hierarchyContent.anchorMax = new Vector2(1f, 1f);
        hierarchyContent.pivot = new Vector2(0.5f, 1f);
        hierarchyContent.offsetMin = Vector2.zero;
        hierarchyContent.offsetMax = Vector2.zero;

        var hierContentLayout = hierContent.AddComponent<VerticalLayoutGroup>();
        hierContentLayout.spacing = 6f;
        hierContentLayout.childControlWidth = true;
        hierContentLayout.childControlHeight = false;
        hierContentLayout.childForceExpandWidth = true;
        hierContentLayout.childForceExpandHeight = false;

        var hierContentFitter = hierContent.AddComponent<ContentSizeFitter>();
        hierContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        hierarchyCol.AddComponent<UIScrollController>().SetContent(hierarchyContent, hierViewportRect);

        // Hierarchy Create Toolbar (Text, Image, Empty)
        GameObject hierCreateToolbar = new("HierarchyCreateToolbar");
        hierCreateToolbar.transform.SetParent(hierarchyCol.transform, false);
        var hierCreateRect = hierCreateToolbar.AddComponent<RectTransform>();
        var hierCreateLE = hierCreateToolbar.AddComponent<LayoutElement>();
        hierCreateLE.preferredHeight = 36f;
        hierCreateLE.minHeight = 36f;

        var createHLayout = hierCreateToolbar.AddComponent<HorizontalLayoutGroup>();
        createHLayout.spacing = 8f;
        createHLayout.childControlWidth = true;
        createHLayout.childControlHeight = true;
        createHLayout.childForceExpandWidth = true;
        createHLayout.childForceExpandHeight = true;

        GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) return;
            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = "TextObject";
            newObj.Config.TextConfig = new TextMeshProUGUISettings();
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, "Text", "btn_hier_add_text");

        GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) return;
            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = "ImageObject";
            newObj.Config.ImageConfig = new ImageSettings();
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, "Image", "btn_hier_add_image");

        GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) return;
            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = "EmptyObject";
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, "Empty", "btn_hier_add_empty");


        // Hierarchy Control Toolbar (Up, Down, Detach, Delete)
        GameObject hierCtrlToolbar = new("HierarchyControlToolbar");
        hierCtrlToolbar.transform.SetParent(hierarchyCol.transform, false);
        var hierCtrlRect = hierCtrlToolbar.AddComponent<RectTransform>();
        var hierCtrlLE = hierCtrlToolbar.AddComponent<LayoutElement>();
        hierCtrlLE.preferredHeight = 36f;
        hierCtrlLE.minHeight = 36f;

        var ctrlHLayout = hierCtrlToolbar.AddComponent<HorizontalLayoutGroup>();
        ctrlHLayout.spacing = 8f;
        ctrlHLayout.childControlWidth = true;
        ctrlHLayout.childControlHeight = true;
        ctrlHLayout.childForceExpandWidth = true;
        ctrlHLayout.childForceExpandHeight = true;

        GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || currentCanvas == null) return;
            MoveSelectedOrder(-1);
        }, "Up", "btn_hier_up");

        GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || currentCanvas == null) return;
            MoveSelectedOrder(1);
        }, "Down", "btn_hier_down");

        GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || selectedObject.Parent == null) return;
            selectedObject.Detach();
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, "Detach", "btn_hier_detach");

        GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null) return;
            var toDelete = selectedObject;
            selectedObject = null;
            toDelete.Dispose();
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, "Del", "btn_hier_del");


        // ==================== 2. Inspector Column ====================
        GameObject inspectorCol = new("InspectorColumn");
        inspectorCol.transform.SetParent(pad.transform, false);
        var inspColRect = inspectorCol.AddComponent<RectTransform>();
        var inspColLE = inspectorCol.AddComponent<LayoutElement>();
        inspColLE.flexibleWidth = 1f;

        var inspBG = inspectorCol.AddComponent<Image>();
        inspBG.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        inspBG.type = Image.Type.Sliced;
        inspBG.color = UIColors.PanelBG;

        var inspVLayout = inspectorCol.AddComponent<VerticalLayoutGroup>();
        inspVLayout.padding = new RectOffset(10, 10, 10, 10);
        inspVLayout.spacing = 10f;
        inspVLayout.childControlWidth = true;
        inspVLayout.childControlHeight = false;
        inspVLayout.childForceExpandWidth = true;
        inspVLayout.childForceExpandHeight = false;

        // Inspector Title
        GameObject inspTitle = new("InspectorTitle");
        inspTitle.transform.SetParent(inspectorCol.transform, false);
        var inspTitleTxt = inspTitle.AddComponent<TextMeshProUGUI>();
        inspTitleTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        inspTitleTxt.fontSize = 20f;
        inspTitleTxt.text = "Inspector";
        inspTitleTxt.color = Color.white;

        // Inspector Scroll View
        GameObject inspViewport = new("InspectorViewport");
        inspViewport.transform.SetParent(inspectorCol.transform, false);
        var inspViewportRect = inspViewport.AddComponent<RectTransform>();
        var inspViewportLE = inspViewport.AddComponent<LayoutElement>();
        inspViewportLE.flexibleHeight = 1f;

        inspViewport.AddComponent<EmptyGraphic>().raycastTarget = true;
        inspViewport.AddComponent<RectMask2D>();

        GameObject inspContent = new("InspectorContent");
        inspContent.transform.SetParent(inspViewport.transform, false);
        inspectorContent = inspContent.AddComponent<RectTransform>();
        inspectorContent.anchorMin = new Vector2(0f, 1f);
        inspectorContent.anchorMax = new Vector2(1f, 1f);
        inspectorContent.pivot = new Vector2(0.5f, 1f);
        inspectorContent.offsetMin = Vector2.zero;
        inspectorContent.offsetMax = Vector2.zero;

        var inspContentLayout = inspContent.AddComponent<VerticalLayoutGroup>();
        inspContentLayout.spacing = 12f;
        inspContentLayout.childControlWidth = true;
        inspContentLayout.childControlHeight = false;
        inspContentLayout.childForceExpandWidth = true;
        inspContentLayout.childForceExpandHeight = false;

        var inspContentFitter = inspContent.AddComponent<ContentSizeFitter>();
        inspContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        inspectorCol.AddComponent<UIScrollController>().SetContent(inspectorContent, inspViewportRect);
    }

    private void MoveSelectedOrder(int direction) {
        var obj = selectedObject;
        if(obj == null) return;

        if(obj.Parent == null) {
            int index = currentCanvas.OvObjects.IndexOf(obj);
            if(index < 0) return;
            int targetIndex = index + direction;
            if(targetIndex >= 0 && targetIndex < currentCanvas.OvObjects.Count) {
                currentCanvas.OvObjects.RemoveAt(index);
                currentCanvas.OvObjects.Insert(targetIndex, obj);
                for(int i = 0; i < currentCanvas.OvObjects.Count; i++) {
                    currentCanvas.OvObjects[i].GameObject.transform.SetSiblingIndex(i);
                }
                RebuildHierarchy();
                SaveConfig();
            }
        } else {
            var parent = obj.Parent;
            int index = parent.Children.IndexOf(obj);
            if(index < 0) return;
            int targetIndex = index + direction;
            if(targetIndex >= 0 && targetIndex < parent.Children.Count) {
                parent.SetChildIndex(obj, targetIndex);
                RebuildHierarchy();
                SaveConfig();
            }
        }
    }

    public void Open(OvCanvas canvas, bool noAnimate = false) {
        currentCanvas = canvas;
        titleText.text = string.IsNullOrEmpty(canvas.Config.Name) ? "(Empty)" : canvas.Config.Name;
        selectedObject = null;

        RebuildHierarchy();
        RebuildInspector();

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

    private void SelectObject(OvObject obj) {
        selectedObject = obj;
        RebuildHierarchy();
        RebuildInspector();
    }

    private void SaveConfig() {
        OverlayCore.SaveAllCanvases();
    }

    private void RebuildHierarchy() {
        foreach(Transform child in hierarchyContent) {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        if(currentCanvas == null) {
            return;
        }

        // Render Canvas root first
        RenderCanvasRootItem();

        for(int i = 0; i < currentCanvas.OvObjects.Count; i++) {
            RenderHierarchyItem(currentCanvas.OvObjects[i], 0);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(hierarchyContent);
    }

    private void RenderCanvasRootItem() {
        var row = GenerateUI.Row(hierarchyContent, 36f);

        GameObject itemBtn = new("CanvasRootButton");
        itemBtn.transform.SetParent(row, false);
        var itemBtnRect = itemBtn.AddComponent<RectTransform>();
        var itemBtnLE = itemBtn.AddComponent<LayoutElement>();
        itemBtnLE.flexibleWidth = 1f;
        itemBtnLE.preferredHeight = 36f;

        var btnImg = itemBtn.AddComponent<Image>();
        btnImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        btnImg.type = Image.Type.Sliced;
        btnImg.color = (selectedObject == null) ? UIColors.ObjectActive : UIColors.ObjectBG;

        var tmp = GenerateUI.AddText(itemBtn.transform, true);
        tmp.text = $"Canvas: {currentCanvas.Config.Name}";
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        GenerateUI.AddOutlineHover(itemBtn, itemBtn.AddComponent<EventTrigger>());
        GenerateUI.AddButton(itemBtn, btn => {
            if(btn == InputButton.Left) {
                SelectObject(null);
            }
        });
    }

    private void RenderHierarchyItem(OvObject obj, int depth) {
        var row = GenerateUI.Row(hierarchyContent, 36f);

        // depth starts at 0, but since we have CanvasRoot, we indent by depth + 1
        GameObject indent = new("Indent");
        indent.transform.SetParent(row, false);
        var indentLE = indent.AddComponent<LayoutElement>();
        indentLE.preferredWidth = (depth + 1) * 16f;

        GameObject itemBtn = new("ItemButton");
        itemBtn.transform.SetParent(row, false);
        var itemBtnRect = itemBtn.AddComponent<RectTransform>();
        var itemBtnLE = itemBtn.AddComponent<LayoutElement>();
        itemBtnLE.flexibleWidth = 1f;
        itemBtnLE.preferredHeight = 36f;

        var btnImg = itemBtn.AddComponent<Image>();
        btnImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        btnImg.type = Image.Type.Sliced;
        btnImg.color = (selectedObject == obj) ? UIColors.ObjectActive : UIColors.ObjectBG;

        var tmp = GenerateUI.AddText(itemBtn.transform, true);
        tmp.text = obj.Config.Name;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        GenerateUI.AddOutlineHover(itemBtn, itemBtn.AddComponent<EventTrigger>());
        GenerateUI.AddButton(itemBtn, btn => {
            if(btn == InputButton.Left) {
                SelectObject(obj);
            }
        });

        for(int i = 0; i < obj.Children.Count; i++) {
            RenderHierarchyItem(obj.Children[i], depth + 1);
        }
    }

    private void RebuildInspector() {
        foreach(Transform child in inspectorContent) {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        if(selectedObject == null) {
            if(currentCanvas == null) return;

            // Render Canvas Own Settings
            var canvasNameRow = GenerateUI.Row(inspectorContent, 50f);
            GenerateUI.Input(canvasNameRow, "", currentCanvas.Config.Name, val => {
                currentCanvas.Config.Name = val;
                titleText.text = val;
                currentCanvas.ApplyConfig();
                RebuildHierarchy(); // Sync Canvas root name instantly
                SaveConfig();
            }, "Canvas Name", null, "canvas_name");

            var canvasRaycastRow = GenerateUI.Row(inspectorContent, 50f);
            GenerateUI.Toggle(canvasRaycastRow, true, currentCanvas.Config.CanvasGroupConfig.BlocksRaycasts, val => {
                currentCanvas.Config.CanvasGroupConfig.BlocksRaycasts = val;
                currentCanvas.ApplyConfig();
                SaveConfig();
            }, "Blocks Raycasts", "blocks_raycasts");

            LayoutRebuilder.ForceRebuildLayoutImmediate(inspectorContent);
            return;
        }

        var obj = selectedObject;

        // Name & Active Header
        var basicRow = GenerateUI.Row(inspectorContent, 50f);
        var activeToggle = GenerateUI.Toggle(basicRow, true, obj.Config.CanvasGroupConfig.Alpha > 0f, val => {
            obj.Config.CanvasGroupConfig.Alpha = val ? 1f : 0f;
            obj.ApplyConfig();
            SaveConfig();
        }, "Active", "obj_active");

        var objNameRow = GenerateUI.Row(inspectorContent, 50f);
        var nameInput = GenerateUI.Input(objNameRow, "OvObject", obj.Config.Name, val => {
            obj.Config.Name = val;
            obj.ApplyConfig();
            RebuildHierarchy();
            SaveConfig();
        }, "Object Name", null, "obj_name");

        // 1. RectTransform Card
        var rectCfg = obj.Config.RectTransformConfig;
        var (transCard, transContent) = GenerateUI.ComponentCard(inspectorContent, "RectTransform", true, active => {
            // RectTransform cannot be disabled
        }, null, showDeleteButton: false);

        var posRowX = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(posRowX, 0f, -1000f, 1000f, rectCfg.AnchoredPosition.x, "F0", false, null, val => {
            rectCfg.AnchoredPosition = new Vector2(val, rectCfg.AnchoredPosition.y);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Pos X", "pos_x");

        var posRowY = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(posRowY, 0f, -1000f, 1000f, rectCfg.AnchoredPosition.y, "F0", false, null, val => {
            rectCfg.AnchoredPosition = new Vector2(rectCfg.AnchoredPosition.x, val);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Pos Y", "pos_y");

        var sizeRowW = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(sizeRowW, 100f, 10f, 2000f, rectCfg.SizeDelta.x, "F0", false, null, val => {
            rectCfg.SizeDelta = new Vector2(val, rectCfg.SizeDelta.y);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Width", "size_w");

        var sizeRowH = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(sizeRowH, 100f, 10f, 2000f, rectCfg.SizeDelta.y, "F0", false, null, val => {
            rectCfg.SizeDelta = new Vector2(rectCfg.SizeDelta.x, val);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Height", "size_h");

        var pivRowX = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(pivRowX, 0.5f, 0f, 1f, rectCfg.Pivot.x, "F2", false, null, val => {
            rectCfg.Pivot = new Vector2(val, rectCfg.Pivot.y);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Pivot X", "piv_x");

        var pivRowY = GenerateUI.Row(transContent, 50f);
        GenerateUI.Slider(pivRowY, 0.5f, 0f, 1f, rectCfg.Pivot.y, "F2", false, null, val => {
            rectCfg.Pivot = new Vector2(rectCfg.Pivot.x, val);
            obj.ApplyConfig();
            SaveConfig();
        }, null, "Pivot Y", "piv_y");

        // 2. Text Component Card
        if(obj.Config.TextConfig != null) {
            var textCfg = obj.Config.TextConfig;
            var (card, cardContent) = GenerateUI.ComponentCard(inspectorContent, "Text (TextMeshPro)", true, active => {
                var tmpComp = obj.GameObject.GetComponent<TextMeshProUGUI>();
                if(tmpComp != null) tmpComp.enabled = active;
                SaveConfig();
            }, () => {
                obj.Config.TextConfig = null;
                obj.ApplyComponent();
                obj.ApplyConfig();
                RebuildHierarchy();
                RebuildInspector();
                SaveConfig();
            });

            var txtValRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Input(txtValRow, "", textCfg.Text, val => {
                textCfg.Text = val;
                obj.ApplyConfig();
                SaveConfig();
            }, "Text Content", null, "txt_content");

            var txtSizeRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(txtSizeRow, 24f, 8f, 120f, textCfg.FontSize, "F0", false, null, val => {
                textCfg.FontSize = val;
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Font Size", "txt_size");

            var rRow = GenerateUI.Row(cardContent, 50f);
            Color curColR = textCfg.Color;
            GenerateUI.Slider(rRow, 1f, 0f, 1f, curColR.r, "F2", false, null, val => {
                Color c = textCfg.Color;
                c.r = val;
                textCfg.Color = c;
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color R", "txt_col_r");

            var gRow = GenerateUI.Row(cardContent, 50f);
            Color curColG = textCfg.Color;
            GenerateUI.Slider(gRow, 1f, 0f, 1f, curColG.g, "F2", false, null, val => {
                Color c = textCfg.Color;
                c.g = val;
                textCfg.Color = c;
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color G", "txt_col_g");

            var bRow = GenerateUI.Row(cardContent, 50f);
            Color curColB = textCfg.Color;
            GenerateUI.Slider(bRow, 1f, 0f, 1f, curColB.b, "F2", false, null, val => {
                Color c = textCfg.Color;
                c.b = val;
                textCfg.Color = c;
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color B", "txt_col_b");

            var aRow = GenerateUI.Row(cardContent, 50f);
            Color curColA = textCfg.Color;
            GenerateUI.Slider(aRow, 1f, 0f, 1f, curColA.a, "F2", false, null, val => {
                Color c = textCfg.Color;
                c.a = val;
                textCfg.Color = c;
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color A", "txt_col_a");
        }

        // 3. Image Component Card
        if(obj.Config.ImageConfig != null) {
            var imgCfg = obj.Config.ImageConfig;
            var (card, cardContent) = GenerateUI.ComponentCard(inspectorContent, "Image", true, active => {
                var imgComp = obj.GameObject.GetComponent<Image>();
                if(imgComp != null) imgComp.enabled = active;
                SaveConfig();
            }, () => {
                obj.Config.ImageConfig = null;
                obj.ApplyComponent();
                obj.ApplyConfig();
                RebuildHierarchy();
                RebuildInspector();
                SaveConfig();
            });

            var rRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(rRow, 1f, 0f, 1f, imgCfg.Color.r, "F2", false, null, val => {
                imgCfg.Color = new Color(val, imgCfg.Color.g, imgCfg.Color.b, imgCfg.Color.a);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color R", "img_col_r");

            var gRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(gRow, 1f, 0f, 1f, imgCfg.Color.g, "F2", false, null, val => {
                imgCfg.Color = new Color(imgCfg.Color.r, val, imgCfg.Color.b, imgCfg.Color.a);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color G", "img_col_g");

            var bRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(bRow, 1f, 0f, 1f, imgCfg.Color.b, "F2", false, null, val => {
                imgCfg.Color = new Color(imgCfg.Color.r, imgCfg.Color.g, val, imgCfg.Color.a);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color B", "img_col_b");

            var aRow = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(aRow, 1f, 0f, 1f, imgCfg.Color.a, "F2", false, null, val => {
                imgCfg.Color = new Color(imgCfg.Color.r, imgCfg.Color.g, imgCfg.Color.b, val);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Color A", "img_col_a");
        }

        // 4. Shadow Component Card
        if(obj.Config.ShadowConfig != null) {
            var shadCfg = obj.Config.ShadowConfig;
            var (card, cardContent) = GenerateUI.ComponentCard(inspectorContent, "Shadow", true, active => {
                var shadComp = obj.GameObject.GetComponent<Shadow>();
                if(shadComp != null) shadComp.enabled = active;
                SaveConfig();
            }, () => {
                obj.Config.ShadowConfig = null;
                obj.ApplyComponent();
                obj.ApplyConfig();
                RebuildInspector();
                SaveConfig();
            });

            var distRowX = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(distRowX, 2f, -50f, 50f, shadCfg.EffectDistance.x, "F0", false, null, val => {
                shadCfg.EffectDistance = new Vector2(val, shadCfg.EffectDistance.y);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Distance X", "shad_dist_x");

            var distRowY = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(distRowY, -2f, -50f, 50f, shadCfg.EffectDistance.y, "F0", false, null, val => {
                shadCfg.EffectDistance = new Vector2(shadCfg.EffectDistance.x, val);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Distance Y", "shad_dist_y");
        }

        // 5. Outline Component Card
        if(obj.Config.OutlineConfig != null) {
            var outCfg = obj.Config.OutlineConfig;
            var (card, cardContent) = GenerateUI.ComponentCard(inspectorContent, "Outline", true, active => {
                var outComp = obj.GameObject.GetComponent<Outline>();
                if(outComp != null) outComp.enabled = active;
                SaveConfig();
            }, () => {
                obj.Config.OutlineConfig = null;
                obj.ApplyComponent();
                obj.ApplyConfig();
                RebuildInspector();
                SaveConfig();
            });

            var distRowX = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(distRowX, 2f, -50f, 50f, outCfg.EffectDistance.x, "F0", false, null, val => {
                outCfg.EffectDistance = new Vector2(val, outCfg.EffectDistance.y);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Distance X", "out_dist_x");

            var distRowY = GenerateUI.Row(cardContent, 50f);
            GenerateUI.Slider(distRowY, -2f, -50f, 50f, outCfg.EffectDistance.y, "F0", false, null, val => {
                outCfg.EffectDistance = new Vector2(outCfg.EffectDistance.x, val);
                obj.ApplyConfig();
                SaveConfig();
            }, null, "Distance Y", "out_dist_y");
        }

        // 6. Add Component Dropdown
        List<string> addableList = ["Add Component..."];
        if(obj.Config.TextConfig == null) addableList.Add("Text (TextMeshPro)");
        if(obj.Config.ImageConfig == null) addableList.Add("Image");
        if(obj.Config.ShadowConfig == null) addableList.Add("Shadow");
        if(obj.Config.OutlineConfig == null) addableList.Add("Outline");

        if(addableList.Count > 1) {
            var addRow = GenerateUI.Row(inspectorContent, 50f);
            GenerateUI.DropDown(
                addRow,
                "Add Component...",
                "Add Component...",
                addableList,
                val => val,
                selected => {
                    if(selected == "Text (TextMeshPro)") {
                        obj.Config.TextConfig = new TextMeshProUGUISettings();
                    } else if(selected == "Image") {
                        obj.Config.ImageConfig = new ImageSettings();
                    } else if(selected == "Shadow") {
                        obj.Config.ShadowConfig = new ShadowSettings();
                    } else if(selected == "Outline") {
                        obj.Config.OutlineConfig = new OutlineSettings();
                    }

                    obj.ApplyComponent();
                    obj.ApplyConfig();

                    RebuildHierarchy();
                    RebuildInspector();
                    SaveConfig();
                },
                "add_component_dd"
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(inspectorContent);
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

        if(GameObject != null) {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
}