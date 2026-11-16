using UnityEngine;

namespace Overlayer.HudEdit;

/// <summary>
/// Made-up game values for the HUD preview window, so a text can be seen as it looks while
/// playing (and in menus) without playing. A game module registers one with
/// <see cref="HudSimulators.Register"/>. Only the preview text sees these values; the real HUD
/// keeps the real ones.
/// </summary>
public abstract class HudSimulator {
    /// <summary>Tag names this simulator makes up values for. Other tags show their real value.</summary>
    public abstract IEnumerable<string> Tags { get; }

    /// <summary>
    /// Moves the made-up game to <paramref name="time"/> seconds into the current phase.
    /// <paramref name="playing"/> false: in a menu after a play (or before the first one).
    /// Called every frame before the preview text is drawn. Time restarts at 0 each phase.
    /// </summary>
    public abstract void Step(double time, bool playing);

    /// <summary>The value of a tag right now. <paramref name="args"/> are the call's arguments.</summary>
    public abstract object Get(string tag, object[] args);

    /// <summary>One short line about what's happening ("Combo 12, last: Perfect"), or null.</summary>
    public virtual string Status => null;

    /// <summary>Builds a simple backdrop that looks a bit like the game inside <paramref name="scene"/>.</summary>
    public virtual void BuildScene(RectTransform scene) { }

    /// <summary>Animates the backdrop (called every frame after <see cref="Step"/>).</summary>
    public virtual void UpdateScene(RectTransform scene) { }
}

public static class HudSimulators {
    private static readonly List<HudSimulator> simulators = [];

    public static void Register(HudSimulator simulator) {
        if (simulator != null && !simulators.Contains(simulator)) {
            simulators.Add(simulator);
        }
    }

    public static void Unregister(HudSimulator simulator) => simulators.Remove(simulator);

    /// <summary>The latest registered one (one game module at a time).</summary>
    internal static HudSimulator Current => simulators.Count > 0 ? simulators[simulators.Count - 1] : null;
}
