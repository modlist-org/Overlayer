using MelonLoader;
using Overlayer.Core;
using Overlayer.Compat.Interface;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(Overlayer.Loader.ML.Loader), Overlayer.Core.Info.Name,  Overlayer.Core.Info.Version,  Overlayer.Core.Info.Author,  Overlayer.Core.Info.GithubLink)]

namespace Overlayer.Loader.ML;

public class Loader : MelonMod, IOverlayerHost, IOverlayerLogger {

    public IOverlayerLogger OverlayerLogger => this;

    public string OverlayerFilePath => MelonEnvironment.UserDataDirectory;

    public string OverlayerDLLPath => MelonEnvironment.UserLibsDirectory;

    public override void OnInitializeMelon() {
        MainCore.Initialize(this);
    }

    public override void OnDeinitializeMelon() {
        MainCore.Dispose();
    }

    public override void OnUpdate() {
        MainCore.Tick();
    }

    public void OverlayerMsg(string msg) => MelonLogger.Msg(msg);
    public void OverlayerWrn(string msg) => MelonLogger.Warning(msg);
    public void OverlayerErr(string msg) => MelonLogger.Error(msg);
}