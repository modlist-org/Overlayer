using MelonLoader;
using MelonLoader.Utils;
using Overlayer.Resource;
using Overlayer.UI;
using Overlayer.UI.UISprites;
using System.Collections;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(Overlayer.Core), "Overlayer", "5.0.0", "modlist.org", "https://github.com/modlist-org/Overlayer-v5")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace Overlayer;

public class Core : MelonMod {
    public static readonly Version OverlayerVersion = new(5, 0, 0);
    internal static Assembly OverlayerAssembly = Assembly.GetExecutingAssembly();
    internal static MelonLogger.Instance Logger;
    internal static GameObject OverlayerObject;
    internal static string FolderPath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        "Overlayer"
    );

    private IEnumerator CreateOverlayerObject() {
        for(;;) {
            if(OverlayerObject == null) {
                OverlayerObject = new GameObject("Overlayer");
                UnityEngine.Object.DontDestroyOnLoad(OverlayerObject);

                if(OverlayerObject != null) {
                    Internal_Initialize();
                    yield break;
                }
            }

            yield return null;
        }
    }

    public override void OnInitializeMelon() {
        Logger = LoggerInstance;
        Initalize();
    }

    public void Initalize() => MelonCoroutines.Start(CreateOverlayerObject());

    private void Internal_Initialize() {
        OverlayerObject.AddComponent<MainThread>();
        ResourceManager.Initialize();
        SpriteDatabase.Initialize();
        UICore.Initialize();
        LoggerInstance.Msg("Initialized.");
    }

    public override void OnUpdate() => UICore.HandleUpdate();

}