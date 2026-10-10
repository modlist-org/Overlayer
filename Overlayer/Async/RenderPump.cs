using HarmonyLib;
using Overlayer.Core;
using System.Reflection;
using UnityEngine;

namespace Overlayer.Async;

/// <summary>
/// Fallback frame driver for titles where no MonoBehaviour update callback
/// fires on runtime-added objects (observed: Superliminal 2019.4 — Awake runs,
/// but Update/LateUpdate/FixedUpdate/OnGUI never do, and MelonLoader OnUpdate
/// never arrives either). Drives <see cref="MainCore.Tick"/> from the native
/// render callbacks instead, which must fire while the game draws frames.
/// Also re-dumps UI state there, since init-time state may change afterwards
/// (e.g. something deactivating our root would explain both dead updates and
/// an invisible panel).
/// </summary>
public static class RenderPump {
    private static readonly object _sync = new();
    private static bool _installed;

    public static void Ensure(HarmonyLib.Harmony harmony, Compat.OverlayerLogger log) {
        lock (_sync) {
            if (_installed) {
                return;
            }
            _installed = true;
        }
        try {
            Camera.onPreRender += OnPreRender;
        } catch (Exception ex) {
            log.Err($"[RenderPump] onPreRender subscribe failed: {ex.GetType().Name}: {ex.Message}");
        }
        try {
            MethodInfo render = typeof(Camera).GetMethod(nameof(Camera.Render), Type.EmptyTypes);
            if (render == null) {
                log.Err("[RenderPump] Camera.Render() not found.");
                return;
            }
            MethodInfo postfix = typeof(RenderPump).GetMethod(nameof(RenderPostfix), BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(render, postfix: new HarmonyMethod(postfix));
        } catch (Exception ex) {
            log.Err($"[RenderPump] Camera.Render patch failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void OnPreRender(Camera cam) => Drive();

    private static void RenderPostfix() => Drive();

    private static void Drive() {
        if (MainCore.Runtime == null) {
            return;
        }
        try {
            MainCore.Tick();
        } catch (Exception ex) {
            try {
                MainCore.Log.Msg($"[RenderPump] Tick threw: {ex.GetType().Name}: {ex.Message}");
            } catch {
            }
        }
    }
}
