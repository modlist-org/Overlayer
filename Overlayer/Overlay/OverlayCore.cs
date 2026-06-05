using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public static class OverlayCore {
    public static GameObject Core { get; private set; }
    public static Transform Transform => Core.transform;

    public static readonly List<OvCanvas> Canvases = [];

    private static readonly string SaveDir = Path.Combine(MainCore.Paths.RootPath, "Canvases");

    public const int LAYER = 5;

    public static void Initialize(GameObject parent) {
        if(parent == null || Core != null) {
            return;
        }

        Core = new GameObject(nameof(OverlayCore)) {
            layer = LAYER
        };
        Core.transform.SetParent(parent.transform, false);

        LoadAllCanvases();
    }

    public static OvCanvas CreateOvCanvas() {
        var canvas = new OvCanvas();
        canvas.RectTransform.SetParent(Transform, false);
        Canvases.Add(canvas);
        return canvas;
    }

    private static void LoadAllCanvases() {
        if(!Directory.Exists(SaveDir)) {
            return;
        }

        var files = Directory.GetFiles(SaveDir, "*.json").OrderBy(Path.GetFileName);

        foreach(var file in files) {
            var settingsWrapper = new SettingsFile<OvCanvas>(file);

            if(settingsWrapper.Load()) {
                var loadedCanvas = settingsWrapper.Data;

                loadedCanvas.RectTransform.SetParent(Transform, false);

                var fullToken = new JObject {
                    [nameof(OvCanvas.Config)] = loadedCanvas.Config.Serialize(),
                    [nameof(OvCanvas.OvObjects)] = new JArray(loadedCanvas.OvObjects.Select(x => x.Serialize()))
                };

                try {
                    var rawToken = JToken.Parse(File.ReadAllText(file));
                    loadedCanvas.Deserialize(rawToken);
                } catch(Exception ex) {
                    MainCore.Logger.Err($"[{nameof(OverlayCore)}] Failed to deserialize canvas from {file}: {ex}");
                }

                Canvases.Add(loadedCanvas);
            } else {
                settingsWrapper.Dispose();
            }
        }
    }
    private static void SaveAllCanvases() {
        try {
            if(Directory.Exists(SaveDir)) {
                var oldFiles = Directory.GetFiles(SaveDir, "Canvas*.json");
                foreach(var f in oldFiles) {
                    File.Delete(f);
                }
            } else {
                Directory.CreateDirectory(SaveDir);
            }

            for(int i = 0; i < Canvases.Count; i++) {
                string filePath = Path.Combine(SaveDir, $"Canvas{i}.json");

                var settingsWrapper = new SettingsFile<OvCanvas>(filePath);

                settingsWrapper.Data.Config = Canvases[i].Config;

                settingsWrapper.Data.OvObjects.Clear();
                foreach(var obj in Canvases[i].OvObjects) {
                    settingsWrapper.Data.OvObjects.Add(obj);
                }

                settingsWrapper.Save();
            }
        } catch(Exception e) {
            MainCore.Logger.Err($"[{nameof(OverlayCore)}] Failed to save all canvases: {e}");
        }
    }

    public static void Dispose() {
        if(Core == null) {
            return;
        }

        SaveAllCanvases();

        for(int i = Canvases.Count - 1; i >= 0; i--) {
            Canvases[i].Dispose();
        }

        Canvases.Clear();

        Object.Destroy(Core);
        Core = null;
    }
}
