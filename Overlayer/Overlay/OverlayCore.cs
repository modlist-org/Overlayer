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
    // Top-level 3D objects (no canvas). Saved under Objects/*.json.
    // shortcut: excluded from .o5cp export; extend O5cpExporter to include them.
    public static readonly List<OvObject> Objects = [];

    private static string SaveDir => Path.Combine(MainCore.Paths?.RootPath ?? Path.Combine(Environment.CurrentDirectory, "UserData", "Overlayer"), "Canvases");
    private static string ObjectSaveDir => Path.Combine(SaveDir, "Objects");
    public static string ExportDir => Path.Combine(SaveDir, "Export");
    private static int pendingLayoutRefreshes;
    // Files of canvases/objects the user deleted. Saving removes only these:
    // a file is never deleted just because its canvas is missing from the
    // list (failed load, scene-wipe reload, partial init), which used to wipe
    // every saved canvas on the next save.
    private static readonly HashSet<string> deletedFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> deletedObjectFiles = new(StringComparer.OrdinalIgnoreCase);

    // Fired whenever the canvas list changes (create/import/clone/delete),
    // including changes made programmatically outside the canvas UI
    // (e.g. module importers), so tile lists can refresh themselves.
    public static event Action OnCanvasesChanged;
    public static event Action OnObjectsChanged;

    private static void NotifyChanged() {
        try {
            OnCanvasesChanged?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Canvas change notification failed: {e.Message}");
        }
    }

    private static void NotifyObjects() {
        try {
            OnObjectsChanged?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Object change notification failed: {e.Message}");
        }
    }

    public static int GetCanvasIndex(OvCanvas canvas)
        => canvas == null ? -1 : Canvases.IndexOf(canvas);

    public static void Initialize(GameObject parent) {
        if (parent == null || Core != null) {
            return;
        }

        FxConverters.RegisterDefaultConverters();

        Core = new GameObject(nameof(OverlayCore));
        Core.transform.SetParent(parent.transform, false);

        LoadAllCanvases();
        LoadAllObjects();
        PackageStore.Initialize();
        PresetStore.Initialize();
    }

    /// <summary>
    /// Rebuilds overlay state after a scene wipe destroyed our objects.
    /// Drops dead canvases (reloaded from disk) and recreates the core.
    /// </summary>
    public static void Reinitialize(GameObject parent) {
        for (int i = Canvases.Count - 1; i >= 0; i--) {
            try {
                var c = Canvases[i];
                if (c == null || c.GameObject == null) {
                    try {
                        c?.Dispose();
                    } catch {
                    }
                    Canvases.RemoveAt(i);
                }
            } catch {
                try {
                    Canvases.RemoveAt(i);
                } catch {
                }
            }
        }
        for (int i = Objects.Count - 1; i >= 0; i--) {
            try {
                var o = Objects[i];
                if (o == null || o.GameObject == null) {
                    try {
                        o?.Dispose();
                    } catch {
                    }
                    Objects.RemoveAt(i);
                }
            } catch {
                try {
                    Objects.RemoveAt(i);
                } catch {
                }
            }
        }
        try {
            Core = null;
        } catch {
        }
        pendingLayoutRefreshes = 0;
        Initialize(parent);
    }

    public static OvCanvas CreateOvCanvas() {
        var canvas = new OvCanvas();
        canvas.RectTransform.SetParent(Transform, false);
        Canvases.Add(canvas);
        NotifyChanged();
        return canvas;
    }

    public static OvObject CreateEmptyObject() {
        var obj = new OvObject(bare: true);
        obj.GameObject.transform.SetParent(Transform, false);
        obj.Config.Name.Value = "EmptyObject";
        obj.ApplyComponent();
        obj.ApplyConfig();
        Objects.Add(obj);
        NotifyObjects();
        return obj;
    }

    public static OvObject CloneObject(OvObject source) {
        if (source == null) {
            return null;
        }
        try {
            var token = Newtonsoft.Json.Linq.JToken.Parse(source.Serialize().ToString());
            var obj = new OvObject(bare: token["Config"]?["RectTransformConfig"] == null);
            obj.Deserialize(token);
            if (!obj.Config.Name.Value.EndsWith(" Copy")) {
                obj.Config.Name.Value = $"{obj.Config.Name.Value} Copy";
            }
            obj.GameObject.transform.SetParent(Transform, false);
            obj.ApplyComponent();
            obj.ApplyConfig();
            int index = Objects.IndexOf(source);
            Objects.Insert(index >= 0 ? index + 1 : Objects.Count, obj);
            for (int i = 0; i < Objects.Count; i++) {
                Objects[i].GameObject.transform.SetSiblingIndex(i);
            }
            SaveAllObjects();
            NotifyObjects();
            return obj;
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to clone object: {e.Message}");
            return null;
        }
    }

    public static bool DeleteObject(OvObject obj) {
        if (obj == null || !Objects.Remove(obj)) {
            return false;
        }
        if (!string.IsNullOrEmpty(obj.SourceFile)) {
            deletedObjectFiles.Add(Path.GetFileName(obj.SourceFile));
        }

        obj.Dispose();
        SaveAllObjects();
        NotifyObjects();
        return true;
    }

    public static string ExportCanvas(OvCanvas canvas, string filePath) {
        if (canvas == null || string.IsNullOrWhiteSpace(filePath)) {
            return null;
        }
        if (filePath.EndsWith(".o5cp", StringComparison.OrdinalIgnoreCase)) {
            return ExportPackage(canvas, filePath, null);
        }
        try {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, canvas.Serialize().ToString());
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Exported canvas '{canvas.Config.Name.Value}' to {filePath}");
            return filePath;
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to export canvas: {e.Message}");
            return null;
        }
    }

    public static string ExportPackage(OvCanvas canvas, string filePath, O5cpExportOptions options) {
        if (canvas == null || string.IsNullOrWhiteSpace(filePath)) {
            return null;
        }
        try {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            var result = O5cpExporter.Export(canvas, filePath, options);
            if (result == null) {
                return null;
            }
            foreach (string warning in result.Warnings) {
                MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Export warning: {warning}");
            }
            MainCore.Log.Msg($"[{nameof(OverlayCore)}] Exported package '{canvas.Config.Name.Value}' to {filePath}");
            return filePath;
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to export package: {e.Message}");
            return null;
        }
    }

    public static bool ImportCanvas(string filePath) {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
            return false;
        }
        if (filePath.EndsWith(".o5cp", StringComparison.OrdinalIgnoreCase)) {
            var entry = PackageStore.Install(filePath, out var warnings);
            foreach (string warning in warnings) {
                MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Import warning: {warning}");
            }
            return entry != null;
        }
        try {
            var wrapper = new SettingsFile<OvCanvas>(filePath);
            if (!wrapper.Load()) {
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
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to import canvas: {e.Message}");
            return false;
        }
    }

    public static OvCanvas PromotePackage(PackageEntry entry, O5cpPromotePlan plan, out List<string> warnings) {
        warnings = [];
        if (entry == null || plan == null) {
            return null;
        }
        var canvas = O5cpPromote.Apply(entry, plan, out warnings);
        if (canvas == null) {
            return null;
        }
        canvas.RectTransform.SetParent(Transform, false);
        canvas.ApplyConfig();
        canvas.RefreshLayouts();
        Canvases.Add(canvas);
        SaveAllCanvases();
        foreach (string warning in warnings) {
            MainCore.Log.Wrn($"[{nameof(OverlayCore)}] Promote warning: {warning}");
        }
        MainCore.Log.Msg($"[{nameof(OverlayCore)}] Promoted package '{entry.Manifest.Package.Name}' to editable canvas");
        NotifyChanged();
        return canvas;
    }

    public static OvCanvas CloneCanvas(OvCanvas source) {
        if (source == null) {
            return null;
        }
        try {
            var token = Newtonsoft.Json.Linq.JToken.Parse(source.Serialize().ToString());
            var canvas = new OvCanvas();
            canvas.Deserialize(token);
            if (!canvas.Config.Name.Value.EndsWith(" Copy")) {
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
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to clone canvas: {e.Message}");
            return null;
        }
    }

    public static bool DeleteOvCanvas(OvCanvas canvas) {
        if (canvas == null || !Canvases.Remove(canvas)) {
            return false;
        }
        if (!string.IsNullOrEmpty(canvas.SourceFile)) {
            deletedFiles.Add(Path.GetFileName(canvas.SourceFile));
        }

        canvas.Dispose();
        SaveAllCanvases();
        NotifyChanged();
        return true;
    }

    private static void LoadAllCanvases() {
        if (!Directory.Exists(SaveDir)) {
            return;
        }

        var files = Directory.GetFiles(SaveDir, "*.json").OrderBy(Path.GetFileName);

        foreach (var file in files) {
            var wrapper = new SettingsFile<OvCanvas>(file);

            if (wrapper.Load()) {
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
        foreach (var canvas in Canvases) {
            canvas?.RefreshFx();
        }
        foreach (var obj in Objects) {
            obj?.RefreshFx();
        }
        PackageStore.Tick();

        if (pendingLayoutRefreshes <= 0 || Core == null) {
            return;
        }

        pendingLayoutRefreshes--;
        foreach (var canvas in Canvases) {
            canvas.RefreshLayouts();
        }
    }

    public static void SaveAllCanvases() {
        try {
            if (!Directory.Exists(SaveDir)) {
                Directory.CreateDirectory(SaveDir);
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int autoIndex = 0;
            foreach (var canvas in Canvases) {
                if (canvas == null || canvas.IsPackage) {
                    continue;
                }
                string fileName = null;
                if (!string.IsNullOrEmpty(canvas?.SourceFile)) {
                    string sourceName = Path.GetFileName(canvas.SourceFile);
                    if (!string.IsNullOrEmpty(sourceName) && !usedNames.Contains(sourceName)) {
                        fileName = sourceName;
                    }
                }
                while (fileName == null) {
                    string candidate = $"Canvas{autoIndex}.json";
                    autoIndex++;
                    if (!usedNames.Contains(candidate)) {
                        fileName = candidate;
                    }
                }
                usedNames.Add(fileName);
                string filePath = Path.Combine(SaveDir, fileName);
                File.WriteAllText(filePath, canvas.Serialize().ToString());
                canvas.SourceFile = filePath;
            }

            DeleteRemoved(SaveDir, deletedFiles, usedNames);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to save all canvases: {e}");
        }
    }

    public static void SaveAllObjects() {
        try {
            if (Objects.Count == 0 && !Directory.Exists(ObjectSaveDir)) {
                return;
            }
            if (!Directory.Exists(ObjectSaveDir)) {
                Directory.CreateDirectory(ObjectSaveDir);
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int autoIndex = 0;
            foreach (var obj in Objects) {
                if (obj == null) {
                    continue;
                }
                string fileName = null;
                if (!string.IsNullOrEmpty(obj?.SourceFile)) {
                    string sourceName = Path.GetFileName(obj.SourceFile);
                    if (!string.IsNullOrEmpty(sourceName) && !usedNames.Contains(sourceName)) {
                        fileName = sourceName;
                    }
                }
                while (fileName == null) {
                    string candidate = $"Object{autoIndex}.json";
                    autoIndex++;
                    if (!usedNames.Contains(candidate)) {
                        fileName = candidate;
                    }
                }
                usedNames.Add(fileName);
                string filePath = Path.Combine(ObjectSaveDir, fileName);
                File.WriteAllText(filePath, obj.Serialize().ToString());
                obj.SourceFile = filePath;
            }

            DeleteRemoved(ObjectSaveDir, deletedObjectFiles, usedNames);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(OverlayCore)}] Failed to save all objects: {e}");
        }
    }

    private static void DeleteRemoved(string dir, HashSet<string> deleted, HashSet<string> usedNames) {
        foreach (string name in deleted) {
            if (!usedNames.Contains(name)) {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }
        deleted.Clear();
    }

    private static void LoadAllObjects() {
        if (!Directory.Exists(ObjectSaveDir)) {
            return;
        }

        var files = Directory.GetFiles(ObjectSaveDir, "*.json").OrderBy(Path.GetFileName);

        foreach (var file in files) {
            var wrapper = new SettingsFile<OvObject>(file);

            if (wrapper.Load()) {
                var obj = wrapper.Data;
                obj.SourceFile = file;
                obj.GameObject.transform.SetParent(Transform, false);
                obj.ApplyComponent();
                obj.ApplyConfig();
                Objects.Add(obj);
            } else {
                wrapper.Dispose();
            }
        }
    }

    public static void Dispose() {
        if (Core == null) {
            return;
        }

        SaveAllCanvases();
        SaveAllObjects();
        PackageStore.UnloadAll();

        for (int i = Canvases.Count - 1; i >= 0; i--) {
            Canvases[i].Dispose();
        }

        Canvases.Clear();

        for (int i = Objects.Count - 1; i >= 0; i--) {
            Objects[i].Dispose();
        }

        Objects.Clear();
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
