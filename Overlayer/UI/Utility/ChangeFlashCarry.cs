using O5Kit.Core;

namespace Overlayer.UI.Utility;

/// <summary>
/// Keeps change bars across a full rebuild of a property panel: a control that now shows a
/// different value than before the rebuild lights up, and one whose flash was still playing
/// carries on from where it was. Matched by control id.
/// </summary>
internal static class ChangeFlashCarry {
    public readonly struct Snapshot(Dictionary<string, (string Shown, float Elapsed)> entries) {
        public readonly Dictionary<string, (string Shown, float Elapsed)> Entries = entries;
    }

    public static Snapshot Take(IEnumerable<O5Object> controls) {
        var entries = new Dictionary<string, (string, float)>();
        foreach (var control in controls) {
            if (control == null || control.IsDisposed || string.IsNullOrEmpty(control.Id) || entries.ContainsKey(control.Id)) {
                continue;
            }
            string shown;
            try {
                shown = control.ShownValue;
            } catch {
                shown = null;
            }
            float elapsed = control.ChangeFlashElapsed;
            if (shown == null && elapsed < 0f) {
                continue;
            }
            entries[control.Id] = (shown, elapsed);
        }
        return new Snapshot(entries);
    }

    public static void Restore(Snapshot snapshot, IEnumerable<O5Object> controls) {
        if (snapshot.Entries == null || snapshot.Entries.Count == 0) {
            return;
        }
        var seen = new HashSet<string>();
        foreach (var control in controls) {
            if (control == null || control.IsDisposed || string.IsNullOrEmpty(control.Id) || !seen.Add(control.Id)
                || !snapshot.Entries.TryGetValue(control.Id, out var before)) {
                continue;
            }
            string shown;
            try {
                shown = control.ShownValue;
            } catch {
                shown = null;
            }
            if (before.Shown != null && shown != null && before.Shown != shown) {
                control.FlashExternalChange();
            } else if (before.Elapsed >= 0f) {
                control.FlashExternalChange(before.Elapsed);
            }
        }
    }
}
