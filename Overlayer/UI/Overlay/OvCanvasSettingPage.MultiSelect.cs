using Newtonsoft.Json.Linq;
using O5Kit.Input;
using Overlayer.IO.Overlay;
using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.UI.Overlay;

// Shift+click in the hierarchy adds/removes objects to a multi-selection.
// The inspector still shows the primary (selectedObject); every edit made there is
// diffed against the previous primary config and the changed values are copied to the others.
public partial class OvCanvasSettingPage {
#pragma warning disable IDE0001
    private readonly System.Collections.Generic.HashSet<OvObject> multiSelected = [];
#pragma warning restore IDE0001
    private JObject multiSnapshot;

    private static bool ShiftHeld()
        => O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

    private void ToggleMultiSelect(OvObject obj) {
        if (obj == selectedObject) {
            return;
        }
        if (!multiSelected.Remove(obj)) {
            multiSelected.Add(obj);
        }
        // Inspector isn't rebuilt on shift+click, so take the diff baseline here.
        SnapshotMultiSelect();
        RebuildHierarchy();
    }

    private void SnapshotMultiSelect() {
        multiSnapshot = selectedObject != null && multiSelected.Count > 0
            ? selectedObject.Config.Serialize() as JObject
            : null;
    }

    private void PropagateMultiSelect() {
        if (selectedObject == null || multiSnapshot == null) {
            return;
        }

        var now = (JObject)selectedObject.Config.Serialize();
        var changes = new System.Collections.Generic.List<(string[] Path, JToken Value)>();
        Diff(multiSnapshot, now, [], changes);
        multiSnapshot = now;
        if (changes.Count == 0) {
            return;
        }

        foreach (var obj in multiSelected) {
            if (obj == selectedObject || !IsInCurrentCanvas(obj)) {
                continue;
            }

            var json = (JObject)obj.Config.Serialize();
            foreach (var (path, value) in changes) {
                SetPath(json, path, value);
            }

            // Fresh instance: Deserialize keeps old values for keys omitted when default (e.g. Enabled).
            var old = obj.Config;
            var config = new OvObjectSettings();
            config.Deserialize(json);
            obj.Config = config;
            obj.ApplyComponent();
            obj.ApplyConfig();
            IO.Fx.FxDisposal.DisposeDeep(old);
        }
    }

    // Collects changed leaves; a null value means the key was removed.
    private static void Diff(JObject before, JObject after, string[] path, System.Collections.Generic.List<(string[], JToken)> changes) {
        foreach (var prop in after.Properties()) {
            if (path.Length == 0 && prop.Name == nameof(OvObjectSettings.Name)) {
                continue;
            }
            string[] childPath = [.. path, prop.Name];
            var old = before[prop.Name];
            if (old is JObject oldObj && prop.Value is JObject newObj) {
                Diff(oldObj, newObj, childPath, changes);
            } else if (!JToken.DeepEquals(old, prop.Value)) {
                changes.Add((childPath, prop.Value));
            }
        }
        foreach (var prop in before.Properties()) {
            if (after[prop.Name] == null) {
                changes.Add(([.. path, prop.Name], null));
            }
        }
    }

    // Only touches values whose parent already exists on the target, so editing e.g. a font size
    // doesn't add a half-empty text component to an image object. Whole components (top level) are added/removed.
    private static void SetPath(JObject root, string[] path, JToken value) {
        JObject parent = root;
        for (int i = 0; i < path.Length - 1; i++) {
            if (parent[path[i]] is not JObject next) {
                return;
            }
            parent = next;
        }

        string key = path[^1];
        if (value == null) {
            parent.Remove(key);
        } else if (path.Length == 1 || parent[key] != null) {
            parent[key] = value.DeepClone();
        }
    }

    private bool IsInCurrentCanvas(OvObject obj) {
        OvObject current = obj;
        for (; current.Parent != null; current = current.Parent) {
            if (!current.Parent.Children.Contains(current)) {
                return false;
            }
        }
        return currentCanvas != null && currentCanvas.OvObjects.Contains(current);
    }
}
