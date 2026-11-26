using Overlayer.Overlay;
using UnityEngine;

namespace Overlayer.HudEdit;

// Read-only views into the editor for the tutorial: what to point at and what the user did.
internal static partial class HudEditor {
    /// <summary>A piece of the editor by its path under the editor's root ("Panel", "Layers", ...).</summary>
    internal static RectTransform Part(string path) {
        var r = root != null ? O5Kit.Behaviour.UnityUtils.AsRectTransform(root.Find(path)) : null;
        return r != null && r.gameObject.activeInHierarchy ? r : null;
    }

    /// <summary>A toolbar / bottom bar / panel button by the id it was made with ("add_text", "grid", "align_l" ...).</summary>
    internal static RectTransform Control(string id) {
        var r = controls.FirstOrDefault(c => c != null && c.Id == "plus_hud_" + id)?.Rect;
        return r != null && r.gameObject.activeInHierarchy ? r : null;
    }

    /// <summary>The open inspector's property area (the column of ƒ buttons), while All properties is shown.</summary>
    internal static RectTransform FxArea() {
        if (!allPropsShown || inspectorHost == null || !inspectorHost.activeInHierarchy) {
            return null;
        }
        return inspectorContent != null && inspectorContent.gameObject.activeInHierarchy ? inspectorContent : null;
    }

    internal static OvObject SelectedObject => Primary?.Object;

    internal static int SelectedCount => selection.Count;

    internal static bool PresetMenuOpen => contextMenu != null && contextMenu.name == "PresetMenu";

    internal static bool AnyContextMenuOpen => contextMenu != null;

    internal static RectTransform ContextMenuRect => contextMenu;

    internal static bool AnyDetached => docks.Any(d => d.Window != null);

    // ---- the tutorial's demos: objects it adds and takes away again, kept out of undo ----

    internal const string DemoName = "Olly's sample";

    internal static void DemoShowLayers() {
        if (!IsOpen || LayersShown) {
            return;
        }
        Core.MainCore.Conf.HudLayersShown.Value = true;
        Core.MainCore.ConfMgr.RequestSave();
        UpdateLayersButton();
        ApplyPanelLayout();
        layersDirty = true;
        TickLayers();
    }

    /// <summary>A new text on the main canvas with its middle on a screen point, selected.</summary>
    internal static OvObject DemoAddText(Vector2 screen, string text) {
        if (!IsOpen) {
            return null;
        }
        var canvas = MainCanvas(true);
        if (canvas.Config != null && !canvas.Config.Enabled.UseFx && !canvas.Config.Enabled.StaticValue) {
            canvas.Config.Enabled.StaticValue = true;
            canvas.ApplyConfig();
        }
        var obj = NewObject(NewKind.Text);
        obj.Config.Name.Value = DemoName;
        obj.Config.TextEngineConfig.Text.StaticValue = text;
        AttachAt(obj, new Slot(canvas, null, -1));
        PlaceAtSoon(obj, screen);
        AfterStructureChange(obj);
        return obj;
    }

    internal static void DemoSetText(OvObject obj, string text) {
        if (obj?.Config.TextEngineConfig == null || obj.GameObject == null) {
            return;
        }
        obj.Config.TextEngineConfig.Text.StaticValue = text;
        obj.ApplyConfig();
        if (IsOpen) {
            RefreshInspectorSoon();
            SyncPanelValues();
        }
    }

    internal static void DemoSetAlpha(OvObject obj, float alpha) {
        if (obj?.GameObject == null) {
            return;
        }
        SetAlpha(obj, alpha);
        obj.ApplyConfig();
        if (IsOpen) {
            SyncPanelValues();
        }
    }

    /// <summary>Opacity from a formula (null: back to the plain value).</summary>
    internal static void DemoSetAlphaFx(OvObject obj, string expression) {
        var alpha = obj?.Config.CanvasGroupConfig?.Alpha;
        if (alpha == null || obj.GameObject == null) {
            return;
        }
        if (expression != null) {
            alpha.Expression = expression;
        }
        alpha.UseFx = expression != null;
        obj.ApplyConfig();
        if (IsOpen) {
            RefreshPanel();
        }
    }

    /// <summary>Position from a formula: sways sideways around where it is (false: back to still).</summary>
    internal static void DemoWobble(OvObject obj, bool on) {
        var pos = obj?.Config.RectTransformConfig?.AnchoredPosition;
        if (pos == null || obj.GameObject == null) {
            return;
        }
        if (on) {
            var p = pos.StaticValue;
            pos.Expression = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[{0:0.##} + Math.sin(Time() * 3) * 40, {1:0.##}]", p.x, p.y);
        }
        pos.UseFx = on;
        obj.ApplyConfig();
        if (IsOpen) {
            RefreshPanel();
        }
    }

    /// <summary>The text from a formula (null: back to the plain text).</summary>
    internal static void DemoTextFx(OvObject obj, string expression) {
        var text = obj?.Config.TextEngineConfig?.Text;
        if (text == null || obj.GameObject == null) {
            return;
        }
        if (expression != null) {
            text.Expression = expression;
        }
        text.UseFx = expression != null;
        obj.ApplyConfig();
        if (IsOpen) {
            RefreshPanel();
        }
    }

    /// <summary>Rotation (degrees) from a formula (null: back to the plain value).</summary>
    internal static void DemoRotationFx(OvObject obj, string expression) => DemoFx(obj, obj?.Config.RectTransformConfig?.Rotation, expression);

    /// <summary>Text color from a formula (null: back to the plain color).</summary>
    internal static void DemoTextColorFx(OvObject obj, string expression) => DemoFx(obj, obj?.Config.TextConfig?.Color, expression);

    private static void DemoFx<T>(OvObject obj, IO.Fx.FxValue<T> fx, string expression) {
        if (fx == null || obj.GameObject == null) {
            return;
        }
        if (expression != null) {
            fx.Expression = expression;
        }
        fx.UseFx = expression != null;
        obj.ApplyConfig();
        if (IsOpen) {
            RefreshPanel();
        }
    }

    internal static void DemoSelectAll(IEnumerable<OvObject> objects) {
        if (IsOpen) {
            SetSelection(elements.Where(e => objects.Any(o => ReferenceEquals(o, e.Object))));
        }
    }

    /// <summary>Groups these as a row (auto layout), like "Group as row" does; the group is a sample too, and stays out of undo.</summary>
    internal static OvObject DemoGroupAsRow(IEnumerable<OvObject> objects) {
        if (!IsOpen) {
            return null;
        }
        int mark = undo.Mark;
        SetSelection(elements.Where(e => objects.Any(o => ReferenceEquals(o, e.Object))));
        GroupAs(Flow.Row);
        undo.DropSince(mark);
        var group = Primary?.Object;
        if (group != null && group.Children.Count > 0) {
            group.Config.Name.Value = DemoName;
            layersDirty = true;
            RefreshPanel();
        }
        return group;
    }

    /// <summary>A piece of the editor anywhere under it, by name ("Dock_layout" ...).</summary>
    internal static RectTransform PartNamed(string name)
        => root != null ? root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(r => r.name == name) : null;

    internal static void DemoSelect(OvObject obj) {
        if (IsOpen) {
            Select(ElementOf(obj));
        }
    }

    /// <summary>Every sample left anywhere (also from a run that was cut short).</summary>
    internal static void DemoRemoveAll() {
        bool any = false;
        foreach (var canvas in OverlayCore.Canvases.Where(c => c != null).ToList()) {
            foreach (var obj in canvas.OvObjects.Where(o => o.Config.Name.Value == DemoName).ToList()) {
                canvas.Detach(obj);
                obj.Dispose();
                any = true;
            }
        }
        if (!any) {
            return;
        }
        OverlayCore.SaveAllCanvases();
        if (IsOpen) {
            AfterStructureChange(null);
        }
    }
}
