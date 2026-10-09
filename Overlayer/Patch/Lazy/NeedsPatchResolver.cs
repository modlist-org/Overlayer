using Overlayer.Tag.Core;
using System.Collections.Concurrent;
using System.Reflection;

namespace Overlayer.Patch.Lazy;

public static class NeedsPatchResolver {
    private static readonly ConcurrentDictionary<MemberInfo, IReadOnlyList<Type>> cache = new();

    public static IReadOnlyList<Type> GetRequiredPatchTypes(TagCore tag) {
        var member = tag?.Member;
        if (member == null) {
            return [];
        }
        return cache.GetOrAdd(member, Scan);
    }

    private static IReadOnlyList<Type> Scan(MemberInfo member) {
        var types = new List<Type>();
        Collect(member, types);

        if (types.Count == 0 && member is MethodInfo method && method.IsSpecialName) {
            var declaring = method.DeclaringType;
            if (declaring != null) {
                foreach (var property in declaring.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)) {
                    if (property.GetGetMethod(true) == method || property.GetSetMethod(true) == method) {
                        Collect(property, types);
                    }
                }
            }
        }

        return types.Count == 0 ? Array.Empty<Type>() : [.. types.Distinct()];
    }

    private static void Collect(MemberInfo member, List<Type> types) {
        foreach (var attr in member.GetCustomAttributes<NeedsPatchAttribute>()) {
            if (attr?.PatchTypes == null) {
                continue;
            }
            foreach (var type in attr.PatchTypes) {
                if (type != null && !types.Contains(type)) {
                    types.Add(type);
                }
            }
        }
    }
}
