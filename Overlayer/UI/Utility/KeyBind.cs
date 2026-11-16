using O5Kit.Input;
using UnityEngine;

namespace Overlayer.UI.Utility;

// A key with optional Ctrl / Shift / Alt, stored as text like "Alt+H" or "Ctrl+Shift+E".
internal readonly struct KeyBind {
    [Flags]
    public enum Mods {
        None = 0,
        Ctrl = 1,
        Shift = 2,
        Alt = 4,
    }

    public readonly KeyCode Key;
    public readonly Mods Modifiers;

    public KeyBind(KeyCode key, Mods modifiers) {
        Key = key;
        Modifiers = modifiers;
    }

    public bool IsNone => Key == KeyCode.None;

    public static KeyBind Parse(string text) {
        var mods = Mods.None;
        var key = KeyCode.None;
        foreach (string raw in (text ?? string.Empty).Split('+')) {
            string part = raw.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) {
                mods |= Mods.Ctrl;
            } else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) {
                mods |= Mods.Shift;
            } else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) {
                mods |= Mods.Alt;
            } else if (Enum.TryParse(part, true, out KeyCode k)) {
                key = k;
            }
        }
        return new KeyBind(key, mods);
    }

    public override string ToString() {
        if (IsNone) {
            return "None";
        }
        var parts = new List<string>();
        if ((Modifiers & Mods.Ctrl) != 0) {
            parts.Add("Ctrl");
        }
        if ((Modifiers & Mods.Shift) != 0) {
            parts.Add("Shift");
        }
        if ((Modifiers & Mods.Alt) != 0) {
            parts.Add("Alt");
        }
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static bool IsModifierKey(KeyCode k)
        => k is KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftShift or KeyCode.RightShift
            or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftCommand or KeyCode.RightCommand
            or KeyCode.LeftWindows or KeyCode.RightWindows or KeyCode.AltGr;

    /// <summary>Modifiers held right now.</summary>
    public static Mods HeldModifiers() {
        var mods = Mods.None;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
            mods |= Mods.Ctrl;
        }
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
            mods |= Mods.Shift;
        }
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) {
            mods |= Mods.Alt;
        }
        return mods;
    }

    /// <summary>
    /// Registers the shortcut once per left/right modifier combination (O5 combos name exact keys),
    /// all under <paramref name="id"/>. <see cref="Unregister"/> removes every variant.
    /// </summary>
    public void Register(string id, Action onPressed) {
        Unregister(id);
        if (IsNone) {
            return;
        }
        var choices = new List<KeyCode[]>();
        if ((Modifiers & Mods.Ctrl) != 0) {
            choices.Add([KeyCode.LeftControl, KeyCode.RightControl]);
        }
        if ((Modifiers & Mods.Shift) != 0) {
            choices.Add([KeyCode.LeftShift, KeyCode.RightShift]);
        }
        if ((Modifiers & Mods.Alt) != 0) {
            choices.Add([KeyCode.LeftAlt, KeyCode.RightAlt]);
        }
        int variants = 1 << choices.Count;
        for (int v = 0; v < variants; v++) {
            var mods = new KeyCode[choices.Count];
            for (int i = 0; i < choices.Count; i++) {
                mods[i] = choices[i][(v >> i) & 1];
            }
            O5ShortcutManager.Register($"{id}#{v}", new O5KeyCombo(Key, mods), onPressed: _ => onPressed());
        }
    }

    public static void Unregister(string id) {
        for (int v = 0; v < 8; v++) {
            O5ShortcutManager.Unregister($"{id}#{v}");
        }
    }
}
