using Overlayer.Patch.Safe;

namespace Overlayer.Patch.Lazy;

public static class LazyPatchController {
    private static readonly HashSet<Type> owned = [];
    private static readonly object syncLock = new();
    private static readonly Dictionary<string, DateTime> touched = new(StringComparer.Ordinal);
    private static readonly TimeSpan TouchTtl = TimeSpan.FromSeconds(10);

    public static void Touch(string tagName) {
        if(string.IsNullOrEmpty(tagName)) {
            return;
        }
        if(!Tag.Core.TagManager.TryGet(tagName, out var tag) || tag == null) {
            return;
        }
        lock(syncLock) {
            touched[tagName] = DateTime.UtcNow;
            foreach(var type in NeedsPatchResolver.GetRequiredPatchTypes(tag)) {
                var patch = SafePatchController.Find(type);
                if(patch == null || patch.IsApplied) {
                    continue;
                }
                patch.Apply();
                if(patch.IsApplied) {
                    owned.Add(type);
                }
            }
        }
    }

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
            DateTime now = DateTime.UtcNow;
            foreach(var entry in touched.ToArray()) {
                if(now - entry.Value > TouchTtl) {
                    touched.Remove(entry.Key);
                    continue;
                }
                if(Tag.Core.TagManager.TryGet(entry.Key, out var tag) && tag != null) {
                    foreach(var type in NeedsPatchResolver.GetRequiredPatchTypes(tag)) {
                        required.Add(type);
                    }
                }
            }

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
