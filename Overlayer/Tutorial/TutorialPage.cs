using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Tutorial;

// The Tutorial page: every chapter with what it covers, which ones are done, and a button to
// start each (or carry on where you left off).
internal static class TutorialPage {
    private static RectTransform list;
    private static TextMeshProUGUI intro;
    private static O5Kit.Control.O5Button continueButton, resetButton, hideButton;
    internal static O5Kit.Behaviour.UIScrollController Scroller { get; private set; }
    private static readonly List<O5Kit.Core.O5Object> controls = [];

    public static void Create(RectTransform page) {
        var ctx = O5KitAdapters.Ctx;
        var (_, content, scroller) = O5Factory.ScrollView(ctx, page, 12f, 18f);
        Scroller = scroller;

        intro = O5Factory.ControlText(ctx, O5Factory.Row(ctx, content, 70f), 16f);
        intro.enableWordWrapping = true;
        intro.color = UiStyle.Muted;

        var top = UiStyle.HRow(content, 8f, 44f);
        continueButton = UiStyle.Button(UiStyle.Cell(top, 220f), "tutorial_continue", string.Empty, Tutorial.Continue, primary: true);
        resetButton = UiStyle.Button(UiStyle.Cell(top, 200f), "tutorial_reset", string.Empty, () => {
            Core.MainCore.Conf.TutorialDone.Value = [];
            Core.MainCore.ConfMgr.RequestSave();
            Refresh();
        });
        var spacer = UiStyle.NewRect("Spacer", top);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        hideButton = UiStyle.Button(UiStyle.Cell(top, 96f), "tutorial_hide", string.Empty, null);
        HoldToConfirm.Attach(hideButton, () => {
            Core.MainCore.Conf.HideTutorial.Value = true;
            Core.MainCore.ConfMgr.RequestSave();
            UI.Factory.MenuFactory.SetItemVisible((int)UI.OriginalMenuState.Tutorial, false);
            UI.Factory.Page.PageSettings.SyncValues();
        });
        UiStyle.Tooltip(hideButton, () => Tutorial.Tr("TUTORIAL_HIDE_TIP", "Hold to hide the Tutorial tab. You can re-enable it in Settings."));
        controls.Add(continueButton);
        controls.Add(resetButton);
        controls.Add(hideButton);

        list = UiStyle.NewRect("Chapters", content);
        var layout = UiStyle.Vertical(list.gameObject, 10f);
        layout.childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (!hooked) {
            hooked = true;
            Tutorial.Changed += Refresh;
            // Texts follow a language change or reload immediately.
            Core.MainCore.Tr.OnLanguageChanged += _ => Refresh();
            Core.MainCore.Tr.OnLoadEnd += _ => Refresh();
        }
        Refresh();
    }

    private static bool hooked;
    private static readonly List<O5Kit.Core.O5Object> rowControls = [];

    private static void Refresh() {
        if (list == null) {
            return;
        }
        intro.text = Tutorial.Tr("TUTORIAL_PAGE_INTRO",
            "Follow along directly in the real UI. Click Start / continue to go through unfinished chapters in order, or pick any chapter individually.");
        continueButton.Text = Tutorial.Tr("TUTORIAL_CONTINUE", "Start / continue");
        continueButton.SetBlocked(Tutorial.Chapters.All(Tutorial.IsDone));
        resetButton.Text = Tutorial.Tr("TUTORIAL_RESET", "Mark all as not done");
        hideButton.Text = Tutorial.Tr("TUTORIAL_HIDE", "Hide");
        foreach (var control in rowControls) {
            control.Dispose();
        }
        rowControls.Clear();
        for (int i = list.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
        }
        var ctx = O5KitAdapters.Ctx;
        int n = 0;
        foreach (var chapter in Tutorial.Chapters) {
            n++;
            bool done = Tutorial.IsDone(chapter);
            var card = UiStyle.Rounded("Chapter_" + chapter.Id, list, new Color(1f, 1f, 1f, done ? 0.03f : 0.06f)).rectTransform;
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;

            var textCol = UiStyle.NewRect("Text", card);
            textCol.anchorMin = Vector2.zero;
            textCol.anchorMax = Vector2.one;
            textCol.offsetMin = new Vector2(20f, 12f);
            textCol.offsetMax = new Vector2(-150f, -12f);
            var colLayout = UiStyle.Vertical(textCol.gameObject, 4f);
            colLayout.childAlignment = TextAnchor.MiddleLeft;

            string mark = done ? "<color=#7CFF8A>✓</color>  " : $"<alpha=#66>{n}.<alpha=#FF>  ";
            var title = O5Factory.ControlText(ctx, UiStyle.NewRect("Title", textCol), 17f, true);
            title.font = ctx.Fonts.Medium;
            title.characterSpacing = -3f;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableWordWrapping = true;
            title.raycastTarget = false;
            // Chapter titles stay in English by design.
            title.text = $"{mark}{chapter.Title}";
            title.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

            var summary = O5Factory.ControlText(ctx, UiStyle.NewRect("Summary", textCol), 14f, true);
            summary.characterSpacing = -3f;
            summary.color = new Color(1f, 1f, 1f, 0.58f);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            summary.enableWordWrapping = true;
            summary.raycastTarget = false;
            summary.text = Tutorial.Tr("TUTORIAL_CH_" + chapter.Id + "_SUM", chapter.Summary);
            summary.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            var cell = UiStyle.NewRect("Start", card);
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(1f, 0.5f);
            cell.anchoredPosition = new Vector2(-16f, 0f);
            cell.sizeDelta = new Vector2(118f, 38f);
            var captured = chapter;
            rowControls.Add(UiStyle.Button(cell, "tutorial_start_" + chapter.Id,
                done ? Tutorial.Tr("TUTORIAL_AGAIN", "Again") : Tutorial.Tr("TUTORIAL_START", "Start"),
                () => Tutorial.Start(captured), primary: !done));
        }
    }
}
