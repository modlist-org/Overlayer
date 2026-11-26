using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Factory;
using O5Kit.Input;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Core;
using Overlayer.TextEngine.Highlight;
using Overlayer.Tween;
using Overlayer.UI.Docs;
using Overlayer.UI.Easing;
using Overlayer.UI.Objects.Impl;
using Overlayer.UI.Overlay;
using Overlayer.UI.Utility;
using Overlayer.Utility;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Factory.Page;

internal static class PageDocs {
    private const float SidebarWidth = 280f;
    private const float LivePollInterval = 0.15f;

    // Monochrome palette derived dynamically from UIColors.BaseColor
    private static Color SidebarBg => UIColors.DocsSidebarBg;
    private static Color StageBg => UIColors.DocsStageBg;
    private static Color CardBg => UIColors.DocsCardBg;
    private static Color SurfaceBg => UIColors.DocsSurfaceBg;
    private static Color CodeBg => UIColors.DocsCodeBg;
    private static Color CodeHeaderBg => UIColors.DocsCodeHeaderBg;
    private static readonly Color BorderSubtle = new(1f, 1f, 1f, 0.08f);
    private static readonly Color RowEvenBg = new(1f, 1f, 1f, 0.018f);
    private static readonly Color RowOddBg = new(1f, 1f, 1f, 0.042f);
    private static readonly Color NavIdleBg = new(1f, 1f, 1f, 0f);
    private static readonly Color NavHoverBg = new(1f, 1f, 1f, 0.045f);
    private static readonly Color NavActiveBg = new(1f, 1f, 1f, 0.095f);
    private static readonly Color PillBg = new(1f, 1f, 1f, 0.055f);
    private static readonly Color PillHoverBg = new(1f, 1f, 1f, 0.12f);
    private static readonly Color BtnIdleBg = new(1f, 1f, 1f, 0.06f);
    private static readonly Color BtnActiveBg = new(1f, 1f, 1f, 0.16f);

    private static Color TextPrimary => UIColors.DocsTextPrimary;
    private static Color TextBody => UIColors.DocsTextBody;
    private static Color TextMuted => UIColors.DocsTextMuted;
    private static Color TextDim => UIColors.DocsTextDim;
    private static readonly Color StatusWarn = new(0.95f, 0.84f, 0.52f, 1f);
    private static readonly Color StatusErr = new(0.92f, 0.45f, 0.48f, 1f);

    private sealed class NavEntry {
        public DocChapter Chapter;
        public Image Bg;
        public Image Bar;
        public Graphic Icon;
        public TextMeshProUGUI Label;
        public ITweenHandle Tween;
        public bool Hovered;
    }

    private sealed class ExplorerCardSlot {
        public GameObject Root;
        public RectTransform Rect;
        public RectTransform TopRowRect;
        public LayoutElement TagBtnLayout;
        public TextMeshProUGUI TagTmp;
        public TextMeshProUGUI TagCheck;
        public TextMeshProUGUI MetaTmp;
        public GameObject LiveValCell;
        public TextMeshProUGUI LiveValTmp;
        public GameObject TryBtnCell;
        public O5Button TryBtn;
        public TextMeshProUGUI DescTmp;
        public int BoundIndex = -1;
        public TagCore Tag;
        public string Snippet;
        public string LastLiveText;
        public bool Hovered;
        public ITweenHandle TagFlash;
        public ITweenHandle TagCheckReset;
    }

    private const float ExplorerCardSpacing = 8f;
    private const int ExplorerOverscan = 2;

    private static RectTransform pageRoot;
    private static RectTransform navContent;
    private static RectTransform bodyContent;
    private static CanvasGroup bodyGroup;
    private static UIScrollController bodyScroller;
    private static ITweenHandle bodyFadeTween;
    private static O5InputField globalSearchInput;

    private static readonly List<O5Object> frameControls = [];
    private static readonly List<O5Object> pageControls = [];
    private static readonly List<NavEntry> navEntries = [];
    private static readonly List<ExplorerCardSlot> explorerPool = [];
    private static readonly List<ExplorerCardSlot> searchTagSlots = [];
    private static readonly List<ExplorerCardSlot> explorerFreeSlots = [];
    private static readonly List<TagCore> explorerFilteredTags = [];
    private static float[] explorerCardTops = [];
    private static float[] explorerCardHeights = [];
    private static readonly Vector3[] viewportCorners = new Vector3[4];

    private static DocChapter[] chapters = [];
    private static string activeChapterId = "overview";
    private static string globalQuery = string.Empty;
    private static string explorerQuery = string.Empty;
    private static string explorerFilter = "All";
    private static string explorerModule = string.Empty;

    // Playground state
    private static TextEngineCore sandboxEngine;
    private static string sandboxTemplate = "{Hour:00}:{Minute:00}:{Second:00}";
    private static TextMeshProUGUI sandboxPreviewText;
    private static GameObject sandboxDiagBox;
    private static TextMeshProUGUI sandboxDiagText;
    private static Image sandboxDiagDot;
    private static O5InputField sandboxInput;
    private static RectTransform playgroundCard;
    private static RectTransform playgroundSlot;
    private static RectTransform playgroundFullscreenOverlay;
    private static LayoutElement playgroundEditorLayout;
    private static LayoutElement playgroundPreviewLayout;
    private static O5Button playgroundExpandBtn;
    private static Action<string> sandboxSyncEditor;
    private static CompileDiagnostic[] sandboxDisplayedDiags = [];

    // Overview live stats
    private static TextMeshProUGUI overviewTagCountText;
    private static TextMeshProUGUI overviewCanvasCountText;
    private static TextMeshProUGUI overviewModuleCountText;

    // Explorer list
    private static RectTransform explorerListRoot;
    private static TextMeshProUGUI explorerCountText;
    private static GameObject explorerEmptyCallout;
    private static RectTransform explorerVirtualHost;
    private static LayoutElement explorerVirtualHostLayout;
    private static int lastExplorerFirstIdx = -1;
    private static int lastExplorerLastIdx = -1;
    private static float lastExplorerViewTop = float.NaN;
    private static float lastExplorerViewBottom = float.NaN;

    // Ease live hover preview popup
    private static EasePreviewPopup easePopup;
    private static Sprite sharpFillSprite;
    private static Sprite sharpOutlineSprite;

    private static bool hooked;
    private static float nextLivePoll;
    private static int lastKnownTagCount = -1;
    private static int codeCounter;

    private static O5Context Ctx => O5KitAdapters.Ctx;
    private static string Tr(string key, string fallback) => DocsContent.Tr(key, fallback);
    private static string TrFmt(string key, string fallback, params object[] args) => DocsContent.TrFmt(key, fallback, args);
    private static string TrKeys(string key, string fallback) => DocsContent.TrKeys(key, fallback);

    public static void Create(RectTransform parent) {
        DisposePageOnly();
        DisposeFrameOnly();

        pageRoot = parent;
        chapters = DocsContent.BuildChapters();

        BuildLayout(parent);

        if (!hooked) {
            hooked = true;
            MainCore.Tr.OnLanguageChanged += _ => RebuildAll();
            MainCore.Tr.OnLoadEnd += _ => RebuildAll();
            MenuFactory.OnStateChanged += state => {
                if (state == (int)OriginalMenuState.Docs) {
                    OnTabActivated();
                } else {
                    HideTransientPopups();
                }
            };
        }

        RenderNav();
        RenderActiveView(animate: false);
    }

    public static void Dispose() {
        bodyFadeTween?.Kill();
        bodyFadeTween = null;
        HideTransientPopups();
        DisposePageOnly();
        DisposeFrameOnly();
        try {
            sandboxEngine?.Dispose();
        } catch {
        }
        sandboxEngine = null;
        pageRoot = null;
        navContent = null;
        bodyContent = null;
        bodyGroup = null;
        bodyScroller = null;
        globalSearchInput = null;
    }

    public static void Tick() {
        if (pageRoot == null || UICore.CanvasObj == null || !UICore.CanvasObj.activeSelf
            || UICore.CurrentMenuState != (int)OriginalMenuState.Docs) {
            easePopup?.Hide();
            return;
        }

        // Exit fullscreen playground on Esc if active
        if (playgroundFullscreenOverlay != null && O5Input.GetKeyDown(KeyCode.Escape)) {
            SetPlaygroundFullscreen(false);
        }

        // Tick live Ease curve hover preview
        easePopup?.Tick();

        // Sandbox live preview updates every frame while visible
        if (sandboxPreviewText != null && sandboxEngine != null) {
            try {
                sandboxPreviewText.text = sandboxEngine.Get();
                UpdateSandboxDiagnostics();
            } catch {
            }
        }

        if (explorerVirtualHost != null && explorerVirtualHost.gameObject.activeSelf && explorerFilteredTags.Count > 0) {
            UpdateExplorerVirtualWindow(force: false);
        }

        // Throttled live updates for Explorer values & Overview counters
        if (Time.unscaledTime >= nextLivePoll) {
            nextLivePoll = Time.unscaledTime + LivePollInterval;
            UpdateExplorerValues();
            UpdateOverviewStats();
        }
    }

    private static void OnTabActivated() {
        if (pageRoot == null) {
            return;
        }
        int tagCount = TagManager.Count;
        UpdateOverviewStats();
        if (activeChapterId == "explorer" && string.IsNullOrWhiteSpace(globalQuery) && tagCount != lastKnownTagCount) {
            lastKnownTagCount = tagCount;
            RenderActiveView(animate: false);
        }
        nextLivePoll = 0f;
    }

    private static void HideTransientPopups() {
        easePopup?.Hide();
        SetPlaygroundFullscreen(false);
    }

    internal static void RefreshHotkeys() => RebuildAll();

    private static void RebuildAll() {
        if (pageRoot == null) {
            return;
        }
        chapters = DocsContent.BuildChapters();
        HideTransientPopups();
        ClearChildren(pageRoot);
        DisposePageOnly();
        DisposeFrameOnly();
        BuildLayout(pageRoot);
        RenderNav();
        RenderActiveView(animate: false);
    }

    // ------------------------------------------------------------------
    // Outer Split-Pane Frame
    // ------------------------------------------------------------------

    private static void BuildLayout(RectTransform parent) {
        // Left navigation rail
        var sidebar = UiStyle.NewRect("DocsSidebar", parent);
        sidebar.anchorMin = new Vector2(0f, 0f);
        sidebar.anchorMax = new Vector2(0f, 1f);
        sidebar.offsetMin = Vector2.zero;
        sidebar.offsetMax = new Vector2(SidebarWidth, 0f);
        var sidebarBg = sidebar.gameObject.AddComponent<Image>();
        sidebarBg.color = SidebarBg;
        sidebarBg.raycastTarget = true;

        var railInner = UiStyle.NewRect("RailInner", sidebar);
        Stretch(railInner);
        var railLayout = UiStyle.Vertical(railInner.gameObject, 10f, new RectOffset(14, 14, 16, 14));
        railLayout.childForceExpandHeight = false;

        // Clean top-left brand: Wiki | vX.Y.Z
        var brandRow = O5Factory.Row(Ctx, railInner, 44f);
        var brandText = O5Factory.ControlText(Ctx, brandRow, Ctx.Theme.FontSizeBody, true);
        brandText.font = Ctx.Fonts.Medium;
        brandText.color = Color.white;
        brandText.rectTransform.anchorMin = Vector2.zero;
        brandText.rectTransform.anchorMax = Vector2.one;
        brandText.rectTransform.offsetMin = new Vector2(6f, 0f);
        brandText.rectTransform.offsetMax = new Vector2(-4f, 0f);
        brandText.alignment = TextAlignmentOptions.MidlineLeft;
        brandText.raycastTarget = false;
        brandText.richText = true;
        brandText.text = $"Wiki <color=#5A5D6B>|</color> <size=17><color=#8F93A6>v{DocsHighlight.Escape(MainCore.Version.ToString())}</color></size>";

        // Global search input (uses default UI border)
        var searchRow = O5Factory.Row(Ctx, railInner);
        globalSearchInput = TrackFrame(O5Factory.Input(Ctx, searchRow, "docs_global_search", new O5InputOptions {
            Placeholder = Tr("DOCS_UI_SEARCH_PLACEHOLDER", "Search docs..."),
            Value = globalQuery,
            OnChanged = val => {
                globalQuery = val ?? string.Empty;
                RenderActiveView(animate: false);
            },
        }));

        UiStyle.Separator(railInner);

        // Scrollable chapter list
        var (_, navScrollContent, _) = O5Factory.ScrollView(Ctx, railInner, spacing: 6f, expandLayout: true);
        navContent = navScrollContent;

        // Right hairline border of sidebar
        var divider = NewImage("Divider", sidebar, BorderSubtle);
        divider.rectTransform.anchorMin = new Vector2(1f, 0f);
        divider.rectTransform.anchorMax = new Vector2(1f, 1f);
        divider.rectTransform.pivot = new Vector2(1f, 0.5f);
        divider.rectTransform.sizeDelta = new Vector2(1f, 0f);
        divider.rectTransform.anchoredPosition = Vector2.zero;

        // Right reading stage
        var stage = UiStyle.NewRect("DocsStage", parent);
        stage.anchorMin = Vector2.zero;
        stage.anchorMax = Vector2.one;
        stage.offsetMin = new Vector2(SidebarWidth, 0f);
        stage.offsetMax = Vector2.zero;
        var stageBg = stage.gameObject.AddComponent<Image>();
        stageBg.color = StageBg;
        stageBg.raycastTarget = false;

        (_, bodyContent, bodyScroller) = O5Factory.ScrollView(
            Ctx,
            stage,
            spacing: 16f,
            contentPadding: new RectOffset(24, 24, 22, 32)
        );
        bodyGroup = bodyContent.gameObject.AddComponent<CanvasGroup>();
    }

    private static void RenderNav() {
        navEntries.Clear();
        if (navContent == null) {
            return;
        }
        ClearChildren(navContent);

        foreach (var chapter in chapters) {
            var captured = chapter;
            var item = SharpRounded("Nav_" + chapter.Id, navContent, NavIdleBg);
            item.raycastTarget = true;
            var le = item.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 48f;
            le.minHeight = 48f;

            var bar = NewImage("Bar", item.rectTransform, Color.clear);
            bar.rectTransform.anchorMin = new Vector2(0f, 0.22f);
            bar.rectTransform.anchorMax = new Vector2(0f, 0.78f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.anchoredPosition = new Vector2(3f, 0f);
            bar.rectTransform.sizeDelta = new Vector2(3f, 0f);

            bool isLogo = chapter.SpriteIcon == UISprite.OV5LogoOutline128;
            Graphic icon;
            if (chapter.SpriteIcon.HasValue) {
                var sprImg = NewImage(
                    isLogo ? "LogoIcon" : "Icon",
                    item.rectTransform,
                    isLogo ? new Color(0.68f, 0.68f, 0.68f, 1f) : TextMuted,
                    MainCore.Spr.Get(chapter.SpriteIcon.Value)
                );
                sprImg.preserveAspect = true;
                icon = sprImg;
            } else {
                icon = MainCore.Icons.Create(
                    item.rectTransform,
                    "Icon",
                    chapter.Icon ?? UIIcon.Book,
                    TextMuted,
                    22f
                );
            }
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(22f, 22f);

            var label = O5Factory.ControlText(Ctx, item.rectTransform, 18f, true);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(44f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            label.alignment = TextAlignmentOptions.Left;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.overflowMode = TextOverflowModes.Ellipsis;
            TmpCompat.SetNoWrap(label);
            label.raycastTarget = false;
            label.text = chapter.Title;

            var entry = new NavEntry {
                Chapter = chapter,
                Bg = item,
                Bar = bar,
                Icon = icon,
                Label = label,
            };
            navEntries.Add(entry);

            UnityUtils.AddEvents(item.gameObject.AddComponent<EventTrigger>(),
                (EventTriggerType.PointerEnter, _ => {
                    entry.Hovered = true;
                    UpdateNavVisual(entry, animate: true);
                }),
                (EventTriggerType.PointerExit, _ => {
                    entry.Hovered = false;
                    UpdateNavVisual(entry, animate: true);
                }),
                (EventTriggerType.PointerClick, _ => SelectChapter(captured.Id)));
        }

        UpdateAllNavVisuals(animate: false);
    }

    private static void UpdateAllNavVisuals(bool animate) {
        foreach (var entry in navEntries) {
            UpdateNavVisual(entry, animate);
        }
    }

    private static Color NavIconColor(NavEntry entry, bool selected) {
        if (entry.Chapter.SpriteIcon == UISprite.OV5LogoOutline128) {
            return selected ? Color.white
                : entry.Hovered ? new Color(0.90f, 0.90f, 0.90f, 1f)
                : new Color(0.68f, 0.68f, 0.68f, 1f);
        }
        return selected ? Color.white : entry.Hovered ? TextPrimary : TextMuted;
    }

    private static void UpdateNavVisual(NavEntry entry, bool animate) {
        bool selected = string.IsNullOrWhiteSpace(globalQuery) && entry.Chapter.Id == activeChapterId;
        Color targetBg = selected ? NavActiveBg : entry.Hovered ? NavHoverBg : NavIdleBg;
        Color targetBar = selected ? Color.white : Color.clear;
        Color targetIcon = NavIconColor(entry, selected);
        Color targetLabel = selected ? Color.white : entry.Hovered ? TextPrimary : TextBody;

        entry.Tween?.Kill();
        if (animate) {
            entry.Tween = O5Seq.New()
                .Join(d => entry.Bg.TColor(targetBg, 0.14f, O5Ease.OutExpo, d))
                .Join(d => entry.Bar.TColor(targetBar, 0.14f, O5Ease.OutExpo, d))
                .Join(d => entry.Icon.TColor(targetIcon, 0.14f, O5Ease.OutExpo, d))
                .Play();
        } else {
            entry.Bg.color = targetBg;
            entry.Bar.color = targetBar;
            entry.Icon.color = targetIcon;
        }
        entry.Label.color = targetLabel;
        entry.Label.font = selected ? Ctx.Fonts.Medium : Ctx.Fonts.Regular;
    }

    private static void SelectChapter(string chapterId) {
        if (!string.IsNullOrWhiteSpace(globalQuery)) {
            globalQuery = string.Empty;
            globalSearchInput?.Set(string.Empty, false);
        } else if (chapterId == activeChapterId) {
            return;
        }
        activeChapterId = chapterId;
        UpdateAllNavVisuals(animate: true);
        RenderActiveView(animate: true);
    }

    private static void OpenInPlayground(string template) {
        sandboxTemplate = template ?? string.Empty;
        if (sandboxEngine != null) {
            sandboxEngine.Text = sandboxTemplate;
        }
        if (activeChapterId == "tags" && string.IsNullOrWhiteSpace(globalQuery) && sandboxSyncEditor != null) {
            sandboxSyncEditor(sandboxTemplate);
            bodyScroller?.ScrollTo(0f, false);
            return;
        }
        SelectChapter("tags");
    }

    // ------------------------------------------------------------------
    // Content Rendering
    // ------------------------------------------------------------------

    private static void RenderActiveView(bool animate) {
        if (bodyContent == null) {
            return;
        }
        HideTransientPopups();
        DisposePageOnly();
        ClearChildren(bodyContent);

        if (!string.IsNullOrWhiteSpace(globalQuery)) {
            UpdateAllNavVisuals(animate: false);
            RenderGlobalSearchResults(globalQuery.Trim());
        } else {
            int idx = Array.FindIndex(chapters, c => c.Id == activeChapterId);
            if (idx < 0) {
                idx = 0;
                activeChapterId = chapters[0].Id;
            }
            var chapter = chapters[idx];
            RenderHeader(chapter);

            switch (chapter.Special) {
                case DocSpecialKind.Overview:
                    RenderOverviewDashboard();
                    break;
                case DocSpecialKind.TagPlayground:
                    RenderTagPlayground();
                    break;
                case DocSpecialKind.TagExplorer:
                    RenderTagExplorer();
                    break;
            }

            foreach (var section in chapter.Sections) {
                RenderSectionCard(section);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
        bodyScroller?.ScrollTo(0f, false);
        if (explorerVirtualHost != null && explorerVirtualHost.gameObject.activeSelf) {
            UpdateExplorerVirtualWindow(force: true);
        }

        bodyFadeTween?.Kill();
        if (animate && bodyGroup != null) {
            bodyGroup.alpha = 0f;
            bodyFadeTween = bodyGroup.TFade(1f, 0.16f, O5Ease.OutSine);
        } else if (bodyGroup != null) {
            bodyGroup.alpha = 1f;
        }
    }

    // Clean chapter header: Title + Description only, no icon box, no eyebrow
    private static void RenderHeader(DocChapter chapter) {
        var wrap = UiStyle.NewRect("Header_" + chapter.Id, bodyContent);
        var layout = UiStyle.Vertical(wrap.gameObject, 8f, new RectOffset(2, 2, 2, 8));
        layout.childForceExpandHeight = false;

        AddWrappedText(wrap, chapter.Title, Ctx.Theme.FontSizeH1, Color.white, medium: true);
        AddWrappedText(wrap, chapter.Subtitle, 20f, TextMuted, lineSpacing: 5f);

        var sep = NewImage("HeaderRule", wrap, BorderSubtle);
        var sepLe = sep.gameObject.AddComponent<LayoutElement>();
        sepLe.preferredHeight = sepLe.minHeight = 1f;
    }

    // ------------------------------------------------------------------
    // Special Module 1: Overview Dashboard (3 clean monochrome stats)
    // ------------------------------------------------------------------

    private static void RenderOverviewDashboard() {
        var statsRow = UiStyle.HRow(bodyContent, 12f, 96f);
        overviewTagCountText = AddStatCard(statsRow, Tr("DOCS_UI_STAT_TAGS", "TAGS"));
        overviewCanvasCountText = AddStatCard(statsRow, Tr("DOCS_UI_STAT_CANVASES", "CANVASES"));
        overviewModuleCountText = AddStatCard(statsRow, Tr("DOCS_UI_STAT_MODULES", "MODULES"));
        UpdateOverviewStats();
    }

    private static TextMeshProUGUI AddStatCard(RectTransform row, string label) {
        var cell = UiStyle.Cell(row, 0f);
        var card = SharpRounded("Stat_" + label, cell, CardBg, BorderSubtle);
        card.raycastTarget = false;
        Stretch(card.rectTransform);

        var caption = O5Factory.ControlText(Ctx, card.rectTransform, 15f, true);
        caption.font = Ctx.Fonts.Monospace;
        caption.color = TextDim;
        caption.rectTransform.anchorMin = new Vector2(0f, 1f);
        caption.rectTransform.anchorMax = new Vector2(1f, 1f);
        caption.rectTransform.pivot = new Vector2(0f, 1f);
        caption.rectTransform.offsetMin = new Vector2(16f, -34f);
        caption.rectTransform.offsetMax = new Vector2(-16f, -12f);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.raycastTarget = false;
        caption.text = label;

        var value = O5Factory.ControlText(Ctx, card.rectTransform, 28f, true);
        value.font = Ctx.Fonts.Medium;
        value.color = Color.white;
        value.rectTransform.anchorMin = new Vector2(0f, 0f);
        value.rectTransform.anchorMax = new Vector2(1f, 1f);
        value.rectTransform.offsetMin = new Vector2(16f, 10f);
        value.rectTransform.offsetMax = new Vector2(-16f, -34f);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        value.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(value);
        value.raycastTarget = false;
        value.text = "0";
        return value;
    }

    private static void UpdateOverviewStats() {
        if (overviewTagCountText != null) {
            overviewTagCountText.text = TagManager.Count.ToString(CultureInfo.InvariantCulture);
        }
        if (overviewCanvasCountText != null) {
            int count = OverlayCore.Canvases?.Count ?? 0;
            overviewCanvasCountText.text = count.ToString(CultureInfo.InvariantCulture);
        }
        if (overviewModuleCountText != null) {
            var mods = MainCore.ModuleService?.LoadedModules;
            overviewModuleCountText.text = mods == null || mods.Count == 0
                ? "0"
                : mods.Count == 1 ? mods[0].Name : $"{mods.Count} ({mods[0].Name}...)";
        }
    }

    // ------------------------------------------------------------------
    // Special Module 2: Live Tag Playground (Code Editor + Fullscreen + Safe Diagnostics)
    // ------------------------------------------------------------------

    private static void RenderTagPlayground() {
        sandboxEngine ??= new TextEngineCore();
        sandboxEngine.Text = sandboxTemplate;
        sandboxDisplayedDiags = [];

        var card = SharpRounded("TagPlayground", bodyContent, CardBg, BorderSubtle);
        card.raycastTarget = false;
        playgroundCard = card.rectTransform;
        var layout = UiStyle.Vertical(card.gameObject, 12f, new RectOffset(18, 18, 16, 16));
        layout.childForceExpandHeight = false;

        // Header row: title + copy icon + fullscreen icon
        var headerRow = UiStyle.HRow(card.rectTransform, 8f, 44f);
        var headLabel = O5Factory.ControlText(Ctx, UiStyle.Cell(headerRow, 0f), Ctx.Theme.FontSizeBody, true);
        headLabel.font = Ctx.Fonts.Medium;
        headLabel.color = Color.white;
        headLabel.alignment = TextAlignmentOptions.MidlineLeft;
        headLabel.raycastTarget = false;
        headLabel.text = "PlayGround";

        AddCopyIconButton(UiStyle.Cell(headerRow, 44f), "docs_sandbox_copy", () => sandboxTemplate, padding: 10f);
        playgroundExpandBtn = TrackPage(MakeIconButton(
            UiStyle.Cell(headerRow, 44f),
            "docs_sandbox_expand",
            UIIcon.Expand,
            () => SetPlaygroundFullscreen(playgroundFullscreenOverlay == null),
            padding: 10f
        ));

        // Preset buttons row
        var presetsRow = UiStyle.HRow(card.rectTransform, 8f, 44f);
        var presets = new (string Label, string Template)[] {
            ("Clock", "{Hour:00}:{Minute:00}:{Second:00}"),
            ("ColorRange", "<color=#{ColorRange(Fps,30,144,FF5555,96FF91,OutCubic)}>{EasedValue(Fps,0,300,OutExpo)} FPS</color>"),
            ("MovingMan", "<size={MovingMan(Second,32,20,20,450,false,OutExpo)}>{Second:00}</size>"),
            ("Math / JS", "{Second:00}s -> {round(Second / 60 * 360)}°"),
            ("Curve", "{EasedValue(Fps,1,400,curve(0,0;0.3,0.9;1,1))} FPS"),
        };
        for (int i = 0; i < presets.Length; i++) {
            var p = presets[i];
            TrackPage(MakeMonoButton(UiStyle.Cell(presetsRow, 0f), "docs_sb_preset_" + i, p.Label, () => {
                sandboxTemplate = p.Template;
                sandboxSyncEditor?.Invoke(sandboxTemplate);
                if (sandboxEngine != null) {
                    sandboxEngine.Text = sandboxTemplate;
                }
            }, active: false, fontSize: 18f));
        }

        // Code Editor (UICodeInputField with syntax highlighting, gutter, completion)
        var editorHost = O5Factory.Row(Ctx, card.rectTransform, 220f);
        playgroundEditorLayout = editorHost.GetComponent<LayoutElement>();
        playgroundEditorLayout.minHeight = 220f;
        playgroundEditorLayout.preferredHeight = 220f;
        playgroundEditorLayout.flexibleHeight = 0f;

        BuildPlaygroundCodeEditor(editorHost);

        // Live preview screen (clipped with RectMask2D so oversized text cannot escape)
        var previewBox = SharpRounded("PreviewBox", card.rectTransform, CodeBg, BorderSubtle);
        previewBox.raycastTarget = false;
        previewBox.gameObject.AddComponent<RectMask2D>();
        playgroundPreviewLayout = previewBox.gameObject.AddComponent<LayoutElement>();
        playgroundPreviewLayout.minHeight = 120f;
        playgroundPreviewLayout.flexibleHeight = 0f;
        var previewLayout = UiStyle.Vertical(previewBox.gameObject, 6f, new RectOffset(16, 16, 14, 14));
        previewLayout.childForceExpandHeight = false;

        sandboxPreviewText = AddWrappedText(previewBox.rectTransform, sandboxEngine.Get(), 28f, Color.white, medium: true, lineSpacing: 6f);
        sandboxPreviewText.richText = true;

        // Dedicated clipped single-line diagnostics bar (only visible on errors/warnings)
        var diagBox = SharpRounded("DiagBar", card.rectTransform, SurfaceBg, BorderSubtle);
        diagBox.raycastTarget = false;
        diagBox.gameObject.AddComponent<RectMask2D>();
        sandboxDiagBox = diagBox.gameObject;
        var diagLe = diagBox.gameObject.AddComponent<LayoutElement>();
        diagLe.minHeight = 42f;
        diagLe.preferredHeight = 42f;
        diagLe.flexibleHeight = 0f;

        sandboxDiagDot = NewImage("DiagDot", diagBox.rectTransform, StatusErr, MainCore.Spr.Get(UISprite.Circle128));
        sandboxDiagDot.rectTransform.anchorMin = sandboxDiagDot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        sandboxDiagDot.rectTransform.pivot = new Vector2(0f, 0.5f);
        sandboxDiagDot.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        sandboxDiagDot.rectTransform.sizeDelta = new Vector2(8f, 8f);

        sandboxDiagText = O5Factory.ControlText(Ctx, diagBox.rectTransform, 16f, true);
        sandboxDiagText.font = Ctx.Fonts.Monospace;
        sandboxDiagText.alignment = TextAlignmentOptions.MidlineLeft;
        sandboxDiagText.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(sandboxDiagText);
        sandboxDiagText.raycastTarget = false;
        sandboxDiagText.rectTransform.anchorMin = Vector2.zero;
        sandboxDiagText.rectTransform.anchorMax = Vector2.one;
        sandboxDiagText.rectTransform.offsetMin = new Vector2(28f, 0f);
        sandboxDiagText.rectTransform.offsetMax = new Vector2(-12f, 0f);

        UpdateSandboxDiagnostics();
    }

    private static void BuildPlaygroundCodeEditor(RectTransform row) {
        const float gutterWidth = 52f;
        string displayedText = sandboxTemplate ?? string.Empty;
        TagSyntaxSpan[] syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
        bool lineNumbersDirty = true;
        int lineNumbersGeometryKey = 0;

        void OnTextChanged(string value) {
            displayedText = value ?? string.Empty;
            sandboxTemplate = displayedText;
            syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
            lineNumbersDirty = true;
            if (sandboxEngine != null) {
                sandboxEngine.Text = displayedText;
            }
        }

        sandboxInput = TrackPage(O5Factory.Input(Ctx, row, "docs_sandbox_code", new O5InputOptions {
            Value = displayedText,
            Placeholder = Tr("DOCS_UI_SANDBOX_PLACEHOLDER", "Enter a tag template (e.g., {Fps:500,0})..."),
            Multiline = true,
            Monospace = true,
            OnChanged = OnTextChanged,
            FieldFactory = UICodeInputField.Attach,
        }));

        var codeInput = UICodeInputField.From(sandboxInput.InputField);
        codeInput.richText = false;
        var text = codeInput.textComponent;
        text.richText = false;
        text.fontSize = 18f;
        text.characterSpacing = 0f;
        text.lineSpacing = 5f;
        TmpCompat.SetWrappingMode(text, TmpCompat.Normal);

        sandboxInput.Placeholder.fontSize = 18f;
        sandboxInput.Placeholder.characterSpacing = 0f;
        TmpCompat.SetWrappingMode(sandboxInput.Placeholder, TmpCompat.Normal);

        RectTransform viewport = codeInput.textViewport;
        Vector2 vpMin = viewport.offsetMin;
        Vector2 vpMax = viewport.offsetMax;
        viewport.offsetMin = new Vector2(vpMin.x + gutterWidth + 8f, vpMin.y);

        // Line number gutter
        var gutterObj = new GameObject("LineNumberGutter");
        gutterObj.transform.SetParent(codeInput.transform, false);
        gutterObj.transform.SetAsFirstSibling();
        var gutterRect = gutterObj.AddComponent<RectTransform>();
        gutterRect.anchorMin = new Vector2(0f, 0f);
        gutterRect.anchorMax = new Vector2(0f, 1f);
        gutterRect.pivot = new Vector2(0f, 0.5f);
        gutterRect.offsetMin = new Vector2(vpMin.x, vpMin.y);
        gutterRect.offsetMax = new Vector2(vpMin.x + gutterWidth, vpMax.y);
        var gutterImage = gutterObj.AddComponent<Image>();
        gutterImage.color = new Color(0f, 0f, 0f, 0.22f);
        gutterImage.raycastTarget = false;
        gutterObj.AddComponent<RectMask2D>();

        var sep = NewImage("Sep", gutterRect, BorderSubtle);
        sep.rectTransform.anchorMin = new Vector2(1f, 0f);
        sep.rectTransform.anchorMax = Vector2.one;
        sep.rectTransform.pivot = new Vector2(1f, 0.5f);
        sep.rectTransform.sizeDelta = new Vector2(1f, 0f);

        var lineNumbers = O5Factory.ControlText(Ctx, gutterRect, 18f, true);
        lineNumbers.font = text.font;
        lineNumbers.fontSize = text.fontSize;
        lineNumbers.characterSpacing = 0f;
        lineNumbers.lineSpacing = text.lineSpacing;
        lineNumbers.alignment = TextAlignmentOptions.TopRight;
        lineNumbers.verticalAlignment = VerticalAlignmentOptions.Top;
        TmpCompat.SetNoWrap(lineNumbers);
        lineNumbers.color = TextDim;
        lineNumbers.raycastTarget = false;
        var numbersRect = lineNumbers.rectTransform;
        numbersRect.anchorMin = Vector2.zero;
        numbersRect.anchorMax = Vector2.one;
        numbersRect.pivot = text.rectTransform.pivot;
        numbersRect.offsetMin = new Vector2(4f, 0f);
        numbersRect.offsetMax = new Vector2(-8f, 0f);

        var follower = gutterObj.AddComponent<UILineNumberGutter>();
        follower.Source = text.rectTransform;
        follower.LineNumbers = numbersRect;

        // Tag + inline JS completion popups
        var tagPopup = new TagCompletionPopup(codeInput, text);
        var jsPopup = new JsCompletionPopup(codeInput, text, true);
        codeInput.HandleKey = key => tagPopup.HandleKey(key) || jsPopup.HandleKey(key);

        Vector3 meshLayoutKey = default;
        TagSyntaxKind?[] highlightKinds = null;
        string highlightSource = null;
        TagSyntaxSpan[] highlightSpans = null;
        int highlightGeometryKey = 0;
        Color32 highlightPlain = default;
        HighlightProbe highlightProbe = HighlightProbe.None;

        codeInput.AfterLabelUpdate = (sourceText, composing) => {
            int geometryKey = BuildTextGeometryKey(sourceText, ref meshLayoutKey);
            if (!composing && (lineNumbersDirty || geometryKey != lineNumbersGeometryKey)) {
                lineNumbersDirty = false;
                lineNumbersGeometryKey = geometryKey;
                UpdateLineNumbers(lineNumbers, displayedText, sandboxDisplayedDiags, sourceText);
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
            if (dirty || geometryKey != highlightGeometryKey || !SameColor(plain, highlightPlain)
                || !highlightProbe.IsIntact(sourceText)) {
                highlightGeometryKey = geometryKey;
                highlightPlain = plain;
                highlightProbe = ApplySyntaxHighlighting(sourceText, highlightKinds);
            }

            tagPopup.Refresh(composing);
            jsPopup.Refresh(composing);
        };

        sandboxSyncEditor = value => {
            displayedText = value ?? string.Empty;
            syntaxSpans = TagSyntaxHighlighter.GetSpans(displayedText);
            lineNumbersDirty = true;
            sandboxInput?.Set(displayedText, false);
        };
    }

    private static void SetPlaygroundFullscreen(bool on) {
        if (on == (playgroundFullscreenOverlay != null)) {
            return;
        }
        if (on) {
            if (playgroundCard == null || UICore.Panel == null) {
                return;
            }
            playgroundFullscreenOverlay = UiStyle.NewRect("DocsPlaygroundFullscreen", UICore.Panel);
            playgroundFullscreenOverlay.SetAsLastSibling();
            Stretch(playgroundFullscreenOverlay);
            var bg = playgroundFullscreenOverlay.gameObject.AddComponent<Image>();
            bg.color = O5Palette.WithAlpha(UIColors.SurfaceOpaque, 0.96f);
            bg.raycastTarget = true;

            playgroundSlot = UiStyle.NewRect("PlaygroundSlot", playgroundCard.parent);
            playgroundSlot.SetSiblingIndex(playgroundCard.GetSiblingIndex());
            var slotLe = playgroundSlot.gameObject.AddComponent<LayoutElement>();
            slotLe.minHeight = playgroundCard.rect.height;
            slotLe.preferredHeight = playgroundCard.rect.height;

            playgroundCard.SetParent(playgroundFullscreenOverlay, false);
            playgroundCard.anchorMin = Vector2.zero;
            playgroundCard.anchorMax = Vector2.one;
            playgroundCard.offsetMin = new Vector2(18f, 18f);
            playgroundCard.offsetMax = new Vector2(-18f, -18f);

            if (playgroundEditorLayout != null) {
                playgroundEditorLayout.minHeight = 280f;
                playgroundEditorLayout.preferredHeight = -1f;
                playgroundEditorLayout.flexibleHeight = 1.3f;
            }
            if (playgroundPreviewLayout != null) {
                playgroundPreviewLayout.minHeight = 160f;
                playgroundPreviewLayout.flexibleHeight = 0.7f;
            }
            if (playgroundExpandBtn != null) {
                playgroundExpandBtn.NormalColor = BtnActiveBg;
                playgroundExpandBtn.UpdateVisual(true);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(playgroundCard);
        } else {
            if (playgroundCard != null && playgroundSlot != null) {
                playgroundCard.SetParent(playgroundSlot.parent, false);
                playgroundCard.SetSiblingIndex(playgroundSlot.GetSiblingIndex());
                playgroundSlot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(playgroundSlot.gameObject);
            }
            if (playgroundEditorLayout != null) {
                playgroundEditorLayout.minHeight = 220f;
                playgroundEditorLayout.preferredHeight = 220f;
                playgroundEditorLayout.flexibleHeight = 0f;
            }
            if (playgroundPreviewLayout != null) {
                playgroundPreviewLayout.minHeight = 120f;
                playgroundPreviewLayout.flexibleHeight = 0f;
            }
            if (playgroundExpandBtn != null) {
                playgroundExpandBtn.NormalColor = BtnIdleBg;
                playgroundExpandBtn.UpdateVisual(true);
            }
            if (playgroundFullscreenOverlay != null) {
                playgroundFullscreenOverlay.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(playgroundFullscreenOverlay.gameObject);
            }
            playgroundFullscreenOverlay = null;
            playgroundSlot = null;
            if (bodyContent != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
            }
        }
    }

    private static void UpdateSandboxDiagnostics() {
        if (sandboxDiagBox == null || sandboxDiagText == null || sandboxDiagDot == null || sandboxEngine == null) {
            return;
        }
        if (sandboxEngine.State == TextEngineState.Compiling) {
            if (sandboxDiagBox.activeSelf) {
                sandboxDiagBox.SetActive(false);
            }
            return;
        }
        var diags = sandboxEngine.GetDiagnostics() ?? [];
        sandboxDisplayedDiags = diags;
        if (diags.Length == 0) {
            if (sandboxDiagBox.activeSelf) {
                sandboxDiagBox.SetActive(false);
            }
            return;
        }
        if (!sandboxDiagBox.activeSelf) {
            sandboxDiagBox.SetActive(true);
        }
        var first = diags[0];
        bool hasError = diags.Any(d => d.Severity == CompileSeverity.Error);
        Color col = hasError ? StatusErr : StatusWarn;
        sandboxDiagDot.color = col;
        sandboxDiagText.color = col;
        int line = GetLine(sandboxTemplate, first.Context.Index) + 1;
        string extra = diags.Length > 1 ? $" (+{diags.Length - 1})" : string.Empty;
        sandboxDiagText.text = DocsHighlight.Escape($"L{line} [{first.Severity}] {FormatDiagnostic(first)}{extra}");
    }

    // ------------------------------------------------------------------
    // Special Module 3: Dynamic Live Tag Explorer
    // ------------------------------------------------------------------

    private static void RenderTagExplorer() {
        var barCard = SharpRounded("ExplorerBar", bodyContent, CardBg, BorderSubtle);
        barCard.raycastTarget = false;
        var barLayout = UiStyle.Vertical(barCard.gameObject, 10f, new RectOffset(16, 16, 14, 14));
        barLayout.childForceExpandHeight = false;

        var searchRow = UiStyle.HRow(barCard.rectTransform, 8f, Ctx.Theme.ControlHeight);
        var expInput = TrackPage(O5Factory.Input(Ctx, UiStyle.Cell(searchRow, 0f), "docs_explorer_search", new O5InputOptions {
            Placeholder = Tr("DOCS_UI_EXPLORER_PLACEHOLDER", "Filter by tag name, description, or module..."),
            Value = explorerQuery,
            OnChanged = v => {
                explorerQuery = v ?? string.Empty;
                RebuildTagExplorerList();
            },
        }));

        TrackPage(MakeMonoButton(UiStyle.Cell(searchRow, 136f), "docs_explorer_refresh",
            Tr("DOCS_UI_REFRESH", "Refresh"), () => RenderActiveView(animate: false), active: false, fontSize: 22f));

        if (explorerFilter == "Params") {
            explorerFilter = "All";
        }

        var filterRow = UiStyle.HRow(barCard.rectTransform, 8f, 46f);
        string[] filters = ["All", "Core", "Module", "JS"];
        string[] filterLabels = [
            Tr("DOCS_UI_FILTER_ALL", "All"),
            Tr("DOCS_UI_FILTER_CORE", "Core"),
            Tr("DOCS_UI_FILTER_MODULE", "Module"),
            "JS",
        ];
        for (int i = 0; i < filters.Length; i++) {
            string f = filters[i];
            bool active = explorerFilter == f;
            TrackPage(MakeMonoButton(UiStyle.Cell(filterRow, 0f), "docs_exp_f_" + f, filterLabels[i], () => {
                explorerFilter = f;
                RenderActiveView(animate: false);
            }, active: active, fontSize: 20f));
        }

        if (explorerFilter == "Module") {
            var moduleNames = TagManager.GetAllTags()
                .Where(t => t != null && MatchesExplorerFilter(t, "Module"))
                .Select(GetTagSourceName)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!string.IsNullOrEmpty(explorerModule) && !moduleNames.Contains(explorerModule, StringComparer.OrdinalIgnoreCase)) {
                explorerModule = string.Empty;
            }
            var moduleValues = new List<string>(moduleNames.Count + 1) { string.Empty };
            moduleValues.AddRange(moduleNames);

            var modRow = O5Factory.Row(Ctx, barCard.rectTransform);
            TrackPage(O5Factory.DropDown(Ctx, modRow, "docs_exp_module", new O5DropdownOptions<string> {
                DefaultValue = string.Empty,
                Value = explorerModule,
                Values = moduleValues,
                Display = m => string.IsNullOrEmpty(m) ? Tr("DOCS_UI_FILTER_ALL", "All") : m,
                OnChanged = m => {
                    explorerModule = m ?? string.Empty;
                    RebuildTagExplorerList();
                },
            }));
        }

        var listHost = UiStyle.NewRect("ExplorerList", bodyContent);
        var listLayout = UiStyle.Vertical(listHost.gameObject, ExplorerCardSpacing);
        listLayout.childForceExpandHeight = false;
        explorerListRoot = listHost;

        explorerCountText = AddWrappedText(explorerListRoot, string.Empty, 18f, TextMuted);
        explorerCountText.margin = new Vector4(2f, 0f, 0f, 2f);

        var emptyWrap = UiStyle.NewRect("ExplorerEmpty", explorerListRoot);
        var emptyLayout = UiStyle.Vertical(emptyWrap.gameObject, 0f);
        emptyLayout.childForceExpandHeight = false;
        RenderCallout(emptyWrap, new DocCallout(
            DocCalloutKind.Info,
            Tr("DOCS_UI_NO_MATCHING_TAGS_TITLE", "No matching tags"),
            Tr("DOCS_UI_NO_MATCHING_TAGS_DESC", "Enter a different query or set the filter to All")
        ));
        explorerEmptyCallout = emptyWrap.gameObject;
        explorerEmptyCallout.SetActive(false);

        explorerVirtualHost = UiStyle.NewRect("ExplorerVirtualHost", explorerListRoot);
        explorerVirtualHost.anchorMin = new Vector2(0f, 1f);
        explorerVirtualHost.anchorMax = new Vector2(1f, 1f);
        explorerVirtualHost.pivot = new Vector2(0.5f, 1f);
        explorerVirtualHostLayout = explorerVirtualHost.gameObject.AddComponent<LayoutElement>();
        explorerVirtualHostLayout.flexibleHeight = 0f;

        RebuildTagExplorerList(resetScroll: false);
    }

    private static void RebuildTagExplorerList(bool resetScroll = true) {
        for (int i = 0; i < explorerPool.Count; i++) {
            ReleaseExplorerCardSlot(explorerPool[i]);
        }

        if (explorerListRoot == null || explorerVirtualHost == null) {
            return;
        }

        var allTags = TagManager.GetAllTags();
        lastKnownTagCount = allTags.Count;

        string normQuery = NormalizeSearch(explorerQuery);
        bool hasQuery = !string.IsNullOrEmpty(normQuery);

        var scopedTags = allTags
            .Where(t => t != null && (!t.IsHidden || (hasQuery && t.Name.Equals(explorerQuery.Trim(), StringComparison.OrdinalIgnoreCase))))
            .Where(t => MatchesExplorerFilter(t, explorerFilter))
            .Where(t => explorerFilter != "Module" || string.IsNullOrEmpty(explorerModule) || string.Equals(GetTagSourceName(t), explorerModule, StringComparison.OrdinalIgnoreCase))
            .ToList();

        explorerFilteredTags.Clear();
        explorerFilteredTags.AddRange(
            scopedTags
                .Where(t => !hasQuery || MatchesTagQuery(t, normQuery))
                .OrderBy(t => SourceSortOrder(t))
                .ThenBy(t => GetTagSourceName(t), StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
        );

        int count = explorerFilteredTags.Count;
        if (explorerCountText != null) {
            explorerCountText.text = TrFmt("DOCS_UI_TAG_COUNT", "{0} / {1} tags", count, scopedTags.Count);
        }

        if (explorerEmptyCallout != null) {
            explorerEmptyCallout.SetActive(count == 0);
        }
        explorerVirtualHost.gameObject.SetActive(count > 0);

        if (count > 0) {
            if (explorerCardTops.Length < count) {
                explorerCardTops = new float[count];
                explorerCardHeights = new float[count];
            }
            float y = 0f;
            for (int i = 0; i < count; i++) {
                float h = MeasureExplorerCardHeight(explorerFilteredTags[i]);
                explorerCardTops[i] = y;
                explorerCardHeights[i] = h;
                y += h + ExplorerCardSpacing;
            }
            float totalHeight = explorerCardTops[count - 1] + explorerCardHeights[count - 1];
            explorerVirtualHostLayout.minHeight = totalHeight;
            explorerVirtualHostLayout.preferredHeight = totalHeight;
        } else {
            explorerVirtualHostLayout.minHeight = 0f;
            explorerVirtualHostLayout.preferredHeight = 0f;
        }

        lastExplorerFirstIdx = -1;
        lastExplorerLastIdx = -1;
        lastExplorerViewTop = float.NaN;
        lastExplorerViewBottom = float.NaN;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyContent);
        if (resetScroll) {
            bodyScroller?.ScrollTo(0f, true);
        }
        UpdateExplorerVirtualWindow(force: true);
    }

    private static float MeasureExplorerCardHeight(TagCore tag) {
        if (string.IsNullOrWhiteSpace(tag?.Description)) {
            return 64f;
        }
        string desc = tag.Description.Trim();
        if (!desc.Contains('\n') && desc.Length <= 58) {
            return 96f;
        }
        return 124f;
    }

    private static void UpdateExplorerVirtualWindow(bool force) {
        if (explorerVirtualHost == null || !explorerVirtualHost.gameObject.activeSelf) {
            return;
        }
        int count = explorerFilteredTags.Count;
        if (count == 0) {
            for (int i = 0; i < explorerPool.Count; i++) {
                ReleaseExplorerCardSlot(explorerPool[i]);
            }
            return;
        }

        float viewTop = 0f;
        float viewBottom = 800f;
        if (bodyScroller != null && bodyScroller.viewport != null) {
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(bodyScroller.viewport, viewportCorners);
            float localTopY = -explorerVirtualHost.InverseTransformPoint(viewportCorners[1]).y;
            float localBottomY = -explorerVirtualHost.InverseTransformPoint(viewportCorners[0]).y;
            if (localBottomY > localTopY + 1f) {
                viewTop = localTopY;
                viewBottom = localBottomY;
            }
        }

        if (!force
            && Mathf.Abs(viewTop - lastExplorerViewTop) < 0.5f
            && Mathf.Abs(viewBottom - lastExplorerViewBottom) < 0.5f) {
            return;
        }
        lastExplorerViewTop = viewTop;
        lastExplorerViewBottom = viewBottom;

        int lo = 0;
        int hi = count - 1;
        int first = count;
        while (lo <= hi) {
            int mid = lo + ((hi - lo) >> 1);
            if (explorerCardTops[mid] + explorerCardHeights[mid] >= viewTop) {
                first = mid;
                hi = mid - 1;
            } else {
                lo = mid + 1;
            }
        }

        if (first >= count) {
            lastExplorerFirstIdx = -1;
            lastExplorerLastIdx = -1;
            for (int i = 0; i < explorerPool.Count; i++) {
                ReleaseExplorerCardSlot(explorerPool[i]);
            }
            return;
        }

        lo = first;
        hi = count - 1;
        int last = first - 1;
        while (lo <= hi) {
            int mid = lo + ((hi - lo) >> 1);
            if (explorerCardTops[mid] <= viewBottom) {
                last = mid;
                lo = mid + 1;
            } else {
                hi = mid - 1;
            }
        }

        if (last < first) {
            lastExplorerFirstIdx = -1;
            lastExplorerLastIdx = -1;
            for (int i = 0; i < explorerPool.Count; i++) {
                ReleaseExplorerCardSlot(explorerPool[i]);
            }
            return;
        }

        int firstIdx = Math.Max(0, first - ExplorerOverscan);
        int lastIdx = Math.Min(count - 1, last + ExplorerOverscan);

        if (!force && firstIdx == lastExplorerFirstIdx && lastIdx == lastExplorerLastIdx) {
            return;
        }
        lastExplorerFirstIdx = firstIdx;
        lastExplorerLastIdx = lastIdx;

        int span = lastIdx - firstIdx + 1;
        Span<bool> covered = span <= 128 ? stackalloc bool[span] : new bool[span];
        covered.Clear();

        explorerFreeSlots.Clear();
        for (int i = 0; i < explorerPool.Count; i++) {
            var slot = explorerPool[i];
            if (!force
                && slot.Root.activeSelf
                && slot.BoundIndex >= firstIdx
                && slot.BoundIndex <= lastIdx
                && !covered[slot.BoundIndex - firstIdx]) {
                covered[slot.BoundIndex - firstIdx] = true;
            } else {
                explorerFreeSlots.Add(slot);
            }
        }

        int freeCursor = explorerFreeSlots.Count - 1;
        for (int idx = firstIdx; idx <= lastIdx; idx++) {
            if (covered[idx - firstIdx]) {
                continue;
            }
            var slot = freeCursor >= 0 ? explorerFreeSlots[freeCursor--] : CreateExplorerCardSlot();
            slot.Rect.anchoredPosition = new Vector2(0f, -explorerCardTops[idx]);
            slot.Rect.sizeDelta = new Vector2(0f, explorerCardHeights[idx]);
            BindExplorerCardSlot(slot, explorerFilteredTags[idx], idx);
        }

        while (freeCursor >= 0) {
            ReleaseExplorerCardSlot(explorerFreeSlots[freeCursor--]);
        }
        explorerFreeSlots.Clear();
    }

    private static ExplorerCardSlot CreateExplorerCardSlot() {
        int slotIdx = explorerPool.Count;
        var slot = CreateTagCardSlot(explorerVirtualHost, $"TagCardSlot_{slotIdx}", $"docs_tag_try_slot_{slotIdx}");
        slot.Rect.anchorMin = new Vector2(0f, 1f);
        slot.Rect.anchorMax = new Vector2(1f, 1f);
        slot.Rect.pivot = new Vector2(0.5f, 1f);
        slot.DescTmp.maxVisibleLines = 2;
        slot.DescTmp.overflowMode = TextOverflowModes.Ellipsis;
        explorerPool.Add(slot);
        return slot;
    }

    private static ExplorerCardSlot CreateTagCardSlot(RectTransform parent, string cardName, string tryButtonId) {
        var card = SharpRounded(cardName, parent, CardBg, BorderSubtle);
        card.raycastTarget = false;

        var layout = UiStyle.Vertical(card.gameObject, 8f, new RectOffset(14, 14, 12, 12));
        layout.childForceExpandHeight = false;

        var topRow = UiStyle.HRow(card.rectTransform, 10f, 40f);

        // Invisible button: only the label is visible and highlights on hover/click
        var tagHit = NewImage("TagCopyBtn", topRow, Color.clear);
        tagHit.raycastTarget = true;
        var tagBtnLe = tagHit.gameObject.AddComponent<LayoutElement>();
        tagBtnLe.flexibleWidth = 0f;

        var tagTmp = O5Factory.ControlText(Ctx, tagHit.rectTransform, 19f, true);
        tagTmp.font = Ctx.Fonts.Monospace;
        tagTmp.color = TextBody;
        tagTmp.alignment = TextAlignmentOptions.MidlineLeft;
        tagTmp.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(tagTmp);
        tagTmp.raycastTarget = false;
        Stretch(tagTmp.rectTransform);

        var tagCheck = MainCore.Icons.Create(tagHit.rectTransform, "Check", UIIcon.Check, Color.white, 20f);
        tagCheck.rectTransform.anchorMin = tagCheck.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        tagCheck.rectTransform.pivot = new Vector2(0f, 0.5f);
        tagCheck.rectTransform.anchoredPosition = new Vector2(2f, 0f);
        tagCheck.rectTransform.sizeDelta = new Vector2(20f, 20f);
        tagCheck.gameObject.SetActive(false);

        var metaText = O5Factory.ControlText(Ctx, UiStyle.Cell(topRow, 0f), 17f, true);
        metaText.font = Ctx.Fonts.Monospace;
        metaText.alignment = TextAlignmentOptions.MidlineLeft;
        metaText.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(metaText);
        metaText.raycastTarget = false;
        metaText.color = TextMuted;

        var valCell = UiStyle.Cell(topRow, 156f);
        var valPill = SharpRounded("LiveVal", valCell, CodeBg, BorderSubtle);
        valPill.raycastTarget = false;
        Stretch(valPill.rectTransform);
        var valTmp = O5Factory.ControlText(Ctx, valPill.rectTransform, 17f, true);
        valTmp.font = Ctx.Fonts.Monospace;
        valTmp.color = TextBody;
        valTmp.alignment = TextAlignmentOptions.MidlineRight;
        valTmp.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(valTmp);
        valTmp.rectTransform.offsetMin = new Vector2(8f, 0f);
        valTmp.rectTransform.offsetMax = new Vector2(-10f, 0f);
        valTmp.raycastTarget = false;

        var tryCell = UiStyle.Cell(topRow, 40f);
        var descTmp = AddWrappedText(card.rectTransform, string.Empty, 17f, TextMuted, lineSpacing: 4f);

        var slot = new ExplorerCardSlot {
            Root = card.gameObject,
            Rect = card.rectTransform,
            TopRowRect = topRow,
            TagBtnLayout = tagBtnLe,
            TagTmp = tagTmp,
            TagCheck = tagCheck,
            MetaTmp = metaText,
            LiveValCell = valCell.gameObject,
            LiveValTmp = valTmp,
            TryBtnCell = tryCell.gameObject,
            DescTmp = descTmp,
        };

        slot.TryBtn = MakeIconButton(tryCell, tryButtonId,
            UIIcon.CodeBlock, () => {
                if (!string.IsNullOrEmpty(slot.Snippet)) {
                    OpenInPlayground(slot.Snippet);
                }
            }, padding: 9f);

        void TweenTagColor(Color target, float duration) {
            slot.TagFlash?.Kill();
            Color start = slot.TagTmp.color;
            slot.TagFlash = Ctx.Tween.TweenFloat(() => 0f, t => {
                if (slot.TagTmp != null) {
                    slot.TagTmp.color = Color.Lerp(start, target, t);
                }
            }, 1f, duration);
        }

        UnityUtils.AddEvents(tagHit.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => {
                slot.Hovered = true;
                TweenTagColor(Color.white, 0.12f);
            }),
            (EventTriggerType.PointerExit, _ => {
                slot.Hovered = false;
                TweenTagColor(TextBody, 0.12f);
            }),
            (EventTriggerType.PointerClick, _ => {
                if (string.IsNullOrEmpty(slot.Snippet)) {
                    return;
                }
                GUIUtility.systemCopyBuffer = slot.Snippet;
                slot.TagTmp.color = UIColors.ObjectActiveBright;
                TweenTagColor(slot.Hovered ? Color.white : TextBody, 0.35f);
                slot.TagTmp.gameObject.SetActive(false);
                slot.TagCheck.gameObject.SetActive(true);
                slot.TagCheckReset?.Kill();
                slot.TagCheckReset = Ctx.Tween.TweenFloat(() => 0f, _ => { }, 1f, 0.85f, () => {
                    if (slot.TagCheck != null) {
                        slot.TagCheck.gameObject.SetActive(false);
                    }
                    if (slot.TagTmp != null) {
                        slot.TagTmp.gameObject.SetActive(true);
                    }
                });
            }));

        return slot;
    }

    private static void BindExplorerCardSlot(ExplorerCardSlot slot, TagCore tag, int index) {
        slot.TagCheckReset?.Kill();
        slot.TagCheckReset = null;
        slot.TagFlash?.Kill();
        slot.TagFlash = null;
        slot.Hovered = false;

        slot.BoundIndex = index;
        slot.Tag = tag;
        slot.Snippet = BuildTagCopySnippet(tag);
        slot.LastLiveText = null;

        string tagLabel = $"{{{tag.Name}}}";
        float approxWidth = Mathf.Clamp(tagLabel.Length * 11.6f + 10f, 64f, 320f);
        slot.TagBtnLayout.minWidth = approxWidth;
        slot.TagBtnLayout.preferredWidth = approxWidth;

        slot.TagCheck.gameObject.SetActive(false);
        slot.TagTmp.gameObject.SetActive(true);
        slot.TagTmp.color = TextBody;
        slot.TagTmp.text = DocsHighlight.Escape(tagLabel);

        string source = GetTagSourceName(tag);
        string sig = FormatTagSignature(tag);
        string ret = FormatTypeName(tag.ReturnType);
        string flags = FormatTagFlags(tag.TagType);
        slot.MetaTmp.text =
            (string.IsNullOrEmpty(sig) ? string.Empty : $"<color=#D6DAE8>{DocsHighlight.Escape(sig)}</color>  ")
            + $"<size=15><color=#8E92A4>{DocsHighlight.Escape(source)} · {DocsHighlight.Escape(ret)}"
            + (string.IsNullOrEmpty(flags) ? string.Empty : $" · {DocsHighlight.Escape(flags)}")
            + "</color></size>";

        bool canLive = CanLiveEvaluate(tag);
        slot.LiveValCell.SetActive(canLive);
        if (canLive) {
            string valText = EvaluateTagPreview(tag);
            slot.LastLiveText = valText;
            slot.LiveValTmp.text = valText;
        }

        slot.TryBtnCell.SetActive((tag.TagType & TagType.JsOnly) == 0);

        bool hasDesc = !string.IsNullOrWhiteSpace(tag.Description);
        slot.DescTmp.gameObject.SetActive(hasDesc);
        if (hasDesc) {
            slot.DescTmp.text = DocsHighlight.Escape(tag.Description.Trim());
        }

        slot.Root.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(slot.Rect);
    }

    private static void ReleaseExplorerCardSlot(ExplorerCardSlot slot) {
        slot.BoundIndex = -1;
        slot.Tag = null;
        slot.Snippet = null;
        slot.LastLiveText = null;
        slot.Hovered = false;
        slot.TagCheckReset?.Kill();
        slot.TagCheckReset = null;
        slot.TagFlash?.Kill();
        slot.TagFlash = null;
        if (slot.TagCheck != null) {
            slot.TagCheck.gameObject.SetActive(false);
        }
        if (slot.TagTmp != null) {
            slot.TagTmp.gameObject.SetActive(true);
            slot.TagTmp.color = TextBody;
        }
        if (slot.Root != null) {
            slot.Root.SetActive(false);
        }
    }

    private static void UpdateExplorerValues() {
        UpdateTagSlotValues(explorerPool);
        UpdateTagSlotValues(searchTagSlots);
    }

    private static void UpdateTagSlotValues(List<ExplorerCardSlot> slots) {
        for (int i = 0; i < slots.Count; i++) {
            var slot = slots[i];
            if (slot.BoundIndex < 0 || slot.Tag == null || !slot.Root.activeSelf || !slot.LiveValCell.activeSelf) {
                continue;
            }
            string next = EvaluateTagPreview(slot.Tag);
            if (!string.Equals(next, slot.LastLiveText, StringComparison.Ordinal)) {
                slot.LastLiveText = next;
                slot.LiveValTmp.text = next;
            }
        }
    }

    private static bool CanLiveEvaluate(TagCore tag) =>
        tag != null && tag.RequiredParameterCount == 0 && (tag.TagType & TagType.JsOnly) == 0;

    private static string EvaluateTagPreview(TagCore tag) {
        try {
            object val = tag.Invoke();
            if (val == null) {
                return "<color=#7A7E90>null</color>";
            }
            string text = val switch {
                double d => double.IsNaN(d) || double.IsInfinity(d) ? d.ToString(CultureInfo.InvariantCulture) : d.ToString("0.##", CultureInfo.InvariantCulture),
                float f => float.IsNaN(f) || float.IsInfinity(f) ? f.ToString(CultureInfo.InvariantCulture) : f.ToString("0.##", CultureInfo.InvariantCulture),
                bool b => b ? "<color=#EAECEF>true</color>" : "<color=#7A7E90>false</color>",
                _ => DocsHighlight.Escape(val.ToString()),
            };
            return text.Length > 20 ? text[..19] + "..." : text;
        } catch {
            return "<color=#7A7E90>N/A</color>";
        }
    }

    // ------------------------------------------------------------------
    // Global Cross-Wiki Search View
    // ------------------------------------------------------------------

    private static void RenderGlobalSearchResults(string query) {
        string norm = NormalizeSearch(query);

        var header = UiStyle.NewRect("SearchHeader", bodyContent);
        var hl = UiStyle.Vertical(header.gameObject, 6f, new RectOffset(2, 2, 2, 8));
        hl.childForceExpandHeight = false;
        AddWrappedText(header, Tr("DOCS_UI_SEARCH_RESULTS", "Search Results"), Ctx.Theme.FontSizeH1, Color.white, medium: true);
        AddWrappedText(header, $"\"{DocsHighlight.Escape(query)}\"", 20f, TextMuted);

        var matchedSections = new List<(DocChapter Chapter, DocSection Section)>();
        foreach (var ch in chapters) {
            foreach (var sec in ch.Sections) {
                if (SectionMatches(ch, sec, norm)) {
                    matchedSections.Add((ch, sec));
                }
            }
        }

        if (matchedSections.Count > 0) {
            AddWrappedText(bodyContent, TrFmt("DOCS_UI_SEARCH_SECTIONS", "Sections ({0})", matchedSections.Count), 20f, TextMuted, medium: true);
            int idx = 0;
            foreach (var (ch, sec) in matchedSections) {
                var capturedCh = ch;
                var card = SharpRounded("ResSec_" + idx, bodyContent, CardBg, BorderSubtle);
                card.raycastTarget = false;
                var cl = UiStyle.Vertical(card.gameObject, 8f, new RectOffset(18, 18, 14, 14));
                cl.childForceExpandHeight = false;

                var row = UiStyle.HRow(card.rectTransform, 10f, 44f);
                var titleTmp = O5Factory.ControlText(Ctx, UiStyle.Cell(row, 0f), 20f, true);
                titleTmp.font = Ctx.Fonts.Medium;
                titleTmp.color = Color.white;
                titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
                titleTmp.raycastTarget = false;
                titleTmp.text = $"<color=#8F93A6>[{DocsHighlight.Escape(ch.Title)}]</color>  {DocsHighlight.Escape(sec.Title)}";

                TrackPage(MakeMonoButton(UiStyle.Cell(row, 104f), "docs_res_open_" + idx,
                    Tr("DOCS_UI_OPEN", "Open"), () => SelectChapter(capturedCh.Id), active: false, fontSize: 18f));

                if (!string.IsNullOrEmpty(sec.Summary)) {
                    AddWrappedText(card.rectTransform, sec.Summary, 18f, TextMuted, lineSpacing: 4f);
                }
                idx++;
            }
        }

        var matchedTags = TagManager.GetAllTags()
            .Where(t => t != null && !t.IsHidden && MatchesTagQuery(t, norm))
            .OrderBy(t => SourceSortOrder(t))
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();

        if (matchedTags.Count > 0) {
            AddWrappedText(bodyContent, TrFmt("DOCS_UI_SEARCH_TAGS", "Tags ({0})", matchedTags.Count), 20f, TextMuted, medium: true);
            for (int i = 0; i < matchedTags.Count; i++) {
                var tag = matchedTags[i];
                var slot = CreateTagCardSlot(bodyContent, "TagCard_" + tag.Name, $"docs_tag_try_{1000 + i}");
                BindExplorerCardSlot(slot, tag, 1000 + i);
                searchTagSlots.Add(slot);
            }
        }

        if (matchedSections.Count == 0 && matchedTags.Count == 0) {
            RenderCallout(bodyContent, new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_UI_NO_RESULTS_TITLE", "No results found"),
                TrKeys("DOCS_UI_NO_RESULTS_DESC", "Search with keywords such as Fps, Fx, AddTab, EasedValue, {HudKey}, or o5cp")
            ));
        }
    }

    // ------------------------------------------------------------------
    // Section & Block Renderers
    // ------------------------------------------------------------------

    private static void RenderSectionCard(DocSection section) {
        var card = SharpRounded("Sec_" + section.Title, bodyContent, CardBg, BorderSubtle);
        card.raycastTarget = false;
        var layout = UiStyle.Vertical(card.gameObject, 12f, new RectOffset(20, 20, 18, 20));
        layout.childForceExpandHeight = false;

        AddWrappedText(card.rectTransform, section.Title, Ctx.Theme.FontSizeBody, Color.white, medium: true);

        if (!string.IsNullOrWhiteSpace(section.Summary)) {
            AddWrappedText(card.rectTransform, section.Summary, 19f, TextMuted, lineSpacing: 5f);
        }

        var hr = NewImage("Hr", card.rectTransform, BorderSubtle);
        var hrLe = hr.gameObject.AddComponent<LayoutElement>();
        hrLe.preferredHeight = hrLe.minHeight = 1f;

        foreach (var block in section.Blocks) {
            switch (block) {
                case DocParagraph p:
                    AddWrappedText(card.rectTransform, p.Text, 20f, TextBody, lineSpacing: 5f);
                    break;
                case DocCallout c:
                    RenderCallout(card.rectTransform, c);
                    break;
                case DocCode code:
                    RenderCodeBlock(card.rectTransform, code);
                    break;
                case DocTable table:
                    RenderTable(card.rectTransform, table);
                    break;
                case DocBadges badges:
                    RenderBadges(card.rectTransform, badges);
                    break;
                case DocLink link:
                    RenderLink(card.rectTransform, link);
                    break;
            }
        }
    }

    private static void RenderCallout(RectTransform parent, DocCallout callout) {
        var box = SharpRounded("Callout", parent, SurfaceBg, BorderSubtle);
        box.raycastTarget = false;
        var layout = UiStyle.Vertical(box.gameObject, 6f, new RectOffset(18, 18, 14, 14));
        layout.childForceExpandHeight = false;

        Color titleCol = callout.Kind == DocCalloutKind.Warning ? StatusWarn : TextPrimary;
        AddWrappedText(box.rectTransform, callout.Title, 20f, titleCol, medium: true);
        AddWrappedText(box.rectTransform, callout.Text, 18f, TextMuted, lineSpacing: 5f);
    }

    // Code block: icon Copy button + read-only drag-selectable UICodeInputField with vertex syntax highlighting
    private static void RenderCodeBlock(RectTransform parent, DocCode code) {
        int id = ++codeCounter;
        var box = SharpRounded("Code_" + id, parent, CodeBg, BorderSubtle);
        box.raycastTarget = false;
        var layout = UiStyle.Vertical(box.gameObject, 0f, new RectOffset(1, 1, 1, 1));
        layout.childForceExpandHeight = false;

        // Top bar
        var bar = NewImage("Header", box.rectTransform, CodeHeaderBg);
        bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        var barLayout = UiStyle.Horizontal(bar.gameObject, 8f, new RectOffset(14, 8, 4, 4));
        barLayout.childAlignment = TextAnchor.MiddleLeft;

        string lang = code.Mode switch {
            DocsHighlight.Mode.Tag => "TAG",
            DocsHighlight.Mode.Js => "JS",
            _ => "CODE",
        };
        var cap = O5Factory.ControlText(Ctx, UiStyle.Cell(bar.rectTransform, 0f), 17f, true);
        cap.font = Ctx.Fonts.Monospace;
        cap.alignment = TextAlignmentOptions.MidlineLeft;
        cap.overflowMode = TextOverflowModes.Ellipsis;
        TmpCompat.SetNoWrap(cap);
        cap.raycastTarget = false;
        cap.color = TextMuted;
        cap.text = $"<color=#EAECEF>{lang}</color>  {DocsHighlight.Escape(code.Caption)}";

        if (code.TryInSandbox) {
            TrackPage(MakeIconButton(UiStyle.Cell(bar.rectTransform, 36f), "docs_try_" + id,
                UIIcon.CodeBlock, () => OpenInPlayground(code.Code), padding: 8f));
        }
        AddCopyIconButton(UiStyle.Cell(bar.rectTransform, 36f), "docs_copy_" + id, () => code.Code);

        var sep = NewImage("CodeSep", box.rectTransform, BorderSubtle);
        sep.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;

        // Read-only selectable code body (mouse-draggable partial copy, non-editable)
        string source = code.Code ?? string.Empty;
        int lineCount = 1;
        foreach (char c in source) {
            if (c == '\n') {
                lineCount++;
            }
        }
        float initHeight = Mathf.Max(52f, (lineCount * 27f) + 26f);

        var bodyRow = O5Factory.Row(Ctx, box.rectTransform, initHeight);
        var bodyLe = bodyRow.GetComponent<LayoutElement>();
        bodyLe.minHeight = initHeight;
        bodyLe.preferredHeight = initHeight;
        bodyLe.flexibleHeight = 0f;

        var roInput = TrackPage(O5Factory.Input(Ctx, bodyRow, "docs_code_ro_" + id, new O5InputOptions {
            Value = source,
            Placeholder = string.Empty,
            Multiline = true,
            Monospace = true,
            FieldFactory = UICodeInputField.Attach,
        }));

        // Strip inner O5 input border/background so it blends seamlessly inside the code block
        foreach (var img in roInput.Rect.GetComponentsInChildren<Image>(true)) {
            if (img.gameObject == roInput.InputField.gameObject) {
                img.color = Color.clear;
            } else if (img.sprite == MainCore.Spr.Get(UISliceSprite.CircleOutline128O32P1024)
                || img.sprite == MainCore.Spr.Get(UISliceSprite.CircleOutline128O16P512)) {
                img.color = Color.clear;
            }
        }

        var codeField = UICodeInputField.From(roInput.InputField);
        codeField.readOnly = true;
        codeField.richText = false;
        codeField.caretWidth = 0;
        codeField.customCaretColor = true;
        codeField.caretColor = Color.clear;
        codeField.selectionColor = O5Palette.WithAlpha(UIColors.ObjectActive, 0.28f);

        var tmp = codeField.textComponent;
        tmp.richText = false;
        tmp.font = Ctx.Fonts.Monospace;
        tmp.fontSize = 18f;
        tmp.color = new Color(0.90f, 0.92f, 0.96f, 1f);
        tmp.characterSpacing = 0f;
        tmp.lineSpacing = 5f;
        TmpCompat.SetWrappingMode(tmp, TmpCompat.Normal);

        if (codeField.textViewport != null) {
            codeField.textViewport.offsetMin = new Vector2(14f, 10f);
            codeField.textViewport.offsetMax = new Vector2(-14f, -10f);
        }

        // Apply syntax vertex highlighting (preserves raw copyable text without <color> tags)
        TagSyntaxSpan[] spans = code.Mode switch {
            DocsHighlight.Mode.Js => JsSyntaxHighlighter.GetSpans(source),
            DocsHighlight.Mode.Tag => TagSyntaxHighlighter.GetSpans(source),
            _ => [],
        };
        TagSyntaxKind?[] kinds = spans.Length > 0 ? TagSyntaxHighlighter.BuildSyntaxKinds(source, spans) : null;
        Vector3 layoutKey = default;
        int lastGeomKey = 0;
        Color32 lastPlain = default;
        HighlightProbe probe = HighlightProbe.None;
        float lastMeasuredHeight = initHeight;

        codeField.AfterLabelUpdate = (sourceText, _) => {
            int geomKey = BuildTextGeometryKey(sourceText, ref layoutKey);
            float needed = Mathf.Max(52f, Mathf.Ceil(sourceText.preferredHeight + 26f));
            if (Mathf.Abs(needed - lastMeasuredHeight) > 2f) {
                lastMeasuredHeight = needed;
                bodyLe.minHeight = needed;
                bodyLe.preferredHeight = needed;
            }
            if (kinds != null) {
                Color32 plain = sourceText.color;
                if (geomKey != lastGeomKey || !SameColor(plain, lastPlain) || !probe.IsIntact(sourceText)) {
                    lastGeomKey = geomKey;
                    lastPlain = plain;
                    probe = ApplySyntaxHighlighting(sourceText, kinds);
                }
            }
        };
    }

    private static void RenderTable(RectTransform parent, DocTable table) {
        var wrap = SharpRounded("Table", parent, SurfaceBg, BorderSubtle);
        wrap.raycastTarget = false;
        var layout = UiStyle.Vertical(wrap.gameObject, 0f, new RectOffset(1, 1, 1, 1));
        layout.childForceExpandHeight = false;

        AddTableRow(wrap.rectTransform, table.KeyHeader, table.ValueHeader, new Color(1f, 1f, 1f, 0.055f), isHeader: true);

        for (int i = 0; i < table.Rows.Length; i++) {
            var (k, v) = table.Rows[i];
            AddTableRow(wrap.rectTransform, k, v, i % 2 == 0 ? RowEvenBg : RowOddBg, isHeader: false);
        }
    }

    private static void AddTableRow(RectTransform parent, string key, string value, Color bgColor, bool isHeader) {
        var row = NewImage(isHeader ? "Head" : "Row", parent, bgColor);
        var h = UiStyle.Horizontal(row.gameObject, 16f, new RectOffset(16, 16, 11, 11));
        h.childAlignment = TextAnchor.UpperLeft;
        h.childForceExpandHeight = false;

        var keyCol = UiStyle.Cell(row.rectTransform, 268f);
        var keyLayout = UiStyle.Vertical(keyCol.gameObject, 0f);
        keyLayout.childForceExpandHeight = false;
        AddWrappedText(
            keyCol,
            DocsHighlight.Escape(key),
            isHeader ? 17f : 18f,
            isHeader ? TextMuted : TextPrimary,
            monospace: !isHeader,
            medium: isHeader,
            lineSpacing: 4f
        );

        var valCol = UiStyle.Cell(row.rectTransform, 0f);
        var valLayout = UiStyle.Vertical(valCol.gameObject, 0f);
        valLayout.childForceExpandHeight = false;
        AddWrappedText(
            valCol,
            DocsHighlight.Escape(value),
            isHeader ? 17f : 18f,
            isHeader ? TextMuted : TextBody,
            medium: isHeader,
            lineSpacing: 5f
        );
    }

    private static void RenderBadges(RectTransform parent, DocBadges badges) {
        var wrap = UiStyle.NewRect("Badges", parent);
        var layout = UiStyle.Vertical(wrap.gameObject, 8f);
        layout.childForceExpandHeight = false;

        if (!string.IsNullOrEmpty(badges.Title)) {
            AddWrappedText(wrap, badges.Title, 18f, TextMuted, medium: true);
        }

        int perRow = badges.Items.Any(s => s.Length > 18) ? 2 : badges.Items.Any(s => s.Length > 10) ? 4 : 5;
        for (int i = 0; i < badges.Items.Length; i += perRow) {
            var row = UiStyle.HRow(wrap, 8f, 42f);
            int end = Math.Min(i + perRow, badges.Items.Length);
            for (int j = i; j < end; j++) {
                string item = badges.Items[j];
                var cell = UiStyle.Cell(row, 0f);
                var pill = SharpRounded("Pill", cell, PillBg, BorderSubtle);
                pill.raycastTarget = true;
                Stretch(pill.rectTransform);
                var tmp = O5Factory.ControlText(Ctx, pill.rectTransform, 16f, true);
                tmp.font = Ctx.Fonts.Monospace;
                tmp.color = TextPrimary;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                TmpCompat.SetNoWrap(tmp);
                tmp.rectTransform.offsetMin = new Vector2(8f, 0f);
                tmp.rectTransform.offsetMax = new Vector2(-8f, 0f);
                tmp.raycastTarget = false;
                tmp.text = DocsHighlight.Escape(item);

                var checkImg = MainCore.Icons.Create(pill.rectTransform, "Check", UIIcon.Check, Color.white, 20f);
                checkImg.rectTransform.anchorMin = checkImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                checkImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                checkImg.rectTransform.sizeDelta = new Vector2(20f, 20f);
                checkImg.gameObject.SetActive(false);

                string captured = item;
                bool isEase = badges.IsEase;
                ITweenHandle flash = null;
                ITweenHandle checkReset = null;
                UnityUtils.AddEvents(pill.gameObject.AddComponent<EventTrigger>(),
                    (EventTriggerType.PointerEnter, _ => {
                        flash?.Kill();
                        flash = pill.TColor(PillHoverBg, 0.12f, O5Ease.OutExpo);
                        if (isEase) {
                            ShowEasePreview(captured, pill.rectTransform);
                        }
                    }),
                    (EventTriggerType.PointerExit, _ => {
                        flash?.Kill();
                        flash = pill.TColor(PillBg, 0.12f, O5Ease.OutExpo);
                        if (isEase) {
                            HideEasePreview(captured);
                        }
                    }),
                    (EventTriggerType.PointerClick, _ => {
                        GUIUtility.systemCopyBuffer = captured;
                        flash?.Kill();
                        pill.color = new Color(1f, 1f, 1f, 0.24f);
                        flash = pill.TColor(PillHoverBg, 0.45f, O5Ease.OutSine);
                        tmp.gameObject.SetActive(false);
                        checkImg.gameObject.SetActive(true);
                        checkReset?.Kill();
                        checkReset = Ctx.Tween.TweenFloat(() => 0f, _ => { }, 1f, 0.85f, () => {
                            if (checkImg != null) {
                                checkImg.gameObject.SetActive(false);
                            }
                            if (tmp != null) {
                                tmp.gameObject.SetActive(true);
                            }
                        });
                    }));
            }
            for (int pad = end - i; pad < perRow; pad++) {
                UiStyle.Cell(row, 0f);
            }
        }
    }

    // Underline-only link with smooth highlight hover (only the link part after prefix is clickable)
    private static void RenderLink(RectTransform parent, DocLink link) {
        var row = UiStyle.NewRect("LinkRow", parent);
        var rowLayout = UiStyle.Horizontal(row.gameObject, 6f, new RectOffset(2, 2, 2, 0));
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        if (!string.IsNullOrEmpty(link.Prefix)) {
            AddWrappedText(row, DocsHighlight.Escape(link.Prefix.TrimEnd()), 19f, TextMuted);
        }

        var tmp = AddWrappedText(row, $"<u>{DocsHighlight.Escape(link.Label)}</u>", 19f, TextMuted);
        tmp.raycastTarget = true;

        Color idleCol = TextMuted;
        Color hoverCol = Color.white;
        ITweenHandle tween = null;

        UnityUtils.AddEvents(tmp.gameObject.AddComponent<EventTrigger>(),
            (EventTriggerType.PointerEnter, _ => {
                tween?.Kill();
                Color start = tmp.color;
                tween = Ctx.Tween.TweenFloat(() => 0f, t => {
                    if (tmp != null) {
                        tmp.color = Color.Lerp(start, hoverCol, t);
                    }
                }, 1f, 0.16f);
            }),
            (EventTriggerType.PointerExit, _ => {
                tween?.Kill();
                Color start = tmp.color;
                tween = Ctx.Tween.TweenFloat(() => 0f, t => {
                    if (tmp != null) {
                        tmp.color = Color.Lerp(start, idleCol, t);
                    }
                }, 1f, 0.16f);
            }),
            (EventTriggerType.PointerClick, _ => {
                try {
                    Application.OpenURL(link.Url);
                } catch {
                }
            }));
    }

    // ------------------------------------------------------------------
    // Live Ease Hover Preview Popup
    // ------------------------------------------------------------------

    private static void ShowEasePreview(string easeName, RectTransform anchor) {
        if (UICore.Panel == null) {
            return;
        }
        easePopup ??= new EasePreviewPopup(UICore.Panel);
        easePopup.Show(easeName, anchor);
    }

    private static void HideEasePreview(string easeName) {
        easePopup?.HideIf(easeName);
    }

    private sealed class EasePreviewPopup {
        private const float PopupW = 224f;
        private const float PopupH = 212f;
        private const float PlotW = 196f;
        private const float PlotH = 100f;
        private const float PreviewBoxW = 94f;
        private const float PreviewBoxH = 54f;
        private const float MoveSeconds = 1.05f;
        private const float HoldSeconds = 0.45f;
        private const int CurveSamples = 48;

        private readonly RectTransform host;
        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly Image cardImg;
        private readonly Image cardBorderImg;
        private readonly Image plotBgImg;
        private readonly Image trackBgImg;
        private readonly Image sizeWrapImg;
        private readonly Image alphaWrapImg;
        private readonly Image sizeFillImg;
        private readonly Image curveDotImg;
        private readonly Image trackDotImg;
        private readonly RectTransform plotRect;
        private readonly List<RectTransform> curveSegments = [];
        private readonly List<Image> curveSegImgs = [];
        private readonly RectTransform curveDot;
        private readonly RectTransform trackRect;
        private readonly RectTransform trackDot;
        private readonly RectTransform sizeBoxRect;
        private readonly Image alphaBoxImg;
        private readonly RectTransform zeroLine;
        private readonly RectTransform oneLine;

        private string activeEase;
        private float clock;
        private float minY;
        private float maxY = 1f;
        private bool visible;

        public EasePreviewPopup(RectTransform panelHost) {
            host = panelHost;
            var card = SharpRounded("DocsEasePreview", panelHost, SurfaceBg, BorderSubtle);
            card.raycastTarget = false;
            cardImg = card;
            root = card.rectTransform;
            cardBorderImg = root.Find("Border")?.GetComponent<Image>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(PopupW, PopupH);

            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // Plot box
            var plotImg = SharpRounded("Plot", root, CodeBg, BorderSubtle);
            plotImg.raycastTarget = false;
            plotBgImg = plotImg;
            plotRect = plotImg.rectTransform;
            plotRect.anchorMin = plotRect.anchorMax = new Vector2(0.5f, 1f);
            plotRect.pivot = new Vector2(0.5f, 1f);
            plotRect.anchoredPosition = new Vector2(0f, -12f);
            plotRect.sizeDelta = new Vector2(PlotW, PlotH);

            zeroLine = MakeHairline(plotRect, "ZeroLine", new Color(1f, 1f, 1f, 0.12f));
            oneLine = MakeHairline(plotRect, "OneLine", new Color(1f, 1f, 1f, 0.12f));

            // Preallocate pooled curve line segments
            for (int i = 0; i < CurveSamples; i++) {
                var seg = UiStyle.NewRect("Seg", plotRect);
                seg.anchorMin = seg.anchorMax = Vector2.zero;
                seg.pivot = new Vector2(0.5f, 0.5f);
                var img = seg.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                curveSegments.Add(seg);
                curveSegImgs.Add(img);
            }

            curveDotImg = NewImage("CurveDot", plotRect, TextPrimary, MainCore.Spr.Get(UISprite.Circle128));
            curveDot = curveDotImg.rectTransform;
            curveDot.anchorMin = curveDot.anchorMax = Vector2.zero;
            curveDot.sizeDelta = new Vector2(8f, 8f);

            // Motion track
            trackBgImg = SharpRounded("Track", root, UIColors.ObjectBG);
            trackBgImg.raycastTarget = false;
            trackRect = trackBgImg.rectTransform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, -120f);
            trackRect.sizeDelta = new Vector2(PlotW, 18f);

            var rail = NewImage("Rail", trackRect, new Color(1f, 1f, 1f, 0.16f));
            rail.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rail.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rail.rectTransform.offsetMin = new Vector2(12f, -1f);
            rail.rectTransform.offsetMax = new Vector2(-12f, 1f);

            trackDotImg = NewImage("TrackDot", trackRect, TextPrimary, MainCore.Spr.Get(UISprite.Circle128));
            trackDot = trackDotImg.rectTransform;
            trackDot.anchorMin = trackDot.anchorMax = new Vector2(0f, 0.5f);
            trackDot.sizeDelta = new Vector2(10f, 10f);

            // Bottom 2-split previews: Left = Size (0..1 of area), Right = Alpha (0..1)
            sizeWrapImg = SharpRounded("SizePreview", root, CodeBg, BorderSubtle);
            sizeWrapImg.raycastTarget = false;
            sizeWrapImg.gameObject.AddComponent<RectMask2D>();
            sizeWrapImg.rectTransform.anchorMin = sizeWrapImg.rectTransform.anchorMax = new Vector2(0f, 0f);
            sizeWrapImg.rectTransform.pivot = new Vector2(0f, 0f);
            sizeWrapImg.rectTransform.anchoredPosition = new Vector2(14f, 12f);
            sizeWrapImg.rectTransform.sizeDelta = new Vector2(PreviewBoxW, PreviewBoxH);

            sizeFillImg = SharpRounded("SizeFill", sizeWrapImg.rectTransform, UIColors.ObjectActiveBright);
            sizeFillImg.raycastTarget = false;
            sizeBoxRect = sizeFillImg.rectTransform;
            sizeBoxRect.anchorMin = sizeBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            sizeBoxRect.pivot = new Vector2(0.5f, 0.5f);
            sizeBoxRect.sizeDelta = Vector2.zero;

            alphaWrapImg = SharpRounded("AlphaPreview", root, CodeBg, BorderSubtle);
            alphaWrapImg.raycastTarget = false;
            alphaWrapImg.rectTransform.anchorMin = alphaWrapImg.rectTransform.anchorMax = new Vector2(1f, 0f);
            alphaWrapImg.rectTransform.pivot = new Vector2(1f, 0f);
            alphaWrapImg.rectTransform.anchoredPosition = new Vector2(-14f, 12f);
            alphaWrapImg.rectTransform.sizeDelta = new Vector2(PreviewBoxW, PreviewBoxH);

            alphaBoxImg = SharpRounded("AlphaFill", alphaWrapImg.rectTransform, UIColors.ObjectActiveBright);
            alphaBoxImg.raycastTarget = false;
            alphaBoxImg.rectTransform.anchorMin = Vector2.zero;
            alphaBoxImg.rectTransform.anchorMax = Vector2.one;
            alphaBoxImg.rectTransform.offsetMin = new Vector2(2f, 2f);
            alphaBoxImg.rectTransform.offsetMax = new Vector2(-2f, -2f);

            ApplyTheme();
            root.gameObject.SetActive(false);
        }

        private void ApplyTheme() {
            if (cardImg != null) {
                cardImg.color = O5Palette.WithAlpha(SurfaceBg, 0.98f);
            }
            if (cardBorderImg != null) {
                cardBorderImg.color = O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.24f);
            }
            if (plotBgImg != null) {
                plotBgImg.color = CodeBg;
            }
            if (sizeWrapImg != null) {
                sizeWrapImg.color = CodeBg;
            }
            if (alphaWrapImg != null) {
                alphaWrapImg.color = CodeBg;
            }
            if (trackBgImg != null) {
                trackBgImg.color = O5Palette.WithAlpha(UIColors.ObjectBG, 0.65f);
            }
            Color segColor = O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.92f);
            for (int i = 0; i < curveSegImgs.Count; i++) {
                if (curveSegImgs[i] != null) {
                    curveSegImgs[i].color = segColor;
                }
            }
            if (curveDotImg != null) {
                curveDotImg.color = TextPrimary;
            }
            if (trackDotImg != null) {
                trackDotImg.color = TextPrimary;
            }
            if (sizeFillImg != null) {
                sizeFillImg.color = O5Palette.WithAlpha(UIColors.ObjectActiveBright, 0.90f);
            }
        }

        private static RectTransform MakeHairline(RectTransform parent, string name, Color color) {
            var img = NewImage(name, parent, color);
            var r = img.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(10f, 0f);
            r.offsetMax = new Vector2(-10f, 1f);
            return r;
        }

        public void Show(string easeName, RectTransform anchor) {
            if (root == null || host == null) {
                return;
            }
            ApplyTheme();
            activeEase = easeName;
            clock = 0f;
            visible = true;
            RebuildCurve(easeName);
            PositionNear(anchor);
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            Tick();
        }

        public void HideIf(string easeName) {
            if (activeEase == easeName) {
                Hide();
            }
        }

        public void Hide() {
            visible = false;
            activeEase = null;
            if (root != null) {
                root.gameObject.SetActive(false);
            }
        }

        public void Dispose() {
            visible = false;
            activeEase = null;
            if (root != null) {
                root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        public void Tick() {
            if (!visible || string.IsNullOrEmpty(activeEase) || root == null) {
                return;
            }
            clock = (clock + Time.unscaledDeltaTime) % (MoveSeconds + HoldSeconds);
            float t = Mathf.Clamp01(clock / MoveSeconds);
            float v = EaseMath.Evaluate(activeEase, t);

            curveDot.anchoredPosition = PlotPoint(t, v);
            float railLeft = 12f;
            float railRight = PlotW - 12f;
            float clampedTrack = Mathf.Lerp(railLeft, railRight, Mathf.Clamp(v, -0.15f, 1.15f));
            trackDot.anchoredPosition = new Vector2(Mathf.Clamp(clampedTrack, 6f, PlotW - 6f), 0f);

            float sizeRatio = Mathf.Max(0f, v);
            sizeBoxRect.sizeDelta = new Vector2((PreviewBoxW - 4f) * sizeRatio, (PreviewBoxH - 4f) * sizeRatio);

            alphaBoxImg.color = O5Palette.WithAlpha(UIColors.ObjectActiveBright, Mathf.Clamp01(v));
        }

        private void RebuildCurve(string easeName) {
            float lo = 0f;
            float hi = 1f;
            var samples = new float[CurveSamples + 1];
            for (int i = 0; i <= CurveSamples; i++) {
                float t = i / (float)CurveSamples;
                float y = EaseMath.Evaluate(easeName, t);
                samples[i] = y;
                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }
            float pad = Mathf.Max(0.08f, (hi - lo) * 0.1f);
            minY = lo - pad;
            maxY = hi + pad;

            float y0 = MapY(0f);
            float y1 = MapY(1f);
            zeroLine.anchoredPosition = new Vector2(0f, y0);
            oneLine.anchoredPosition = new Vector2(0f, y1);

            const float thickness = 2.2f;
            for (int i = 0; i < CurveSamples; i++) {
                Vector2 a = PlotPoint(i / (float)CurveSamples, samples[i]);
                Vector2 b = PlotPoint((i + 1) / (float)CurveSamples, samples[i + 1]);
                Vector2 dir = b - a;
                var seg = curveSegments[i];
                if (dir.sqrMagnitude < 1e-6f) {
                    seg.gameObject.SetActive(false);
                    continue;
                }
                seg.anchoredPosition = (a + b) * 0.5f;
                seg.sizeDelta = new Vector2(dir.magnitude + thickness * 0.7f, thickness);
                seg.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                seg.gameObject.SetActive(true);
            }
        }

        private Vector2 PlotPoint(float t, float y) {
            const float padX = 12f;
            float px = Mathf.Lerp(padX, PlotW - padX, t);
            return new Vector2(px, MapY(y));
        }

        private float MapY(float y) {
            const float padY = 10f;
            float u = Mathf.InverseLerp(minY, maxY, y);
            return Mathf.Lerp(padY, PlotH - padY, u);
        }

        private void PositionNear(RectTransform anchor) {
            if (anchor == null || host == null) {
                return;
            }
            var corners = new Vector3[4];
            O5Kit.Behaviour.UnityUtils.GetWorldCornersSafe(anchor, corners);
            Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;
            Vector3 botCenterWorld = (corners[0] + corners[3]) * 0.5f;

            Vector2 topLocal = host.InverseTransformPoint(topCenterWorld);
            Vector2 botLocal = host.InverseTransformPoint(botCenterWorld);
            Rect bounds = host.rect;

            float halfW = PopupW * 0.5f;
            float x = Mathf.Clamp(topLocal.x, bounds.xMin + halfW + 10f, bounds.xMax - halfW - 10f);

            // Prefer above the badge; flip below if not enough vertical room
            float aboveY = topLocal.y + 8f;
            float y = aboveY + PopupH <= bounds.yMax - 8f
                ? aboveY
                : Mathf.Max(bounds.yMin + 8f, botLocal.y - PopupH - 8f);

            root.anchoredPosition = new Vector2(x, y);
        }
    }

    // ------------------------------------------------------------------
    // Visual / Control Helpers
    // ------------------------------------------------------------------

    private static Image SharpRounded(string name, Transform parent, Color fill, Color? border = null) {
        var img = UiStyle.NewRect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = GetSharpFillSprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = fill;
        img.raycastTarget = true;

        if (border.HasValue) {
            var outline = UiStyle.NewRect("Border", img.rectTransform).gameObject.AddComponent<Image>();
            outline.sprite = GetSharpOutlineSprite();
            outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = 1f;
            outline.color = border.Value;
            outline.raycastTarget = false;
            var le = outline.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Stretch(outline.rectTransform);
        }
        return img;
    }

    private static TControl SharpenControl<TControl>(TControl ctrl) where TControl : O5Object {
        if (ctrl?.Rect == null) {
            return ctrl;
        }
        Sprite p512 = MainCore.Spr.Get(UISliceSprite.Circle128P512);
        Sprite p1024 = MainCore.Spr.Get(UISliceSprite.Circle128P1024);
        Sprite o32p512 = MainCore.Spr.Get(UISliceSprite.CircleOutline128O32P512);
        Sprite o32p1024 = MainCore.Spr.Get(UISliceSprite.CircleOutline128O32P1024);
        Sprite o16p512 = MainCore.Spr.Get(UISliceSprite.CircleOutline128O16P512);
        Sprite sharpFill = GetSharpFillSprite();
        Sprite sharpOutline = GetSharpOutlineSprite();

        foreach (var img in ctrl.Rect.GetComponentsInChildren<Image>(true)) {
            if (img.type != Image.Type.Sliced || img.sprite == null) {
                continue;
            }
            if (img.sprite == o32p512 || img.sprite == o32p1024 || img.sprite == o16p512) {
                img.sprite = sharpOutline;
                img.pixelsPerUnitMultiplier = 1f;
            } else if (img.sprite == p512 || img.sprite == p1024) {
                img.sprite = sharpFill;
                img.pixelsPerUnitMultiplier = 1f;
            }
        }
        return ctrl;
    }

    internal static Sprite GetSharpFillSprite()
        => sharpFillSprite ??= BuildSharpSliceSprite(outline: false);

    internal static Sprite GetSharpOutlineSprite()
        => sharpOutlineSprite ??= BuildSharpSliceSprite(outline: true);

    private static Sprite BuildSharpSliceSprite(bool outline) {
        // 4x supersampled rounded-rect SDF (radius = 24 texels = 6px @ 400 PPU, 8-texel flat center, 4-texel = 1px stroke).
        const int r = 24;
        const int center = 8;
        const int size = (r * 2) + center;
        const float ppu = 400f;
        const float thickness = 4f;
        const float aa = 2f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++) {
            float py = y + 0.5f;
            float qy = Mathf.Max(r - py, py - (size - r), 0f);
            for (int x = 0; x < size; x++) {
                float px = x + 0.5f;
                float qx = Mathf.Max(r - px, px - (size - r), 0f);
                float signedDist = Mathf.Sqrt((qx * qx) + (qy * qy)) - r;
                float outer = Mathf.Clamp01(0.5f - (signedDist / aa));
                float alpha = outline
                    ? outer * Mathf.Clamp01(0.5f + ((signedDist + thickness) / aa))
                    : outer;
                pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            ppu,
            0,
            SpriteMeshType.FullRect,
            new Vector4(r, r, r, r)
        );
    }

    private static O5Button MakeMonoButton(
        Transform parent,
        string id,
        string text,
        Action onClick,
        bool active = false,
        float fontSize = 18f
    ) {
        var button = SharpenControl(O5Factory.Button(Ctx, parent, id, new O5ButtonOptions { Text = text, OnClick = onClick }));
        button.NormalColor = active ? BtnActiveBg : BtnIdleBg;
        button.UpdateVisual(true);
        if (button.Label != null) {
            button.Label.fontSize = fontSize;
            button.Label.font = active ? Ctx.Fonts.Medium : Ctx.Fonts.Regular;
            button.Label.color = active ? Color.white : TextBody;
            button.Label.alignment = TextAlignmentOptions.Center;
        }
        return button;
    }

    private static O5Button MakeIconButton(
        Transform parent,
        string id,
        UIIcon icon,
        Action onClick,
        float padding = 8f
    ) {
        var button = SharpenControl(O5Factory.Button(Ctx, parent, id, new O5ButtonOptions {
            Icon = MainCore.Icons.Get(icon),
            IconPadding = padding,
            OnClick = onClick,
        }));
        button.NormalColor = BtnIdleBg;
        if (button.Icon != null) {
            button.Icon.color = TextBody;
        }
        button.UpdateVisual(true);
        return button;
    }

    private static O5Button AddCopyIconButton(RectTransform slot, string id, Func<string> getText, float padding = 8f) {
        O5Button button = null;
        ITweenHandle resetTimer = null;
        button = MakeIconButton(slot, id, UIIcon.Clone, () => {
            GUIUtility.systemCopyBuffer = getText() ?? string.Empty;
            if (button?.Icon == null) {
                return;
            }
            button.SetIcon(MainCore.Icons.Get(UIIcon.Check));
            resetTimer?.Kill();
            resetTimer = Ctx.Tween.TweenFloat(() => 0f, _ => { }, 1f, 0.85f, () => {
                if (button?.Rect == null || button.Icon == null) {
                    return;
                }
                button.SetIcon(MainCore.Icons.Get(UIIcon.Clone));
            });
        }, padding: padding);
        TrackPage(button);
        return button;
    }

    private static TextMeshProUGUI AddWrappedText(
        Transform parent,
        string text,
        float fontSize,
        Color color,
        bool monospace = false,
        bool medium = false,
        float lineSpacing = 2f
    ) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = monospace ? Ctx.Fonts.Monospace : medium ? Ctx.Fonts.Medium : Ctx.Fonts.Regular;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.lineSpacing = lineSpacing;
        tmp.richText = true;
        tmp.raycastTarget = false;
        TmpCompat.SetWrappingMode(tmp, TmpCompat.Normal);
        tmp.text = text ?? string.Empty;
        return tmp;
    }

    private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null) {
        var rect = UiStyle.NewRect(name, parent);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TControl TrackFrame<TControl>(TControl ctrl) where TControl : O5Object {
        frameControls.Add(ctrl);
        return ctrl;
    }

    private static TControl TrackPage<TControl>(TControl ctrl) where TControl : O5Object {
        pageControls.Add(ctrl);
        return ctrl;
    }

    private static void DisposeFrameOnly() {
        foreach (var ctrl in frameControls) {
            try { ctrl.Dispose(); } catch { }
        }
        frameControls.Clear();
        navEntries.Clear();
    }

    private static void DisposePageOnly() {
        SetPlaygroundFullscreen(false);
        easePopup?.Dispose();
        easePopup = null;
        for (int i = 0; i < explorerPool.Count; i++) {
            var slot = explorerPool[i];
            slot.TagFlash?.Kill();
            slot.TagCheckReset?.Kill();
            try { slot.TryBtn?.Dispose(); } catch { }
        }
        explorerPool.Clear();
        for (int i = 0; i < searchTagSlots.Count; i++) {
            var slot = searchTagSlots[i];
            slot.TagFlash?.Kill();
            slot.TagCheckReset?.Kill();
            try { slot.TryBtn?.Dispose(); } catch { }
        }
        searchTagSlots.Clear();
        explorerFreeSlots.Clear();
        explorerFilteredTags.Clear();
        explorerCardTops = [];
        explorerCardHeights = [];
        lastExplorerFirstIdx = -1;
        lastExplorerLastIdx = -1;
        lastExplorerViewTop = float.NaN;
        lastExplorerViewBottom = float.NaN;
        explorerCountText = null;
        explorerEmptyCallout = null;
        explorerVirtualHost = null;
        explorerVirtualHostLayout = null;
        foreach (var ctrl in pageControls) {
            try { ctrl.Dispose(); } catch { }
        }
        pageControls.Clear();
        sandboxPreviewText = null;
        sandboxDiagBox = null;
        sandboxDiagText = null;
        sandboxDiagDot = null;
        sandboxInput = null;
        playgroundCard = null;
        playgroundEditorLayout = null;
        playgroundPreviewLayout = null;
        playgroundExpandBtn = null;
        sandboxSyncEditor = null;
        sandboxDisplayedDiags = [];
        overviewTagCountText = null;
        overviewCanvasCountText = null;
        overviewModuleCountText = null;
        explorerListRoot = null;
    }

    private static void ClearChildren(Transform parent) {
        if (parent == null) {
            return;
        }
        for (int i = parent.childCount - 1; i >= 0; i--) {
            var go = parent.GetChild(i).gameObject;
            go.SetActive(false);
            UnityEngine.Object.Destroy(go);
        }
    }

    // ------------------------------------------------------------------
    // Code Editor Syntax Coloring & Diagnostic Formatting
    // ------------------------------------------------------------------

    private static int BuildTextGeometryKey(TMP_Text text, ref Vector3 layoutKey) {
        Rect layoutRect = text.rectTransform.rect;
        Vector3 currentLayout = new(layoutRect.width, layoutRect.height, text.canvas ? text.canvas.scaleFactor : 1f);
        if (text.havePropertiesChanged || currentLayout != layoutKey) {
            layoutKey = currentLayout;
            text.ForceMeshUpdate();
        }
        unchecked {
            int hash = 17;
            Rect rect = text.rectTransform.rect;
            hash = (hash * 31) + rect.width.GetHashCode();
            hash = (hash * 31) + rect.height.GetHashCode();
            hash = (hash * 31) + text.textInfo.characterCount;
            for (int i = 0; i < text.textInfo.characterCount; i++) {
                var ch = text.textInfo.characterInfo[i];
                if (!ch.isVisible) {
                    continue;
                }
                hash = (hash * 31) + ch.bottomLeft.GetHashCode();
                hash = (hash * 31) + ch.topRight.GetHashCode();
            }
            return hash;
        }
    }

    private static HighlightProbe ApplySyntaxHighlighting(TMP_Text text, TagSyntaxKind?[] kinds) {
        HighlightProbe probe = HighlightProbe.None;
        Color32 plain = text.color;
        var textInfo = text.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++) {
            var character = textInfo.characterInfo[i];
            if (!character.isVisible) {
                continue;
            }
            Color32 color = character.index >= 0 && character.index < kinds.Length && kinds[character.index].HasValue
                ? SyntaxColor(kinds[character.index].Value)
                : plain;
            int material = character.materialReferenceIndex;
            int vertex = character.vertexIndex;
            var colors = textInfo.meshInfo[material].colors32;
            colors[vertex] = color;
            colors[vertex + 1] = color;
            colors[vertex + 2] = color;
            colors[vertex + 3] = color;
            if (probe.Material < 0 && !SameColor(color, plain)) {
                probe = new HighlightProbe(material, vertex, color);
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        return probe;
    }

    private static bool SameColor(Color32 a, Color32 b)
        => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    private static Color32 SyntaxColor(TagSyntaxKind kind) {
        uint c = TagSyntaxHighlighter.GetColorRgba(kind);
        return new Color32((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }

    private readonly struct HighlightProbe(int material, int vertex, Color32 color) {
        public static readonly HighlightProbe None = new(-1, 0, default);
        public readonly int Material = material;
        private readonly int vertex = vertex;
        private readonly Color32 color = color;

        public bool IsIntact(TMP_Text text) {
            if (Material < 0) {
                return true;
            }
            var meshInfo = text.textInfo.meshInfo;
            if (Material >= meshInfo.Length) {
                return false;
            }
            var colors = meshInfo[Material].colors32;
            return colors != null && vertex < colors.Length && SameColor(colors[vertex], color);
        }
    }

    private static void UpdateLineNumbers(
        TextMeshProUGUI lineNumbers,
        string value,
        CompileDiagnostic[] diagnostics,
        TMP_Text layout
    ) {
        if (lineNumbers == null) {
            return;
        }
        value ??= string.Empty;
        int count = 1;
        foreach (char c in value) {
            if (c == '\n') {
                count++;
            }
        }
        var severities = new CompileSeverity?[count];
        foreach (var d in diagnostics ?? []) {
            int line = GetLine(value, d.Context.Index);
            if (line >= 0 && line < count && (!severities[line].HasValue || d.Severity > severities[line].Value)) {
                severities[line] = d.Severity;
            }
        }

        string Number(int i) => severities[i] switch {
            CompileSeverity.Error => $"<color=#E2676D>{i + 1}</color>",
            CompileSeverity.Warning => $"<color=#FFE591>{i + 1}</color>",
            CompileSeverity.Info => $"<color=#96B7FF>{i + 1}</color>",
            _ => (i + 1).ToString(CultureInfo.InvariantCulture)
        };

        var labels = new List<string>(count);
        int logical = 0;
        if (layout != null) {
            var info = layout.textInfo;
            for (int i = 0; i < info.lineCount && logical < count; i++) {
                int ci = info.lineInfo[i].firstCharacterIndex;
                int start = ci >= 0 && ci < info.characterCount ? info.characterInfo[ci].index : -1;
                bool startsLine = i == 0 || (start > 0 && start <= value.Length && value[start - 1] == '\n');
                labels.Add(startsLine ? Number(logical++) : string.Empty);
            }
        }
        while (logical < count) {
            labels.Add(Number(logical++));
        }
        lineNumbers.text = string.Join("\n", labels);
    }

    private static string FormatDiagnostic(CompileDiagnostic diagnostic) {
        object[] data = diagnostic.Data ?? [];
        string Data(int index, string fallback = "?")
            => index < data.Length && data[index] != null ? FirstLine(data[index].ToString()) : fallback;
        string ArgNum() => data.Length > 0 && data[0] is int idx
            ? (idx + 1).ToString(CultureInfo.InvariantCulture)
            : Data(0);

        string raw = diagnostic.Id switch {
            DiagnosticId.TagNotFound => FormatMissingTag(diagnostic),
            DiagnosticId.ArgConvertFail => $"Argument {ArgNum()} ('{Data(1)}') cannot convert to {Data(2)}",
            DiagnosticId.ArgTooFew => $"Expected at least {Data(0)} arguments; got {Data(1)}",
            DiagnosticId.ArgTooMany => $"Expected at most {Data(0)} arguments; got {Data(1)}",
            DiagnosticId.FormatFail => $"Invalid format '{Data(0)}'",
            DiagnosticId.JsOnlyBlocked => $"Tag '{Data(0)}' is only available in JavaScript",
            DiagnosticId.AdvancedTagException => Data(0, "Advanced tag failed"),
            DiagnosticId.JsError => Data(0, "JavaScript error"),
            DiagnosticId.LegacyJsExprDeprecated => "{JSExpr} is deprecated. Write expressions directly inside braces.",
            DiagnosticId.InternalError => "Internal compiler error",
            _ => diagnostic.Id.ToString(),
        };
        return FirstLine(raw);
    }

    private static string FormatMissingTag(CompileDiagnostic diagnostic) {
        string name = diagnostic.Data?.Length > 0 && diagnostic.Data[0] != null
            ? FirstLine(diagnostic.Data[0].ToString())
            : diagnostic.Context.TagName;
        string suggestion = diagnostic.Data?.Length > 1 && diagnostic.Data[1] != null
            ? FirstLine(diagnostic.Data[1].ToString())
            : null;
        return suggestion == null
            ? $"Tag '{name}' not found"
            : $"Tag '{name}' not found. Did you mean '{suggestion}'?";
    }

    private static string FirstLine(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }
        int nl = text.IndexOfAny(['\r', '\n']);
        string line = nl >= 0 ? text[..nl] : text;
        return line.Trim();
    }

    private static int GetLine(string source, int index) {
        source ??= string.Empty;
        int limit = MathCompat.Clamp(index, 0, source.Length);
        int line = 0;
        for (int i = 0; i < limit; i++) {
            if (source[i] == '\n') {
                line++;
            }
        }
        return line;
    }

    // ------------------------------------------------------------------
    // Search & Tag Metadata Helpers
    // ------------------------------------------------------------------

    private static string NormalizeSearch(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return string.Empty;
        }
        string n = StringUtils.Normalize(raw);
        return DocsContent.IsKorean ? StringUtils.NormalizeToHangulChosung(n) : n;
    }

    private static bool MatchesText(string haystack, string normQuery) {
        if (string.IsNullOrEmpty(haystack)) {
            return false;
        }
        string n = StringUtils.Normalize(haystack);
        if (n.Contains(normQuery)) {
            return true;
        }
        return DocsContent.IsKorean && StringUtils.NormalizeToHangulChosung(n).Contains(normQuery);
    }

    private static bool SectionMatches(DocChapter ch, DocSection sec, string normQuery) {
        if (MatchesText(ch.Title, normQuery) || MatchesText(sec.Title, normQuery) || MatchesText(sec.Summary, normQuery)) {
            return true;
        }
        foreach (var block in sec.Blocks) {
            switch (block) {
                case DocParagraph p when MatchesText(p.Text, normQuery):
                    return true;
                case DocCallout c when MatchesText(c.Title, normQuery) || MatchesText(c.Text, normQuery):
                    return true;
                case DocCode code when MatchesText(code.Caption, normQuery) || MatchesText(code.Code, normQuery):
                    return true;
                case DocTable table when table.Rows.Any(r => MatchesText(r.Key, normQuery) || MatchesText(r.Value, normQuery)):
                    return true;
                case DocBadges b when MatchesText(b.Title, normQuery) || b.Items.Any(i => MatchesText(i, normQuery)):
                    return true;
                case DocLink l when MatchesText(l.Label, normQuery) || MatchesText(l.Url, normQuery):
                    return true;
            }
        }
        return false;
    }

    private static bool MatchesExplorerFilter(TagCore tag, string filter) => filter switch {
        "Core" => !tag.IsJS && tag.Member?.DeclaringType?.Assembly == MainCore.Asm,
        "Module" => !tag.IsJS && tag.Member?.DeclaringType?.Assembly != MainCore.Asm,
        "JS" => tag.IsJS,
        _ => true,
    };

    private static bool MatchesTagQuery(TagCore tag, string normQuery) =>
        MatchesText(tag.Name, normQuery)
        || MatchesText(tag.Description, normQuery)
        || MatchesText(GetTagSourceName(tag), normQuery);

    private static int SourceSortOrder(TagCore tag) {
        if (tag.IsJS) return 2;
        return tag.Member?.DeclaringType?.Assembly == MainCore.Asm ? 0 : 1;
    }

    private static string GetTagSourceName(TagCore tag) {
        if (tag.IsJS) {
            return "JS";
        }
        var asm = tag.Member?.DeclaringType?.Assembly;
        if (asm == null || asm == MainCore.Asm) {
            return "Core";
        }
        string name = asm.GetName().Name ?? "Module";
        const string prefix = "Overlayer.Module.";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
    }

    private static string FormatTagSignature(TagCore tag) {
        if (tag.Parameters == null || tag.Parameters.Length == 0) {
            return string.Empty;
        }
        var parts = new string[tag.Parameters.Length];
        for (int i = 0; i < tag.Parameters.Length; i++) {
            var p = tag.Parameters[i];
            if (p == null) {
                parts[i] = "arg";
                continue;
            }
            string typeName = tag.IsJS ? string.Empty : FormatTypeName(p.ParameterType) + " ";
            string opt = p.HasDefaultValue ? "?" : string.Empty;
            parts[i] = $"{typeName}{p.Name}{opt}";
        }
        return $"({string.Join(", ", parts)})";
    }

    private static string BuildTagCopySnippet(TagCore tag) {
        if (tag.Parameters == null || tag.Parameters.Length == 0 || tag.RequiredParameterCount == 0) {
            return $"{{{tag.Name}}}";
        }
        var args = new string[tag.RequiredParameterCount];
        for (int i = 0; i < tag.RequiredParameterCount; i++) {
            args[i] = tag.Parameters[i]?.Name ?? $"arg{i}";
        }
        return $"{{{tag.Name}({string.Join(", ", args)})}}";
    }

    private static string FormatTypeName(Type type) {
        if (type == null) return "any";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "any";
        return type.Name;
    }

    private static string FormatTagFlags(TagType flags) {
        if (flags == TagType.None) return string.Empty;
        var list = new List<string>(2);
        if ((flags & TagType.ProcessFormat) != 0) list.Add("Fmt");
        if ((flags & TagType.JsOnly) != 0) list.Add("JsOnly");
        return list.Count > 0 ? string.Join("·", list) : string.Empty;
    }
}
