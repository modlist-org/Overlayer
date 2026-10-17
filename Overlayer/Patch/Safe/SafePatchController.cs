using Overlayer.Core;

namespace Overlayer.Patch.Safe;

public static class SafePatchController {
    private static readonly List<SafeConditionalPatch> patches = [];
    private static readonly object syncLock = new();

    public static void Add(SafeConditionalPatch patch) {
        lock (syncLock) {
            if (!patches.Contains(patch)) {
                patches.Add(patch);
                MainCore.Log.Msg($"[{nameof(SafePatchController)}] {patch.GetType().Name}");
            } else {
                MainCore.Log.Wrn($"[{nameof(SafePatchController)}] Patch skipped. Already registered: {patch.GetType().Name}");
            }
        }
    }

    public static void Remove(SafeConditionalPatch patch) {
        lock (syncLock) {
            if (!patches.Contains(patch)) {
                MainCore.Log.Wrn($"[{nameof(SafePatchController)}] Cannot remove patch. Not found in controller: {patch.GetType().Name}");
                return;
            }

            if (patch.IsApplied) {
                patch.Remove();
            }

            patches.Remove(patch);

            MainCore.Log.Msg($"[{nameof(SafePatchController)}] unloaded patch: {patch.GetType().Name}");
        }
    }

    public static T[] Get<T>() where T : SafeConditionalPatch {
        lock (syncLock) {
            return [.. patches.OfType<T>()];
        }
    }

    public static SafeConditionalPatch Find(Type patchType) {
        lock (syncLock) {
            foreach (var patch in patches) {
                if (patch != null && patch.GetType() == patchType) {
                    return patch;
                }
            }
        }
        return null;
    }

    public static void ApplyAll() {
        SafeConditionalPatch[] copy;
        lock (syncLock) {
            copy = [.. patches];
        }
        foreach (var patch in copy) {
            patch.Apply();
        }
    }

    public static Task ApplyAllAsync() {
        SafeConditionalPatch[] copy;
        lock (syncLock) {
            copy = [.. patches];
        }
        return Task.Run(() => {
            foreach (var patch in copy) {
                patch.Apply();
            }
        });
    }

    public static void UnloadAll() {
        SafeConditionalPatch[] copy;
        lock (syncLock) {
            copy = [.. patches];
        }
        foreach (var patch in copy) {
            patch.Remove();
        }
    }

    public static Task UnloadAllAsync() {
        SafeConditionalPatch[] copy;
        lock (syncLock) {
            copy = [.. patches];
        }
        return Task.Run(() => {
            foreach (var patch in copy) {
                patch.Remove();
            }
        });
    }

    public static Task ApplyAsync(SafeConditionalPatch patch)
        => patch != null ? patch.ApplyAsync() : Task.CompletedTask;
}
