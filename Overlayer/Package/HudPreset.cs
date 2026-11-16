using Newtonsoft.Json.Linq;
using Overlayer.IO;
using Overlayer.IO.Fx;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent.Impl;
using System.IO.Compression;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.Package;

/// <summary>
/// Simple HUD presets built in code: text and box objects driven by tags, no fonts or images,
/// so the HUD editor can drop them straight into a canvas as normal editable objects. They are
/// also packed as a plain .o5cp so they show up in the regular preset list.
/// </summary>
public static class HudPreset {
    public const string ElementCategory = "Element";
    public const string LayoutCategory = "Layout";

    // Fixed so the same preset always packs to the same bytes (installs are keyed by file hash).
    private static readonly DateTimeOffset PackTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>One object (and its children) of a preset.</summary>
    public sealed class Node {
        public readonly OvObjectSettings Config = new();
        public readonly List<Node> Children = [];

        public Node Add(params Node[] children) {
            Children.AddRange(children);
            return this;
        }

        /// <summary>Any other tweak to the object's settings.</summary>
        public Node Set(Action<OvObjectSettings> edit) {
            edit(Config);
            return this;
        }

        /// <summary>
        /// Visible only while <paramref name="condition"/> (a JS expression, e.g. <c>IsPlaying()</c>)
        /// is true, and always while the HUD editor is open so it can still be edited.
        /// </summary>
        public Node ShowWhen(string condition) {
            Config.CanvasGroupConfig ??= new CanvasGroupSettings();
            Config.CanvasGroupConfig.Alpha = new FxValue<float>($"({condition}) || HudEditorOpen() ? 1 : 0");
            return this;
        }

        /// <summary>Stretches across the parent's width at height <paramref name="y"/> (0 bottom, 1 top).</summary>
        public Node StretchX(float y, float height, float offsetY = 0f) {
            var rt = Config.RectTransformConfig;
            rt.AnchorMin.StaticValue = new Vector2(0f, y);
            rt.AnchorMax.StaticValue = new Vector2(1f, y);
            rt.Pivot.StaticValue = new Vector2(0.5f, y);
            rt.AnchoredPosition.StaticValue = new Vector2(0f, offsetY);
            rt.SizeDelta.StaticValue = new Vector2(0f, height);
            return this;
        }

        public JToken Serialize() {
            var json = new JObject { [nameof(Config)] = Config.Serialize() };
            if (Children.Count > 0) {
                json[nameof(Children)] = new JArray(Children.Select(c => c.Serialize()));
            }
            return json;
        }
    }

    private static Node Rect(string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot) {
        var node = new Node();
        node.Config.Name.Value = name;
        var rt = node.Config.RectTransformConfig = new RectTransformSettings();
        rt.AnchorMin.StaticValue = rt.AnchorMax.StaticValue = anchor;
        rt.Pivot.StaticValue = pivot ?? anchor;
        rt.AnchoredPosition.StaticValue = position;
        rt.SizeDelta.StaticValue = size;
        node.Config.CanvasGroupConfig = new CanvasGroupSettings();
        return node;
    }

    /// <summary>
    /// A text object. <paramref name="anchor"/> is also the pivot, so a text anchored to the
    /// top-left corner grows away from it; <paramref name="text"/> may contain tags.
    /// </summary>
    public static Node Text(string name, string text, float fontSize, Vector2 anchor, Vector2 position,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, Color? color = null, float width = 600f) {
        var node = Rect(name, anchor, position, new Vector2(width, fontSize * 1.4f), null);
        var tmp = node.Config.TextConfig = new TextMeshProUGUISettings();
        tmp.FontSize.StaticValue = fontSize;
        tmp.Alignment.StaticValue = alignment;
        tmp.Color.StaticValue = new GradientColor(color ?? Color.white, true);
        // An outline thick enough for big numbers fills small captions in.
        tmp.EnableOutline.StaticValue = fontSize >= 20f;
        tmp.OutlineWidth.StaticValue = 0.15f;
        tmp.OutlineColor.StaticValue = new GradientColor(new Color(0f, 0f, 0f, 0.8f), true);
        node.Config.TextEngineConfig = OvTextSettings.FromLegacy(text);
        return node;
    }

    /// <summary>A plain colored box. <paramref name="sizeFx"/> (a JS expression giving [w, h]) makes it a bar.</summary>
    public static Node Box(string name, Color color, Vector2 anchor, Vector2 position, Vector2 size,
        Vector2? pivot = null, string sizeFx = null) {
        var node = Rect(name, anchor, position, size, pivot);
        node.Config.ImageConfig = new ImageSettings();
        node.Config.ImageConfig.Color.StaticValue = color;
        node.Config.ImageConfig.RaycastTarget.StaticValue = false;
        if (!string.IsNullOrEmpty(sizeFx)) {
            node.Config.RectTransformConfig.SizeDelta = new FxValue<Vector2>(sizeFx);
        }
        return node;
    }

    /// <summary>
    /// A track with a fill that covers <paramref name="progress"/> (a JS expression giving 0..1) of it.
    /// The fill follows the track's size, so the bar can be stretched or resized freely.
    /// </summary>
    public static Node Bar(string name, string progress, Color fill, Color track, Vector2 anchor, Vector2 position,
        Vector2 size, Vector2? pivot = null) {
        var bar = Box(name, track, anchor, position, size, pivot);
        var fillNode = Box("Fill", fill, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
        fillNode.Config.RectTransformConfig.AnchorMax = new FxValue<Vector2>($"[Math.min(Math.max({progress}, 0), 1), 1]");
        return bar.Add(fillNode);
    }

    /// <summary>An empty object holding others (move them together).</summary>
    public static Node Group(string name, Vector2 anchor, Vector2 position, Vector2 size, params Node[] children)
        => Rect(name, anchor, position, size, null).Add(children);

    /// <summary>
    /// A group covering the whole canvas: children keep their corner anchors, so a full layout
    /// is one object to move, hide or delete (ungroup it to split it up).
    /// </summary>
    public static Node Screen(string name, params Node[] children) {
        var node = Rect(name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, null);
        node.Config.RectTransformConfig.AnchorMin.StaticValue = Vector2.zero;
        node.Config.RectTransformConfig.AnchorMax.StaticValue = Vector2.one;
        return node.Add(children);
    }

    /// <summary>Packs objects as a resource-free .o5cp.</summary>
    public static byte[] Pack(string id, string name, string description, IEnumerable<Node> objects,
        IEnumerable<O5cpModuleEntry> modules = null) {
        var canvas = new OvCanvasSettings();
        canvas.Name.Value = name;
        var canvasJson = new JObject {
            ["Config"] = canvas.Serialize(),
            ["OvObjects"] = new JArray(objects.Select(o => o.Serialize())),
        };
        var manifest = new O5cpManifest {
            Package = new O5cpPackageInfo {
                Id = id,
                Name = name,
                Version = "1.0.0",
                Author = "Overlayer",
                Description = description ?? string.Empty,
                CreatedAt = PackTime.UtcDateTime.ToString("o"),
                AppVersion = Core.Info.Version,
                MinAppVersion = Core.Info.Version,
            },
        };
        if (modules != null) {
            manifest.Modules.AddRange(modules);
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
            foreach (var (path, text) in new[] {
                (O5cpFormat.ManifestFile, manifest.Serialize().ToString()),
                (O5cpFormat.DefaultCanvasFile, canvasJson.ToString()),
            }) {
                var entry = zip.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
                entry.LastWriteTime = PackTime;
                using var entryStream = entry.Open();
                byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
                entryStream.Write(data, 0, data.Length);
            }
        }
        return stream.ToArray();
    }

    /// <summary>A preset entry for <see cref="PresetStore"/>; the bytes are built once, on first use.</summary>
    public static PackagePreset Preset(string id, string name, string category, string module, string description,
        Func<IEnumerable<Node>> build, IEnumerable<O5cpModuleEntry> modules = null) {
        byte[] cached = null;
        return new PackagePreset {
            Id = id,
            Name = name,
            Category = category,
            Module = module,
            ReadBytes = () => cached ??= Pack(id, name, description, build(), modules),
        };
    }
}
