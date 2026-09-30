using System.Reflection;

namespace Overlayer.Patch.Lazy;

// Declares which patches a tag needs, right next to [Tag] (TagDesc-style).
// The lazy controller applies them on first use and releases them when
// no active overlay text uses the tag anymore.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public sealed class NeedsPatchAttribute(params Type[] patchTypes) : Attribute {
    public Type[] PatchTypes { get; } = patchTypes ?? [];
}
