using HarmonyLib;
using Overlayer.Core;
using System.Reflection;

namespace Overlayer.Patch.Safe;

public abstract class SafeConditionalPatch(string id) {
    public string Id { get; } = id;
    public bool IsApplied { get; private set; }
    private readonly object syncLock = new();
    private MethodBase targetMethod;
    protected MethodBase TargetMethod => targetMethod ??= GetTargetMethod();
    // Patch methods actually applied, so Remove() unpatches only these and not
    // other patches that share the same target method.
    private MethodInfo[] appliedMethods = [];

    public void Apply() {
        lock (syncLock) {
            if (IsApplied || !ShouldApply()) {
                return;
            }

            try {
                var method = TargetMethod;
                HarmonyMethod prefix = Prefix(), postfix = Postfix(), transpiler = Transpiler();
                SafePatchManager.Harmony.Patch(method, prefix, postfix, transpiler);
                appliedMethods = [.. new[] { prefix, postfix, transpiler }
                    .Where(h => h?.method != null)
                    .Select(h => h.method)];
                IsApplied = true;
                MainCore.Log.Msg($"[{nameof(SafePatch)}] {Id} Applied");
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(SafePatch)}] {Id} Apply Failed: {e.Message}");
            }
        }
    }

    public Task ApplyAsync() => Task.Run(Apply);

    public void Remove() {
        lock (syncLock) {
            if (!IsApplied) {
                return;
            }

            try {
                var method = TargetMethod;
                foreach (var patchMethod in appliedMethods) {
                    SafePatchManager.Harmony.Unpatch(method, patchMethod);
                }
                appliedMethods = [];
                IsApplied = false;
                MainCore.Log.Msg($"[{nameof(SafePatch)}] {Id} Removed");
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(SafePatch)}] {Id} Remove Failed: {e.Message}");
            }
        }
    }

    public Task RemoveAsync() => Task.Run(Remove);

    protected abstract bool ShouldApply();
    internal bool WantsApply() {
        try {
            return ShouldApply();
        } catch {
            return false;
        }
    }
    protected abstract MethodBase GetTargetMethod();
    protected virtual HarmonyMethod Prefix() => null;
    protected virtual HarmonyMethod Postfix() => null;
    protected virtual HarmonyMethod Transpiler() => null;
}
