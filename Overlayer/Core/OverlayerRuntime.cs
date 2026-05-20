using System.Reflection;
using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Compat.Interface;
using Overlayer.Core.Service;
using Overlayer.IO;
using Overlayer.Patch.Safe;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Core;

public sealed class OverlayerRuntime {
    public Version Version { get; }
    public Assembly Assembly { get; }
    
    public OverlayerLogger Logger { get; }

    public SettingsManager Config { get; }

    public ModState State { get; }
    
    public event Action<bool, bool> OnModEnabledChanged;

    public PathService Paths { get; }
    
    public LocalizationService Localization { get; private set; }

    public GameObject RootObject { get; private set; }

    private readonly IOverlayerHost host;

    private readonly RuntimeServices services;
    private readonly RuntimeTicks ticks;
    
    private UIService uiService;

    public OverlayerRuntime(
        IOverlayerHost host
    ) {
        this.host = host;
        
        Version = new Version(Info.Version);
        Assembly = Assembly.GetExecutingAssembly();
        Logger = new OverlayerLogger(
            host.OverlayerLogger
        );
        Paths = new PathService(
            System.IO.Path.Combine(
                host.OverlayerFilePath,
                "Overlayer"
            ),
            host.OverlayerDLLPath
        );
        Config = new SettingsManager(Paths.ConfigPath);
        State = new ModState();
        services = new RuntimeServices();
        ticks = new RuntimeTicks();
    }

    public void Initialize() {
        Paths.Initialize();

        CreateRootObject();

        RootObject.AddComponent<MainThread>();

        Config.Load();
        
        Localization = new LocalizationService(Paths.LangPath, Config.Data, Logger);

        uiService = new UIService();
        
        services.Add(Localization);
        services.Add(new ResourceService());
        services.Add(new SpriteService());
        services.Add(uiService);
        
        ticks.Add(uiService);

        services.Initialize();

        SetModEnabled(Config.Data.Active, false);

        Logger.Msg("Hello");
    }
    
    public void Tick() {
        ticks.Tick();
    }

    public void Dispose() {
        SetModEnabled(false, true);

        Config.Save();

        services.Dispose();

        if(RootObject != null) {
            Object.Destroy(RootObject);

            RootObject = null;
        }

        Logger.Msg("Bye");
    }

    public void SetModEnabled(
        bool enabled, bool isDispose
    ) {
        if(State.IsEnabled == enabled) {
            return;
        }

        State.IsEnabled = enabled;

        OnModEnabledChanged?.Invoke(enabled, isDispose);
        
        if(enabled) {
            SafePatchController.ApplyAll();
            Logger.Msg("Mod Enabled");
        } else {
            SafePatchController.UnloadAll();
            Logger.Msg("Mod Disabled");
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