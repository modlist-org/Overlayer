using UnityEngine;

namespace Overlayer.Tutorial;

/// <summary>
/// One step: a short explanation in a bubble, pointing at a piece of the real UI. With
/// <see cref="Done"/> it waits for the user to do the thing (and moves on by itself); without
/// it the bubble has a Next button.
/// </summary>
internal sealed class TutorialStep {
    /// <summary>Lang key; the English text is the fallback.</summary>
    public string Key;
    public string Title;
    public string Text;
    /// <summary>What to point at (null or hidden: the bubble sits at the bottom middle).</summary>
    public Func<RectTransform> Target;
    /// <summary>Done when this turns true; null = Next button.</summary>
    public Func<bool> Done;
    /// <summary>Runs when the step starts (open a page, make an example ...).</summary>
    public Action Enter;
    /// <summary>Skipped (as if done) when this is false, e.g. a step about a game module that isn't there.</summary>
    public Func<bool> When;

    public TutorialStep(string key, string title, string text) {
        Key = key;
        Title = title;
        Text = text;
    }

    public TutorialStep At(Func<RectTransform> target) {
        Target = target;
        return this;
    }

    public TutorialStep Until(Func<bool> done) {
        Done = done;
        return this;
    }

    public TutorialStep OnEnter(Action enter) {
        Enter = enter;
        return this;
    }

    public TutorialStep Only(Func<bool> when) {
        When = when;
        return this;
    }
}

internal sealed class TutorialChapter {
    public string Id;
    public string Title;
    public string Summary;
    public List<TutorialStep> Steps = [];

    public TutorialChapter(string id, string title, string summary, params TutorialStep[] steps) {
        Id = id;
        Title = title;
        Summary = summary;
        Steps.AddRange(steps);
    }
}
