using HarmonyLib;
using Overlayer.Core;
using System.Reflection;

namespace Overlayer.Patch.Safe;

public static class SafePatchManager {
    internal static readonly HarmonyLib.Harmony Harmony = new($"Overlayer.Patch.Safe.{nameof(SafePatchManager)}");
    private static readonly HashSet<string> appliedPatches = [];

    public static void ApplyPatch(Type type) {
        if(type == null) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] Type is null");
            return;
        }

        var attr = type.GetCustomAttribute<SafePatchAttribute>();
        if(attr == null) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {type.Name} Has No SafePatchAttribute");
            return;
        }

        if(appliedPatches.Contains(attr.Id)) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {attr.Id} Already Applied");
            return;
        }

        try {
            var method = SafePatch.GetMethodSafe(attr.TargetType, attr.TargetMethod);
            var transpiler = type.GetMethod("Transpiler", BindingFlags.Public | BindingFlags.Static);
            Harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
            appliedPatches.Add(attr.Id);
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {attr.Id} Patched");
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(SafePatch)}] {attr.Id} Patch Failed: {e.Message}");
        }
    }

    public static void RemovePatch(Type type) {
        if(type == null) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] Type is null, cannot unpatch");
            return;
        }

        var attr = type.GetCustomAttribute<SafePatchAttribute>();
        if(attr == null) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {type.Name} Has No SafePatchAttribute, cannot unpatch");
            return;
        }

        if(!appliedPatches.Contains(attr.Id)) {
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {attr.Id} Is Not Applied, nothing to unpatch");
            return;
        }

        try {
            var method = SafePatch.GetMethodSafe(attr.TargetType, attr.TargetMethod);
            Harmony.Unpatch(method, HarmonyPatchType.All, Harmony.Id);
            appliedPatches.Remove(attr.Id);
            MainCore.Logger.Msg($"[{nameof(SafePatch)}] {attr.Id} Unpatched");
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(SafePatch)}] {attr.Id} Unpatch Failed: {e.Message}");
        }
    }
}