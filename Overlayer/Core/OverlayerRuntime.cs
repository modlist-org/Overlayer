using Overlayer.Async;
using Overlayer.Compat;
using Overlayer.Compat.Interface;
using Overlayer.Core.Service;
using Overlayer.IO;
using Overlayer.Patch.Safe;
using Overlayer.Resource;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Core;
using System.Reflection;
using System.Text;
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

    public SettingsFile<CoreSettings> Config;

    public LocalizationService Localization { get; private set; }

    public ResourceManager Resource { get; }
    public SpriteManager Sprite { get; }

    public GameObject RootObject { get; private set; }

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

        Task.Run(async () => {
            await TagManager.InitializeAsync(Assembly);

            var engine = new TextEngineCore {
                Text = "Value: {Test:F10}, Bad: {Unknown:1}"
            };

            var text = engine.Text;
            var diags = engine.GetDiagnostics();

            MainCore.Logger.Msg("\n" + RenderRustDiagnostics(text, diags));

            MainCore.Logger.Msg($"[TEST] {engine.Get()}");
        });

        SetModEnabled(Config.Data.Active, false);

        Logger.Msg("Hello");

        moduleService.DiscoverAndRegisterModules();
        moduleService.InitializeAllModules();
    }

    [Tag(TagType = TagType.ProcessFormat)]
    public static double Test() => 123.12312312;

    static string RenderRustDiagnostics(string text, IEnumerable<CompileDiagnostic> diags) {
        var sb = new StringBuilder();

        foreach(var d in diags) {
            var ctx = d.Context;

            string level = d.Severity switch {
                CompileSeverity.Error => "ERROR",
                CompileSeverity.Warning => "WARN",
                _ => "INFO"
            };

            sb.AppendLine($"{level} [{d.Id}] : {d.Id}");

            sb.AppendLine($"    ---> textEngine:{ctx.Index}");

            sb.AppendLine($"    {text}");

            sb.AppendLine($"    {BuildLineMarker(ctx.Index, ctx.Length)} here");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    static string BuildLineMarker(int start, int length) {
        var sb = new StringBuilder();

        for(int i = 0; i < start; i++) {
            sb.Append(' ');
        }

        int len = Math.Max(length, 1);

        for(int i = 0; i < len; i++) {
            sb.Append('^');
        }

        return sb.ToString();
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