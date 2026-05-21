using Overlayer.Compat;
using Overlayer.Compat.Interface;
using Overlayer.Core.Service;
using Overlayer.IO;
using Overlayer.Localization;
using Overlayer.Resource;
using System.Reflection;

namespace Overlayer.Core;

public static class MainCore {
    public static OverlayerRuntime Runtime { get; private set; }

    public static event Action<bool, bool> OnModEnabledChanged {
        add => Runtime.OnModEnabledChanged += value;
        remove => Runtime.OnModEnabledChanged -= value;
    }

    public static Version Version => Runtime.Version;
    public static Assembly Assembly => Runtime.Assembly;
    public static OverlayerLogger Logger => Runtime.Logger;
    public static PathService Paths => Runtime.Paths;
    public static SettingsFile<CoreSettings> ConfigFile => Runtime.Config;
    public static CoreSettings Config => Runtime.Config.Data;
    public static Translator Tr => Runtime.Localization.Translator;
    public static ResourceManager Res => Runtime.Resource;
    public static SpriteManager Spr => Runtime.Sprite;

    public static IOverlayerHost Host => Runtime.Host;

    public static UnityEngine.GameObject Root => Runtime.RootObject;

    public static bool IsModEnabled => Runtime.State.IsEnabled;

    public static void Initialize(IOverlayerHost host) {
        if(Runtime != null) {
            return;
        }

        Runtime = new OverlayerRuntime(host);

        Runtime.Initialize();
    }

    public static void Tick() => Runtime?.Tick();

    public static void Dispose() {
        if(Runtime == null) {
            return;
        }

        Runtime.Dispose();
        Runtime = null;
    }

    public static void SetModEnabled(bool enabled) => Runtime.SetModEnabled(enabled, false);
}