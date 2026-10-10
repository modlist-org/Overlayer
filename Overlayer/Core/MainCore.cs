using Overlayer.Compat;
using Overlayer.Compat.Interface;
using Overlayer.Core.Service;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Overlay;
using Overlayer.Resource;
using Overlayer.V8;
using System.Reflection;

namespace Overlayer.Core;

public static class MainCore {
    public static OverlayerRuntime Runtime { get; private set; }

    public static event Action<bool, bool> OnModEnabledChanged {
        add => Runtime.OnModEnabledChanged += value;
        remove => Runtime.OnModEnabledChanged -= value;
    }

    public static Version Version => Runtime.Version;
    public static Assembly Asm => Runtime.Assembly;
    public static HarmonyLib.Harmony Har => Runtime.Harmony;
    public static OverlayerLogger Log => Runtime.Logger;
    public static PathService Paths => Runtime.Paths;
    public static SettingsFile<CoreSettings> ConfMgr => Runtime.Config;
    public static CoreSettings Conf => Runtime.Config.Data;
    public static Translator Tr => Runtime.Localization.Translator;
    public static ResourceManager Res => Runtime.Resource;
    public static SpriteManager Spr => Runtime.Sprite;
    public static IOverlayerHost Host => Runtime.Host;
    public static UnityEngine.GameObject Root => Runtime.RootObject;
    public static V8Manager V8 => Runtime.V8Manager;
    public static ModuleService ModuleService => Runtime.ModuleService;
    public static CameraManager Cam => Runtime.CameraManager;
    public static bool IsModEnabled => Runtime.State.IsEnabled;

    public static void Initialize(IOverlayerHost host) {
        if (Runtime != null) {
            return;
        }
        if (IsSceneLoaded()) {
            DoInitialize(host);
        } else {
            // No scene is active yet (e.g. Superliminal 2019.4 inits mods ~3s
            // before the first scene load). GameObjects + DontDestroyOnLoad
            // created now die with the bootstrap context on first scene load,
            // killing our root, UI and update pumps. Defer until the scene.
            _pendingHost = host;
            try {
                host.OverlayerLogger.OverlayerMsg("[Overlayer] No scene loaded yet; deferring initialization to first scene load.");
            } catch {
            }
        }
    }

    /// <summary>Runs deferred initialization, if any. Called on scene load.</summary>
    public static void EnsureInitialized() {
        if (Runtime != null || _pendingHost == null) {
            return;
        }
        var host = _pendingHost;
        _pendingHost = null;
        DoInitialize(host);
    }

    /// <summary>Rebuilds root-owned state if a scene wipe destroyed it. Called on scene load.</summary>
    public static void EnsureRootAlive() {
        try {
            Runtime?.EnsureRootAlive();
        } catch {
        }
    }

    private static void DoInitialize(IOverlayerHost host) {
        if (Runtime != null) {
            return;
        }

        Runtime = new OverlayerRuntime(host);

        Runtime.Initialize();
    }

    private static IOverlayerHost _pendingHost;

    private static bool IsSceneLoaded() {
        try {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded;
        } catch {
            return true;
        }
    }

    public static void Tick() {
        // Driven from two sources (MelonLoader OnUpdate + our own UpdatePump).
        // Run once per frame: Time.frameCount exists on every Unity version.
        try {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _lastTickFrame) {
                return;
            }
            _lastTickFrame = frame;
        } catch {
        }
        try {
            Runtime?.Tick();
        } catch (Exception e) {
            try {
                Runtime?.Logger?.Err($"[MainCore] Tick failed: {e.GetType().Name}: {e.Message}");
            } catch {
            }
        }
        try {
            OverlayCore.Tick();
        } catch (Exception e) {
            try {
                Runtime?.Logger?.Err($"[MainCore] Overlay tick failed: {e.Message}");
            } catch {
            }
        }
        try {
            UI.Factory.Page.PageOverlayer.Tick();
        } catch (Exception e) {
            try {
                Runtime?.Logger?.Err($"[MainCore] Page tick failed: {e.Message}");
            } catch {
            }
        }
    }

    private static int _lastTickFrame = -1;

    public static void Dispose() {
        if (Runtime == null) {
            return;
        }

        Runtime.Dispose();
        Runtime = null;
    }

    public static void SetModEnabled(bool enabled) {
        if (enabled) {
            Runtime.SetModEnabled(enabled, false);
            Runtime.SetModEnabledLate(enabled, false);
        } else {
            Runtime.SetModEnabledLate(enabled, false);
            Runtime.SetModEnabled(enabled, false);
        }
    }
}
