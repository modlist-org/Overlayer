using Overlayer.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Overlayer.UI.Easing;

// Saved curves ("favorites"), kept in Easings.json next to Settings.json as [{ "Name", "Curve" }].
internal static class EaseLibrary {
    public sealed class Entry(string name, string curve) {
        public string Name = name;
        public string Curve = curve;
    }

    private static List<Entry> entries;

    private static string FilePath => Path.Combine(MainCore.Paths.RootPath, "Easings.json");

    public static IReadOnlyList<Entry> Entries {
        get {
            if (entries == null) {
                Load();
            }
            return entries;
        }
    }

    private static void Load() {
        entries = [];
        try {
            if (!File.Exists(FilePath)) {
                return;
            }
            foreach (var token in JArray.Parse(File.ReadAllText(FilePath))) {
                string name = token["Name"]?.Value<string>();
                string curve = token["Curve"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(curve)) {
                    entries.Add(new Entry(name, curve));
                }
            }
        } catch (Exception e) {
            MainCore.Log.Err($"[Easing] Failed to read {FilePath}: {e.Message}");
        }
    }

    private static void Save() {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var array = new JArray(entries.Select(e => new JObject { ["Name"] = e.Name, ["Curve"] = e.Curve }));
            File.WriteAllText(FilePath, array.ToString());
        } catch (Exception e) {
            MainCore.Log.Err($"[Easing] Failed to save {FilePath}: {e.Message}");
        }
    }

    /// <summary>Adds or overwrites by name.</summary>
    public static void Put(string name, string curve) {
        name = name.Trim();
        var existing = Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) {
            existing.Curve = curve;
        } else {
            entries.Add(new Entry(name, curve));
        }
        Save();
    }

    public static void Remove(Entry entry) {
        if (entries != null && entries.Remove(entry)) {
            Save();
        }
    }
}
