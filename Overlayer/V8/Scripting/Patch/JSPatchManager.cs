using HarmonyLib;
using Microsoft.ClearScript;
using Overlayer.Core;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Overlayer.V8.Scripting.Patch;

public static class JSPatchManager {
    private static readonly HarmonyLib.Harmony Harmony = new("Overlayer.JSPatch");
    private static readonly object Sync = new();
    private static int nextHandle = 1;
    private static readonly Dictionary<int, Registration> ByHandle = new();
    private static readonly Dictionary<string, List<int>> ByFile = new(StringComparer.Ordinal);
    private static readonly Dictionary<MethodBase, ForwardedPatch> Forwarded = new();
    private static readonly ConcurrentDictionary<MethodBase, ParameterInfo[]> ParamCache = new();
    private static readonly HashSet<string> WarnedOnce = new();

    public sealed class Registration {
        public int Handle;
        public string File;
        public MethodBase Target;
        public ScriptObject Prefix;
        public ScriptObject Postfix;
        public string PrefixSource;
        public string PostfixSource;
        public int PrefixArity;
        public int PostfixArity;
        public bool PrefixRest;
        public bool PostfixRest;
        public int PrefixInstance = -1;
        public int PostfixInstance = -1;
        public int Depth;
    }

    private sealed class ForwardedPatch {
        public MethodBase Target;
        public MethodInfo PrefixMethod;
        public MethodInfo PostfixMethod;
        public readonly List<int> Handles = [];
        public Registration[] Snapshot = [];
    }

    public static int Add(string file, MethodBase target, ScriptObject prefix, ScriptObject postfix,
        string prefixSource, string postfixSource, int prefixArity, int postfixArity,
        bool prefixRest, bool postfixRest, int prefixInstance = -1, int postfixInstance = -1) {
        if (prefix == null && postfix == null) {
            throw new InvalidOperationException("AddPatch needs at least a prefix or a postfix function.");
        }
        bool isVoid = (target as MethodInfo)?.ReturnType == typeof(void);
        bool isStatic = target.IsStatic;
        lock (Sync) {
            if (!Forwarded.TryGetValue(target, out var fwd)) {
                fwd = new ForwardedPatch { Target = target };
                try {
                    if (prefix != null) {
                        fwd.PrefixMethod = PrefixMethodFor(target, isVoid, isStatic);
                        Harmony.Patch(target, prefix: new HarmonyMethod(fwd.PrefixMethod));
                    }
                    if (postfix != null) {
                        fwd.PostfixMethod = PostfixMethodFor(target, isVoid, isStatic);
                        Harmony.Patch(target, postfix: new HarmonyMethod(fwd.PostfixMethod));
                    }
                } catch {
                    if (fwd.PrefixMethod != null) {
                        try { Harmony.Unpatch(target, fwd.PrefixMethod); } catch { }
                    }
                    throw;
                }
                Forwarded[target] = fwd;
            } else {
                if (prefix != null && fwd.PrefixMethod == null) {
                    fwd.PrefixMethod = PrefixMethodFor(target, isVoid, isStatic);
                    Harmony.Patch(target, prefix: new HarmonyMethod(fwd.PrefixMethod));
                }
                if (postfix != null && fwd.PostfixMethod == null) {
                    fwd.PostfixMethod = PostfixMethodFor(target, isVoid, isStatic);
                    Harmony.Patch(target, postfix: new HarmonyMethod(fwd.PostfixMethod));
                }
            }
            int handle = nextHandle++;
            ByHandle[handle] = new Registration {
                Handle = handle,
                File = file,
                Target = target,
                Prefix = prefix,
                Postfix = postfix,
                PrefixSource = prefixSource,
                PostfixSource = postfixSource,
                PrefixArity = prefixArity,
                PostfixArity = postfixArity,
                PrefixRest = prefixRest,
                PostfixRest = postfixRest,
                PrefixInstance = prefixInstance,
                PostfixInstance = postfixInstance,
            };
            fwd.Handles.Add(handle);
            RefreshSnapshot(fwd);
            if (!ByFile.TryGetValue(file, out var list)) {
                list = [];
                ByFile[file] = list;
            }
            list.Add(handle);
            return handle;
        }
    }

    public static IReadOnlyList<(int handle, string target)> GetFilePatches(string file) {
        lock (Sync) {
            if (!ByFile.TryGetValue(file, out var list)) {
                return [];
            }
            var result = new List<(int, string)>(list.Count);
            foreach (int handle in list) {
                if (ByHandle.TryGetValue(handle, out var reg)) {
                    var m = reg.Target;
                    result.Add((handle,
                        $"{m.DeclaringType?.Name}::{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"));
                }
            }
            return result;
        }
    }

    public static bool Remove(int handle) {
        lock (Sync) {
            if (!ByHandle.TryGetValue(handle, out var reg)) {
                return false;
            }
            ByHandle.Remove(handle);
            if (ByFile.TryGetValue(reg.File, out var list)) {
                list.Remove(handle);
                if (list.Count == 0) {
                    ByFile.Remove(reg.File);
                }
            }
            if (Forwarded.TryGetValue(reg.Target, out var fwd)) {
                fwd.Handles.Remove(handle);
                if (fwd.Handles.Count == 0) {
                    try { Harmony.Unpatch(reg.Target, HarmonyPatchType.All, Harmony.Id); } catch { }
                    Forwarded.Remove(reg.Target);
                } else {
                    RefreshSnapshot(fwd);
                }
            }
            return true;
        }
    }

    public static void RemoveFile(string file) {
        int[] handles;
        lock (Sync) {
            if (!ByFile.TryGetValue(file, out var list)) {
                return;
            }
            handles = [.. list];
        }
        foreach (int handle in handles) {
            Remove(handle);
        }
        lock (Sync) {
            WarnedOnce.RemoveWhere(k => k.StartsWith(file + "|", StringComparison.Ordinal));
        }
    }

    public static void RemoveAll() {
        int[] handles;
        lock (Sync) {
            handles = [.. ByHandle.Keys];
            WarnedOnce.Clear();
        }
        foreach (int handle in handles) {
            Remove(handle);
        }
    }

    private static MethodInfo PrefixMethodFor(MethodBase target, bool isVoid, bool isStatic) {
        try {
            if (isVoid) {
                return typeof(JSPatchManager).GetMethod(
                    isStatic ? nameof(PrefixVoidStaticCore) : nameof(PrefixVoidCore),
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            Type ret = ((MethodInfo)target).ReturnType;
            string name = isStatic ? nameof(PrefixStaticCore) : nameof(PrefixCore);
            return typeof(JSPatchManager).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(ret);
        } catch (Exception e) {
            throw new InvalidOperationException(
                "JS patching cannot cover this signature on the current runtime (IL2CPP/AOT cannot generate it).", e);
        }
    }

    private static MethodInfo PostfixMethodFor(MethodBase target, bool isVoid, bool isStatic) {
        try {
            if (isVoid) {
                return typeof(JSPatchManager).GetMethod(
                    isStatic ? nameof(PostfixVoidStaticCore) : nameof(PostfixVoidCore),
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            Type ret = ((MethodInfo)target).ReturnType;
            string name = isStatic ? nameof(PostfixStaticCore) : nameof(PostfixCore);
            return typeof(JSPatchManager).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(ret);
        } catch (Exception e) {
            throw new InvalidOperationException(
                "JS patching cannot cover this signature on the current runtime (IL2CPP/AOT cannot generate it).", e);
        }
    }

    private static void RefreshSnapshot(ForwardedPatch fwd) {
        var snapshot = new Registration[fwd.Handles.Count];
        for (int i = 0; i < fwd.Handles.Count; i++) {
            snapshot[i] = ByHandle[fwd.Handles[i]];
        }
        fwd.Snapshot = snapshot;
    }

    private static Registration[] ForTarget(MethodBase target) {
        lock (Sync) {
            if (!Forwarded.TryGetValue(target, out var fwd) || fwd.Snapshot.Length == 0) {
                return null;
            }
            return fwd.Snapshot;
        }
    }

    private static void WarnOnce(string key, string message) {
        lock (Sync) {
            if (!WarnedOnce.Add(key)) {
                return;
            }
        }
        MainCore.Log.Wrn($"[JSPatch] {message}");
    }

    private static object InvokeJs(Registration reg, ScriptObject fn, object[] callArgs, string phase) {
        if (reg.Depth > 0) {
            return null;
        }
        reg.Depth++;
        try {
            return MainCore.V8.InvokeCallback(fn, callArgs);
        } catch (ObjectDisposedException) {
            return null;
        } catch (Exception e) {
            WarnOnce($"{reg.File}|{reg.Handle}|{phase}|invoke", $"{reg.File}: {phase} threw, continuing original ({e.GetType().Name}: {e.Message})");
            return null;
        } finally {
            reg.Depth--;
        }
    }

    private static object[] PrefixCallArgs(Registration reg, object[] args, object instance) {
        int eff = reg.PrefixInstance >= 0 ? Math.Max(0, reg.PrefixArity - 1) : reg.PrefixArity;
        object[] baseArgs;
        if (reg.PrefixRest || eff > 1) {
            int n = reg.PrefixRest ? args.Length : Math.Min(eff, args.Length);
            baseArgs = new object[n];
            Array.Copy(args, baseArgs, n);
        } else if (eff == 0) {
            baseArgs = [];
        } else {
            baseArgs = [args];
        }
        if (reg.PrefixInstance < 0) {
            return baseArgs;
        }
        return InsertAt(baseArgs, Math.Min(reg.PrefixInstance, baseArgs.Length), instance);
    }

    private static object[] PostfixCallArgs(Registration reg, object[] args, object result, object instance) {
        int eff = reg.PostfixInstance >= 0 ? Math.Max(0, reg.PostfixArity - 1) : reg.PostfixArity;
        object[] baseArgs;
        if (reg.PostfixRest || eff > 2) {
            int n = (reg.PostfixRest || reg.PrefixRest) ? args.Length : Math.Min(eff - 1, args.Length);
            baseArgs = new object[n + 1];
            Array.Copy(args, baseArgs, n);
            baseArgs[n] = result;
        } else if (eff == 0) {
            baseArgs = [];
        } else {
            baseArgs = [args, result];
        }
        if (reg.PostfixInstance < 0) {
            return baseArgs;
        }
        return InsertAt(baseArgs, Math.Min(reg.PostfixInstance, baseArgs.Length), instance);
    }

    private static object[] InsertAt(object[] arr, int index, object value) {
        var r = new object[arr.Length + 1];
        Array.Copy(arr, 0, r, 0, index);
        r[index] = value;
        Array.Copy(arr, index, r, index + 1, arr.Length - index);
        return r;
    }

    private static bool IsSkipWithResult(object ret, out object result) {
        result = null;
        if (ret is ScriptObject obj) {
            var prop = obj.GetProperty("result");
            if (prop != null && prop != Undefined.Value) {
                result = prop;
                return true;
            }
        }
        return false;
    }

    // argIndex >= 0 names the warning "arg{i}" lazily, so the per-call path builds no strings.
    private static object Coerce(object value, object original, Type t, Registration reg, string where, int argIndex = -1) {
        if (value == null || value == Undefined.Value) {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null) {
                return original;
            }
            return null;
        }
        if (t.IsInstanceOfType(value)) {
            return value;
        }
        try {
            if (t.IsEnum) {
                if (value is string s) {
                    return Enum.Parse(t, s, true);
                }
                return Enum.ToObject(t, Convert.ChangeType(value, Enum.GetUnderlyingType(t), CultureInfo.InvariantCulture));
            }
            return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
        } catch {
            where ??= $"arg{argIndex}";
            WarnOnce($"{reg.File}|{reg.Handle}|{where}", $"{reg.File}: cannot convert {value.GetType().Name} to {t.Name}, keeping original");
            return original;
        }
    }

    private static void CoerceArgs(Registration reg, object[] args, ParameterInfo[] parameters) {
        for (int i = 0; i < args.Length && i < parameters.Length; i++) {
            object original = args[i];
            args[i] = Coerce(args[i], original, parameters[i].ParameterType, reg, null, i);
        }
    }

    private static ParameterInfo[] TargetParameters(MethodBase target)
        => ParamCache.GetOrAdd(target, static t => t.GetParameters());

    private static bool RunPrefixes(MethodBase target, object instance, object[] args, Type ret, out object result) {
        result = null;
        var regs = ForTarget(target);
        if (regs == null) {
            return true;
        }
        var parameters = TargetParameters(target);
        foreach (var reg in regs) {
            if (reg.Prefix == null) {
                continue;
            }
            object ret2 = InvokeJs(reg, reg.Prefix, PrefixCallArgs(reg, args, instance), "prefix");
            CoerceArgs(reg, args, parameters);
            if (ret2 is bool b && !b) {
                return false;
            }
            if (IsSkipWithResult(ret2, out object skipResult)) {
                result = ret == typeof(void) ? null : Coerce(skipResult, null, ret, reg, "result");
                return false;
            }
        }
        return true;
    }

    private static void RunPostfixes(MethodBase target, object instance, object[] args, Type ret, ref object current) {
        var regs = ForTarget(target);
        if (regs == null) {
            return;
        }
        foreach (var reg in regs) {
            if (reg.Postfix == null) {
                continue;
            }
            object ret2 = InvokeJs(reg, reg.Postfix, PostfixCallArgs(reg, args, current, instance), "postfix");
            if (ret2 != null && ret2 != Undefined.Value && ret != typeof(void)) {
                current = Coerce(ret2, current, ret, reg, "result");
            }
        }
    }

    private static bool PrefixVoidCore(object __instance, object[] __args, MethodBase __originalMethod) {
        return RunPrefixes(__originalMethod, __instance, __args, typeof(void), out _);
    }

    private static bool PrefixVoidStaticCore(object[] __args, MethodBase __originalMethod) {
        return RunPrefixes(__originalMethod, null, __args, typeof(void), out _);
    }

    private static bool PrefixCore<T>(object __instance, object[] __args, MethodBase __originalMethod, ref T __result) {
        if (!RunPrefixes(__originalMethod, __instance, __args, typeof(T), out object result)) {
            if (result != null) {
                __result = (T)result;
            }
            return false;
        }
        return true;
    }

    private static bool PrefixStaticCore<T>(object[] __args, MethodBase __originalMethod, ref T __result) {
        if (!RunPrefixes(__originalMethod, null, __args, typeof(T), out object result)) {
            if (result != null) {
                __result = (T)result;
            }
            return false;
        }
        return true;
    }

    private static void PostfixVoidCore(object __instance, object[] __args, MethodBase __originalMethod) {
        object current = null;
        RunPostfixes(__originalMethod, __instance, __args, typeof(void), ref current);
    }

    private static void PostfixVoidStaticCore(object[] __args, MethodBase __originalMethod) {
        object current = null;
        RunPostfixes(__originalMethod, null, __args, typeof(void), ref current);
    }

    private static void PostfixCore<T>(object __instance, object[] __args, MethodBase __originalMethod, ref T __result) {
        object current = __result;
        RunPostfixes(__originalMethod, __instance, __args, typeof(T), ref current);
        if (current != null || !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null) {
            __result = (T)current;
        }
    }

    private static void PostfixStaticCore<T>(object[] __args, MethodBase __originalMethod, ref T __result) {
        object current = __result;
        RunPostfixes(__originalMethod, null, __args, typeof(T), ref current);
        if (current != null || !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null) {
            __result = (T)current;
        }
    }
}
