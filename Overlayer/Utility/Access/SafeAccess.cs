using System.Reflection;

namespace Overlayer.Utility.Access;

public static class SafeAccess {
    private static readonly List<WeakReference<SafeMemberBase>> members = [];
    private static readonly object syncLock = new();
    private static readonly HashSet<string> warned = [];
    private static readonly Dictionary<string, Type> typeCache = [];

    // User-facing override (module setting). Null keeps per-member Mode.
    public static SafeResolveMode? ModeOverride { get; set; }

    public static void Register(SafeMemberBase member) {
        if(member == null) {
            return;
        }
        lock(syncLock) {
            SweepLocked();
            members.Add(new WeakReference<SafeMemberBase>(member));
        }
    }

    public static void Init(params Assembly[] assemblies) {
        lock(syncLock) {
            typeCache.Clear();
            foreach(var asm in assemblies) {
                if(asm == null) {
                    continue;
                }
                Type[] types;
                try {
                    types = asm.GetTypes();
                } catch(ReflectionTypeLoadException e) {
                    types = e.Types.Where(t => t != null).ToArray()!;
                } catch {
                    continue;
                }
                foreach(var type in types) {
                    if(type?.FullName == null) {
                        continue;
                    }
                    typeCache.TryAdd(type.FullName, type);
                    if(type.Name != type.FullName) {
                        typeCache.TryAdd(type.Name, type);
                    }
                }
            }
            SweepLocked();
            int total = 0;
            int resolved = 0;
            foreach(var weak in members) {
                if(weak.TryGetTarget(out var member) && member != null) {
                    total++;
                    try {
                        if(member.Resolve()) {
                            resolved++;
                        }
                    } catch {
                    }
                }
            }
            if(total > 0) {
                Logger?.Invoke($"[SafeAccess] Resolved {resolved}/{total} members.");
            }
        }
    }

    public static Type FindType(string typeName) {
        if(string.IsNullOrEmpty(typeName)) {
            return null;
        }
        lock(syncLock) {
            if(typeCache.TryGetValue(typeName, out var cached)) {
                return cached;
            }
            foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                Type found;
                try {
                    found = asm.GetType(typeName, false, false);
                } catch {
                    continue;
                }
                if(found != null) {
                    typeCache[typeName] = found;
                    return found;
                }
            }
            return null;
        }
    }

    internal static void WarnOnce(string message) {
        try {
            lock(warned) {
                if(!warned.Add(message)) {
                    return;
                }
            }
            Logger?.Invoke(message);
        } catch {
        }
    }

    public static Action<string> Logger { get; set; }

    private static void SweepLocked() {
        for(int i = members.Count - 1; i >= 0; i--) {
            if(!members[i].TryGetTarget(out _)) {
                members.RemoveAt(i);
            }
        }
    }
}
