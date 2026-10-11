using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlayer.Core;

public class CameraManager {
    private Camera cachedCamera;

    public Func<Camera> CustomCameraProvider { get; set; }

    public event Action<Camera> OnCameraChanged;

    public CameraManager() {
        SceneManager.sceneLoaded +=
#if ML && IL2CPP
            new Action<Scene, LoadSceneMode>(OnSceneLoaded);
#else
            OnSceneLoaded;
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        UpdateCamera();
    }

    public void SetCamera(Camera camera) {
        if (cachedCamera == camera) return;
        cachedCamera = camera;
        OnCameraChanged?.Invoke(cachedCamera);
    }

    public Camera Camera {
        get {
            if (cachedCamera == null) {
                UpdateCamera();
            }
            return cachedCamera;
        }
    }

    public Camera UpdateCamera() {
        var found = (CustomCameraProvider?.Invoke()) ?? Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
        SetCamera(found);
        return cachedCamera;
    }

    public void Reset() {
        SceneManager.sceneLoaded -=
#if ML && IL2CPP
            new Action<Scene, LoadSceneMode>(OnSceneLoaded);
#else
            OnSceneLoaded;
#endif
        cachedCamera = null;
        CustomCameraProvider = null;
        OnCameraChanged = null;
    }
}