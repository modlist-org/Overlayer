using System.Reflection;

namespace Overlayer.Patch.Lazy;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public sealed class NeedsPatchAttribute(params Type[] patchTypes) : Attribute {
    public Type[] PatchTypes { get; } = patchTypes ?? [];
}
