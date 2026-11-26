using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using Overlayer.TextEngine.Highlight;
using Overlayer.UI;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Overlay;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// Double-click a text to open a compact, movable, syntax-highlighted code editor right over it;
// and the tab on the right edge that pulls the side panel back out when it's hidden.
internal static partial class HudEditor {
    // ---- edit text in place ----

    private const float InlineHeaderHeight = 28f;
    private const float InlineGutterWidth = 34f;
    private const float InlineOpacity = 0.8f;

    private static RectTransform inlineHost;
    private static RectTransform inlineDot;
    private static RectTransform inlineTetherLine;
    private static Image inlineTetherImage;
    private static RectTransform inlineTargetDot;
    private static HudElement inlineTarget;
    private static O5InputField inlineField;
    private static O5Button inlineSaveButton;
    private static TagCompletionPopup inlineTagPopup;
    private static JsCompletionPopup inlineJsPopup;
    private static HudElement pendingInlineEdit;
    private static int inlineOpenedFrame = -1;
    private static bool inlineClosing;
    private static readonly Vector3[] inlineDotCorners = new Vector3[4];

    private static bool InlineEditOpen => inlineHost != null;

    private static bool CanEditText(HudElement el) => el != null && el.IsAlive && !el.ReadOnly
        && el.Object.Config.TextConfig != null && el.Object.Config.TextEngineConfig?.Text.UseFx != true;

    public static void BeginInlineEdit(HudElement el) {
        if (!CanEditText(el)) {
            return;
        }
        EndInlineEdit();
        if (!ReferenceEquals(Primary, el) || selection.Count != 1) {
            Select(el);
        }
        inlineTarget = el;
        var ctx = O5KitAdapters.Ctx;
        el.GetScreenCorners(corners);
        Bounds(corners, out float minX, out float maxX, out float minY, out float maxY);
        Vector2 a = ToLocal(new Vector2(minX, minY)), b = ToLocal(new Vector2(maxX, maxY));

        var card = UiStyle.Rounded("InlineText", root, UIColors.SurfaceOpaque);
        inlineHost = card.rectTransform;
        var cg = inlineHost.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = InlineOpacity;
        inlineHost.anchorMin = inlineHost.anchorMax = new Vector2(0.5f, 0.5f);
        inlineHost.pivot = new Vector2(0f, 1f);
        float maxW = Mathf.Max(360f, Mathf.Min(560f, root.rect.width - 24f));
        float maxH = Mathf.Max(156f, Mathf.Min(300f, root.rect.height - 24f));
        var size = new Vector2(
            Mathf.Clamp(b.x - a.x + 80f, 360f, maxW),
            Mathf.Clamp(b.y - a.y + InlineHeaderHeight + 52f, 156f, maxH));
        inlineHost.sizeDelta = size;
        float hw = root.rect.width * 0.5f, hh = root.rect.height * 0.5f;
        // Prefer below the text so the edited text stays visible right above the top-left tether dot.
        float belowY = a.y - 16f;
        float posY = belowY - size.y >= -hh + 8f ? belowY : b.y + size.y + 16f;
        inlineHost.anchoredPosition = new Vector2(
            Mathf.Clamp(a.x, -hw + 8f, hw - size.x - 8f),
            Mathf.Clamp(posY, -hh + size.y + 8f, hh - 8f));
        inlineHost.SetAsLastSibling();
        inlineOpenedFrame = Time.frameCount;
        inlineClosing = false;

        // Draggable title bar with a top-left tether dot pointing to the edited text.
        var bar = NewImage("Title", inlineHost, new Color(1f, 1f, 1f, 0.04f));
        bar.raycastTarget = true;
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.sizeDelta = new Vector2(0f, InlineHeaderHeight);
        UnityUtils.AddEvents(bar.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerDown, _ => inlineHost?.SetAsLastSibling()),
            (EventTriggerType.Drag, e => {
                if (inlineHost != null && UnityUtils.AsPointerEventData(e) is { } pe) {
                    inlineHost.anchoredPosition += pe.delta / UiScale;
                    UpdateInlineTether();
                }
            }),
            (EventTriggerType.PointerUp, _ => RefocusInlineInput()),
            (EventTriggerType.EndDrag, _ => RefocusInlineInput()));

        var circle = MainCore.Spr.Get(UISprite.Circle128);
        inlineDot = NewRect("Dot", bar.rectTransform);
        inlineDot.anchorMin = inlineDot.anchorMax = new Vector2(0f, 0.5f);
        inlineDot.pivot = new Vector2(0.5f, 0.5f);
        inlineDot.anchoredPosition = new Vector2(14f, 0f);
        inlineDot.sizeDelta = new Vector2(10f, 10f);
        var dotRing = NewImage("Ring", inlineDot, SelectedColor, circle);
        Stretch(dotRing.rectTransform);
        var dotCore = NewImage("Core", inlineDot, Color.white, circle);
        dotCore.rectTransform.anchorMin = dotCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dotCore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dotCore.rectTransform.sizeDelta = new Vector2(6f, 6f);

        if (gizmoLayer != null) {
            inlineTetherImage = NewImage("InlineTetherLine", gizmoLayer,
                O5Kit.Core.O5Palette.WithAlpha(SelectedColor, 0.85f), UILineSprite.AntialiasedLine);
            inlineTetherLine = inlineTetherImage.rectTransform;
            inlineTetherLine.gameObject.SetActive(false);
        }
        if (pixelHandleLayer != null) {
            inlineTargetDot = BuildAutoAnchorDot("InlineTargetDot", pixelHandleLayer);
        }

        var saveCell = NewRect("Save", bar.rectTransform);
        saveCell.anchorMin = saveCell.anchorMax = saveCell.pivot = new Vector2(1f, 0.5f);
        saveCell.anchoredPosition = new Vector2(-6f, 0f);
        saveCell.sizeDelta = new Vector2(20f, 20f);
        inlineSaveButton = UiStyle.IconButton(saveCell, "plus_hud_inline_save", UIIcon.Check, () => CommitAndCloseInlineEdit(), 4f, primary: true);

        var divider = NewImage("Divider", inlineHost, UiStyle.Line);
        divider.raycastTarget = false;
        divider.rectTransform.anchorMin = new Vector2(0f, 1f);
        divider.rectTransform.anchorMax = Vector2.one;
        divider.rectTransform.pivot = new Vector2(0.5f, 1f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -InlineHeaderHeight);
        divider.rectTransform.sizeDelta = new Vector2(0f, 1f);

        var body = NewRect("Body", inlineHost);
        Stretch(body);
        body.offsetMin = new Vector2(8f, 8f);
        body.offsetMax = new Vector2(-8f, -(InlineHeaderHeight + 7f));

        string displayedText = TextOf(el.Object) ?? string.Empty;
        TagSyntaxSpan[] syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
        bool lineNumbersDirty = true;
        int lineNumbersGeometryKey = 0;

        inlineField = O5Factory.Input(ctx, body, "plus_hud_inline", new O5InputOptions {
            Value = displayedText,
            Multiline = true,
            Monospace = true,
            FieldFactory = UICodeInputField.Attach,
            OnChanged = v => {
                if (inlineClosing) {
                    return;
                }
                displayedText = v ?? string.Empty;
                syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
                lineNumbersDirty = true;
                QuickEdit("text", TextOf, SetText, displayedText, false);
            },
        });
        var r = inlineField.Rect;
        Stretch(r);

        var codeInput = UICodeInputField.From(inlineField.InputField);
        var textComp = inlineField.InputField.textComponent;
        textComp.fontSize = 15f;
        textComp.characterSpacing = 0f;
        textComp.lineSpacing = 4f;
        TmpCompat.SetWrappingMode(textComp, TmpCompat.Normal);
        if (inlineField.Placeholder != null) {
            inlineField.Placeholder.fontSize = 15f;
            inlineField.Placeholder.characterSpacing = 0f;
            TmpCompat.SetWrappingMode(inlineField.Placeholder, TmpCompat.Normal);
        }

        RectTransform viewport = inlineField.InputField.textViewport;
        Vector2 vpMin = viewport.offsetMin;
        Vector2 vpMax = viewport.offsetMax;
        viewport.offsetMin = new Vector2(vpMin.x + InlineGutterWidth + 8f, vpMin.y);

        var gutterObj = new GameObject("LineNumberGutter");
        gutterObj.transform.SetParent(inlineField.InputField.transform, false);
        gutterObj.transform.SetAsFirstSibling();
        var gutterRect = gutterObj.AddComponent<RectTransform>();
        gutterRect.anchorMin = new Vector2(0f, 0f);
        gutterRect.anchorMax = new Vector2(0f, 1f);
        gutterRect.pivot = new Vector2(0f, 0.5f);
        gutterRect.offsetMin = new Vector2(vpMin.x, vpMin.y);
        gutterRect.offsetMax = new Vector2(vpMin.x + InlineGutterWidth, vpMax.y);
        var gutterImage = gutterObj.AddComponent<Image>();
        gutterImage.color = new Color(0f, 0f, 0f, 0.18f);
        gutterImage.raycastTarget = false;
        gutterObj.AddComponent<RectMask2D>();

        var sep = NewImage("Separator", gutterRect, new Color(1f, 1f, 1f, 0.1f));
        sep.raycastTarget = false;
        sep.rectTransform.anchorMin = new Vector2(1f, 0f);
        sep.rectTransform.anchorMax = Vector2.one;
        sep.rectTransform.pivot = new Vector2(1f, 0.5f);
        sep.rectTransform.sizeDelta = new Vector2(1f, 0f);

        var lineNumbers = O5Factory.ControlText(ctx, gutterRect, 15f, true);
        lineNumbers.name = "LineNumbers";
        lineNumbers.font = textComp.font;
        lineNumbers.fontSize = textComp.fontSize;
        lineNumbers.characterSpacing = 0f;
        lineNumbers.lineSpacing = textComp.lineSpacing;
        lineNumbers.alignment = TextAlignmentOptions.TopRight;
        lineNumbers.verticalAlignment = VerticalAlignmentOptions.Top;
        TmpCompat.SetNoWrap(lineNumbers);
        lineNumbers.color = new Color(1f, 1f, 1f, 0.3f);
        lineNumbers.raycastTarget = false;
        var numbersRect = lineNumbers.rectTransform;
        numbersRect.anchorMin = Vector2.zero;
        numbersRect.anchorMax = Vector2.one;
        numbersRect.pivot = textComp.rectTransform.pivot;
        numbersRect.offsetMin = new Vector2(4f, 0f);
        numbersRect.offsetMax = new Vector2(-6f, 0f);

        var follower = gutterObj.AddComponent<UILineNumberGutter>();
        follower.Source = textComp.rectTransform;
        follower.LineNumbers = numbersRect;

        inlineTagPopup = new TagCompletionPopup(codeInput, textComp);
        inlineJsPopup = new JsCompletionPopup(codeInput, textComp, true);
        codeInput.HandleKey = key => {
            if (inlineTagPopup != null && inlineTagPopup.HandleKey(key)) {
                return true;
            }
            if (inlineJsPopup != null && inlineJsPopup.HandleKey(key)) {
                return true;
            }
            if (key == KeyCode.Escape) {
                CommitAndCloseInlineEdit();
                return true;
            }
            return false;
        };

        Vector3 meshLayoutKey = default;
        TagSyntaxKind?[] highlightKinds = null;
        string highlightSource = null;
        TagSyntaxSpan[] highlightSpans = null;
        int highlightGeometryKey = 0;
        Color32 highlightPlain = default;
        OvInspectorBuilder.HighlightProbe highlightProbe = OvInspectorBuilder.HighlightProbe.None;

        codeInput.AfterLabelUpdate = (sourceText, composing) => {
            if (inlineClosing) {
                return;
            }
            string currentText = codeInput.text ?? string.Empty;
            if (!string.Equals(currentText, displayedText, System.StringComparison.Ordinal)) {
                displayedText = currentText;
                syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
                lineNumbersDirty = true;
            }
            int geometryKey = OvInspectorBuilder.BuildTextGeometryKey(sourceText, ref meshLayoutKey);
            if (!composing && (lineNumbersDirty || geometryKey != lineNumbersGeometryKey)) {
                lineNumbersDirty = false;
                lineNumbersGeometryKey = geometryKey;
                OvInspectorBuilder.UpdateLineNumbers(lineNumbers, displayedText, null, sourceText);
            }

            string source = composing ? null : displayedText;
            TagSyntaxSpan[] spans = composing ? [] : syntaxSpans;
            bool dirty = false;
            if (highlightKinds == null || !ReferenceEquals(source, highlightSource) || !ReferenceEquals(spans, highlightSpans)) {
                highlightSource = source;
                highlightSpans = spans;
                highlightKinds = TagSyntaxHighlighter.BuildSyntaxKinds(source, spans);
                dirty = true;
            }

            Color32 plain = sourceText.color;
            if (dirty || geometryKey != highlightGeometryKey || !OvInspectorBuilder.SameColor(plain, highlightPlain)
                || !highlightProbe.IsIntact(sourceText)) {
                highlightGeometryKey = geometryKey;
                highlightPlain = plain;
                highlightProbe = OvInspectorBuilder.ApplySyntaxHighlighting(sourceText, highlightKinds);
            }

            inlineTagPopup?.Refresh(composing);
            inlineJsPopup?.Refresh(composing);
        };

        // Enter inserts a new line; clicking outside, the check button, or Esc saves and closes.
        inlineField.InputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        inlineField.InputField.ActivateInputField();
        inlineField.InputField.Select();
        inlineField.InputField.MoveTextEnd(false);
        UpdateInlineTether();
    }

    private static void UpdateInlineTether() {
        if (inlineTetherLine == null || inlineTargetDot == null) {
            return;
        }
        if (inlineHost == null || inlineDot == null || inlineTarget == null || !inlineTarget.IsAlive) {
            inlineTetherLine.gameObject.SetActive(false);
            inlineTargetDot.gameObject.SetActive(false);
            return;
        }
        UnityUtils.GetWorldCornersSafe(inlineDot, inlineDotCorners);
        Vector2 dotScreen = (inlineDotCorners[0] + inlineDotCorners[2]) * 0.5f;

        inlineTarget.GetScreenCorners(corners);
        Bounds(corners, out float minX, out float maxX, out float minY, out float maxY);
        Vector2 targetScreen = new(
            Mathf.Clamp(dotScreen.x, minX, maxX),
            Mathf.Clamp(dotScreen.y, minY, maxY));

        if ((targetScreen - dotScreen).sqrMagnitude > 4f) {
            if (inlineTetherImage != null) {
                inlineTetherImage.color = O5Kit.Core.O5Palette.WithAlpha(SelectedColor, 0.85f);
            }
            PlacePixelLine(inlineTetherLine, dotScreen, targetScreen, 2f);
            inlineTetherLine.SetAsLastSibling();
            inlineTargetDot.anchoredPosition = targetScreen;
            inlineTargetDot.gameObject.SetActive(true);
            inlineTargetDot.SetAsLastSibling();
        } else {
            inlineTetherLine.gameObject.SetActive(false);
            inlineTargetDot.gameObject.SetActive(false);
        }
    }

    private static void RefocusInlineInput() {
        if (!inlineClosing && inlineField?.InputField != null) {
            inlineField.InputField.ActivateInputField();
        }
    }

    private static bool PointerOverInlineEditor(Vector2 mouse) {
        if (inlineHost != null && RectTransformUtility.RectangleContainsScreenPoint(inlineHost, mouse, null)) {
            return true;
        }
        foreach (var hit in raycastHits) {
            if (hit.gameObject == null) {
                continue;
            }
            for (var t = hit.gameObject.transform; t != null; t = t.parent) {
                if (t == inlineHost || t.name is "TagCompletion" or "JsCompletion") {
                    return true;
                }
            }
        }
        return false;
    }

    private static void CommitAndCloseInlineEdit(bool immediate = false) {
        if (inlineField == null || inlineClosing) {
            if (immediate) {
                EndInlineEdit();
            }
            return;
        }
        inlineClosing = true;
        string value = inlineField.InputField != null ? inlineField.InputField.text : inlineField.Value;
        QuickEdit("text", TextOf, SetText, value, true);
        if (inlineField.InputField != null) {
            inlineField.InputField.DeactivateInputField();
        }
        if (immediate) {
            EndInlineEdit();
            SyncPanelValues();
        } else {
            MainThread.Enqueue(() => {
                EndInlineEdit();
                SyncPanelValues();
            });
        }
    }

    /// <summary>Where full-screen editors go while the editor is open (Overlayer's own panel is hidden then).</summary>
    internal static RectTransform FillHost => IsOpen ? root : null;

    private static void EndInlineEdit() {
        inlineClosing = false;
        inlineOpenedFrame = -1;
        inlineTarget = null;
        inlineDot = null;
        if (inlineTetherLine != null) {
            UnityEngine.Object.Destroy(inlineTetherLine.gameObject);
            inlineTetherLine = null;
            inlineTetherImage = null;
        }
        if (inlineTargetDot != null) {
            UnityEngine.Object.Destroy(inlineTargetDot.gameObject);
            inlineTargetDot = null;
        }
        if (inlineSaveButton != null) {
            var btn = inlineSaveButton;
            inlineSaveButton = null;
            btn.Dispose();
        }
        if (inlineTagPopup != null) {
            var p = inlineTagPopup;
            inlineTagPopup = null;
            p.Dispose();
        }
        if (inlineJsPopup != null) {
            var p = inlineJsPopup;
            inlineJsPopup = null;
            p.Dispose();
        }
        if (inlineField != null) {
            var field = inlineField;
            inlineField = null;
            field.Dispose();
        }
        if (inlineHost != null) {
            UnityEngine.Object.Destroy(inlineHost.gameObject);
            inlineHost = null;
        }
    }

    // ---- pull tab ----

    private const float TabWidth = 22f;
    private const float TabHeight = 104f;

    private static RectTransform pullTab;
    private static bool pullingPanel;
    private static readonly Color TabColor = new(0.2f, 0.2f, 0.25f, 0.95f);
    private static readonly Color TabHover = new(0.28f, 0.28f, 0.36f, 0.98f);
    private static float pulled;

    private static void BuildPullTab() {
        // Plain: the rounded sprite's corners are wider than this narrow tab.
        var tab = NewImage("PullTab", root, TabColor);
        tab.raycastTarget = true;
        pullTab = tab.rectTransform;
        pullTab.anchorMin = pullTab.anchorMax = new Vector2(1f, 0.5f);
        pullTab.pivot = new Vector2(1f, 0.5f);
        // Pokes out from the right edge (the rounded right side goes off screen).
        pullTab.sizeDelta = new Vector2(TabWidth + 10f, TabHeight);
        pullTab.anchoredPosition = new Vector2(10f, 0f);
        for (int i = 0; i < 3; i++) {
            var line = NewImage("Grip", pullTab, new Color(1f, 1f, 1f, 0.75f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(2f, 28f);
            line.rectTransform.anchoredPosition = new Vector2(7f + i * 5f, 0f);
        }
        UnityUtils.AddEvents(tab.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => tab.color = TabHover),
            (EventTriggerType.PointerExit, _ => tab.color = TabColor),
            (EventTriggerType.PointerClick, _ => OpenPanelFromTab()),
            (EventTriggerType.BeginDrag, _ => {
                panelOnLeft = false;
                UpdateSideLabel();
                pullingPanel = true;
                pulled = 0f;
                ApplyPanelLayout();
                SlidePanel();
            }),
            (EventTriggerType.Drag, e => {
                if (UnityUtils.AsPointerEventData(e) is { } pe) {
                    pulled = Mathf.Clamp(pulled - pe.delta.x / UiScale, 0f, PanelWidth + Margin);
                    SlidePanel();
                }
            }),
            (EventTriggerType.EndDrag, _ => {
                pullingPanel = false;
                // Pulled out a third of the way or more: it stays (pinned).
                if (pulled >= PanelWidth * 0.3f) {
                    OpenPanelFromTab();
                } else {
                    panel.anchoredPosition = new Vector2(-Margin, -TopInset);
                    ApplyPanelLayout();
                }
            }));
        pullTab.AddToolTip(O5KitAdapters.Ctx, Tr("HUD_SHOW_PANEL", "Show panel"));
    }

    // Hidden panel slides in from the right edge as far as it was pulled.
    private static void SlidePanel() {
        if (panel == null) {
            return;
        }
        panel.anchoredPosition = new Vector2(-Margin + (PanelWidth + Margin) - pulled, -TopInset);
        pullTab.anchoredPosition = new Vector2(10f - pulled, 0f);
    }

    private static void OpenPanelFromTab() {
        pullingPanel = false;
        panelOnLeft = false;
        UpdateSideLabel();
        O5KitAdapters.Ctx.Tooltip.Hide();
        MainCore.Conf.HudPanelPinned.Value = true;
        MainCore.ConfMgr.RequestSave();
        UpdatePinLabel();
        panel.anchoredPosition = new Vector2(-Margin, -TopInset);
        ApplyPanelLayout();
    }

    private static void UpdatePullTab() {
        if (pullTab == null) {
            return;
        }
        pullTab.gameObject.SetActive(!panelShown || pullingPanel);
        if (!pullingPanel) {
            pullTab.anchoredPosition = new Vector2(10f, 0f);
        }
        pullTab.SetAsLastSibling();
    }
}
