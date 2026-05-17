using MelonLoader;
using MelonLoader.Utils;
using Overlayer;
using Overlayer.Async;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Patch.Safe;
using Overlayer.Resource;
using Overlayer.UI;
using Overlayer.UI.SpriteManage;
using System.Collections;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(Core), Info.Name, Info.Version, Info.Author, Info.DownloadLink)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace Overlayer;

public class Core : MelonMod {
    public static readonly Version OverlayerVersion = new(Overlayer.Info.Version);
    internal static Assembly OverlayerAssembly = Assembly.GetExecutingAssembly();
    internal static MelonLogger.Instance Logger;
    internal static GameObject OverlayerObject;
    internal static Translator Tr;
    public static Settings Config;

    public static bool IsModEnabled { get; private set; } = false;
    public static event Action<bool> OnModEnabledChanged;

    public static readonly string OverlayerPath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        "Overlayer"
    );

    private IEnumerator InitializeRoutine() {
        yield return CreateOverlayerObject();
        yield return InitializeAsync();
    }

    private IEnumerator CreateOverlayerObject() {
        for(; ; ) {
            if(OverlayerObject == null) {
                OverlayerObject = new GameObject("Overlayer");

                UnityEngine.Object
                    .DontDestroyOnLoad(OverlayerObject);

                if(OverlayerObject != null) {
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void DistroyOverlayerObject() {
        if(OverlayerObject != null) {
            UnityEngine.Object.Destroy(OverlayerObject);
            OverlayerObject = null;
        }
    }

    public override void OnInitializeMelon() {
        LoggerInstance.Msg("Starting");
        Logger = LoggerInstance;
        Tr = new Translator();
        Config = new Settings();
        Initialize();
    }

    public override void OnUpdate() => UICore.HandleUpdate();

    public override void OnApplicationQuit() {
        Config.Save();
        Dispose();
    }

    public void TranslatorLogLinker(string log) => Logger.Msg(log);

    public void Initialize() => MelonCoroutines.Start(InitializeRoutine());

    private async Task InitializeAsync() {
        await Task.Run(Config.Load);
        Tr.Language = Config.Language;
        Tr.SetLog(TranslatorLogLinker);
        _ = Tr.Load(Path.Combine(OverlayerPath, "Lang"));

        OverlayerObject.AddComponent<MainThread>();

        ResourceManager.Initialize();
        SpriteDatabase.Initialize();

        SetModEnabled(Config.Active);

        UICore.Initialize();

        LoggerInstance.Msg("Ok");
    }

    public void Dispose() {
        UICore.Dispose();

        SetModEnabled(false);

        SpriteDatabase.Dispose();
        ResourceManager.Dispose();

        DistroyOverlayerObject();
    }

    public static void SetModEnabled(bool enabled) {
        if(enabled) {
            if(IsModEnabled) {
                return;
            }

            SafePatchController.ApplyAll();
            IsModEnabled = true;
            OnModEnabledChanged?.Invoke(IsModEnabled);
            Logger.Msg("Mod Enabled");
        } else {
            if(!IsModEnabled) {
                return;
            }

            SafePatchController.UnloadAll();
            IsModEnabled = false;
            OnModEnabledChanged?.Invoke(IsModEnabled);
            Logger.Msg("Mod Disabled");
        }
    }
}