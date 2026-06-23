using Microsoft.ClearScript.V8;
using GTweens.Contexts;
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
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using Overlayer.Tag.Runtime;

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

    public GTweensContext TweensContext { get; }

    public V8ScriptEngine V8Engine { get; private set; }

    public ModuleService ModuleService { get; private set; }

    public readonly IOverlayerHost Host;

    private readonly RuntimeServices services;
    private readonly RuntimeTicks ticks;

    private UIService uiService;
    private TweenService tweenService;

    public OverlayerRuntime(IOverlayerHost host) {
        Host = host;

        Version = new Version(Info.Version);
        Assembly = Assembly.GetExecutingAssembly();
        Harmony = host.OverlayerHarmony;
        Logger = new OverlayerLogger(
            host.OverlayerLogger
        );
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
        services = new RuntimeServices();
        ticks = new RuntimeTicks();
        TweensContext = new GTweensContext();
    }

    public void Initialize() {
        Paths.Initialize();

        CreateRootObject();

        RootObject.AddComponent<MainThread>();

        Config.Load();

        Localization = new LocalizationService(Paths.LangPath, Config, Logger);

        uiService = new UIService();
        tweenService = new TweenService(TweensContext);
        ModuleService = new ModuleService(Logger);

        services.Add(Localization);
        services.Add(uiService);
        services.Add(tweenService);

        ticks.Add(uiService);
        ticks.Add(tweenService);
        ticks.Add(TagCache.Instance);

        services.Initialize();

        V8Engine = new();

        SetModEnabled(Config.Data.Active, false);

        Logger.Msg("Hello");

        ModuleService.DiscoverAndRegisterModules();
        ModuleService.InitializeAllModules();

        SetModEnabledLate(Config.Data.Active, false);
    }

    public void Tick() => ticks.Tick();

    public void Dispose() {
        SetModEnabledLate(false, true);
        SetModEnabled(false, true);

        Config.Save();

        ModuleService?.Dispose();

        services.Dispose();

        Sprite.Dispose();
        Resource.Dispose();

        if(RootObject != null) {
            Object.Destroy(RootObject);

            RootObject = null;
        }

        Logger.Msg("Bye");
    }

    public void SetModEnabled(bool enabled, bool isDispose) {
        if(State.IsEnabled == enabled) {
            return;
        }

        State.IsEnabled = enabled;

        if(!isDispose) {
            Config.Data.Active = enabled;
            Config.RequestSave();
        }

        if(enabled) {
            UserResourceManager.Initialize();

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

            UserResourceManager.Dispose();

            Logger.Msg("Mod Disabled");
        }
    }

    private bool isLateEnabled;
    public void SetModEnabledLate(bool enabled, bool isDispose) {
        if(isLateEnabled == enabled) {
            return;
        }

        isLateEnabled = enabled;

        if(enabled) {
            _ = TagManager.RegisterAsync(Assembly);

            if(ModuleService != null) {
                foreach(var module in ModuleService.LoadedModules) {
                    if(module != null) {
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
    }
}