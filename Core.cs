using MelonLoader;
using MelonLoader.Utils;
using Overlayer.Resource;
using Overlayer.UI;
using System.Reflection;

[assembly: MelonInfo(typeof(Overlayer.Core), "Overlayer", "5.0.0", "modlist.org", "https://github.com/modlist-org/Overlayer-v5")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace Overlayer;

public class Core : MelonMod {
    public static readonly Version OverlayerVersion = new(5, 0, 0);
    internal static Assembly OverlayerAssembly = Assembly.GetExecutingAssembly();
    internal static string FolderPath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        "Overlayer"
    );

    public override void OnInitializeMelon() {
        ResourceManager.Initialize();
        UICore.Initialize();
        LoggerInstance.Msg("Initialized.");
    }

    public override void OnUpdate() => UICore.HandleUpdate();

}