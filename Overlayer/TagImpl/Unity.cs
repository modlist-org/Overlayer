using Overlayer.Tag.Core;
using UnityEngine.SceneManagement;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
#endif

namespace Overlayer.TagImpl;

public static class Unity {
    private static string _cachedSceneName = string.Empty;

    static Unity() {
        SceneManager.sceneLoaded +=
#if IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<Scene, LoadSceneMode>>(
                new Action<Scene, LoadSceneMode>(
#endif
                    (scene, mode) => _cachedSceneName = scene.name
#if IL2CPP
                )
            )
#endif
            ;

        _cachedSceneName = SceneManager.GetActiveScene().name;
    }

    [Tag(Desc = "[Unity] Name of the currently active Unity scene")]
    public static string SceneName => _cachedSceneName;
    
    [Tag(Desc = "[Unity] Current screen width in pixels")]
    public static int ScreenWidth => UnityEngine.Screen.width;
    
    [Tag(Desc = "[Unity] Current screen height in pixels")]
    public static int ScreenHeight => UnityEngine.Screen.height;
    
    [Tag(Desc = "[Unity] Current target refresh rate of the monitor")]
    public static int RefreshRate => (int)UnityEngine.Screen.currentResolution.refreshRateRatio.value;
    
    [Tag(Desc = "[Unity] Returns true if the game is in fullscreen mode")]
    public static bool IsFullScreen => UnityEngine.Screen.fullScreen;
    
    [Tag(Desc = "[Unity] Current Time.timeScale value")]
    public static float TimeScale => UnityEngine.Time.timeScale;
    
    [Tag(Desc = "[Unity] Total time elapsed since game start, in seconds")]
    public static double TimeSinceStart => UnityEngine.Time.unscaledTimeAsDouble;
    
    [Tag(Desc = "[Unity] Time in seconds since the last frame\n(affected by timeScale)")]
    public static float DeltaTime => UnityEngine.Time.deltaTime;

    [Tag(Desc = "[Unity] Unscaled time in seconds since the last frame\n(unaffected by timeScale)")]
    public static float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;

    [Tag(Desc = "[Unity] Time in seconds since the start of the frame\n(affected by timeScale)")]
    public static double Time => UnityEngine.Time.timeAsDouble;

    [Tag(Desc = "[Unity] Interval in seconds at which physics\nand other fixed frame rate updates are performed")]
    public static float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;

    [Tag(Desc = "[Unity] Time in seconds since the last fixed update\n(affected by timeScale)")]
    public static float FixedTime => UnityEngine.Time.fixedTime;

    [Tag(Desc = "[Unity] Total number of frames that have passed since game start")]
    public static int FrameCount => UnityEngine.Time.frameCount;

    [Tag(Desc = "[Unity] Maximum time a frame can take\n(prevents physics spiral of death)")]
    public static float MaximumDeltaTime => UnityEngine.Time.maximumDeltaTime;
    
    [Tag(Desc = "[Unity] Returns true if the application window currently has focus")]
    public static bool IsFocused => UnityEngine.Application.isFocused;

    [Tag(Desc = "[Unity] Unity engine version")]
    public static string UnityVersion => UnityEngine.Application.unityVersion;
    
    [Tag(Desc = "[Unity] Current mouse X position in screen coordinates")]
    public static float MouseX => UnityEngine.Input.mousePosition.x;

    [Tag(Desc = "[Unity] Current mouse Y position in screen coordinates")]
    public static float MouseY => UnityEngine.Input.mousePosition.y;

    [Tag(Desc = "[Unity] Returns true if the mouse cursor is currently visible")]
    public static bool IsCursorVisible => UnityEngine.Cursor.visible;

    [Tag(Desc = "[Unity] Current cursor lock state (None, Locked, Confined)")]
    public static string CursorLockState => UnityEngine.Cursor.lockState.ToString();
    
    [Tag(Desc = "[Unity] Currently applied Quality Level index")]
    public static int QualityLevel => UnityEngine.QualitySettings.GetQualityLevel();

    [Tag(Desc = "[Unity] Currently applied Quality Level name")]
    public static string QualityName => UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()];

    [Tag(Desc = "[Unity] VSync count setting\n(0: Disabled, 1: Every VSync, 2: Every second VSync)")]
    public static int VSyncCount => UnityEngine.QualitySettings.vSyncCount;

    [Tag(Desc = "[Unity] Target frame rate set in QualitySettings (-1 if uncapped)")]
    public static int TargetFrameRate => UnityEngine.Application.targetFrameRate;

    [Tag(Desc = "[Unity] DPI (dots per inch) of the current screen")]
    public static float ScreenDpi => UnityEngine.Screen.dpi;
    
    [Tag(Desc = "[Unity] Master volume of the AudioListener (0.0 to 1.0)")]
    public static float MasterVolume => UnityEngine.AudioListener.volume;

    [Tag(Desc = "[Unity] Returns true if all audio in the game is currently muted")]
    public static bool IsAudioMuted => UnityEngine.AudioListener.pause;
    
    [Tag(Desc = "[Unity] Operating system version and build info")]
    public static string OperatingSystem => UnityEngine.SystemInfo.operatingSystem;

    [Tag(Desc = "[Unity] Device model name")]
    public static string DeviceModel => UnityEngine.SystemInfo.deviceModel;

    [Tag(Desc = "[Unity] Graphics API type (Direct3D11, Vulkan, Metal, etc.)")]
    public static string GraphicsDeviceType => UnityEngine.SystemInfo.graphicsDeviceType.ToString();
    
    [Tag(Desc = "[Unity] Max supported texture size in pixels")]
    public static int MaxTextureSize => UnityEngine.SystemInfo.maxTextureSize;
}