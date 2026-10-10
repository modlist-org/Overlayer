using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Overlayer.Utility.Access;

public static class SafeAccess {
    private static readonly List<WeakReference<SafeMemberBase>> members = [];
    private static readonly object syncLock = new();
    private static readonly HashSet<string> warned = [];
    private static readonly Dictionary<string, Type> typeCache = [];

    public static SafeResolveMode? ModeOverride { get; set; }

    public static void Register(SafeMemberBase member) {
        if (member == null) {
            return;
        }
        lock (syncLock) {
            SweepLocked();
            members.Add(new WeakReference<SafeMemberBase>(member));
        }
    }

    public static void Init(params Assembly[] assemblies) {
        lock (syncLock) {
            typeCache.Clear();
            foreach (var asm in assemblies) {
                if (asm == null) {
                    continue;
                }
                Type[] types;
                try {
                    types = asm.GetTypes();
                } catch (ReflectionTypeLoadException e) {
                    types = [.. e.Types.Where(t => t != null)]!;
                } catch {
                    continue;
                }
                foreach (var type in types) {
                    if (type?.FullName == null) {
                        continue;
                    }
                    typeCache.TryAdd(type.FullName, type);
                    if (type.Name != type.FullName) {
                        typeCache.TryAdd(type.Name, type);
                    }
                }
            }
            SweepLocked();
            int total = 0;
            int resolved = 0;
            foreach (var weak in members) {
                if (weak.TryGetTarget(out var member) && member != null) {
                    total++;
                    try {
                        if (member.Resolve()) {
                            resolved++;
                        }
                    } catch {
                    }
                }
            }
            if (total > 0) {
                Logger?.Invoke($"[SafeAccess] Resolved {resolved}/{total} members.");
            }
        }
    }

    public static Type FindType(string typeName) {
        if (string.IsNullOrEmpty(typeName)) {
            return null;
        }
        lock (syncLock) {
            if (typeCache.TryGetValue(typeName, out var cached)) {
                return cached;
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                Type found;
                try {
                    found = asm.GetType(typeName, false, false);
                } catch {
                    continue;
                }
                if (found != null) {
                    typeCache[typeName] = found;
                    return found;
                }
            }

            if (typeName.IndexOf('.') < 0) {
                Type unique = null;
                bool ambiguous = false;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    Type[] types;
                    try {
                        types = asm.GetTypes();
                    } catch {
                        continue;
                    }
                    foreach (var t in types) {
                        if (t.Name != typeName) {
                            continue;
                        }
                        if (unique != null && unique != t) {
                            ambiguous = true;
                            break;
                        }
                        unique ??= t;
                    }
                    if (ambiguous) {
                        break;
                    }
                }
                if (!ambiguous && unique != null) {
                    typeCache[typeName] = unique;
                    return unique;
                }
            }
            return null;
        }
    }

    internal static void WarnOnce(string message) {
        try {
            lock (warned) {
                if (!warned.Add(message)) {
                    return;
                }
            }
            Logger?.Invoke(message);
        } catch {
        }
    }

    public static Action<string> Logger { get; set; }

    // Concurrent so cache hits (every script-side read/write/call) skip syncLock; misses still build under it.
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> readers = new();
    private static readonly ConcurrentDictionary<(Type, string, int), Func<object, object[], object>> callers = new();
    private static readonly ConcurrentDictionary<(Type, string), Action<object, object>> writers = new();

    public static bool TryRead(object target, string member, out object value) {
        value = null;
        if (target == null || string.IsNullOrEmpty(member)) {
            return false;
        }
        Func<object, object> reader;
        try {
            var key = (target.GetType(), member);
            if (!readers.TryGetValue(key, out reader)) {
                lock (syncLock) {
                    if (!readers.TryGetValue(key, out reader)) {
                        reader = BuildReader(key.Item1, member);
                        readers[key] = reader;
                    }
                }
            }
        } catch {
            return false;
        }
        if (reader == null) {
            return false;
        }
        try {
            value = reader(target);
            return true;
        } catch {
            return false;
        }
    }

    public static bool TryCall(object target, string method, out object result, params object[] args) {
        result = null;
        if (target == null || string.IsNullOrEmpty(method)) {
            return false;
        }
        args ??= [];
        Func<object, object[], object> caller;
        try {
            var key = (target.GetType(), method, args.Length);
            if (!callers.TryGetValue(key, out caller)) {
                lock (syncLock) {
                    if (!callers.TryGetValue(key, out caller)) {
                        caller = BuildCaller(key.Item1, method, args.Length);
                        callers[key] = caller;
                    }
                }
            }
        } catch {
            return false;
        }
        if (caller == null) {
            return false;
        }
        try {
            result = caller(target, args);
            return true;
        } catch {
            return false;
        }
    }

    public static bool TryWrite(object target, string member, object value) {
        if (target == null || string.IsNullOrEmpty(member)) {
            return false;
        }
        Action<object, object> writer;
        try {
            var key = (target.GetType(), member);
            if (!writers.TryGetValue(key, out writer)) {
                lock (syncLock) {
                    if (!writers.TryGetValue(key, out writer)) {
                        writer = BuildWriter(key.Item1, member);
                        writers[key] = writer;
                    }
                }
            }
        } catch {
            return false;
        }
        if (writer == null) {
            return false;
        }
        try {
            writer(target, value);
            return true;
        } catch {
            return false;
        }
    }

    private static Action<object, object> BuildWriter(Type type, string member) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var inst = Expression.Parameter(typeof(object), "instance");
        var val = Expression.Parameter(typeof(object), "value");
        var field = type.GetField(member, flags);
        if (field != null && !field.IsInitOnly) {
            Expression target = field.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Assign(Expression.Field(target, field), Expression.Convert(val, field.FieldType));
            return Expression.Lambda<Action<object, object>>(body, inst, val).Compile();
        }
        var set = type.GetProperty(member, flags)?.GetSetMethod(true);
        if (set != null) {
            Expression target = set.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Call(target, set, Expression.Convert(val, set.GetParameters()[0].ParameterType));
            return Expression.Lambda<Action<object, object>>(body, inst, val).Compile();
        }
        return null;
    }

    private static Func<object, object> BuildReader(Type type, string member) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var inst = Expression.Parameter(typeof(object), "instance");
        var field = type.GetField(member, flags);
        if (field != null) {
            Expression target = field.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Convert(Expression.Field(target, field), typeof(object));
            return Expression.Lambda<Func<object, object>>(body, inst).Compile();
        }
        var property = type.GetProperty(member, flags);
        var get = property?.GetGetMethod(true);
        if (get != null) {
            Expression target = get.IsStatic ? null : Expression.Convert(inst, type);
            var body = Expression.Convert(Expression.Call(target, get), typeof(object));
            return Expression.Lambda<Func<object, object>>(body, inst).Compile();
        }
        return null;
    }

    private static Func<object, object[], object> BuildCaller(Type type, string method, int arity) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var match = type.GetMethods(flags).FirstOrDefault(m => m.Name == method && m.GetParameters().Length == arity);
        if (match == null) {
            return null;
        }
        var inst = Expression.Parameter(typeof(object), "instance");
        var args = Expression.Parameter(typeof(object[]), "args");
        var ps = match.GetParameters();
        var callArgs = new Expression[ps.Length];
        for (int i = 0; i < ps.Length; i++) {
            callArgs[i] = Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), ps[i].ParameterType);
        }
        Expression target = match.IsStatic ? null : Expression.Convert(inst, type);
        Expression call = Expression.Call(target, match, callArgs);
        Expression body = match.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Constant(null, typeof(object)))
            : Expression.Convert(call, typeof(object));
        return Expression.Lambda<Func<object, object[], object>>(body, inst, args).Compile();
    }

    private static void SweepLocked() {
        for (int i = members.Count - 1; i >= 0; i--) {
            if (!members[i].TryGetTarget(out _)) {
                members.RemoveAt(i);
            }
        }
    }
}
