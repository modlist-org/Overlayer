using HarmonyLib;
using O5Kit.Input;
using Overlayer.Core;
using Overlayer.Patch.Safe;
using System.Reflection;
using UnityEngine;
using LegacyInputField = UnityEngine.UI.InputField;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Patch;

public sealed class SP_LinuxTMPKeyInput() : SafeConditionalPatch(nameof(SP_LinuxTMPKeyInput)) {
    protected override bool ShouldApply()
        => LinuxTextInputFix.IsSupported && MainCore.Conf.LinuxTextInputFix.Value && TargetMethod != null;

    protected override MethodBase GetTargetMethod()
        => typeof(TMP_InputField).GetMethod("KeyPressed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    protected override HarmonyMethod Prefix()
        => new(typeof(SP_LinuxTMPKeyInput).GetMethod(nameof(PrefixImpl), BindingFlags.Static | BindingFlags.NonPublic));

    private static bool PrefixImpl(Event __0)
        => __0 == null || LinuxTextInputFix.Process(__0);
}

public sealed class SP_LinuxLegacyKeyInput() : SafeConditionalPatch(nameof(SP_LinuxLegacyKeyInput)) {
    protected override bool ShouldApply()
        => LinuxTextInputFix.IsSupported && MainCore.Conf.LinuxTextInputFix.Value && TargetMethod != null;

    protected override MethodBase GetTargetMethod()
        => typeof(LegacyInputField).GetMethod("KeyPressed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    protected override HarmonyMethod Prefix()
        => new(typeof(SP_LinuxLegacyKeyInput).GetMethod(nameof(PrefixImpl), BindingFlags.Static | BindingFlags.NonPublic));

    private static bool PrefixImpl(Event __0)
        => __0 == null || LinuxTextInputFix.Process(__0);
}

internal static class LinuxTextInputFix {
    internal static bool IsSupported => Application.platform == RuntimePlatform.LinuxPlayer;

    private static SP_LinuxTMPKeyInput tmpPatch;
    private static SP_LinuxLegacyKeyInput legacyPatch;

    internal static void Register() {
        if (!IsSupported) {
            return;
        }
        tmpPatch ??= new SP_LinuxTMPKeyInput();
        legacyPatch ??= new SP_LinuxLegacyKeyInput();
        SafePatchController.Add(tmpPatch);
        SafePatchController.Add(legacyPatch);
    }

    internal static void ApplyFromConfig() {
        if (!IsSupported) {
            return;
        }
        bool enable = MainCore.IsModEnabled && MainCore.Conf.LinuxTextInputFix.Value;
        ApplyState(tmpPatch, enable);
        ApplyState(legacyPatch, enable);
    }

    internal static void Dispose() {
        if (tmpPatch != null) {
            SafePatchController.Remove(tmpPatch);
            tmpPatch = null;
        }
        if (legacyPatch != null) {
            SafePatchController.Remove(legacyPatch);
            legacyPatch = null;
        }
        ClearPending();
    }

    private static void ApplyState(SafeConditionalPatch patch, bool enable) {
        if (patch == null) {
            return;
        }
        if (enable) {
            patch.Apply();
        } else {
            patch.Remove();
        }
    }

    private enum PendingKind {
        None,
        Text,
        Physical,
    }

    private static PendingKind pendingKind;
    private static int pendingFrame = -1;
    private const int PendingLifetimeFrames = 1;

    public static bool Process(Event evt) {
        int pendingAge = Time.frameCount - pendingFrame;
        if (pendingKind != PendingKind.None && (pendingAge < 0 || pendingAge > PendingLifetimeFrames)) {
            ClearPending();
        }

        // Never interfere while an IME (kime, fcitx, ibus) is composing.
        if (!string.IsNullOrEmpty(O5Input.CompositionString)) {
            ClearPending();
            return true;
        }

        // Committed non-ASCII Unicode (e.g. Hangul/CJK from XIM): never overwrite or drop.
        if (evt.character >= 0x80) {
            evt.modifiers &= ~EventModifiers.FunctionKey;
            pendingKind = PendingKind.Text;
            pendingFrame = Time.frameCount;
            return true;
        }

        if (evt.keyCode == KeyCode.None && IsAsciiText(evt.character)) {
            if (pendingKind == PendingKind.Physical) {
                ClearPending();
                return false;
            }

            evt.modifiers &= ~EventModifiers.FunctionKey;
            pendingKind = PendingKind.Text;
            pendingFrame = Time.frameCount;
            return true;
        }

        if (evt.keyCode == KeyCode.None || IsModifierKey(evt.keyCode)) {
            return true;
        }

        bool ctrlAltCmd = (evt.modifiers & (EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != 0;
        if (ctrlAltCmd) {
            ClearPending();
            return true;
        }

        bool hasFunctionKey = (evt.modifiers & EventModifiers.FunctionKey) != 0;
        bool shiftHeld = (evt.modifiers & (EventModifiers.Shift | EventModifiers.FunctionKey)) != 0
            || O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

        if (IsAsciiText(evt.character)) {
            if (pendingKind == PendingKind.Text) {
                ClearPending();
                return false;
            }

            if (shiftHeld && TryGetCharacter(evt, shifted: true, out char shiftedChar)) {
                evt.character = shiftedChar;
            }
            evt.modifiers &= ~EventModifiers.FunctionKey;
            pendingKind = PendingKind.Physical;
            pendingFrame = Time.frameCount;
            return true;
        }

        // Physical keydown with '\0': only synthesize ASCII when Shift or the Linux FunctionKey bug
        // stripped the character; leave unshifted '\0' alone so XIM/kime-filtered Korean keys don't leak ASCII.
        if (evt.character == '\0' && (shiftHeld || hasFunctionKey) && TryGetCharacter(evt, shiftHeld, out char value)) {
            if (pendingKind == PendingKind.Text) {
                ClearPending();
                return false;
            }

            evt.character = value;
            evt.modifiers &= ~EventModifiers.FunctionKey;
            pendingKind = PendingKind.Physical;
            pendingFrame = Time.frameCount;
            return true;
        }

        ClearPending();
        return true;
    }

    private static void ClearPending() {
        pendingKind = PendingKind.None;
        pendingFrame = -1;
    }

    private static bool IsAsciiText(char value) => value is >= ' ' and <= '~';

    private static bool IsModifierKey(KeyCode key) => key is
        KeyCode.LeftShift or KeyCode.RightShift or
        KeyCode.LeftControl or KeyCode.RightControl or
        KeyCode.LeftAlt or KeyCode.RightAlt or
        KeyCode.LeftCommand or KeyCode.RightCommand or
        KeyCode.CapsLock;

    private static bool TryGetCharacter(Event evt, bool shifted, out char value) {
        value = '\0';
        bool caps = (evt.modifiers & EventModifiers.CapsLock) != 0;

        if (evt.keyCode >= KeyCode.A && evt.keyCode <= KeyCode.Z) {
            value = (char)('a' + evt.keyCode - KeyCode.A);
            if (shifted ^ caps) {
                value = char.ToUpperInvariant(value);
            }
            return true;
        }

        const string plainNumbers = "0123456789";
        const string shiftedNumbers = ")!@#$%^&*(";
        if (evt.keyCode >= KeyCode.Alpha0 && evt.keyCode <= KeyCode.Alpha9) {
            int index = evt.keyCode - KeyCode.Alpha0;
            value = shifted ? shiftedNumbers[index] : plainNumbers[index];
            return true;
        }

        (char plain, char upper) = evt.keyCode switch {
            KeyCode.Space => (' ', ' '),
            KeyCode.Minus => ('-', '_'),
            KeyCode.Equals => ('=', '+'),
            KeyCode.LeftBracket => ('[', '{'),
            KeyCode.RightBracket => (']', '}'),
            KeyCode.Backslash => ('\\', '|'),
            KeyCode.Semicolon => (';', ':'),
            KeyCode.Quote => ('\'', '"'),
            KeyCode.Comma => (',', '<'),
            KeyCode.Period => ('.', '>'),
            KeyCode.Slash => ('/', '?'),
            KeyCode.BackQuote => ('`', '~'),
            _ => ('\0', '\0')
        };
        value = shifted ? upper : plain;
        return value != '\0';
    }
}
