using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.UI.Generator;
using Overlayer.UI.Objects;
using Overlayer.Localization;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using Overlayer.Tween;
using GTweens.Easings;
using UnityEngine.EventSystems;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.IO.Overlay;

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
    private readonly Action onBackAction;

    private OvCanvas currentCanvas;
    private OvObject selectedObject;

    private RectTransform hierarchyContent;
    private RectTransform inspectorContent;

    private OvObject draggedObject;
    private OvObject hierarchyDropTarget;
    private RectTransform hierarchyDropRect;
    private Image hierarchyDropImage;
    private Color hierarchyDropBaseColor;
    private GameObject hierarchyDropLine;
    private bool hierarchyDropOnCanvas;
    private HierarchyDropZone hierarchyDropZone;
    private bool hierarchyDropVisualActive;

    private GTween canvasFadeTween;

    private enum HierarchyDropZone { Before, Inside, After }

#pragma warning disable IDE0001
    private readonly System.Collections.Generic.List<UIObject> hierarchyUiObjects = [];
    private readonly System.Collections.Generic.List<UIObject> inspectorUiObjects = [];
    private readonly System.Collections.Generic.List<UIObject> permanentUiObjects = [];
#pragma warning restore IDE0001

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
        titleGo.AddComponent<TextLocalization>().Init("CANVAS_TITLE", "Canvas Settings");

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
        hierVLayout.padding = new RectOffset {
            left = 10,
            right = 10,
            top = 10,
            bottom = 10
        };
        hierVLayout.spacing = 10f;
        hierVLayout.childControlWidth = true;
        hierVLayout.childControlHeight = true; // Enabled to honor child heights
        hierVLayout.childForceExpandWidth = true;
        hierVLayout.childForceExpandHeight = false;

        // Hierarchy Title
        GameObject hierTitle = new("HierarchyTitle");
        hierTitle.transform.SetParent(hierarchyCol.transform, false);
        var hierTitleTxt = hierTitle.AddComponent<TextMeshProUGUI>();
        hierTitleTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        hierTitleTxt.fontSize = 20f;
        hierTitleTxt.text = MainCore.Tr.Get("HIERARCHY", "Hierarchy");
        hierTitleTxt.color = Color.white;
        hierTitleTxt.gameObject.AddComponent<TextLocalization>().Init("HIERARCHY", "Hierarchy");
        var hierTitleLE = hierTitle.AddComponent<LayoutElement>();
        hierTitleLE.preferredHeight = 30f;
        hierTitleLE.minHeight = 30f;

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
        hierContentLayout.childControlHeight = true;
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
        hierCreateLE.flexibleWidth = 0f;
        hierCreateLE.flexibleHeight = 0f;

        var createHLayout = hierCreateToolbar.AddComponent<HorizontalLayoutGroup>();
        createHLayout.spacing = 8f;
        createHLayout.childControlWidth = true;
        createHLayout.childControlHeight = true;
        createHLayout.childForceExpandWidth = true;
        createHLayout.childForceExpandHeight = true;

        var btnText = GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = MainCore.Tr.Get("DEFAULT_TEXT_OBJECT_NAME", "TextObject");
            newObj.Config.TextConfig = new TextMeshProUGUISettings();
            newObj.Config.TextEngineConfig = new OvTextSettings();
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Tr.Get("BUTTON_TEXT", "Text"), "btn_hier_add_text");
        btnText.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_TEXT", "Text");
        btnText.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnText);

        var btnImage = GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = MainCore.Tr.Get("DEFAULT_IMAGE_OBJECT_NAME", "ImageObject");
            newObj.Config.ImageConfig = new ImageSettings();
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Tr.Get("BUTTON_IMAGE", "Image"), "btn_hier_add_image");
        btnImage.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_IMAGE", "Image");
        btnImage.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnImage);

        var btnEmpty = GenerateUI.Button(hierCreateToolbar.transform, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null ? selectedObject.CreateOvObject() : currentCanvas.CreateOvObject();
            newObj.Config.Name = MainCore.Tr.Get("DEFAULT_EMPTY_OBJECT_NAME", "EmptyObject");
            newObj.ApplyComponent();
            newObj.ApplyConfig();
            selectedObject = newObj;
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Tr.Get("BUTTON_EMPTY", "Empty"), "btn_hier_add_empty");
        btnEmpty.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_EMPTY", "Empty");
        btnEmpty.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnEmpty);

        // Hierarchy Control Toolbar (Up, Down, Detach, Delete)
        GameObject hierCtrlToolbar = new("HierarchyControlToolbar");
        hierCtrlToolbar.transform.SetParent(hierarchyCol.transform, false);
        var hierCtrlRect = hierCtrlToolbar.AddComponent<RectTransform>();
        var hierCtrlLE = hierCtrlToolbar.AddComponent<LayoutElement>();
        hierCtrlLE.preferredHeight = 36f;
        hierCtrlLE.minHeight = 36f;
        hierCtrlLE.flexibleWidth = 0f;
        hierCtrlLE.flexibleHeight = 0f;

        var ctrlHLayout = hierCtrlToolbar.AddComponent<HorizontalLayoutGroup>();
        ctrlHLayout.spacing = 8f;
        ctrlHLayout.childControlWidth = true;
        ctrlHLayout.childControlHeight = true;
        ctrlHLayout.childForceExpandWidth = true;
        ctrlHLayout.childForceExpandHeight = true;

        var btnUp = GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || currentCanvas == null) {
                return;
            }

            MoveSelectedOrder(-1);
        }, MainCore.Tr.Get("BUTTON_UP", "Up"), "btn_hier_up");
        btnUp.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_UP", "Up");
        btnUp.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnUp);

        var btnDown = GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || currentCanvas == null) {
                return;
            }

            MoveSelectedOrder(1);
        }, MainCore.Tr.Get("BUTTON_DOWN", "Down"), "btn_hier_down");
        btnDown.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_DOWN", "Down");
        btnDown.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnDown);

        var btnDetach = GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null || selectedObject.Parent == null) {
                return;
            }

            selectedObject.Detach();
            currentCanvas.Attach(selectedObject);
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Tr.Get("BUTTON_DETACH", "Detach"), "btn_hier_detach");
        btnDetach.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_DETACH", "Detach");
        btnDetach.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnDetach);

        var btnDel = GenerateUI.Button(hierCtrlToolbar.transform, () => {
            if(selectedObject == null) {
                if(currentCanvas == null) {
                    return;
                }

                var canvasToDelete = currentCanvas;
                currentCanvas = null;
                if(OverlayCore.DeleteOvCanvas(canvasToDelete)) {
                    onBackAction?.Invoke();
                }
                return;
            }

            var toDelete = selectedObject;
            selectedObject = null;
            if(toDelete.Parent == null) {
                currentCanvas.Detach(toDelete);
            }
            toDelete.Dispose();
            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Tr.Get("BUTTON_DELETE", "Del"), "btn_hier_del");
        btnDel.Label.gameObject.AddComponent<TextLocalization>().Init("BUTTON_DELETE", "Del");
        btnDel.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnDel);

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
        inspVLayout.padding = new RectOffset {
            left = 10,
            right = 10,
            top = 10,
            bottom = 10
        };
        inspVLayout.spacing = 10f;
        inspVLayout.childControlWidth = true;
        inspVLayout.childControlHeight = true; // Enabled to honor child heights
        inspVLayout.childForceExpandWidth = true;
        inspVLayout.childForceExpandHeight = false;

        // Inspector Title
        GameObject inspTitle = new("InspectorTitle");
        inspTitle.transform.SetParent(inspectorCol.transform, false);
        var inspTitleTxt = inspTitle.AddComponent<TextMeshProUGUI>();
        inspTitleTxt.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        inspTitleTxt.fontSize = 20f;
        inspTitleTxt.text = MainCore.Tr.Get("INSPECTOR", "Inspector");
        inspTitleTxt.color = Color.white;
        inspTitleTxt.gameObject.AddComponent<TextLocalization>().Init("INSPECTOR", "Inspector");
        var inspTitleLE = inspTitle.AddComponent<LayoutElement>();
        inspTitleLE.preferredHeight = 30f;
        inspTitleLE.minHeight = 30f;

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
        inspContentLayout.childControlHeight = true;
        inspContentLayout.childForceExpandWidth = true;
        inspContentLayout.childForceExpandHeight = false;

        var inspContentFitter = inspContent.AddComponent<ContentSizeFitter>();
        inspContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        inspectorCol.AddComponent<UIScrollController>().SetContent(inspectorContent, inspViewportRect);
    }

    private void MoveSelectedOrder(int direction) {
        var obj = selectedObject;
        if(obj == null) {
            return;
        }

        if(obj.Parent == null) {
            int index = currentCanvas.OvObjects.IndexOf(obj);
            if(index < 0) {
                return;
            }

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
            if(index < 0) {
                return;
            }

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
        titleText.text = string.IsNullOrEmpty(canvas.Config.Name)
            ? MainCore.Tr.Get("EMPTY", "(Empty)")
            : canvas.Config.Name;
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

    private void SaveConfig() => OverlayCore.SaveAllCanvases();

    private void RebuildHierarchy() {
        draggedObject = null;
        ClearHierarchyDropState();

        foreach(var obj in hierarchyUiObjects) {
            obj.Dispose();
        }
        hierarchyUiObjects.Clear();

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
        var row = GenerateUI.Row(hierarchyContent, 50f);

        // Add Horizontal Layout to Row to organize indent & button
        var hLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.spacing = 4f;
        hLayout.padding = new RectOffset {
            left = 0,
            right = 0,
            top = 0,
            bottom = 0
        };

        GameObject itemBtn = new("CanvasRootButton");
        itemBtn.transform.SetParent(row, false);
        var itemBtnRect = itemBtn.AddComponent<RectTransform>();
        var itemBtnLE = itemBtn.AddComponent<LayoutElement>();
        itemBtnLE.flexibleWidth = 1f;
        itemBtnLE.preferredHeight = 50f;

        var btnImg = itemBtn.AddComponent<Image>();
        btnImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        btnImg.type = Image.Type.Sliced;
        btnImg.color = (selectedObject == null) ? UIColors.ObjectActive : UIColors.ObjectBG;

        var tmp = GenerateUI.AddText(itemBtn.transform, true);
        tmp.text = string.Format(
            MainCore.Tr.Get("CANVAS_ROOT", "Canvas: {0}"),
            currentCanvas.Config.Name
        );
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;

        var trigger = itemBtn.AddComponent<EventTrigger>();
        GenerateUI.AddOutlineHover(itemBtn, trigger);
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerClick, eventData => {
                if(((PointerEventData)eventData).button == InputButton.Left && draggedObject == null) SelectObject(null);
            }),
            (EventTriggerType.PointerEnter, _ => SetHierarchyDropTarget(null, itemBtnRect, btnImg, true)),
            (EventTriggerType.PointerExit, _ => ClearHierarchyDropTarget(itemBtnRect))
        );
        itemBtnRect.offsetMax = Vector2.zero;
    }

    private void RenderHierarchyItem(OvObject obj, int depth) {
        var row = GenerateUI.Row(hierarchyContent, 36f);

        var hLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.spacing = 4f;
        hLayout.padding = new RectOffset {
            left = 0,
            right = 0,
            top = 0,
            bottom = 0
        };

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

        AddHierarchyDragHandle(itemBtn.transform);

        var tmp = GenerateUI.AddText(itemBtn.transform, true);
        tmp.text = obj.Config.Name;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        tmp.rectTransform.offsetMin = new Vector2(28f, 0f);

        var trigger = itemBtn.AddComponent<EventTrigger>();
        GenerateUI.AddOutlineHover(itemBtn, trigger);
        CanvasGroup dragCanvasGroup = itemBtn.AddComponent<CanvasGroup>();
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerClick, eventData => {
                if(((PointerEventData)eventData).button == InputButton.Left && draggedObject == null) SelectObject(obj);
            }),
            (EventTriggerType.BeginDrag, eventData => {
                if(((PointerEventData)eventData).button != InputButton.Left) return;
                draggedObject = obj;
                selectedObject = obj;
                dragCanvasGroup.alpha = 0.45f;
                dragCanvasGroup.blocksRaycasts = false;
            }),
            (EventTriggerType.Drag, UpdateHierarchyDropPreview),
            (EventTriggerType.EndDrag, _ => {
                dragCanvasGroup.alpha = 1f;
                dragCanvasGroup.blocksRaycasts = true;
                CompleteHierarchyDrag();
            }),
            (EventTriggerType.PointerEnter, _ => SetHierarchyDropTarget(obj, itemBtnRect, btnImg, false)),
            (EventTriggerType.PointerExit, _ => ClearHierarchyDropTarget(itemBtnRect))
        );
        itemBtnRect.offsetMax = Vector2.zero;

        for(int i = 0; i < obj.Children.Count; i++) {
            RenderHierarchyItem(obj.Children[i], depth + 1);
        }
    }

    private static void AddHierarchyDragHandle(Transform parent) {
        GameObject handle = new("DragHandle");
        handle.transform.SetParent(parent, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = new Vector2(14f, 0f);
        handleRect.sizeDelta = new Vector2(10f, 12f);

        for(int i = 0; i < 3; i++) {
            GameObject bar = new($"Bar{i}");
            bar.transform.SetParent(handle.transform, false);
            RectTransform barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(0f, 3f - i * 3f);
            barRect.sizeDelta = new Vector2(8f, 1.2f);
            Image barImage = bar.AddComponent<Image>();
            barImage.color = new Color(1f, 1f, 1f, 0.58f);
            barImage.raycastTarget = false;
        }
    }

    private void SetHierarchyDropTarget(OvObject target, RectTransform rect, Image image, bool canvas) {
        if(draggedObject == null || target == draggedObject || IsDescendantOf(target, draggedObject)) return;
        ResetHierarchyDropVisual();
        hierarchyDropTarget = target;
        hierarchyDropRect = rect;
        hierarchyDropImage = image;
        hierarchyDropBaseColor = image.color;
        hierarchyDropOnCanvas = canvas;
        UpdateHierarchyDropPreview(null);
    }

    private void ClearHierarchyDropTarget(RectTransform rect) {
        if(hierarchyDropRect != rect) return;
        ResetHierarchyDropVisual();
        hierarchyDropTarget = null;
        hierarchyDropRect = null;
        hierarchyDropImage = null;
        hierarchyDropOnCanvas = false;
    }

    private void UpdateHierarchyDropPreview(BaseEventData _) {
        if(draggedObject == null || hierarchyDropRect == null || hierarchyDropImage == null) return;

        HierarchyDropZone zone = HierarchyDropZone.Inside;
        if(!hierarchyDropOnCanvas) {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hierarchyDropRect,
                Overlayer.Compat.OVC.OVC_Input.MousePosition,
                null,
                out Vector2 point
            )) return;
            float edge = hierarchyDropRect.rect.height * 0.27f;
            if(point.y > hierarchyDropRect.rect.yMax - edge) zone = HierarchyDropZone.Before;
            else if(point.y < hierarchyDropRect.rect.yMin + edge) zone = HierarchyDropZone.After;
        }

        if(hierarchyDropVisualActive && zone == hierarchyDropZone) return;
        ResetHierarchyDropVisual();
        hierarchyDropZone = zone;
        hierarchyDropVisualActive = true;
        if(zone == HierarchyDropZone.Inside) {
            hierarchyDropImage.color = UIColors.ObjectButton;
            return;
        }

        hierarchyDropLine = new GameObject("HierarchyDropLine");
        hierarchyDropLine.transform.SetParent(hierarchyDropRect, false);
        var lineRect = hierarchyDropLine.AddComponent<RectTransform>();
        lineRect.anchorMin = zone == HierarchyDropZone.Before ? new Vector2(0f, 1f) : Vector2.zero;
        lineRect.anchorMax = zone == HierarchyDropZone.Before ? Vector2.one : new Vector2(1f, 0f);
        lineRect.pivot = zone == HierarchyDropZone.Before ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(0f, 3f);
        lineRect.anchoredPosition = Vector2.zero;
        var line = hierarchyDropLine.AddComponent<Image>();
        line.color = UIColors.ObjectActiveBright;
        line.raycastTarget = false;
    }

    private void ResetHierarchyDropVisual() {
        if(hierarchyDropImage != null) hierarchyDropImage.color = hierarchyDropBaseColor;
        if(hierarchyDropLine != null) UnityEngine.Object.Destroy(hierarchyDropLine);
        hierarchyDropLine = null;
        hierarchyDropVisualActive = false;
    }

    private void CompleteHierarchyDrag() {
        OvObject moving = draggedObject;
        draggedObject = null;
        if(moving == null || hierarchyDropRect == null) {
            ClearHierarchyDropState();
            return;
        }

        OvObject target = hierarchyDropTarget;
        HierarchyDropZone zone = hierarchyDropZone;
        bool canvas = hierarchyDropOnCanvas;
        ClearHierarchyDropState();

        if(!canvas && (target == null || target == moving || IsDescendantOf(target, moving))) return;

        if(moving.Parent != null) moving.Detach();
        else currentCanvas.Detach(moving);

        if(canvas) {
            currentCanvas.Attach(moving);
        } else if(zone == HierarchyDropZone.Inside) {
            target.Attach(moving);
        } else if(target.Parent != null) {
            OvObject parent = target.Parent;
            parent.Attach(moving);
            int index = parent.Children.IndexOf(target) + (zone == HierarchyDropZone.After ? 1 : 0);
            parent.SetChildIndex(moving, index);
        } else {
            currentCanvas.Attach(moving);
            currentCanvas.OvObjects.Remove(moving);
            int index = currentCanvas.OvObjects.IndexOf(target) + (zone == HierarchyDropZone.After ? 1 : 0);
            currentCanvas.OvObjects.Insert(Math.Clamp(index, 0, currentCanvas.OvObjects.Count), moving);
            SyncRootSiblingOrder();
        }

        RebuildHierarchy();
        RebuildInspector();
        SaveConfig();
    }

    private void ClearHierarchyDropState() {
        ResetHierarchyDropVisual();
        hierarchyDropTarget = null;
        hierarchyDropRect = null;
        hierarchyDropImage = null;
        hierarchyDropOnCanvas = false;
    }

    private static bool IsDescendantOf(OvObject candidate, OvObject ancestor) {
        for(OvObject current = candidate; current != null; current = current.Parent) {
            if(current == ancestor) return true;
        }
        return false;
    }

    private void SyncRootSiblingOrder() {
        for(int i = 0; i < currentCanvas.OvObjects.Count; i++) {
            currentCanvas.OvObjects[i].GameObject.transform.SetSiblingIndex(i);
        }
    }

    private void RebuildInspector() {
        foreach(var uiObj in inspectorUiObjects) {
            uiObj.Dispose();
        }
        inspectorUiObjects.Clear();

        foreach(Transform child in inspectorContent) {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        if(currentCanvas == null) {
            return;
        }

        Action apply = selectedObject != null
            ? selectedObject.ApplyConfig
            : currentCanvas.ApplyConfig;

        var builder = new OvInspectorBuilder(
            inspectorContent,
            inspectorUiObjects,
            apply,
            SaveConfig,
            RebuildInspector,
            RebuildHierarchy
        );

        if(selectedObject == null) {
            builder.BuildCanvas(currentCanvas, value => {
                titleText.text = string.IsNullOrWhiteSpace(value)
                    ? MainCore.Tr.Get("EMPTY", "(Empty)")
                    : value;
            });
        } else {
            builder.BuildObject(selectedObject);
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

        foreach(var obj in hierarchyUiObjects) {
            obj.Dispose();
        }
        hierarchyUiObjects.Clear();

        foreach(var obj in inspectorUiObjects) {
            obj.Dispose();
        }
        inspectorUiObjects.Clear();

        foreach(var obj in permanentUiObjects) {
            obj.Dispose();
        }
        permanentUiObjects.Clear();

        if(GameObject != null) {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
}
