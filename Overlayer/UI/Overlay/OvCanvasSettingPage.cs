using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Tween;
using O5Kit.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
using O5Kit.Factory;
using O5Kit.Control;
using Overlayer.Localization;
using O5Kit.Behaviour;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
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

    private readonly Action onBackAction;

    private OvCanvas currentCanvas;
    private OvObject selectedObject;
    private CanvasTabState activeTab;
    private readonly System.Collections.Generic.List<CanvasTabState> canvasTabs = [];
    private RectTransform tabsContent;
    private CanvasTabState draggedCanvasTab;
    private RectTransform canvasTabDragPlaceholder;
    private LayoutElement draggedCanvasTabLayout;
    private Vector2 canvasTabDragOriginalAnchorMin;
    private Vector2 canvasTabDragOriginalAnchorMax;
    private Vector2 canvasTabDragOriginalPivot;
    private Vector2 canvasTabDragOriginalSizeDelta;
    private float canvasTabDragPointerOffsetX;
    private float canvasTabDragPreviousCenterX;
    private float canvasTabDragY;
    private float canvasTabDragWidth;
    private bool canvasTabDragReordered;
    private bool suppressCanvasTabClick;
    private O5Button deleteButton;
    private OvCanvas armedDeleteCanvas;
    private DateTime armedDeleteTime;

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

    private ITweenHandle canvasFadeTween;

    private enum HierarchyDropZone { Before, Inside, After }

    private sealed class CanvasTabState(OvCanvas canvas) {
        public OvCanvas Canvas { get; } = canvas;
        public RectTransform TabRect;
        public OvObject SelectedObject;
        public readonly System.Collections.Generic.HashSet<OvObject> CollapsedObjects = [];
    }

#pragma warning disable IDE0001
    private readonly System.Collections.Generic.List<O5Object> hierarchyUiObjects = [];
    private readonly System.Collections.Generic.HashSet<OvObject> collapsedObjects = [];
    private OvObject foldoutAnimTarget;
    private float foldoutAnimFrom;
    private readonly System.Collections.Generic.List<O5Object> inspectorUiObjects = [];
    private readonly System.Collections.Generic.List<O5Object> permanentUiObjects = [];
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
        backBtnRect.sizeDelta = new Vector2(26, 26);
        backBtnRect.anchoredPosition = new Vector2(34, 0);
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

        var backBtnGoOvent = backBtnGo.AddComponent<OventHandler>();
        backBtnGoOvent.OnClick += btn => {
            if(btn == InputButton.Left) {
                onBackAction?.Invoke();
            }
        };

        // Canvas tabs
        GameObject tabsViewport = new("CanvasTabsViewport");
        tabsViewport.transform.SetParent(headerGo.transform, false);
        var tabsViewportRect = tabsViewport.AddComponent<RectTransform>();
        tabsViewportRect.anchorMin = Vector2.zero;
        tabsViewportRect.anchorMax = Vector2.one;
        tabsViewportRect.offsetMin = new Vector2(58f, 4f);
        tabsViewportRect.offsetMax = new Vector2(-6f, -4f);
        var tabsViewportImage = tabsViewport.AddComponent<Image>();
        tabsViewportImage.color = Color.clear;
        tabsViewportImage.raycastTarget = true;
        tabsViewport.AddComponent<RectMask2D>();

        GameObject tabsContentObject = new("CanvasTabs");
        tabsContentObject.transform.SetParent(tabsViewport.transform, false);
        tabsContent = tabsContentObject.AddComponent<RectTransform>();
        tabsContent.anchorMin = new Vector2(0f, 0f);
        tabsContent.anchorMax = new Vector2(0f, 1f);
        tabsContent.pivot = new Vector2(0f, 0.5f);
        tabsContent.sizeDelta = Vector2.zero;
        var tabsLayout = tabsContentObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.padding = new RectOffset(2, 2, 2, 2);
        tabsLayout.spacing = 4f;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = false;
        tabsLayout.childForceExpandWidth = false;
        tabsLayout.childForceExpandHeight = false;
        tabsLayout.childAlignment = TextAnchor.MiddleLeft;
        var tabsFitter = tabsContentObject.AddComponent<ContentSizeFitter>();
        tabsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        tabsFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        var tabsScroll = tabsViewport.AddComponent<ScrollRect>();
        tabsScroll.content = tabsContent;
        tabsScroll.viewport = tabsViewportRect;
        tabsScroll.horizontal = true;
        tabsScroll.vertical = false;
        tabsScroll.movementType = ScrollRect.MovementType.Clamped;
        tabsScroll.inertia = true;

        GameObject tabDivider = new("TabDivider");
        tabDivider.transform.SetParent(headerGo.transform, false);
        var tabDividerRect = tabDivider.AddComponent<RectTransform>();
        tabDividerRect.anchorMin = new Vector2(0f, 0f);
        tabDividerRect.anchorMax = new Vector2(1f, 0f);
        tabDividerRect.pivot = new Vector2(0.5f, 0f);
        tabDividerRect.sizeDelta = new Vector2(0f, 1f);
        tabDividerRect.anchoredPosition = Vector2.zero;
        var tabDividerImage = tabDivider.AddComponent<Image>();
        tabDividerImage.color = new Color32(255, 255, 255, 120);
        tabDividerImage.raycastTarget = false;

        // Pad (Layout Area)
        GameObject pad = new("Pad");
        pad.transform.SetParent(GameObject.transform, false);

        RectTransform padRect = pad.AddComponent<RectTransform>();
        padRect.anchorMin = Vector2.zero;
        padRect.anchorMax = Vector2.one;
        padRect.pivot = new Vector2(0.5f, 0.5f);
        padRect.offsetMin = new Vector2(18f, 18f);
        padRect.offsetMax = new Vector2(-18f, -68f);

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

        // Hierarchy Scroll View
        hierarchyContent = O5Factory.ScrollView(O5KitAdapters.Ctx, hierarchyCol.transform, 6f, expandLayout: true).content;

        // Hierarchy Create Toolbar
        GameObject hierCreateToolbar = new("HierarchyCreateToolbar");
        hierCreateToolbar.transform.SetParent(hierarchyCol.transform, false);

        var hierCreateRect = hierCreateToolbar.AddComponent<RectTransform>();

        var hierCreateLE = hierCreateToolbar.AddComponent<LayoutElement>();
        hierCreateLE.preferredHeight = 78f;
        hierCreateLE.minHeight = 78f;
        hierCreateLE.flexibleWidth = 0f;
        hierCreateLE.flexibleHeight = 0f;

        var createVLayout = hierCreateToolbar.AddComponent<VerticalLayoutGroup>();
        createVLayout.spacing = 6f;
        createVLayout.childControlWidth = true;
        createVLayout.childControlHeight = true;
        createVLayout.childForceExpandWidth = true;
        createVLayout.childForceExpandHeight = false;


        // Empty Row
        RectTransform emptyRow = CreateHierarchyControlRow(hierCreateToolbar.transform);

        var btnEmpty = O5Factory.Button(O5KitAdapters.Ctx, emptyRow, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null
                ? selectedObject.CreateOvObject()
                : currentCanvas.CreateOvObject();

            newObj.Config.Name = "EmptyObject";
            newObj.ApplyComponent();
            newObj.ApplyConfig();

            SelectObject(newObj);
            SaveConfig();
        }, MainCore.Spr.Get(UISprite.Cube128), "btn_hier_add_empty", height: 36f);

        btnEmpty.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnEmpty);


        // Text / Image Row
        RectTransform createRow = CreateHierarchyControlRow(hierCreateToolbar.transform);

        var btnText = O5Factory.Button(O5KitAdapters.Ctx, createRow, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null
                ? selectedObject.CreateOvObject()
                : currentCanvas.CreateOvObject();

            newObj.Config.Name = "TextObject";
            newObj.Config.TextConfig = new TextMeshProUGUISettings();
            newObj.Config.TextEngineConfig = new OvTextSettings();
            newObj.Config.ContentSizeFitterConfig ??= new ContentSizeFitterSettings();

            newObj.ApplyComponent();
            newObj.ApplyConfig();

            SelectObject(newObj);
            SaveConfig();
        }, MainCore.Spr.Get(UISprite.Text128), "btn_hier_add_text", height: 36f);

        btnText.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnText);

        var btnImage = O5Factory.Button(O5KitAdapters.Ctx, createRow, () => {
            if(currentCanvas == null) {
                return;
            }

            OvObject newObj = selectedObject != null
                ? selectedObject.CreateOvObject()
                : currentCanvas.CreateOvObject();

            newObj.Config.Name = "ImageObject";
            newObj.Config.ImageConfig = new ImageSettings();

            newObj.ApplyComponent();
            newObj.ApplyConfig();

            SelectObject(newObj);
            SaveConfig();
        }, MainCore.Spr.Get(UISprite.Image128), "btn_hier_add_image", height: 36f);

        btnImage.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnImage);

        // Hierarchy Control Toolbar
        GameObject hierCtrlToolbar = new("HierarchyControlToolbar");
        hierCtrlToolbar.transform.SetParent(hierarchyCol.transform, false);

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

        // Clone
        var btnClone = O5Factory.Button(O5KitAdapters.Ctx, hierCtrlToolbar.transform, () => {
            if(selectedObject == null || currentCanvas == null) {
                return;
            }

            OvObject source = selectedObject;
            OvObject clone = source.Clone();

            if (!source.Config.Name.Value.EndsWith(" Copy")) {
                clone.Config.Name.Value = $"{source.Config.Name.Value} Copy";
            }
            clone.ApplyConfig();

            if(source.Parent != null) {
                OvObject parent = source.Parent;
                int index = parent.Children.IndexOf(source);

                parent.Attach(clone);
                parent.SetChildIndex(clone, index + 1);
            } else {
                int index = currentCanvas.OvObjects.IndexOf(source);

                currentCanvas.Attach(clone);
                currentCanvas.OvObjects.Remove(clone);
                currentCanvas.OvObjects.Insert(index + 1, clone);

                SyncRootSiblingOrder();
            }

            SelectObject(clone);
            SaveConfig();
        }, MainCore.Spr.Get(UISprite.Clone128), "btn_hier_clone", height: 36f);

        btnClone.Rect.offsetMax = Vector2.zero;
        permanentUiObjects.Add(btnClone);

        // Delete
        var btnDel = O5Factory.Button(O5KitAdapters.Ctx, hierCtrlToolbar.transform, () => {
            if(selectedObject == null) {
                if(currentCanvas == null) {
                    return;
                }

                if(armedDeleteCanvas != currentCanvas ||
                    (DateTime.Now - armedDeleteTime).TotalSeconds > 5) {
                    armedDeleteCanvas = currentCanvas;
                    armedDeleteTime = DateTime.Now;
                    if(deleteButton != null && deleteButton.Icon != null) {
                        deleteButton.Icon.color = UIColors.SoftRed;
                    }
                    return;
                }

                DisarmDeleteButton();
                var canvasToDelete = currentCanvas;

                if(OverlayCore.DeleteOvCanvas(canvasToDelete)) {
                    CloseCanvasTab(activeTab);
                }

                return;
            }

            var toDelete = selectedObject;

            OvObject nextSelect = null;

            if (toDelete.Parent != null) {
                var siblings = toDelete.Parent.Children;
                int index = siblings.IndexOf(toDelete);

                if (index > 0) {
                    nextSelect = siblings[index - 1];
                } else if (index + 1 < siblings.Count) {
                    nextSelect = siblings[index + 1];
                } else {
                    nextSelect = toDelete.Parent;
                }
            } else if (currentCanvas != null) {
                var rootObjects = currentCanvas.OvObjects;
                int index = rootObjects.IndexOf(toDelete);

                if (index > 0) {
                    nextSelect = rootObjects[index - 1];
                } else if (index + 1 < rootObjects.Count) {
                    nextSelect = rootObjects[index + 1];
                }
            }
            // ------------------------------------------

            if(toDelete.Parent == null) {
                currentCanvas?.Detach(toDelete);
            }

            toDelete.Dispose();

            // Switch selection to the browsed object
            selectedObject = nextSelect;
            SaveActiveTabState();

            RebuildHierarchy();
            RebuildInspector();
            SaveConfig();
        }, MainCore.Spr.Get(UISprite.X128), "btn_hier_del", height: 36f);

        btnDel.Rect.offsetMax = Vector2.zero;
        deleteButton = btnDel;
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

        // Inspector Scroll View
        inspectorContent = O5Factory.ScrollView(O5KitAdapters.Ctx, inspectorCol.transform, 12f, expandLayout: true).content;
        headerGo.transform.SetAsLastSibling();
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

    private void DisarmDeleteButton() {
        armedDeleteCanvas = null;
        if(deleteButton != null && deleteButton.Icon != null) {
            deleteButton.Icon.color = Color.white;
        }
    }

    public void Open(OvCanvas canvas, bool noAnimate = false) {
        CanvasTabState tab = null;
        foreach(var openTab in canvasTabs) {
            if(openTab.Canvas == canvas) {
                tab = openTab;
                break;
            }
        }

        if(tab == null) {
            tab = new CanvasTabState(canvas);
            canvasTabs.Add(tab);
        }

        ActivateCanvasTab(tab, noAnimate);
    }

    private void SaveActiveTabState() {
        if(activeTab == null) {
            return;
        }

        activeTab.SelectedObject = selectedObject;
        activeTab.CollapsedObjects.Clear();
        activeTab.CollapsedObjects.UnionWith(collapsedObjects);
    }

    private void ActivateCanvasTab(CanvasTabState tab, bool noAnimate = false) {
        if(activeTab == tab && GameObject.activeSelf) {
            return;
        }

        SaveActiveTabState();
        activeTab = tab;
        currentCanvas = tab.Canvas;
        selectedObject = tab.SelectedObject;
        collapsedObjects.Clear();
        collapsedObjects.UnionWith(tab.CollapsedObjects);
        foldoutAnimTarget = null;
        DisarmDeleteButton();

        RebuildHierarchy();
        RebuildInspector();
        RebuildCanvasTabs();

        bool wasActive = GameObject.activeSelf;
        GameObject.SetActive(true);
        if(noAnimate) {
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = true;
        } else if(!wasActive) {
            canvasFadeTween?.Kill();
            canvasFadeTween = CanvasGroup.TFade(1f, 0.25f, O5Ease.OutCubic, () => CanvasGroup.blocksRaycasts = true);
        }
    }

    private void CloseCanvasTab(CanvasTabState tab) {
        int index = canvasTabs.IndexOf(tab);
        if(index < 0) {
            return;
        }

        bool wasActive = activeTab == tab;
        if(wasActive) {
            SaveActiveTabState();
        }
        canvasTabs.RemoveAt(index);

        if(canvasTabs.Count == 0) {
            activeTab = null;
            currentCanvas = null;
            selectedObject = null;
            collapsedObjects.Clear();
            RebuildCanvasTabs();
            onBackAction?.Invoke();
            return;
        }

        if(wasActive) {
            ActivateCanvasTab(canvasTabs[Math.Min(index, canvasTabs.Count - 1)], true);
        } else {
            RebuildCanvasTabs();
        }
    }

    private void RebuildCanvasTabs() {
        if(tabsContent == null) {
            return;
        }

        for(int i = tabsContent.childCount - 1; i >= 0; i--) {
            GameObject child = tabsContent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }

        foreach(var tab in canvasTabs) {
            tab.TabRect = null;
        }

        foreach(var tab in canvasTabs) {
            GameObject tabObject = new("CanvasTab");
            tabObject.transform.SetParent(tabsContent, false);
            var tabRect = tabObject.AddComponent<RectTransform>();
            tabRect.sizeDelta = new Vector2(0f, 34f);
            tab.TabRect = tabRect;
            var tabLE = tabObject.AddComponent<LayoutElement>();
            tabLE.preferredWidth = 220f;
            tabLE.minWidth = 120f;
            tabLE.preferredHeight = 34f;
            tabLE.minHeight = 34f;
            var tabImage = tabObject.AddComponent<Image>();
            tabImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            tabImage.type = Image.Type.Sliced;
            tabImage.color = tab == activeTab ? UIColors.ObjectButton : UIColors.ObjectBG;

            var tabLayout = tabObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.padding = new RectOffset(12, 14, 2, 2);
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = true;
            var tabTrigger = tabObject.AddComponent<EventTrigger>();
            AddCanvasTabHoverHighlight(tabImage, tabTrigger, tab == activeTab);
            UnityUtils.AddEvents(tabTrigger,
                (EventTriggerType.PointerDown, _ => suppressCanvasTabClick = false),
                (EventTriggerType.PointerClick, e => {
                    PointerEventData pointer =
#if ML && IL2CPP
                        e.TryCast<PointerEventData>();
#else
                        e as PointerEventData;
#endif
                    if(suppressCanvasTabClick) {
                        suppressCanvasTabClick = false;
                        return;
                    }
                    if(pointer != null && pointer.button == InputButton.Left) {
                        ActivateCanvasTab(tab);
                    }
                }),
                (EventTriggerType.BeginDrag, e => {
                    PointerEventData pointer =
#if ML && IL2CPP
                        e.TryCast<PointerEventData>();
#else
                        e as PointerEventData;
#endif
                    if(pointer == null || pointer.button != InputButton.Left) {
                        return;
                    }

                    BeginCanvasTabDrag(tab, pointer);
                }),
                (EventTriggerType.Drag, e => {
                    if(draggedCanvasTab != tab) {
                        return;
                    }

                    PointerEventData pointer =
#if ML && IL2CPP
                        e.TryCast<PointerEventData>();
#else
                        e as PointerEventData;
#endif
                    if(pointer != null && MoveCanvasTab(tab, pointer)) {
                        canvasTabDragReordered = true;
                    }
                }),
                (EventTriggerType.EndDrag, _ => {
                    if(draggedCanvasTab != tab) {
                        return;
                    }

                    EndCanvasTabDrag(tab);
                })
            );

            GameObject labelObject = new("CanvasTabLabel");
            labelObject.transform.SetParent(tabObject.transform, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
            label.fontSize = 12f;
            label.text = string.IsNullOrEmpty(tab.Canvas.Config.Name)
                ? MainCore.Tr.Get("EMPTY", "(Empty)")
                : tab.Canvas.Config.Name;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var labelLE = labelObject.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;
            labelLE.minWidth = 40f;

            GameObject closeObject = new("CloseTab");
            closeObject.transform.SetParent(tabObject.transform, false);
            var closeRect = closeObject.AddComponent<RectTransform>();
            var closeLE = closeObject.AddComponent<LayoutElement>();
            closeLE.preferredWidth = 12f;
            closeLE.minWidth = 12f;
            closeLE.preferredHeight = 12f;
            closeLE.minHeight = 12f;
            var closeImage = closeObject.AddComponent<Image>();
            closeImage.sprite = MainCore.Spr.Get(UISprite.X128);
            closeImage.preserveAspect = true;
            closeImage.color = Color.white;
            var closeTrigger = closeObject.AddComponent<EventTrigger>();
            UnityUtils.AddEvents(closeTrigger,
                (EventTriggerType.PointerClick, e => {
                    PointerEventData pointer =
#if ML && IL2CPP
                        e.TryCast<PointerEventData>();
#else
                        e as PointerEventData;
#endif
                    if(pointer != null && pointer.button == InputButton.Left) {
                        CloseCanvasTab(tab);
                    }
                })
            );
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tabsContent);
        if(activeTab != null) {
            for(int i = 0; i < canvasTabs.Count; i++) {
                if(canvasTabs[i] == activeTab) {
                    var scroll = tabsContent.GetComponentInParent<ScrollRect>();
                    if(scroll != null) {
                        var activeRect = tabsContent.GetChild(i) as RectTransform;
                        float viewportWidth = scroll.viewport.rect.width;
                        float contentWidth = tabsContent.rect.width;
                        float maxOffset = Mathf.Max(0f, contentWidth - viewportWidth);
                        float currentOffset = -tabsContent.anchoredPosition.x;
                        float targetOffset = currentOffset;
                        if(activeRect != null) {
                            float tabLeft = activeRect.anchoredPosition.x;
                            float tabRight = tabLeft + activeRect.rect.width;
                            if(tabLeft < currentOffset) {
                                targetOffset = tabLeft;
                            } else if(tabRight > currentOffset + viewportWidth) {
                                targetOffset = tabRight - viewportWidth;
                            }
                        }
                        targetOffset = Mathf.Clamp(targetOffset, 0f, maxOffset);
                        tabsContent.anchoredPosition = new Vector2(-targetOffset, tabsContent.anchoredPosition.y);
                        scroll.StopMovement();
                    }
                    break;
                }
            }
        }
    }

    private void BeginCanvasTabDrag(CanvasTabState tab, PointerEventData pointer) {
        if(canvasTabs.Count < 2 || tab.TabRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tabsContent,
            pointer.position,
            pointer.pressEventCamera,
            out Vector2 localPointer
        )) {
            return;
        }

        RectTransform tabRect = tab.TabRect;
        LayoutElement tabLayout = tabRect.GetComponent<LayoutElement>();
        Vector3 tabCenter = tabsContent.InverseTransformPoint(tabRect.TransformPoint(tabRect.rect.center));
        float width = tabRect.rect.width;
        float height = tabRect.rect.height;

        GameObject placeholderObject = new("CanvasTabDragPlaceholder");
        placeholderObject.transform.SetParent(tabsContent, false);
        canvasTabDragPlaceholder = placeholderObject.AddComponent<RectTransform>();
        var placeholderLayout = placeholderObject.AddComponent<LayoutElement>();
        placeholderLayout.minWidth = width;
        placeholderLayout.preferredWidth = width;
        placeholderLayout.minHeight = height;
        placeholderLayout.preferredHeight = height;
        placeholderLayout.flexibleWidth = 0f;
        placeholderLayout.flexibleHeight = 0f;
        canvasTabDragPlaceholder.SetSiblingIndex(tabRect.GetSiblingIndex());

        draggedCanvasTab = tab;
        draggedCanvasTabLayout = tabLayout;
        draggedCanvasTabLayout.ignoreLayout = true;
        canvasTabDragOriginalAnchorMin = tabRect.anchorMin;
        canvasTabDragOriginalAnchorMax = tabRect.anchorMax;
        canvasTabDragOriginalPivot = tabRect.pivot;
        canvasTabDragOriginalSizeDelta = tabRect.sizeDelta;
        canvasTabDragPointerOffsetX = localPointer.x - tabCenter.x;
        canvasTabDragPreviousCenterX = tabCenter.x;
        canvasTabDragY = tabCenter.y;
        canvasTabDragWidth = width;
        canvasTabDragReordered = false;

        tabRect.anchorMin = new Vector2(0f, 0.5f);
        tabRect.anchorMax = new Vector2(0f, 0.5f);
        tabRect.pivot = new Vector2(0.5f, 0.5f);
        tabRect.sizeDelta = new Vector2(width, height);
        tabRect.anchoredPosition = new Vector2(tabCenter.x, tabCenter.y);
        tabRect.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tabsContent);
    }

    private bool MoveCanvasTab(CanvasTabState tab, PointerEventData pointer) {
        if(draggedCanvasTab != tab || canvasTabDragPlaceholder == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tabsContent,
            pointer.position,
            pointer.pressEventCamera,
            out Vector2 localPointer
        )) {
            return false;
        }

        RectTransform firstSlot = GetCanvasTabSlotRect(canvasTabs[0]);
        RectTransform lastSlot = GetCanvasTabSlotRect(canvasTabs[canvasTabs.Count - 1]);
        float minCenter = GetCanvasTabLocalX(firstSlot, firstSlot.rect.xMin) + canvasTabDragWidth * 0.5f;
        float maxCenter = GetCanvasTabLocalX(lastSlot, lastSlot.rect.xMax) - canvasTabDragWidth * 0.5f;
        float centerX = Mathf.Clamp(localPointer.x - canvasTabDragPointerOffsetX, minCenter, maxCenter);
        tab.TabRect.anchoredPosition = new Vector2(centerX, canvasTabDragY);

        int currentIndex = canvasTabs.IndexOf(tab);
        float deltaX = centerX - canvasTabDragPreviousCenterX;
        canvasTabDragPreviousCenterX = centerX;
        if(Mathf.Abs(deltaX) < 0.01f) {
            canvasTabDragReordered = true;
            return true;
        }

        float crossingEdge = centerX + Mathf.Sign(deltaX) * canvasTabDragWidth * 0.5f;
        int insertIndex = 0;
        foreach(var otherTab in canvasTabs) {
            if(otherTab == tab) {
                continue;
            }

            RectTransform otherSlot = GetCanvasTabSlotRect(otherTab);
            float otherCenter = GetCanvasTabLocalX(otherSlot, otherSlot.rect.center.x);
            if(otherCenter < crossingEdge) {
                insertIndex++;
            }
        }

        if(insertIndex == currentIndex) {
            canvasTabDragReordered = true;
            return true;
        }

        canvasTabs.RemoveAt(currentIndex);
        canvasTabs.Insert(Math.Min(Math.Max(insertIndex, 0), canvasTabs.Count), tab);
        for(int i = 0; i < canvasTabs.Count; i++) {
            GetCanvasTabSlotRect(canvasTabs[i]).SetSiblingIndex(i);
        }
        tab.TabRect.SetAsLastSibling();

        LayoutRebuilder.ForceRebuildLayoutImmediate(tabsContent);
        canvasTabDragReordered = true;
        return true;
    }

    private void EndCanvasTabDrag(CanvasTabState tab) {
        if(draggedCanvasTab != tab) {
            return;
        }

        suppressCanvasTabClick = canvasTabDragReordered;
        if(canvasTabDragPlaceholder != null) {
            canvasTabDragPlaceholder.SetParent(null, false);
            UnityEngine.Object.Destroy(canvasTabDragPlaceholder.gameObject);
            canvasTabDragPlaceholder = null;
        }

        if(tab.TabRect != null) {
            tab.TabRect.anchorMin = canvasTabDragOriginalAnchorMin;
            tab.TabRect.anchorMax = canvasTabDragOriginalAnchorMax;
            tab.TabRect.pivot = canvasTabDragOriginalPivot;
            tab.TabRect.sizeDelta = canvasTabDragOriginalSizeDelta;
        }
        if(draggedCanvasTabLayout != null) {
            draggedCanvasTabLayout.ignoreLayout = false;
        }

        for(int i = 0; i < canvasTabs.Count; i++) {
            canvasTabs[i].TabRect?.SetSiblingIndex(i);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(tabsContent);

        draggedCanvasTab = null;
        draggedCanvasTabLayout = null;
        canvasTabDragReordered = false;
    }

    private RectTransform GetCanvasTabSlotRect(CanvasTabState tab) {
        return tab == draggedCanvasTab && canvasTabDragPlaceholder != null
            ? canvasTabDragPlaceholder
            : tab.TabRect;
    }

    private float GetCanvasTabLocalX(RectTransform rect, float x) {
        Vector3 localPoint = tabsContent.InverseTransformPoint(
            rect.TransformPoint(new Vector3(x, rect.rect.center.y, 0f))
        );
        return localPoint.x;
    }

    private static void AddCanvasTabHoverHighlight(Image background, EventTrigger trigger, bool active) {
        Color normalColor = background.color;
        Color hoverColor = active
            ? O5KitAdapters.Ctx.Theme.ButtonHover
            : Color.Lerp(normalColor, O5KitAdapters.Ctx.Theme.ButtonHover, 0.3f);
        ITweenHandle hoverTween = null;

        void Tint(Color color) {
            hoverTween?.Kill();
            hoverTween = background.TColor(color, 0.12f, O5Ease.OutSine);
        }

        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerEnter, () => Tint(hoverColor)),
            (EventTriggerType.PointerExit, () => Tint(normalColor))
        );
    }

    private void SelectObject(OvObject obj) {
        selectedObject = obj;
        if(obj != null) {
            DisarmDeleteButton();
            for(OvObject parent = obj.Parent; parent != null; parent = parent.Parent) {
                collapsedObjects.Remove(parent);
            }
        }
        SaveActiveTabState();
        RebuildHierarchy();
        RebuildInspector();
    }

    private static RectTransform CreateHierarchyControlRow(Transform parent) {
        GameObject rowObject = new("HierarchyControlRow");
        rowObject.transform.SetParent(parent, false);
        RectTransform row = rowObject.AddComponent<RectTransform>();
        var rowElement = rowObject.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 36f;
        rowElement.minHeight = 36f;

        var rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        return row;
    }

    private void SaveConfig() => OverlayCore.SaveAllCanvases();

    private void RebuildHierarchy() {
        draggedObject = null;
        ClearHierarchyDropState();

        foreach(var obj in hierarchyUiObjects) {
            obj.Dispose();
        }
        hierarchyUiObjects.Clear();

        for(int i = hierarchyContent.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(hierarchyContent.GetChild(i).gameObject);
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
        var row = O5Factory.Row(O5KitAdapters.Ctx, hierarchyContent, 50f);

        // Add Horizontal Layout to Row to organize indent & button
        var hLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.spacing = 4f;
        hLayout.padding = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 };

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

        var tmp = O5Factory.ControlText(O5KitAdapters.Ctx, itemBtn.transform, 24f);
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
        O5Effects.HoverOutline(O5KitAdapters.Ctx, itemBtn, trigger);
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerClick, eventData => {
#pragma warning disable IDE0019
                var pointer =
#pragma warning restore IDE0019
#if ML && IL2CPP
                    eventData.TryCast<PointerEventData>();
#else
                    eventData as PointerEventData;
#endif
                if(pointer == null || pointer.button != InputButton.Left) {
                    return;
                }

                if(draggedObject == null) {
                    SelectObject(null);
                }
            }
        ),
            (EventTriggerType.PointerEnter, _ => SetHierarchyDropTarget(null, itemBtnRect, btnImg, true)),
            (EventTriggerType.PointerExit, _ => ClearHierarchyDropTarget(itemBtnRect))
        );
        itemBtnRect.offsetMax = Vector2.zero;
    }

    private void RenderHierarchyItem(OvObject obj, int depth) {
        var row = O5Factory.Row(O5KitAdapters.Ctx, hierarchyContent, 36f);

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
        indentLE.preferredWidth = depth * 16f;

        bool hasChildren = obj.Children.Count > 0;
        bool collapsed = hasChildren && collapsedObjects.Contains(obj);

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

        RectTransform foldoutRect = null;
        if(hasChildren) {
            GameObject foldout = new("Foldout");
            foldout.transform.SetParent(itemBtn.transform, false);
            foldoutRect = foldout.AddComponent<RectTransform>();
            foldoutRect.anchorMin = new Vector2(0f, 0.5f);
            foldoutRect.anchorMax = new Vector2(0f, 0.5f);
            foldoutRect.pivot = new Vector2(0.5f, 0.5f);
            foldoutRect.anchoredPosition = new Vector2(14f, 0f);
            foldoutRect.sizeDelta = new Vector2(12f, 12f);

            var foldoutImg = foldout.AddComponent<Image>();
            foldoutImg.sprite = O5KitAdapters.Ctx.Sprites.Icon("Triangle128");
            foldoutImg.color = Color.white;
            foldoutImg.preserveAspect = true;
            float targetRot = collapsed ? 0f : 180f;
            foldout.transform.localRotation = Quaternion.Euler(0f, 0f, targetRot);
            if(obj == foldoutAnimTarget) {
                foldoutAnimTarget = null;
                float fromRot = foldoutAnimFrom;
                var foldoutT = foldout.transform;
                O5KitAdapters.Ctx.Tween.TweenFloat(
                    () => 0f,
                    t => {
                        if(foldoutT) {
                            foldoutT.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(fromRot, targetRot, t));
                        }
                    },
                    1f, 0.4f, null, O5Ease.OutBack
                );
            }
        }

        var tmp = O5Factory.ControlText(O5KitAdapters.Ctx, itemBtn.transform, 24f, true);
        tmp.text = obj.Config.Name;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        tmp.rectTransform.offsetMin = new Vector2(28f, 0f);

        var trigger = itemBtn.AddComponent<EventTrigger>();
        O5Effects.HoverOutline(O5KitAdapters.Ctx, itemBtn, trigger);
        CanvasGroup dragCanvasGroup = itemBtn.AddComponent<CanvasGroup>();
        UnityUtils.AddEvents(trigger,
            (EventTriggerType.PointerClick, e => {
#pragma warning disable IDE0019
                var ped =
#pragma warning restore IDE0019
#if ML && IL2CPP
                    e.TryCast<PointerEventData>();
#else
                    e as PointerEventData;
#endif
                if(ped == null || ped.button != InputButton.Left) {
                    return;
                }

                if(hasChildren && foldoutRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    foldoutRect, ped.position, ped.pressEventCamera
                )) {
                    foldoutAnimFrom = collapsedObjects.Contains(obj) ? 0f : 180f;
                    if(!collapsedObjects.Add(obj)) {
                        collapsedObjects.Remove(obj);
                    }
                    foldoutAnimTarget = obj;
                    SaveActiveTabState();
                    RebuildHierarchy();
                    return;
                }

                if(draggedObject == null) {
                    SelectObject(obj);
                }
            }
        ),
            (EventTriggerType.BeginDrag, e => {
#pragma warning disable IDE0019
                var ped =
#pragma warning restore IDE0019
#if ML && IL2CPP
                    e.TryCast<PointerEventData>();
#else
                    e as PointerEventData;
#endif

                if(ped == null || ped.button != InputButton.Left) {
                    return;
                }

                draggedObject = obj;
                selectedObject = obj;
                SaveActiveTabState();
                dragCanvasGroup.alpha = 0.45f;
                dragCanvasGroup.blocksRaycasts = false;
            }
        ),
            (EventTriggerType.Drag, UpdateHierarchyDropPreview),
            (EventTriggerType.EndDrag, _ => {
                dragCanvasGroup.alpha = 1f;
                dragCanvasGroup.blocksRaycasts = true;
                CompleteHierarchyDrag();
            }
        ),
            (EventTriggerType.PointerEnter, _ => {
                SetHierarchyDropTarget(
                    obj,
                    itemBtnRect,
                    btnImg,
                    false
                );
            }
        ),
            (EventTriggerType.PointerExit, _ => ClearHierarchyDropTarget(itemBtnRect))
        );
        itemBtnRect.offsetMax = Vector2.zero;

        if(collapsed) {
            return;
        }

        for(int i = 0; i < obj.Children.Count; i++) {
            RenderHierarchyItem(obj.Children[i], depth + 1);
        }
    }

    private void SetHierarchyDropTarget(OvObject target, RectTransform rect, Image image, bool canvas) {
        if(draggedObject == null || target == draggedObject || IsDescendantOf(target, draggedObject)) {
            return;
        }

        ResetHierarchyDropVisual();
        hierarchyDropTarget = target;
        hierarchyDropRect = rect;
        hierarchyDropImage = image;
        hierarchyDropBaseColor = image.color;
        hierarchyDropOnCanvas = canvas;
        UpdateHierarchyDropPreview(null);
    }

    private void ClearHierarchyDropTarget(RectTransform rect) {
        if(hierarchyDropRect != rect) {
            return;
        }

        ResetHierarchyDropVisual();
        hierarchyDropTarget = null;
        hierarchyDropRect = null;
        hierarchyDropImage = null;
        hierarchyDropOnCanvas = false;
    }

    private void UpdateHierarchyDropPreview(BaseEventData _) {
        if(draggedObject == null || hierarchyDropRect == null || hierarchyDropImage == null) {
            return;
        }

        HierarchyDropZone zone = HierarchyDropZone.Inside;
        if(!hierarchyDropOnCanvas) {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hierarchyDropRect,
                O5Kit.Input.O5Input.MousePosition,
                null,
                out Vector2 point
            )) {
                return;
            }

            float edge = hierarchyDropRect.rect.height * 0.27f;
            if(point.y > hierarchyDropRect.rect.yMax - edge) {
                zone = HierarchyDropZone.Before;
            } else if(point.y < hierarchyDropRect.rect.yMin + edge) {
                zone = HierarchyDropZone.After;
            }
        }

        if(hierarchyDropVisualActive && zone == hierarchyDropZone) {
            return;
        }

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
        hierarchyDropImage?.color = hierarchyDropBaseColor;
        if(hierarchyDropLine != null) {
            UnityEngine.Object.Destroy(hierarchyDropLine);
        }

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

        if(!canvas && (target == null || target == moving || IsDescendantOf(target, moving))) {
            return;
        }

        if(moving.Parent != null) {
            moving.Detach();
        } else {
            currentCanvas.Detach(moving);
        }

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
            currentCanvas.OvObjects.Insert(Math.Min(Math.Max(index, 0), currentCanvas.OvObjects.Count), moving);
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
            if(current == ancestor) {
                return true;
            }
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

        for(int i = inspectorContent.childCount - 1; i >= 0; i--) {
            var child = inspectorContent.GetChild(i);
            if(child != null) {
                UnityEngine.Object.Destroy(child.gameObject);
            }
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
            builder.BuildCanvas(currentCanvas, _ => RebuildCanvasTabs());
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
            canvasFadeTween = CanvasGroup.TFade(0f, 0.25f, O5Ease.OutCubic, () => GameObject.SetActive(false));
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
