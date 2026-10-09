using System.Reflection;

namespace Overlayer.IO.Fx;

// Deep-disposes FxValue engines held anywhere inside an Overlayer settings
// graph (e.g. OvObjectSettings). Settings only reference FxValue<T> of
// primitives/structs, so disposing each FxValue once is sufficient;
// the walk is bounded to Overlayer.* instance fields to avoid wandering
// into Unity or framework object graphs.
internal static class FxDisposal {
    private sealed class RefComparer : IEqualityComparer<object> {
        public static readonly RefComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    public static void DisposeDeep(object root) {
        if (root == null) {
            return;
        }
        var seen = new HashSet<object>(RefComparer.Instance);
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0) {
            object current = stack.Pop();
            if (current == null || !seen.Add(current)) {
                continue;
            }
            if (current is IDisposable disposable) {
                try {
                    disposable.Dispose();
                } catch {
                }
                continue;
            }
            Type type = current.GetType();
            if (type.IsPrimitive || type.IsEnum || current is string) {
                continue;
            }
            if (type.Namespace == null || !type.Namespace.StartsWith("Overlayer.", StringComparison.Ordinal)) {
                continue;
            }
            FieldInfo[] fields;
            try {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            } catch {
                continue;
            }
            foreach (var field in fields) {
                object value;
                try {
                    value = field.GetValue(current);
                } catch {
                    continue;
                }
                if (value == null || value.GetType().IsPrimitive || value is string) {
                    continue;
                }
                stack.Push(value);
            }
        }
    }
}
