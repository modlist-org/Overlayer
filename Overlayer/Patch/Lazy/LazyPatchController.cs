using Overlayer.Patch.Safe;

namespace Overlayer.Patch.Lazy;

// Applies patches required by tags currently used in overlay texts,
// and releases ones the lazy system owns once nothing needs them.
// Manually applied patches, and ones still wanted by their own gate,
// are never touched.
public static class LazyPatchController {
    private static readonly HashSet<Type> owned = [];
    private static readonly object syncLock = new();

    public static void Sync(IEnumerable<string> activeTagNames) {
        HashSet<Type> required = [];
        if(activeTagNames != null) {
            foreach(string name in activeTagNames) {
                if(string.IsNullOrEmpty(name)) {
                    continue;
                }
                if(!Tag.Core.TagManager.TryGet(name, out var tag) || tag == null) {
                    continue;
                }
                foreach(var type in NeedsPatchResolver.GetRequiredPatchTypes(tag)) {
                    required.Add(type);
                }
            }
        }

        lock(syncLock) {
            foreach(var type in required) {
                var patch = SafePatchController.Find(type);
                if(patch == null || patch.IsApplied) {
                    continue;
                }
                patch.Apply();
                if(patch.IsApplied) {
                    owned.Add(type);
                }
            }

            foreach(var type in owned.ToArray()) {
                if(required.Contains(type)) {
                    continue;
                }
                var patch = SafePatchController.Find(type);
                if(patch != null && patch.IsApplied && !patch.WantsApply()) {
                    patch.Remove();
                }
                if(patch == null || !patch.IsApplied) {
                    owned.Remove(type);
                }
            }
        }
    }
}
