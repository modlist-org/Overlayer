using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Compat;

/// <summary>
/// Runtime dispatch for <c>UnityEngine.Time</c> double-precision clock properties
/// (<c>timeAsDouble</c>, <c>unscaledTimeAsDouble</c>, <c>realtimeSinceStartupAsDouble</c>,
/// <c>fixedTimeAsDouble</c>, <c>fixedUnscaledTimeAsDouble</c>), which do not exist on
/// old Unity (pre-2020, e.g. Superliminal on 2019.4). Looked up by name so no method
/// references the missing accessors statically; falls back to the float clocks.
/// On IL2CPP (modern games only) the properties are called directly.
/// </summary>
public static class UnityTimeCompat {
#if !IL2CPP
    private static readonly Func<double> TimeGetter =
        Resolve("timeAsDouble", static () => (double)Time.time);

    private static readonly Func<double> UnscaledTimeGetter =
        Resolve("unscaledTimeAsDouble", static () => (double)Time.unscaledTime);

    private static readonly Func<double> RealtimeGetter =
        Resolve("realtimeSinceStartupAsDouble", static () => (double)Time.realtimeSinceStartup);

    private static readonly Func<double> FixedTimeGetter =
        Resolve("fixedTimeAsDouble", static () => (double)Time.fixedTime);

    private static readonly Func<double> FixedUnscaledTimeGetter =
        Resolve("fixedUnscaledTimeAsDouble", static () => (double)Time.fixedUnscaledTime);

    private static Func<double> Resolve(string propertyName, Func<double> fallback) {
        try {
            PropertyInfo prop = typeof(Time).GetProperty(
                propertyName, BindingFlags.Public | BindingFlags.Static);
            MethodInfo getter = prop?.GetGetMethod();
            if (getter == null) {
                return fallback;
            }
            try {
                return (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), getter);
            } catch {
                return () => {
                    try {
                        return Convert.ToDouble(prop.GetValue(null));
                    } catch {
                        return fallback();
                    }
                };
            }
        } catch {
            return fallback;
        }
    }
#endif

    public static double TimeAsDouble {
        get {
#if IL2CPP
            return Time.timeAsDouble;
#else
            return TimeGetter();
#endif
        }
    }

    public static double UnscaledTimeAsDouble {
        get {
#if IL2CPP
            return Time.unscaledTimeAsDouble;
#else
            return UnscaledTimeGetter();
#endif
        }
    }

    public static double RealtimeSinceStartupAsDouble {
        get {
#if IL2CPP
            return Time.realtimeSinceStartupAsDouble;
#else
            return RealtimeGetter();
#endif
        }
    }

    public static double FixedTimeAsDouble {
        get {
#if IL2CPP
            return Time.fixedTimeAsDouble;
#else
            return FixedTimeGetter();
#endif
        }
    }

    public static double FixedUnscaledTimeAsDouble {
        get {
#if IL2CPP
            return Time.fixedUnscaledTimeAsDouble;
#else
            return FixedUnscaledTimeGetter();
#endif
        }
    }
}

/// <summary>
/// Compatibility shim for <c>Collider2D.compositeOperation</c>, which does not exist on
/// old Unity Physics2D (e.g. 2019.4). When the property is absent, sets are ignored and
/// reads return <see cref="CompositeNone"/> (0). Values match the modern enum:
/// None=0, Merge=1, Intersect=2, Difference=3, Flip=4.
/// </summary>
public static class ColliderCompat {
    public const int CompositeNone = 0;
    public const int CompositeMerge = 1;
    public const int CompositeIntersect = 2;
    public const int CompositeDifference = 3;
    public const int CompositeFlip = 4;

    private static readonly string[] CompositeNames = [
        "None",
        "Merge",
        "Intersect",
        "Difference",
        "Flip"
    ];

#if !IL2CPP
    private static readonly PropertyInfo CompositeProp =
        typeof(Collider2D).GetProperty("compositeOperation");
#endif

    public static bool HasCompositeOperation {
        get {
#if IL2CPP
            return true;
#else
            return CompositeProp != null;
#endif
        }
    }

    public static void SetCompositeOperation(Collider2D collider, int mode) {
        if (collider == null) {
            return;
        }
#if IL2CPP
        collider.compositeOperation = (Collider2D.CompositeOperation)mode;
#else
        if (CompositeProp == null) {
            return;
        }
        try {
            CompositeProp.SetValue(collider, Enum.ToObject(CompositeProp.PropertyType, mode), null);
        } catch {
        }
#endif
    }

    public static int GetCompositeOperation(Collider2D collider) {
        if (collider == null) {
            return CompositeNone;
        }
#if IL2CPP
        return (int)collider.compositeOperation;
#else
        if (CompositeProp == null) {
            return CompositeNone;
        }
        try {
            return Convert.ToInt32(CompositeProp.GetValue(collider, null));
        } catch {
            return CompositeNone;
        }
#endif
    }

    public static int CompositeFromName(string name, int fallback) {
        if (!string.IsNullOrEmpty(name)) {
            for (int i = 0; i < CompositeNames.Length; i++) {
                if (string.Equals(name, CompositeNames[i], StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }
            if (int.TryParse(name, out int numeric)) {
                return Math.Min(Math.Max(numeric, CompositeNone), CompositeFlip);
            }
        }
        return fallback;
    }

    public static string CompositeToName(int mode) {
        if (mode < CompositeNone || mode > CompositeFlip) {
            mode = CompositeNone;
        }
        return CompositeNames[mode];
    }
}

/// <summary>
/// Compatibility shim for <c>HorizontalOrVerticalLayoutGroup.reverseArrangement</c>,
/// which does not exist on old Unity UI (pre-2020.2, e.g. Superliminal on 2019.4).
/// </summary>
public static class LayoutGroupCompat {
#if !IL2CPP
    private static readonly PropertyInfo ReverseArrangementProp =
        typeof(HorizontalOrVerticalLayoutGroup).GetProperty("reverseArrangement", BindingFlags.Public | BindingFlags.Instance);
#endif

    public static bool GetReverseArrangement(HorizontalOrVerticalLayoutGroup group) {
        if (group == null) {
            return false;
        }
#if IL2CPP
        return group.reverseArrangement;
#else
        if (ReverseArrangementProp == null) {
            return false;
        }
        try {
            return (bool)ReverseArrangementProp.GetValue(group, null);
        } catch {
            return false;
        }
#endif
    }

    public static void SetReverseArrangement(HorizontalOrVerticalLayoutGroup group, bool value) {
        if (group == null) {
            return;
        }
#if IL2CPP
        group.reverseArrangement = value;
#else
        if (ReverseArrangementProp == null) {
            return;
        }
        try {
            ReverseArrangementProp.SetValue(group, value, null);
        } catch {
        }
#endif
    }
}
