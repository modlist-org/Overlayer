#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif
using System;
using System.Reflection;
using UnityEngine;

namespace Overlayer.Compat;

/// <summary>
/// Compatibility shim for <c>TMPro.TextWrappingModes</c> / <c>TMP_Text.textWrappingMode</c>,
/// which do not exist on old TextMeshPro (Unity 2019 era, e.g. Superliminal).
/// Old TMP only has the <c>enableWordWrapping</c> boolean.
/// All new-TMP access goes through reflection with <see langword="int"/> mode values so that
/// no method JIT-compiled on an old runtime ever references the missing enum type
/// (any static typeref would throw <see cref="TypeLoadException"/> at JIT time).
/// Mode values match the modern enum: NoWrap=0, Normal=1, PreserveWhitespace=2,
/// PreserveWhitespaceNoWrap=3.
/// </summary>
public static class TmpCompat {
    public const int NoWrap = 0;
    public const int Normal = 1;
    public const int PreserveWhitespace = 2;
    public const int PreserveWhitespaceNoWrap = 3;

    private static readonly string[] Names = [
        "NoWrap",
        "Normal",
        "PreserveWhitespace",
        "PreserveWhitespaceNoWrap"
    ];

#if !IL2CPP
    private static readonly PropertyInfo WrappingProp =
        typeof(TMP_Text).GetProperty("textWrappingMode");

    private static readonly Type WrappingType = WrappingProp?.PropertyType;

    private static readonly PropertyInfo WordWrapProp =
        typeof(TMP_Text).GetProperty("enableWordWrapping");
#endif

    /// <summary>True when the runtime TMP has <c>textWrappingMode</c> (modern TMP).</summary>
    public static bool HasWrappingMode {
        get {
#if IL2CPP
            return true;
#else
            return WrappingProp != null;
#endif
        }
    }

    public static void SetNoWrap(TMP_Text tmp) => SetWrappingMode(tmp, NoWrap);

    public static void SetWrappingMode(TMP_Text tmp, int mode) {
        if(tmp == null) {
            return;
        }
#if IL2CPP
        tmp.textWrappingMode = (TextWrappingModes)mode;
#else
        if(WrappingProp != null && WrappingType != null) {
            try {
                WrappingProp.SetValue(tmp, Enum.ToObject(WrappingType, mode), null);
                return;
            } catch {
            }
        }
        try {
            WordWrapProp?.SetValue(tmp, mode != NoWrap && mode != PreserveWhitespaceNoWrap, null);
        } catch {
        }
#endif
    }

    public static int GetWrappingMode(TMP_Text tmp) {
        if(tmp == null) {
            return Normal;
        }
#if IL2CPP
        return (int)tmp.textWrappingMode;
#else
        if(WrappingProp != null) {
            try {
                return Convert.ToInt32(WrappingProp.GetValue(tmp, null));
            } catch {
            }
        }
        try {
            object raw = WordWrapProp?.GetValue(tmp, null);
            if(raw is bool wordWrap) {
                return wordWrap ? Normal : NoWrap;
            }
        } catch {
        }
        return Normal;
#endif
    }

    public static int FromName(string name, int fallback) {
        if(!string.IsNullOrEmpty(name)) {
            for(int i = 0; i < Names.Length; i++) {
                if(string.Equals(name, Names[i], StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }
            if(int.TryParse(name, out int numeric)) {
                return Math.Min(Math.Max(numeric, NoWrap), PreserveWhitespaceNoWrap);
            }
        }
        return fallback;
    }

    public static string ToName(int mode) {
        if(mode < NoWrap || mode > PreserveWhitespaceNoWrap) {
            mode = Normal;
        }
        return Names[mode];
    }
}
