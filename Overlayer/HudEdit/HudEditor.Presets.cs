using Newtonsoft.Json.Linq;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Overlay;
using Overlayer.Package;
using Overlayer.UI.Utility;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.HudEdit;

// Presets in the HUD editor: resource-free ones drop into the main canvas as normal, editable
// objects (one undo step); ones that bring fonts / images / scripts install as a package like
// the canvas list's preset button does.
internal static partial class HudEditor {
    private const float PresetMenuWidth = 300f;
    private const float PresetMenuMaxHeight = 520f;

    /// <summary>Inserts a preset. <paramref name="screen"/>: put a single-object preset there.</summary>
    public static void InsertPreset(PackagePreset preset, Vector2? screen) {
        byte[] data;
        try {
            data = preset.ReadBytes();
        } catch (Exception e) {
            MainCore.Log.Err($"[HudEditor] Preset '{preset.Name}' failed to load: {e.Message}");
            return;
        }
        if (!TryReadObjects(data, out var objects, out bool hasResources)) {
            MainCore.Log.Err($"[HudEditor] Preset '{preset.Name}' isn't a valid package.");
            return;
        }
        if (hasResources) {
            var entry = PackageStore.InstallBytes(data, out var warnings);
            foreach (string warning in warnings) {
                MainCore.Log.Wrn($"[HudEditor] {preset.Name}: {warning}");
            }
            MainCore.Log.Msg(entry != null
                ? $"[HudEditor] '{preset.Name}' brings its own fonts / images, so it was installed as a package. Use Copy on it in the canvas list to edit it."
                : $"[HudEditor] '{preset.Name}' could not be installed.");
            return;
        }

        var placement = CurrentPlacement is Placement.Parent ? Placement.MainCanvas : CurrentPlacement;
        var slot = ResolveSlot(placement);
        if (slot.Canvas == null) {
            return;
        }
        var created = new List<(OvObject Obj, Slot Slot)>();
        foreach (var token in objects) {
            var obj = new OvObject(bare: token["Config"]?["RectTransformConfig"] == null);
            obj.Deserialize(token);
            obj.ApplyComponent();
            AttachAt(obj, slot);
            int index = slot.Parent != null ? slot.Parent.Children.IndexOf(obj) : slot.Canvas.OvObjects.IndexOf(obj);
            created.Add((obj, new Slot(slot.Canvas, slot.Parent, index)));
        }
        if (created.Count == 1 && screen.HasValue) {
            PlaceAtSoon(created[0].Obj, screen.Value);
        }
        undo.Push(new ActionCommand(
            () => {
                foreach (var (obj, s) in Enumerable.Reverse(created)) {
                    ToTrash(obj, s);
                }
            },
            () => {
                foreach (var (obj, s) in created) {
                    FromTrash(obj, s);
                }
            }));
        AfterStructureChange(null, created.Select(c => c.Obj));
    }

    private static bool TryReadObjects(byte[] data, out List<JToken> objects, out bool hasResources) {
        objects = [];
        hasResources = false;
        try {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            string Read(string path) {
                var entry = zip.GetEntry(path);
                if (entry == null) {
                    return null;
                }
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            }
            string manifestText = Read(O5cpFormat.ManifestFile);
            if (manifestText == null || !O5cpManifest.TryParse(JToken.Parse(manifestText), out var manifest, out _)) {
                return false;
            }
            hasResources = manifest.Fonts.Count > 0 || manifest.Textures.Count > 0
                || manifest.Sprites.Count > 0 || manifest.Scripts.Count > 0;
            string canvasText = Read(manifest.CanvasFile);
            if (canvasText == null) {
                return false;
            }
            if (JToken.Parse(canvasText)["OvObjects"] is JArray array) {
                objects.AddRange(array);
            }
            return true;
        } catch {
            return false;
        }
    }

    // ---- picker ----

    private static void OpenPresetMenu(Vector2 at, Vector2? insertAt) {
        CloseContextMenu();
        PresetStore.RefreshFilePresets();
        var presets = PresetStore.Presets;

        contextMenu = UiStyle.Rounded("PresetMenu", root, UiStyle.Surface).rectTransform;
        contextMenu.anchorMin = contextMenu.anchorMax = Vector2.zero;
        contextMenu.pivot = new Vector2(0f, 1f);
        var (_, content, _) = O5Factory.ScrollView(O5KitAdapters.Ctx, contextMenu, 2f, 6f);

        void Section(string title, IEnumerable<PackagePreset> items) {
            var list = items.ToList();
            if (list.Count == 0) {
                return;
            }
            UiStyle.Section(content, title);
            foreach (var preset in list) {
                var captured = preset;
                string source = string.IsNullOrEmpty(preset.Module) ? "File" : preset.Module;
                string label = $"{preset.Name}<pos=70%><alpha=#55>{source}";
                var button = Button(O5Factory.Row(O5KitAdapters.Ctx, content, 32f), "preset_" + preset.Id, label, () => {
                    CloseContextMenu();
                    InsertPreset(captured, insertAt);
                }, align: TextAlignmentOptions.MidlineLeft);
                if (button.Label != null) {
                    button.Label.fontSize = 16f;
                }
                button.NormalColor = Color.clear;
                button.UpdateVisual(true);
            }
        }

        Section("Elements", presets.Where(p => p.Category == HudPreset.ElementCategory));
        Section("Layouts", presets.Where(p => p.Category == HudPreset.LayoutCategory));
        Section("Other presets", presets.Where(p => p.Category != HudPreset.ElementCategory && p.Category != HudPreset.LayoutCategory));
        if (presets.Count == 0) {
            UiStyle.Section(content, "No presets yet");
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float height = Mathf.Min(content.rect.height + 14f, PresetMenuMaxHeight);
        contextMenu.sizeDelta = new Vector2(PresetMenuWidth, height);

        // Keep the whole menu on screen.
        Vector2 local = at / UiScale;
        Vector2 screen = new(Screen.width / UiScale, Screen.height / UiScale);
        local.x = Mathf.Clamp(local.x, 4f, screen.x - PresetMenuWidth - 4f);
        local.y = Mathf.Max(local.y, height + 4f);
        contextMenu.anchoredPosition = local;
    }
}
