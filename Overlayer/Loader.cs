using MelonLoader;
using MelonLoader.Utils;
using Overlayer;
using Overlayer.Compat.Interface;
using Overlayer.Core;
using Overlayer.Overlay;

#if ML
[assembly: MelonInfo(typeof(Loader), Overlayer.Core.Info.Name, Overlayer.Core.Info.Version, Overlayer.Core.Info.Author, Overlayer.Core.Info.GithubLink)]
#endif

namespace Overlayer;

#if ML
public class Loader : MelonMod, IOverlayerHost, IOverlayerLogger {

    public IOverlayerLogger OverlayerLogger => this;

    public string OverlayerFilePath => MelonEnvironment.UserDataDirectory;

    public HarmonyLib.Harmony OverlayerHarmony => HarmonyInstance;

    public override void OnInitializeMelon() => MainCore.Initialize(this);

    public override void OnDeinitializeMelon() => MainCore.Dispose();

    public override void OnUpdate() => MainCore.Tick();

    public override void OnSceneWasLoaded(int buildIndex, string sceneName) => OverlayCore.RequestLayoutRefresh();

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => OverlayCore.RequestLayoutRefresh();

    public void OverlayerMsg(string msg) => MelonLogger.Msg(msg);
    public void OverlayerWrn(string msg) => MelonLogger.Warning(msg);
    public void OverlayerErr(string msg) => MelonLogger.Error(msg);
}
#endif
