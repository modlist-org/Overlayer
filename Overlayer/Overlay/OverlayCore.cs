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

    public static int GetCanvasIndex(OvCanvas canvas)
        => canvas == null ? -1 : Canvases.IndexOf(canvas);

    public static void Initialize(GameObject parent) {
        if(parent == null || Core != null) {
            return;
        }

        Core = new GameObject(nameof(OverlayCore));
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
            var wrapper = new SettingsFile<OvCanvas>(file);

            if(wrapper.Load()) {
                var canvas = wrapper.Data;
                canvas.RectTransform.SetParent(Transform, false);
                canvas.ApplyConfig();
                Canvases.Add(canvas);
            }
        }
    }

    private static void SaveAllCanvases() {
        try {
            if(!Directory.Exists(SaveDir)) {
                Directory.CreateDirectory(SaveDir);
            }

            for(int i = 0; i < Canvases.Count; i++) {
                string filePath = Path.Combine(SaveDir, $"Canvas{i}.json");
                var wrapper = new SettingsFile<OvCanvas>(filePath);

                wrapper.Data.Config = Canvases[i].Config.Copy();
                wrapper.Data.OvObjects.Clear();
                wrapper.Data.OvObjects.AddRange(Canvases[i].OvObjects);

                wrapper.Save();
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to save all canvases: {e}");
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
