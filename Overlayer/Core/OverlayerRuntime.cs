//using Microsoft.ClearScript.V8;
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
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Core;

public sealed class OverlayerRuntime {
    public Version Version { get; }
    public Assembly Assembly { get; }

    public OverlayerLogger Logger { get; }

    public ModState State { get; }

    public event Action<bool, bool> OnModEnabledChanged;

    public PathService Paths { get; }

    public SettingsFile<CoreSettings> Config { get; }

    public LocalizationService Localization { get; private set; }

    public ResourceManager Resource { get; }
    public SpriteManager Sprite { get; }

    public GameObject RootObject { get; private set; }

    //public V8ScriptEngine V8Engine { get; private set; }

    public readonly IOverlayerHost Host;

    private readonly RuntimeServices services;
    private readonly RuntimeTicks ticks;

    private UIService uiService;
    private ModuleService moduleService;

    public OverlayerRuntime(IOverlayerHost host) {
        Host = host;

        Version = new Version(Info.Version);
        Assembly = Assembly.GetExecutingAssembly();
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
    }

    public void Initialize() {
        Paths.Initialize();

        CreateRootObject();

        RootObject.AddComponent<MainThread>();

        Config.Load();

        Localization = new LocalizationService(Paths.LangPath, Config, Logger);

        uiService = new UIService();
        moduleService = new ModuleService(Logger);

        services.Add(Localization);
        services.Add(uiService);

        ticks.Add(uiService);

        services.Initialize();

        //V8Engine = new();

        SetModEnabled(Config.Data.Active, false);

        Logger.Msg("Hello");

        moduleService.DiscoverAndRegisterModules();
        moduleService.InitializeAllModules();
    }

    public void Tick() => ticks.Tick();

    public void Dispose() {
        SetModEnabled(false, true);

        Config.Save();

        moduleService?.Dispose();

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

        OnModEnabledChanged?.Invoke(enabled, isDispose);
        moduleService?.NotifyEnabledChanged(enabled, isDispose);

        if(enabled) {
            UserResourceManager.Initialize();

            Task.Run(() => {
                _ = TagManager.InitializeAsync(Assembly);
            });

            SafePatchController.ApplyAll();

            OverlayCore.Initialize(RootObject);

            // OvObject Test Logic Begin
            
            var canvas = OverlayCore.CreateOvCanvas();

            var obj = canvas.CreateOvObject(new IO.Overlay.OvObjectSettings() {
                RectTransformConfig = new() {
                    SizeDelta = new Vector2(200, 200),
                },
                TextConfig = new(),
                ImageConfig = new(),
                OutlineConfig = new(),
            });
            obj.ApplyComponent();
            obj.ApplyConfig();
            
            /* Fuck
            [ERROR] [Overlayer] System.NullReferenceException: Object reference not set to an instance of an object
            at Overlayer.IO.UnityComponent.Impl.ImageSettings.ToUnity (UnityEngine.GameObject target) [0x00008] in <f64de5634531445fa16ca2041a434909>:0 
            at Overlayer.Overlay.OvObject.ApplyConfig () [0x00061] in <f64de5634531445fa16ca2041a434909>:0 
            at Overlayer.Core.OverlayerRuntime.SetModEnabled (System.Boolean enabled, System.Boolean isDispose) [0x0012e] in <f64de5634531445fa16ca2041a434909>:0 
            at Overlayer.Core.OverlayerRuntime.Initialize () [0x000b9] in <f64de5634531445fa16ca2041a434909>:0 
            at Overlayer.Core.MainCore.Initialize (Overlayer.Compat.Interface.IOverlayerHost host) [0x00021] in <f64de5634531445fa16ca2041a434909>:0 
            at Overlayer.Loader.ML.Loader.OnInitializeMelon () [0x00001] in <760fe7bc661f443fa2dae6bde9ae6513>:0 
            at MelonLoader.MelonBase.LoaderInitialized () [0x00000] in /home/runner/work/MelonLoader/MelonLoader/MelonLoader/Melons/MelonBase.cs:474 
            */
            
            // Test End

            Logger.Msg("Mod Enabled");
        } else {
            OverlayCore.Dispose();

            SafePatchController.UnloadAll();

            TagManager.Dispose();

            UserResourceManager.Dispose();

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