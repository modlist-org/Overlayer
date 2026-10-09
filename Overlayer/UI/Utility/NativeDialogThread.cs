using HarmonyLib;
using NativeFileDialog.Extended;
using Overlayer.Async;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Overlayer.UI.Utility;

internal static class NativeDialogThread {
    private static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static int mainThreadId;

    // macOS AppKit (NSOpenPanel/NSSavePanel) aborts the process when used off the main thread,
    // so run inline there (blocks the frame while the dialog is open). Elsewhere keep it off the game loop.
    public static Task<string> Run(Func<string> dialog) =>
        IsMac
            ? Task.FromResult(dialog())
            : Task.Run(dialog);

    /// <summary>
    /// Other mods (e.g. the KeyViewer module) call NFD from Task.Run, which hard-crashes the game on macOS.
    /// Patch every NFD dialog so an off-main-thread call is run on the main thread and the caller waits.
    /// Must be called from the main thread.
    /// </summary>
    public static void Install(HarmonyLib.Harmony harmony) {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        if (!IsMac) {
            return;
        }
        var prefix = typeof(NativeDialogThread).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var method in typeof(NFD).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
            if (method.ReturnType != typeof(void)) {
                // Typed per return type so __result needs no boxing support from Harmony.
                harmony.Patch(method, prefix: new HarmonyMethod(prefix.MakeGenericMethod(method.ReturnType)));
            }
        }
    }

    private static bool Prefix<T>(MethodBase __originalMethod, object[] __args, ref T __result) {
        if (Thread.CurrentThread.ManagedThreadId == mainThreadId) {
            return true;
        }
        object result = null;
        Exception error = null;
        using var done = new ManualResetEventSlim();
        MainThread.Enqueue(() => {
            try {
                // Re-enters this prefix on the main thread, which lets the original run.
                result = __originalMethod.Invoke(null, __args);
            } catch (TargetInvocationException e) {
                error = e.InnerException ?? e;
            } catch (Exception e) {
                error = e;
            } finally {
                done.Set();
            }
        });
        done.Wait();
        if (error != null) {
            throw error;
        }
        __result = (T)result;
        return false;
    }
}
