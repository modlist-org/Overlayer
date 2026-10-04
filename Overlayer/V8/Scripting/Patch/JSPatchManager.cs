using HarmonyLib;
using Microsoft.ClearScript;
using Overlayer.Core;
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
        public int Depth;
    }

    private sealed class ForwardedPatch {
        public MethodBase Target;
        public MethodInfo PrefixMethod;
        public MethodInfo PostfixMethod;
        public readonly List<int> Handles = [];
    }

    public static int Add(string file, MethodBase target, ScriptObject prefix, ScriptObject postfix,
        string prefixSource, string postfixSource, int prefixArity, int postfixArity,
        bool prefixRest, bool postfixRest) {
        if(prefix == null && postfix == null) {
            throw new InvalidOperationException("AddPatch needs at least a prefix or a postfix function.");
        }
        bool isVoid = (target as MethodInfo)?.ReturnType == typeof(void);
        bool isStatic = target.IsStatic;
        lock(Sync) {
            if(!Forwarded.TryGetValue(target, out var fwd)) {
                fwd = new ForwardedPatch { Target = target };
                try {
                    if(prefix != null) {
                        fwd.PrefixMethod = PrefixMethodFor(target, isVoid, isStatic);
                        Harmony.Patch(target, prefix: new HarmonyMethod(fwd.PrefixMethod));
                    }
                    if(postfix != null) {
                        fwd.PostfixMethod = PostfixMethodFor(target, isVoid, isStatic);
                        Harmony.Patch(target, postfix: new HarmonyMethod(fwd.PostfixMethod));
                    }
                } catch {
                    if(fwd.PrefixMethod != null) {
                        try { Harmony.Unpatch(target, fwd.PrefixMethod); } catch { }
                    }
                    throw;
                }
                Forwarded[target] = fwd;
            } else {
                if(prefix != null && fwd.PrefixMethod == null) {
                    fwd.PrefixMethod = PrefixMethodFor(target, isVoid, isStatic);
                    Harmony.Patch(target, prefix: new HarmonyMethod(fwd.PrefixMethod));
                }
                if(postfix != null && fwd.PostfixMethod == null) {
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
            };
            fwd.Handles.Add(handle);
            if(!ByFile.TryGetValue(file, out var list)) {
                list = [];
                ByFile[file] = list;
            }
            list.Add(handle);
            return handle;
        }
    }

    public static bool Remove(int handle) {
        lock(Sync) {
            if(!ByHandle.TryGetValue(handle, out var reg)) {
                return false;
            }
            ByHandle.Remove(handle);
            if(ByFile.TryGetValue(reg.File, out var list)) {
                list.Remove(handle);
                if(list.Count == 0) {
                    ByFile.Remove(reg.File);
                }
            }
            if(Forwarded.TryGetValue(reg.Target, out var fwd)) {
                fwd.Handles.Remove(handle);
                if(fwd.Handles.Count == 0) {
                    try { Harmony.Unpatch(reg.Target, HarmonyPatchType.All, Harmony.Id); } catch { }
                    Forwarded.Remove(reg.Target);
                }
            }
            return true;
        }
    }

    public static void RemoveFile(string file) {
        int[] handles;
        lock(Sync) {
            if(!ByFile.TryGetValue(file, out var list)) {
                return;
            }
            handles = [.. list];
        }
        foreach(int handle in handles) {
            Remove(handle);
        }
        lock(Sync) {
            WarnedOnce.RemoveWhere(k => k.StartsWith(file + "|", StringComparison.Ordinal));
        }
    }

    public static void RemoveAll() {
        int[] handles;
        lock(Sync) {
            handles = [.. ByHandle.Keys];
            WarnedOnce.Clear();
        }
        foreach(int handle in handles) {
            Remove(handle);
        }
    }

    private static MethodInfo PrefixMethodFor(MethodBase target, bool isVoid, bool isStatic) {
        try {
            if(isVoid) {
                return typeof(JSPatchManager).GetMethod(
                    isStatic ? nameof(PrefixVoidStaticCore) : nameof(PrefixVoidCore),
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            Type ret = ((MethodInfo)target).ReturnType;
            string name = isStatic ? nameof(PrefixStaticCore) : nameof(PrefixCore);
            return typeof(JSPatchManager).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(ret);
        } catch(Exception e) {
            throw new InvalidOperationException(
                "JS patching cannot cover this signature on the current runtime (IL2CPP/AOT cannot generate it).", e);
        }
    }

    private static MethodInfo PostfixMethodFor(MethodBase target, bool isVoid, bool isStatic) {
        try {
            if(isVoid) {
                return typeof(JSPatchManager).GetMethod(
                    isStatic ? nameof(PostfixVoidStaticCore) : nameof(PostfixVoidCore),
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            Type ret = ((MethodInfo)target).ReturnType;
            string name = isStatic ? nameof(PostfixStaticCore) : nameof(PostfixCore);
            return typeof(JSPatchManager).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(ret);
        } catch(Exception e) {
            throw new InvalidOperationException(
                "JS patching cannot cover this signature on the current runtime (IL2CPP/AOT cannot generate it).", e);
        }
    }

    private static List<Registration> ForTarget(MethodBase target) {
        lock(Sync) {
            if(!Forwarded.TryGetValue(target, out var fwd)) {
                return null;
            }
            var regs = new List<Registration>(fwd.Handles.Count);
            foreach(int handle in fwd.Handles) {
                if(ByHandle.TryGetValue(handle, out var reg)) {
                    regs.Add(reg);
                }
            }
            return regs;
        }
    }

    private static void WarnOnce(string key, string message) {
        lock(Sync) {
            if(!WarnedOnce.Add(key)) {
                return;
            }
        }
        MainCore.Log.Wrn($"[JSPatch] {message}");
    }

    private static object InvokeJs(Registration reg, ScriptObject fn, object[] callArgs, string phase) {
        if(reg.Depth > 0) {
            return null;
        }
        reg.Depth++;
        try {
            return MainCore.V8.InvokeCallback(fn, callArgs);
        } catch(ObjectDisposedException) {
            return null;
        } catch(Exception e) {
            WarnOnce($"{reg.File}|{reg.Handle}|{phase}|invoke", $"{reg.File}: {phase} threw, continuing original ({e.GetType().Name}: {e.Message})");
            return null;
        } finally {
            reg.Depth--;
        }
    }

    private static object[] PrefixCallArgs(Registration reg, object[] args) {
        if(reg.PrefixRest || reg.PrefixArity > 1) {
            int n = reg.PrefixRest ? args.Length : Math.Min(reg.PrefixArity, args.Length);
            var spread = new object[n];
            Array.Copy(args, spread, n);
            return spread;
        }
        return [args];
    }

    private static object[] PostfixCallArgs(Registration reg, object[] args, object result) {
        if(reg.PostfixRest || reg.PostfixArity > 2) {
            int n = (reg.PostfixRest || reg.PrefixRest) ? args.Length : Math.Min(reg.PostfixArity - 1, args.Length);
            var spread = new object[n + 1];
            Array.Copy(args, spread, n);
            spread[n] = result;
            return spread;
        }
        return [args, result];
    }

    private static bool IsSkipWithResult(object ret, out object result) {
        result = null;
        if(ret is ScriptObject obj) {
            var prop = obj.GetProperty("result");
            if(prop != null && prop != Undefined.Value) {
                result = prop;
                return true;
            }
        }
        return false;
    }

    private static object Coerce(object value, object original, Type t, Registration reg, string where) {
        if(value == null || value == Undefined.Value) {
            if(t.IsValueType && Nullable.GetUnderlyingType(t) == null) {
                return original;
            }
            return null;
        }
        if(t.IsInstanceOfType(value)) {
            return value;
        }
        try {
            if(t.IsEnum) {
                if(value is string s) {
                    return Enum.Parse(t, s, true);
                }
                return Enum.ToObject(t, Convert.ChangeType(value, Enum.GetUnderlyingType(t), CultureInfo.InvariantCulture));
            }
            return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
        } catch {
            WarnOnce($"{reg.File}|{reg.Handle}|{where}", $"{reg.File}: cannot convert {value.GetType().Name} to {t.Name}, keeping original");
            return original;
        }
    }

    private static void CoerceArgs(Registration reg, object[] args, object[] originals, ParameterInfo[] parameters) {
        for(int i = 0; i < args.Length && i < parameters.Length; i++) {
            object original = i < originals.Length ? originals[i] : null;
            args[i] = Coerce(args[i], original, parameters[i].ParameterType, reg, $"arg{i}");
        }
    }

    private static ParameterInfo[] TargetParameters(MethodBase target) => target.GetParameters();

    private static bool RunPrefixes(MethodBase target, object[] args, Type ret, out object result) {
        result = null;
        var regs = ForTarget(target);
        if(regs == null) {
            return true;
        }
        var parameters = TargetParameters(target);
        foreach(var reg in regs) {
            if(reg.Prefix == null) {
                continue;
            }
            var snapshot = new object[args.Length];
            Array.Copy(args, snapshot, args.Length);
            object ret2 = InvokeJs(reg, reg.Prefix, PrefixCallArgs(reg, args), "prefix");
            CoerceArgs(reg, args, snapshot, parameters);
            if(ret2 is bool b && !b) {
                return false;
            }
            if(IsSkipWithResult(ret2, out object skipResult)) {
                result = ret == typeof(void) ? null : Coerce(skipResult, null, ret, reg, "result");
                return false;
            }
        }
        return true;
    }

    private static void RunPostfixes(MethodBase target, object[] args, Type ret, ref object current) {
        var regs = ForTarget(target);
        if(regs == null) {
            return;
        }
        foreach(var reg in regs) {
            if(reg.Postfix == null) {
                continue;
            }
            object ret2 = InvokeJs(reg, reg.Postfix, PostfixCallArgs(reg, args, current), "postfix");
            if(ret2 != null && ret2 != Undefined.Value && ret != typeof(void)) {
                current = Coerce(ret2, current, ret, reg, "result");
            }
        }
    }

    private static bool PrefixVoidCore(object __instance, object[] __args, MethodBase __originalMethod) {
        return RunPrefixes(__originalMethod, __args, typeof(void), out _);
    }

    private static bool PrefixVoidStaticCore(object[] __args, MethodBase __originalMethod) {
        return RunPrefixes(__originalMethod, __args, typeof(void), out _);
    }

    private static bool PrefixCore<T>(object __instance, object[] __args, MethodBase __originalMethod, ref T __result) {
        if(!RunPrefixes(__originalMethod, __args, typeof(T), out object result)) {
            if(result != null) {
                __result = (T)result;
            }
            return false;
        }
        return true;
    }

    private static bool PrefixStaticCore<T>(object[] __args, MethodBase __originalMethod, ref T __result) {
        if(!RunPrefixes(__originalMethod, __args, typeof(T), out object result)) {
            if(result != null) {
                __result = (T)result;
            }
            return false;
        }
        return true;
    }

    private static void PostfixVoidCore(object __instance, object[] __args, MethodBase __originalMethod) {
        object current = null;
        RunPostfixes(__originalMethod, __args, typeof(void), ref current);
    }

    private static void PostfixVoidStaticCore(object[] __args, MethodBase __originalMethod) {
        object current = null;
        RunPostfixes(__originalMethod, __args, typeof(void), ref current);
    }

    private static void PostfixCore<T>(object __instance, object[] __args, MethodBase __originalMethod, ref T __result) {
        object current = __result;
        RunPostfixes(__originalMethod, __args, typeof(T), ref current);
        if(current != null || !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null) {
            __result = (T)current;
        }
    }

    private static void PostfixStaticCore<T>(object[] __args, MethodBase __originalMethod, ref T __result) {
        object current = __result;
        RunPostfixes(__originalMethod, __args, typeof(T), ref current);
        if(current != null || !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null) {
            __result = (T)current;
        }
    }
}
