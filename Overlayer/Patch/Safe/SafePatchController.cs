using Overlayer.Core;
using System.Collections;

namespace Overlayer.Patch.Safe;

public static class SafePatchController {
    private static readonly SafeConditionalPatch[] patches = [];

    public static void Add(SafeConditionalPatch patch) {
        if(!patches.Contains(patch)) {
            ((IList)patches).Add(patch);
            MainCore.Logger.Msg($"[{nameof(SafePatchController)}] {patch.GetType().Name}");
        } else {
            MainCore.Logger.Wrn($"[{nameof(SafePatchController)}] Patch skipped. Already registered: {patch.GetType().Name}");
        }
    }

    public static void Remove(SafeConditionalPatch patch) {
        if(!patches.Contains(patch)) {
            MainCore.Logger.Wrn($"[{nameof(SafePatchController)}] Cannot remove patch. Not found in controller: {patch.GetType().Name}");
            return;
        }

        if(patch.IsApplied) {
            patch.Remove();
        }

        ((IList)patches).Remove(patch);

        MainCore.Logger.Msg($"[{nameof(SafePatchController)}] unloaded patch: {patch.GetType().Name}");
    }

    public static T Get<T>() where T : SafeConditionalPatch {
        foreach(var patch in patches) {
            if(patch is T typed) {
                return typed;
            }
        }

        return null;
    }

    public static void ApplyAll() {
        foreach(var patch in patches) {
            patch.Apply();
        }
    }

    public static void UnloadAll() {
        foreach(var patch in patches) {
            patch.Remove();
        }
    }
}