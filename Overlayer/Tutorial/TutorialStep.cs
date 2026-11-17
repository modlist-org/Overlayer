using Overlayer.Tutorial.Olly;
using UnityEngine;

namespace Overlayer.Tutorial;

/// <summary>
/// One step: Olly says a line (typed out), with an optional written note under it, pointing at
/// a piece of the real UI. With <see cref="Done"/> it waits for the user to do the thing (and
/// moves on by itself); without it the bubble has a Next button. A <see cref="Demo"/> is Olly
/// doing it herself first.
/// </summary>
internal sealed class TutorialStep {
    /// <summary>Lang key; the English text is the fallback.</summary>
    public string Key;
    public string Title;
    /// <summary>What Olly says.</summary>
    public string Text;
    /// <summary>The written explanation under it (lists, code). Optional.</summary>
    public string Note;
    public Face Face = Face.Normal;
    /// <summary>What to point at (null or hidden: the bubble sits at the bottom middle).</summary>
    public Func<RectTransform> Target;
    /// <summary>Done when this turns true; null = Next button.</summary>
    public Func<bool> Done;
    /// <summary>Runs when the step starts (open a page, make an example ...).</summary>
    public Action Enter;
    /// <summary>Runs when leaving the step, either way.</summary>
    public Action Exit;
    /// <summary>Skipped (as if done) when this is false, e.g. a step about a game module that isn't there.</summary>
    public Func<bool> When;
    public TutorialDemo Demo;
    /// <summary>In the course, the step is done for the user with this (getting to a page, say) and skipped.</summary>
    public Action Auto;
    /// <summary>Buttons instead of Next. A choice that doesn't start something else moves on.</summary>
    public List<TutorialChoice> Choices;

    public TutorialStep(string key, string title, string text, string note = null) {
        Key = key;
        Title = title;
        Text = text;
        Note = note;
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

    public TutorialStep OnExit(Action exit) {
        Exit = exit;
        return this;
    }

    public TutorialStep Only(Func<bool> when) {
        When = when;
        return this;
    }

    public TutorialStep Looks(Face face) {
        Face = face;
        return this;
    }

    public TutorialStep AutoInCourse(Action auto) {
        Auto = auto;
        return this;
    }

    public TutorialStep Shows(TutorialDemo demo) {
        Demo = demo;
        return this;
    }

    public TutorialStep Ask(params TutorialChoice[] choices) {
        Choices = [.. choices];
        return this;
    }
}

internal sealed class TutorialChoice(string key, string text, Action pick, bool primary = false) {
    public readonly string Key = key;
    public readonly string Text = text;
    public readonly Action Pick = pick;
    public readonly bool Primary = primary;
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
