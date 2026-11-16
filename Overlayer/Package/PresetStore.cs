using Newtonsoft.Json.Linq;
using Overlayer.Core;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Package;

public sealed class PackagePreset {
    public string Id;
    public string Name;
    public string Module;
    /// <summary>Optional grouping in pickers, e.g. <see cref="HudPreset.ElementCategory"/>.</summary>
    public string Category;
    public Func<byte[]> ReadBytes;
}

public static class PresetStore {
    public static event Action OnChanged;

    private static readonly Dictionary<string, PackagePreset> byId = [];

    public static IReadOnlyList<PackagePreset> Presets => [
        ..byId.Values
        .OrderBy(p => string.IsNullOrEmpty(p.Module) ? 1 : 0)
        .ThenBy(p => p.Module ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
    ];

    public static void Register(PackagePreset preset) {
        if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || preset.ReadBytes == null) {
            return;
        }
        byId[preset.Id] = preset;
        NotifyChanged();
    }

    public static void Unregister(string id) {
        if (byId.Remove(id)) {
            NotifyChanged();
        }
    }

    public static void UnregisterByModule(string module) {
        bool removed = false;
        foreach (string id in byId.Keys.ToArray()) {
            if (byId.TryGetValue(id, out var preset)
                && string.Equals(preset.Module, module, StringComparison.Ordinal)) {
                byId.Remove(id);
                removed = true;
            }
        }
        if (removed) {
            NotifyChanged();
        }
    }

    public static void Initialize() {
        try {
            Directory.CreateDirectory(MainCore.Paths.PresetsPath);
        } catch {
        }
        RegisterBuiltIn();
        RefreshFilePresets();
    }

    // Game-independent starters; modules add their own (accuracy, combo, ...).
    private static void RegisterBuiltIn() {
        const string module = "Overlayer";
        Register(HudPreset.Preset("overlayer:fps", "FPS", HudPreset.ElementCategory, module, "Frames per second",
            () => [HudPreset.Text("FPS", "{Fps:500,0} FPS", 28f, new Vector2(1f, 1f), new Vector2(-24f, -20f), TextAlignmentOptions.TopRight)]));
        Register(HudPreset.Preset("overlayer:clock", "Clock", HudPreset.ElementCategory, module, "Current time",
            () => [HudPreset.Text("Clock", "{Hour:00}:{Minute:00}", 28f, new Vector2(1f, 1f), new Vector2(-24f, -60f), TextAlignmentOptions.TopRight)]));
    }

    public static void RefreshFilePresets() {
        foreach (string id in byId.Keys.ToArray()) {
            if (byId.TryGetValue(id, out var preset) && string.IsNullOrEmpty(preset.Module)) {
                byId.Remove(id);
            }
        }
        string dir;
        try {
            dir = MainCore.Paths.PresetsPath;
            if (!Directory.Exists(dir)) {
                return;
            }
        } catch {
            return;
        }
        foreach (string file in Directory.GetFiles(dir, "*.o5cp").OrderBy(Path.GetFileName)) {
            try {
                string name = null;
                try {
                    string text = PackageStore.ReadZipText(file, O5cpFormat.ManifestFile);
                    if (text != null && O5cpManifest.TryParse(JToken.Parse(text), out var manifest, out _)) {
                        name = manifest.Package.Name;
                    }
                } catch {
                }
                if (string.IsNullOrWhiteSpace(name)) {
                    name = Path.GetFileNameWithoutExtension(file);
                }
                string captured = file;
                byId["file:" + Path.GetFileName(file)] = new PackagePreset {
                    Id = "file:" + Path.GetFileName(file),
                    Name = name,
                    Module = string.Empty,
                    ReadBytes = () => File.ReadAllBytes(captured),
                };
            } catch {
            }
        }
        NotifyChanged();
    }

    private static void NotifyChanged() {
        try {
            OnChanged?.Invoke();
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(PresetStore)}] Change notification failed: {e.Message}");
        }
    }
}
