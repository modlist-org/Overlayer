using Overlayer.Core;
using System;
using System.Collections.Generic;

namespace Overlayer.HudEdit;

// One undoable step. Each command restores everything it touched, so a
// multi-object move or a create/delete undoes as a single Ctrl+Z.
internal interface IHudCommand {
    void Undo();
    void Redo();
}

internal sealed class TransformCommand(List<(HudElement Element, HudElement.State Before, HudElement.State After)> changes) : IHudCommand {
    public void Undo() {
        foreach (var (el, before, _) in changes) {
            if (el.IsAlive) {
                el.Apply(before);
            }
        }
    }

    public void Redo() {
        foreach (var (el, _, after) in changes) {
            if (el.IsAlive) {
                el.Apply(after);
            }
        }
    }
}

internal sealed class ActionCommand(Action undo, Action redo) : IHudCommand {
    public void Undo() => undo();
    public void Redo() => redo();
}

internal sealed class UndoStack {
    private const int Limit = 200;
    private readonly List<IHudCommand> entries = [];
    private int cursor;

    public void Push(IHudCommand command) {
        if (entries.Count > cursor) {
            entries.RemoveRange(cursor, entries.Count - cursor);
        }
        entries.Add(command);
        if (entries.Count > Limit) {
            entries.RemoveAt(0);
        }
        cursor = entries.Count;
    }

    public bool Undo() {
        if (cursor == 0) {
            return false;
        }
        Run(entries[--cursor].Undo);
        return true;
    }

    public bool Redo() {
        if (cursor >= entries.Count) {
            return false;
        }
        Run(entries[cursor++].Redo);
        return true;
    }

    private static void Run(Action action) {
        try {
            action();
        } catch (Exception e) {
            MainCore.Log.Err($"[HudEditor] Undo/redo failed: {e.Message}");
        }
    }

    public void Clear() {
        entries.Clear();
        cursor = 0;
    }
}
