using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Compat.Interface;
using Overlayer.Core.Service;
using Overlayer.IO;
using Overlayer.IO.User;
using Overlayer.Overlay;
using Overlayer.Patch.Safe;
using Overlayer.Resource;
using Overlayer.Tag.Core;
using Overlayer.V8;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Core;

public sealed class OverlayerRuntime {
    public Version Version { get; }
    public Assembly Assembly { get; }
    public HarmonyLib.Harmony Harmony { get; }

    public OverlayerLogger Logger { get; }

    public ModState State { get; }

    public event Action<bool, bool> OnModEnabledChanged;

    public PathService Paths { get; }

    public SettingsFile<CoreSettings> Config { get; }

    public LocalizationService Localization { get; private set; }

    public ResourceManager Resource { get; }
    public SpriteManager Sprite { get; }

    public GameObject RootObject { get; private set; }

    public V8Manager V8Manager { get; private set; }

    public ModuleService ModuleService { get; private set; }

    public CameraManager CameraManager { get; }

    public readonly IOverlayerHost Host;

    private readonly RuntimeServices services;
    private readonly RuntimeTicks ticks;

    private UIService uiService;

    public OverlayerRuntime(IOverlayerHost host) {
        Host = host;

        Version = new Version(Info.Version);
        Assembly = Assembly.GetExecutingAssembly();
        Harmony = host.OverlayerHarmony;
        UI.Utility.NativeDialogThread.Install(Harmony);
        Logger = new OverlayerLogger(
            host.OverlayerLogger
        );
        Utility.Access.SafeAccess.Logger = message => Logger.Wrn(message);
        State = new ModState();
        Paths = new PathService(
            Path.Combine(
                host.OverlayerFilePath,
                "Overlayer"
            )
        );
        Config = new SettingsFile<CoreSettings>(Paths.ConfigPath);
        Resource = new ResourceManager(
            Assembly,
            "Overlayer.Resource.Embedded."
        );
        Sprite = new SpriteManager(Resource);
        V8Manager = new V8Manager();
        CameraManager = new CameraManager();
        services = new RuntimeServices();
        ticks = new RuntimeTicks();
    }

    public void Initialize() {
        Paths.Initialize();

        CreateRootObject();

        RootObject.AddComponent<MainThread>();
        RootObject.AddComponent<UpdatePump>();
        RenderPump.Ensure(Harmony, Logger);

        Config.Load();

        Localization = new LocalizationService(Paths.LangPath, Config, Logger);

        uiService = new UIService();
        ModuleService = new ModuleService(Logger);

        services.Add(Localization);
        services.Add(uiService);
        services.Add(V8Manager);

        ticks.Add(uiService);
        ticks.Add(new TagImpl.KpsTracker());
        ticks.Add(new TagImpl.FpsTracker());

        services.Initialize();

        SetModEnabled(Config.Data.Active, false);

        Logger.Msg("Hello");

        Update.UpdateService.Initialize();

        ModuleService.DiscoverAndRegisterModules();
        ModuleService.InitializeAllModules();

        SetModEnabledLate(Config.Data.Active, false);

        _ = V8Manager.LoadScriptsAsync();
    }

    public void Tick() => ticks.Tick();

    public void Dispose() {
        _disposed = true;
        Compat.O5KitAdapters.Teardown();
        SetModEnabledLate(false, true);
        SetModEnabled(false, true);

        Config.Save();

        ModuleService?.Dispose();

        services.Dispose();

        Sprite.Dispose();
        Resource.Dispose();

        if (RootObject != null) {
            Object.Destroy(RootObject);

            RootObject = null;
        }

        Logger.Msg("Bye");
    }

    public void SetModEnabled(bool enabled, bool isDispose) {
        if (State.IsEnabled == enabled) {
            return;
        }

        State.IsEnabled = enabled;

        if (!isDispose) {
            Config.Data.Active = enabled;
            Config.RequestSave();
        }

        if (enabled) {
            UserResourceManager.Initialize();
            Compat.O5KitAdapters.SetUserFontsEnabled(true);

            SafePatchController.ApplyAll();

            OverlayCore.Initialize(RootObject);

            OnModEnabledChanged?.Invoke(true, isDispose);
            ModuleService?.NotifyEnabledChanged(true, isDispose);

            Logger.Msg("Mod Enabled");
        } else {
            ModuleService?.NotifyEnabledChanged(false, isDispose);
            OnModEnabledChanged?.Invoke(false, isDispose);

            OverlayCore.Dispose();

            SafePatchController.UnloadAll();

            Compat.O5KitAdapters.SetUserFontsEnabled(false);
            UserResourceManager.Dispose();

            Logger.Msg("Mod Disabled");
        }
    }

    private bool isLateEnabled;
    public void SetModEnabledLate(bool enabled, bool isDispose) {
        if (isLateEnabled == enabled) {
            return;
        }

        isLateEnabled = enabled;

        if (enabled) {
            _ = TagManager.RegisterAsync(Assembly);

            if (ModuleService != null) {
                foreach (var module in ModuleService.LoadedModules) {
                    if (module != null) {
                        var moduleAsm = module.GetType().Assembly;
                        _ = TagManager.RegisterAsync(moduleAsm);
                    }
                }
            }
        } else {
            TagManager.Dispose();
        }
    }

    private void CreateRootObject() {
        RootObject = new GameObject(
            "Overlayer"
        );

        Object.DontDestroyOnLoad(
            RootObject
        );

        // UniverseLib pattern: extra protection against scene-unload wipes.
        try {
            RootObject.hideFlags |= HideFlags.HideAndDontSave;
        } catch {
        }
    }

    private bool _disposed;
    private int _resurrectCount;

    /// <summary>
    /// Rebuilds root-owned state when a scene wipe destroyed it despite
    /// DontDestroyOnLoad (observed in Superliminal 2019.4: everything under
    /// our root is dead right after the first scene load). Called on scene
    /// load; a no-op while the root is alive.
    /// </summary>
    public void EnsureRootAlive() {
        if (_disposed) {
            return;
        }
        bool alive = false;
        try {
            alive = RootObject != null;
        } catch {
            alive = false;
        }
        if (alive) {
            return;
        }
        _resurrectCount++;
        Logger.Msg($"[Overlayer] Root object lost (rebuild #{_resurrectCount}). Rebuilding UI.");
        try {
            int purged = O5Kit.Core.O5Object.PurgeDead();
            Logger.Msg($"[Overlayer] Purged {purged} dead UI controls.");
        } catch (Exception ex) {
            Logger.Wrn($"[Overlayer] Control purge failed: {ex.Message}");
        }
        try {
            CreateRootObject();
        } catch (Exception ex) {
            Logger.Err($"[Overlayer] Root recreation failed: {ex.Message}");
            return;
        }
        try {
            RootObject.AddComponent<MainThread>();
            RootObject.AddComponent<UpdatePump>();
        } catch (Exception ex) {
            Logger.Err($"[Overlayer] Pump recreation failed: {ex.Message}");
        }
        try {
            uiService?.Reinitialize();
        } catch (Exception ex) {
            Logger.Err($"[Overlayer] UI rebuild failed: {ex.GetType().Name}: {ex.Message}");
        }
        try {
            OverlayCore.Reinitialize(RootObject);
        } catch (Exception ex) {
            Logger.Err($"[Overlayer] Overlay rebuild failed: {ex.GetType().Name}: {ex.Message}");
        }
        try {
            Compat.O5KitAdapters.Ctx?.NotifyEnabledChanged(State.IsEnabled);
        } catch (Exception ex) {
            Logger.Wrn($"[Overlayer] Enabled-state push failed: {ex.Message}");
        }
        Logger.Msg("[Overlayer] Root rebuild complete.");
    }
}
