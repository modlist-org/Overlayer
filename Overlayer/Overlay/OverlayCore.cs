using Overlayer.Core;
using Overlayer.IO;
using Overlayer.IO.Fx;
using Overlayer.Package;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overlayer.Overlay;

public static class OverlayCore {
    public static GameObject Core { get; private set; }
    public static Transform Transform => Core.transform;

    public static readonly List<OvCanvas> Canvases = [];

    private static readonly string SaveDir = Path.Combine(MainCore.Paths.RootPath, "Canvases");
    public static string ExportDir => Path.Combine(SaveDir, "Export");
    private static int pendingLayoutRefreshes;
    private static readonly HashSet<string> knownFiles = new(StringComparer.OrdinalIgnoreCase);

    // Fired whenever the canvas list changes (create/import/clone/delete),
    // including changes made programmatically outside the canvas UI
    // (e.g. module importers), so tile lists can refresh themselves.
    public static event Action OnCanvasesChanged;

    private static void NotifyChanged() {
        try {
            OnCanvasesChanged?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Canvas change notification failed: {e.Message}");
        }
    }

    public static int GetCanvasIndex(OvCanvas canvas)
        => canvas == null ? -1 : Canvases.IndexOf(canvas);

    public static void Initialize(GameObject parent) {
        if(parent == null || Core != null) {
            return;
        }

        FxConverters.RegisterDefaultConverters();

        Core = new GameObject(nameof(OverlayCore));
        Core.transform.SetParent(parent.transform, false);

        LoadAllCanvases();
        PackageStore.Initialize();
    }

    public static OvCanvas CreateOvCanvas() {
        var canvas = new OvCanvas();
        canvas.RectTransform.SetParent(Transform, false);
        Canvases.Add(canvas);
        NotifyChanged();
        return canvas;
    }

    public static string ExportCanvas(OvCanvas canvas, string filePath) {
        if(canvas == null || string.IsNullOrWhiteSpace(filePath)) {
            return null;
        }
        if(filePath.EndsWith(".o5cp", StringComparison.OrdinalIgnoreCase)) {
            return ExportPackage(canvas, filePath, null);
        }
        try {
            string dir = Path.GetDirectoryName(filePath);
            if(!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, canvas.Serialize().ToString());
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Exported canvas '{canvas.Config.Name.Value}' to {filePath}");
            return filePath;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to export canvas: {e.Message}");
            return null;
        }
    }

    public static string ExportPackage(OvCanvas canvas, string filePath, O5cpExportOptions options) {
        if(canvas == null || string.IsNullOrWhiteSpace(filePath)) {
            return null;
        }
        try {
            string dir = Path.GetDirectoryName(filePath);
            if(!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            var result = O5cpExporter.Export(canvas, filePath, options);
            if(result == null) {
                return null;
            }
            foreach(string warning in result.Warnings) {
                MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Export warning: {warning}");
            }
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Exported package '{canvas.Config.Name.Value}' to {filePath}");
            return filePath;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to export package: {e.Message}");
            return null;
        }
    }

    public static bool ImportCanvas(string filePath) {
        if(string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
            return false;
        }
        if(filePath.EndsWith(".o5cp", StringComparison.OrdinalIgnoreCase)) {
            var entry = PackageStore.Install(filePath, out var warnings);
            foreach(string warning in warnings) {
                MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Import warning: {warning}");
            }
            return entry != null;
        }
        try {
            var wrapper = new SettingsFile<OvCanvas>(filePath);
            if(!wrapper.Load()) {
                wrapper.Dispose();
                MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to import canvas: bad file {filePath}");
                return false;
            }
            var canvas = wrapper.Data;
            canvas.RectTransform.SetParent(Transform, false);
            canvas.ApplyConfig();
            canvas.RefreshLayouts();
            Canvases.Add(canvas);
            SaveAllCanvases();
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Imported canvas '{canvas.Config.Name.Value}' from {filePath}");
            NotifyChanged();
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to import canvas: {e.Message}");
            return false;
        }
    }

    public static OvCanvas PromotePackage(PackageEntry entry, O5cpPromotePlan plan, out List<string> warnings) {
        warnings = [];
        if(entry == null || plan == null) {
            return null;
        }
        var canvas = O5cpPromote.Apply(entry, plan, out warnings);
        if(canvas == null) {
            return null;
        }
        canvas.RectTransform.SetParent(Transform, false);
        canvas.ApplyConfig();
        canvas.RefreshLayouts();
        Canvases.Add(canvas);
        SaveAllCanvases();
        foreach(string warning in warnings) {
            MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Promote warning: {warning}");
        }
        MainCore.Log.Msg($"[{nameof(OverlayCore)}] Promoted package '{entry.Manifest.Package.Name}' to editable canvas");
        NotifyChanged();
        return canvas;
    }

    public static OvCanvas CloneCanvas(OvCanvas source) {
        if(source == null) {
            return null;
        }
        try {
            var token = Newtonsoft.Json.Linq.JToken.Parse(source.Serialize().ToString());
            var canvas = new OvCanvas();
            canvas.Deserialize(token);
            if(!canvas.Config.Name.Value.EndsWith(" Copy")) {
                canvas.Config.Name.Value = $"{canvas.Config.Name.Value} Copy";
            }
            canvas.RectTransform.SetParent(Transform, false);
            canvas.ApplyConfig();
            canvas.RefreshLayouts();
            Canvases.Add(canvas);
            SaveAllCanvases();
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Cloned canvas '{source.Config.Name.Value}'");
            NotifyChanged();
            return canvas;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to clone canvas: {e.Message}");
            return null;
        }
    }

    public static bool DeleteOvCanvas(OvCanvas canvas) {
        if(canvas == null || !Canvases.Remove(canvas)) {
            return false;
        }

        canvas.Dispose();
        SaveAllCanvases();
        NotifyChanged();
        return true;
    }

    private static void LoadAllCanvases() {
        if(!Directory.Exists(SaveDir)) {
            return;
        }

        var files = Directory.GetFiles(SaveDir, "*.json").OrderBy(Path.GetFileName);

        foreach(var file in files) {
            knownFiles.Add(Path.GetFileName(file));
            var wrapper = new SettingsFile<OvCanvas>(file);

            if(wrapper.Load()) {
                var canvas = wrapper.Data;
                canvas.SourceFile = file;
                canvas.RectTransform.SetParent(Transform, false);
                canvas.ApplyConfig();
                canvas.RefreshLayouts();
                Canvases.Add(canvas);
            } else {
                wrapper.Dispose();
            }
        }
    }

    public static void RequestLayoutRefresh() => pendingLayoutRefreshes = Math.Max(pendingLayoutRefreshes, 3);

    public static void Tick() {
        foreach(var canvas in Canvases) {
            canvas?.RefreshFx();
        }
        PackageStore.Tick();

        if(pendingLayoutRefreshes <= 0 || Core == null) {
            return;
        }

        pendingLayoutRefreshes--;
        foreach(var canvas in Canvases) {
            canvas.RefreshLayouts();
        }
    }

    public static void SaveAllCanvases() {
        try {
            if(!Directory.Exists(SaveDir)) {
                Directory.CreateDirectory(SaveDir);
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int autoIndex = 0;
            foreach(var canvas in Canvases) {
                if(canvas == null || canvas.IsPackage) {
                    continue;
                }
                string fileName = null;
                if(!string.IsNullOrEmpty(canvas?.SourceFile)) {
                    string sourceName = Path.GetFileName(canvas.SourceFile);
                    if(!string.IsNullOrEmpty(sourceName) && !usedNames.Contains(sourceName)) {
                        fileName = sourceName;
                    }
                }
                while(fileName == null) {
                    string candidate = $"Canvas{autoIndex}.json";
                    autoIndex++;
                    if(!usedNames.Contains(candidate)) {
                        fileName = candidate;
                    }
                }
                usedNames.Add(fileName);
                string filePath = Path.Combine(SaveDir, fileName);
                File.WriteAllText(filePath, canvas.Serialize().ToString());
                canvas.SourceFile = filePath;
            }

            foreach(string staleFile in Directory.GetFiles(SaveDir, "*.json")) {
                string staleName = Path.GetFileName(staleFile);
                if(!usedNames.Contains(staleName) && knownFiles.Contains(staleName)) {
                    File.Delete(staleFile);
                }
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
        PackageStore.UnloadAll();

        for(int i = Canvases.Count - 1; i >= 0; i--) {
            Canvases[i].Dispose();
        }

        Canvases.Clear();
        pendingLayoutRefreshes = 0;

        GameObject core = Core;
        Core = null;
        Object.Destroy(core);

        // NOTE: Do NOT clear Fx converters here. They are stateless pure
        // functions, and settings can still be saved after dispose
        // (e.g. UserResources). Without the raw writers, serializing Unity
        // structs like Rect/Vector2 falls back to JToken.FromObject and
        // crashes with a self-referencing loop (Rect.position ->
        // Vector2.normalized -> ...).
    }
}
